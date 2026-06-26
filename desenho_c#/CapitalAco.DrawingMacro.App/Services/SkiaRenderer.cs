using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    /*
     * CADEIA DE PENSAMENTO (CHAIN OF THOUGHT) - SkiaRenderer:
     * A renderização precisa ser idêntica em tela (WPF) e no relatório (QuestPDF). Por isso:
     * 1. Criamos um motor unificado baseado em SkiaSharp que desenha em um SKCanvas genérico.
     * 2. O desenho 3D (Extrusão) é renderizado via algoritmo do pintor (Painter's Algorithm):
     *    - Projetamos as coordenadas 2D do perfil (frente) no espaço oblíquo (trás) com profundidade de perspectiva.
     *    - Classificamos as faces por profundidade no eixo Y (mais distantes primeiro).
     *    - Desenhamos as faces ocultas/sombreadas com SKPath preenchidos.
     *    - Desenhamos as linhas de dobra de conexão.
     *    - Desenhamos a face frontal com contorno espesso (perfil mestre).
     * 3. Anotação de Cotas: Calculamos a normal de cada segmento e deslocamos a linha de cota para o lado de fora (oposto ao lado interno).
     *    Escrevemos o valor de comprimento no centro. Os ângulos de dobra são colocados nas interseções.
     * 4. Planificação: Desenhamos a chapa esticada (retângulo plano) com linhas tracejadas/contínuas de dobra (cima/baixo) e cotas acumuladas.
     * 5. Interoperabilidade WPF: Fornecemos um método utilitário que converte a imagem SkiaSharp em BitmapSource WPF (via memory stream PNG) sem travas ou leaks.
     */
    public class SkiaRenderer : ISkiaRenderer
    {
        private static readonly SKColor FundoCor            = new SKColor(255, 255, 255);
        private static readonly SKColor PerfilContornoCor    = new SKColor(38,  52,  70);
        private static readonly SKColor PerfilPreenchimentoCor = new SKColor(176, 190, 208);
        // Paleta da chapa metálica
        private static readonly SKColor AcoBase             = new SKColor(172, 185, 202);  // face frontal / tom médio
        private static readonly SKColor AcoClaro            = new SKColor(218, 228, 242);  // face bem iluminada
        private static readonly SKColor AcoEscuro           = new SKColor(88,  100, 118);  // face em sombra
        private static readonly SKColor AcoFundoCor         = new SKColor(148, 162, 178);  // tampa traseira
        private static readonly SKColor AcoAresta           = new SKColor(38,  52,  70);   // contorno frontal
        private static readonly SKColor AcoArestaBack       = new SKColor(80,  94,  112);  // arestas traseiras / conexão
        // Paleta de cotas — segura para impressão P&B (alto contraste em qualquer modo de cor)
        private static readonly SKColor CotaLinhaCor        = new SKColor(30,  30,  40);   // quase-preto
        private static readonly SKColor CotaTextoCor        = new SKColor(12,  22,  52);   // marinho escuro → fundo badge externo
        private static readonly SKColor CotaInternaLinhaCor = new SKColor(30,  30,  40);   // quase-preto
        private static readonly SKColor CotaInternaTextoCor = new SKColor(20,  20,  20);   // preto → texto badge interno
        private static readonly SKColor AnguloTextoCor      = new SKColor(20,  20,  20);   // preto
        private static readonly SKColor SegmentoDestaqueCor = new SKColor(255, 120, 0);

        // Fonte negrito real (superior ao FakeBoldText em impressão).
        private static readonly SKTypeface FonteNegrito =
            SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default;

        public System.Windows.Media.ImageSource RenderToImageSource(InstrucoesPolares polar, int width, int height, IGeometryService geometryService, float fonteCota = 12f, float fonteAngulo = 11f, bool mostrarMedidas = true, int? segmentoDestacado = null, bool destacarProximaOrigem = false, bool forcarDesenho3D = false)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(FundoCor);

            var dim = geometryService.CalcularDimensoesAcabadas(polar);
            RenderizarPeca(canvas, new SKSize(width, height), polar, dim, mostrarMedidas, geometryService, fonteCota, fonteAngulo, segmentoDestacado, destacarProximaOrigem, forcarDesenho3D);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;

            var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public void RenderizarPeca(
            SKCanvas canvas,
            SKSize size,
            InstrucoesPolares polar,
            DimensoesAcabadas? dimensoes,
            bool mostrarMedidas,
            IGeometryService geometryService,
            float fonteCota = 12f,
            float fonteAngulo = 11f,
            int? segmentoDestacado = null,
            bool destacarProximaOrigem = false,
            bool forcarDesenho3D = false)
        {
            if (polar.CoordenadasPolares.Count == 0)
                return;

            // Chapa plana: exibe apenas dimensões em texto grande, sem desenho 3D.
            // forcarDesenho3D mantém o comportamento original para o preview do editor durante o desenho.
            if (!forcarDesenho3D && polar.SegmentosOriginal.Count == 1 && !polar.SegmentosOriginal[0].EhCurvo)
            {
                DesenharChapaLisa(canvas, size, polar.SegmentosOriginal[0].Medida, polar.Comprimento, fonteCota);
                return;
            }

            // 1. Calcular coordenadas retangulares
            var parciais = geometryService.GerarCoordenadasRetangularesParciais(polar.CoordenadasPolares);
            var absolutas = geometryService.GerarCoordenadasRetangularesAbsolutas(parciais);

            // Ajustar escala para caber na tela com margem
            double margem = 0.15;
            double canvasW = size.Width;
            double canvasH = size.Height;
            double utilW = canvasW * (1 - 2 * margem);
            double utilH = canvasH * (1 - 2 * margem);

            // Para segmentos curvos, o arco pode se afastar do segmento de centro (corda) além dos pontos absolutos
            // (no caso de um tubo calandrado a 360°, a corda tem comprimento zero), então inflamos a caixa delimitadora
            // pelo raio neutro da curva antes de calcular a escala, para a curva inteira caber no desenho.
            var pontosParaEscala = new List<(double X, double Y)>(absolutas);
            for (int i = 0; i < polar.SegmentosOriginal.Count && i < absolutas.Count; i++)
            {
                var seg = polar.SegmentosOriginal[i];
                if (!seg.EhCurvo || seg.CurvaInfo == null) continue;

                double rInternoBbox = seg.CurvaInfo.TipoRaio == "interno" ? seg.CurvaInfo.Raio : seg.CurvaInfo.Raio - polar.Espessura;
                double rExternoBbox = rInternoBbox + polar.Espessura;
                var (cx, cy) = absolutas[i];
                pontosParaEscala.Add((cx - rExternoBbox, cy - rExternoBbox));
                pontosParaEscala.Add((cx + rExternoBbox, cy + rExternoBbox));
            }

            double minX = pontosParaEscala.Min(p => p.X);
            double maxX = pontosParaEscala.Max(p => p.X);
            double minY = pontosParaEscala.Min(p => p.Y);
            double maxY = pontosParaEscala.Max(p => p.Y);

            double dimX = maxX - minX;
            double dimY = maxY - minY;
            if (dimX == 0) dimX = 1;
            if (dimY == 0) dimY = 1;

            // Escala exclusivamente pelo perfil — a extrusão não interfere na escala nem no posicionamento.
            double comprimento = polar.Comprimento;
            double angulo3D = 55.0 * (Math.PI / 180.0);
            double escala = Math.Min(utilW / dimX, utilH / dimY);

            // Perfil centralizado no canvas (as margens de 15% ficam livres para a extrusão).
            double fatorX = (canvasW - dimX * escala) / 2.0 - minX * escala;
            double fatorY = (canvasH - dimY * escala) / 2.0 - minY * escala;

            // Extrusão proporcional ao comprimento, limitada a caber exatamente nas margens
            // do lado onde ela cresce (direita e cima), sem alterar fatorX/fatorY.
            double profundidadeModelo = Math.Min(comprimento * 0.25, Math.Max(dimX, dimY) * 0.6);
            double compEsboco = profundidadeModelo * escala;
            double margemDireita = (canvasW - dimX * escala) / 2.0;
            double margemCima    = (canvasH - dimY * escala) / 2.0;
            compEsboco = Math.Max(0, Math.Min(compEsboco,
                Math.Min(margemDireita / Math.Cos(angulo3D), margemCima / Math.Sin(angulo3D))));
            float dx = (float)(compEsboco * Math.Cos(angulo3D));
            float dy = (float)(-compEsboco * Math.Sin(angulo3D));

            var coordenadasNoCanvas = absolutas.Select(p => new SKPoint(
                (float)(p.X * escala + fatorX),
                (float)(p.Y * escala + fatorY)
            )).ToList();

            // Tubo calandrado fechado (360°): é sempre peça de um único segmento (ver GeradorPecaService),
            // então tratamos como um caso totalmente dedicado (cilindro 3D) em vez de forçar o caminho
            // genérico de polígono, que degenera (corda de comprimento zero = sem extrusão visível).
            if (polar.SegmentosOriginal.Count == 1
                && polar.SegmentosOriginal[0].EhCurvo
                && polar.SegmentosOriginal[0].CurvaInfo != null
                && polar.SegmentosOriginal[0].CurvaInfo!.AnguloCurva >= 359.5)
            {
                var infoTubo = polar.SegmentosOriginal[0].CurvaInfo!;
                double rInternoTubo = infoTubo.TipoRaio == "interno" ? infoTubo.Raio : infoTubo.Raio - polar.Espessura;
                double rNeutroTubo = rInternoTubo + polar.KFactor * polar.Espessura;
                float raioCanvasTubo = (float)Math.Max(0.5, rNeutroTubo * escala);

                DesenharTuboRedondo3D(canvas, coordenadasNoCanvas[0], raioCanvasTubo, dx, dy);

                if (mostrarMedidas)
                {
                    DesenharCotaTubo(canvas, coordenadasNoCanvas[0], raioCanvasTubo, dx, dy, infoTubo, polar, fonteCota);
                }
                return;
            }

            // 2. Tesselar segmentos curvos em pequenos trechos retos, para que o arco apareça de fato
            // curvado tanto no contorno frontal quanto nas faces extrudadas (e não apenas como uma corda reta).
            var azimutesOriginais = geometryService.ObterAzimutesDeSegmentos(polar.SegmentosOriginal);
            var pontosTess = new List<SKPoint> { coordenadasNoCanvas[0] };
            var origemTrecho = new List<int>();
            for (int i = 0; i < coordenadasNoCanvas.Count - 1; i++)
            {
                var segOriginal = i < polar.SegmentosOriginal.Count ? polar.SegmentosOriginal[i] : null;

                if (segOriginal is { EhCurvo: true, CurvaInfo: not null } && i < azimutesOriginais.Count)
                {
                    var infoCurva = segOriginal.CurvaInfo!;
                    double rInterno = infoCurva.TipoRaio == "interno" ? infoCurva.Raio : infoCurva.Raio - polar.Espessura;
                    double rNeutro = rInterno + polar.KFactor * polar.Espessura;
                    float raioCanvas = (float)Math.Max(0.5, rNeutro * escala);
                    bool sentidoHorario = DeterminarSentidoHorarioCurva(i, azimutesOriginais);
                    double azStart = ObterAzimuteInicialCurva(i, azimutesOriginais, infoCurva.AnguloCurva, sentidoHorario);
                    double azCentro = azStart + (sentidoHorario ? 90.0 : -90.0);

                    double centroModelX = absolutas[i].X + rNeutro * Math.Sin(azCentro * Math.PI / 180.0);
                    double centroModelY = absolutas[i].Y - rNeutro * Math.Cos(azCentro * Math.PI / 180.0);
                    var centroCanvas = new SKPoint(
                        (float)(centroModelX * escala + fatorX),
                        (float)(centroModelY * escala + fatorY));

                    foreach (var sp in TesselarArco(centroCanvas, raioCanvas, azCentro, infoCurva.AnguloCurva, sentidoHorario))
                    {
                        pontosTess.Add(sp);
                        origemTrecho.Add(i);
                    }
                }
                else
                {
                    pontosTess.Add(coordenadasNoCanvas[i + 1]);
                    origemTrecho.Add(i);
                }
            }

            // Índices em pontosTess que correspondem aos vértices ORIGINAIS (não às subdivisões da
            // tesselação) — só ali existe uma dobra/transição real para desenhar a aresta de conexão.
            var indicesOriginais = new List<int> { 0 };
            for (int i = 0; i < origemTrecho.Count; i++)
            {
                if (i == origemTrecho.Count - 1 || origemTrecho[i + 1] != origemTrecho[i])
                    indicesOriginais.Add(i + 1);
            }

            // 3. Extrusão com espessura real da chapa
            double espessuraPx = Math.Max(3.0, polar.Espessura * escala);
            float halfPx = (float)(espessuraPx / 2.0);

            // Dois contornos paralelos ao eixo neutro (face externa e face interna da chapa)
            var outerTess = ComputarOffsetPolyline(pontosTess, halfPx);
            var innerTess = ComputarOffsetPolyline(pontosTess, -halfPx);
            var outerBack = outerTess.Select(p => new SKPoint(p.X + dx, p.Y + dy)).ToList();
            var innerBack = innerTess.Select(p => new SKPoint(p.X + dx, p.Y + dy)).ToList();

            // Direção de luz (norm. aproximada): vinda de cima-esquerda na tela
            const float LX = -0.40f, LY = -0.92f;

            // Coletar todas as faces laterais + tampa traseira para o Painter's Algorithm
            var todasFaces = new List<(double Depth, SKPoint[] Pts, SKColor Cor)>();

            // Tampa traseira
            {
                var pts = outerBack.Concat(innerBack.AsEnumerable().Reverse()).ToArray();
                double d = pts.Average(p => (double)p.Y * Math.Abs(dy) - (double)p.X * dx);
                todasFaces.Add((d, pts, AcoFundoCor));
            }

            // Faces laterais por segmento tesselado (outer + inner)
            for (int i = 0; i < pontosTess.Count - 1; i++)
            {
                // Normal da face = direção do offset (outer ou inner) em relação ao neutro
                float onx = outerTess[i].X - pontosTess[i].X;
                float ony = outerTess[i].Y - pontosTess[i].Y;
                float olen = MathF.Sqrt(onx * onx + ony * ony);
                if (olen > 1e-6f) { onx /= olen; ony /= olen; }

                float dotOuter = onx * LX + ony * LY;
                float dotInner = -(onx * LX + ony * LY);

                var corOuter = InterpolateColor(AcoBase, AcoClaro, Math.Clamp(dotOuter * 0.85f + 0.55f, 0f, 1f));
                var corInner = InterpolateColor(AcoEscuro, AcoBase, Math.Clamp(dotInner * 0.85f + 0.55f, 0f, 1f));

                var outerPts = new[] { outerTess[i], outerTess[i + 1], outerBack[i + 1], outerBack[i] };
                double od = outerPts.Average(p => (double)p.Y * Math.Abs(dy) - (double)p.X * dx);
                todasFaces.Add((od, outerPts, corOuter));

                var innerPts = new[] { innerTess[i], innerTess[i + 1], innerBack[i + 1], innerBack[i] };
                double id = innerPts.Average(p => (double)p.Y * Math.Abs(dy) - (double)p.X * dx);
                todasFaces.Add((id, innerPts, corInner));
            }

            // Ordenar profundidade (mais distantes primeiro) e desenhar
            todasFaces.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            foreach (var (_, pts, cor) in todasFaces)
            {
                fillPaint.Color = cor;
                using var fp = new SKPath();
                fp.MoveTo(pts[0]);
                for (int j = 1; j < pts.Length; j++) fp.LineTo(pts[j]);
                fp.Close();
                canvas.DrawPath(fp, fillPaint);
            }

            // 4. Arestas de profundidade: extremidades do perfil + lado convexo de cada dobra.
            //    O produto cruzado dos vetores de chegada/saída em cada vértice determina
            //    qual lado é convexo (visível) e qual é côncavo (cruzaria o vazio interior).
            using var endDepthPaint = new SKPaint
            {
                Color = AcoArestaBack,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.1f,
                IsAntialias = true
            };
            // Extremidades do perfil
            canvas.DrawLine(outerTess[0],  outerBack[0],  endDepthPaint);
            canvas.DrawLine(innerTess[0],  innerBack[0],  endDepthPaint);
            canvas.DrawLine(outerTess[^1], outerBack[^1], endDepthPaint);
            canvas.DrawLine(innerTess[^1], innerBack[^1], endDepthPaint);

            // Dobras internas — apenas o lado convexo
            for (int k = 1; k < indicesOriginais.Count - 1; k++)
            {
                int idx = indicesOriginais[k];
                float ax = pontosTess[idx].X - pontosTess[idx - 1].X;
                float ay = pontosTess[idx].Y - pontosTess[idx - 1].Y;
                float bx = pontosTess[idx + 1].X - pontosTess[idx].X;
                float by = pontosTess[idx + 1].Y - pontosTess[idx].Y;
                float cross = ax * by - ay * bx;
                if (cross > 1f)
                    canvas.DrawLine(outerTess[idx], outerBack[idx], endDepthPaint);
                else if (cross < -1f)
                    canvas.DrawLine(innerTess[idx], innerBack[idx], endDepthPaint);
            }

            // 5. Tampa frontal (face da chapa visível ao espectador) — desenhada por último, sempre à frente
            {
                using var frontPath = new SKPath();
                frontPath.MoveTo(outerTess[0]);
                for (int i = 1; i < outerTess.Count; i++) frontPath.LineTo(outerTess[i]);
                for (int i = innerTess.Count - 1; i >= 0; i--) frontPath.LineTo(innerTess[i]);
                frontPath.Close();
                using var frontFill = new SKPaint { Color = AcoBase, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawPath(frontPath, frontFill);
            }

            // Contorno frontal (outer + inner + tampas das extremidades)
            using var contourPaint = new SKPaint
            {
                Color = AcoAresta,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.6f,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = true
            };
            using var outerFrontPath = new SKPath();
            outerFrontPath.MoveTo(outerTess[0]);
            for (int i = 1; i < outerTess.Count; i++) outerFrontPath.LineTo(outerTess[i]);
            canvas.DrawPath(outerFrontPath, contourPaint);

            using var innerFrontPath = new SKPath();
            innerFrontPath.MoveTo(innerTess[0]);
            for (int i = 1; i < innerTess.Count; i++) innerFrontPath.LineTo(innerTess[i]);
            canvas.DrawPath(innerFrontPath, contourPaint);

            canvas.DrawLine(outerTess[0], innerTess[0], contourPaint);
            canvas.DrawLine(outerTess[^1], innerTess[^1], contourPaint);

            // Rótulo do comprimento paralelo à aresta extrudada inferior-direita
            if (mostrarMedidas && compEsboco > 8.0)
            {
                // Seleciona o vértice da face interna mais inferior-direito (max Y + peso de X)
                int idxComp = 0;
                float maxScore = float.MinValue;
                for (int k = 0; k < innerTess.Count; k++)
                {
                    float s = innerTess[k].Y + innerTess[k].X * 0.25f;
                    if (s > maxScore) { maxScore = s; idxComp = k; }
                }
                float midCompX = (innerTess[idxComp].X + innerBack[idxComp].X) / 2f;
                float midCompY = (innerTess[idxComp].Y + innerBack[idxComp].Y) / 2f;

                // Offset perpendicular à extrusão para afastar o texto da aresta
                float extLen = (float)Math.Sqrt(dx * dx + dy * dy);
                float enx = extLen > 1e-6f ? -dy / extLen : 0f;
                float eny = extLen > 1e-6f ?  dx / extLen : 1f;
                float fonteComp = Math.Max(9f, fonteCota * 1.1f);
                midCompX += enx * fonteComp * 0.85f;
                midCompY += eny * fonteComp * 0.85f;

                float angDeg = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                using var compPaint = new SKPaint
                {
                    Color = CotaTextoCor,
                    TextSize = fonteComp,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    Typeface = FonteNegrito
                };
                canvas.Save();
                canvas.Translate(midCompX, midCompY);
                canvas.RotateDegrees(angDeg);
                canvas.DrawText($"{comprimento:F0}", 0f, 0f, compPaint);
                canvas.Restore();
            }

            // Destacar segmento ativo (fase de inserção de medidas no modo rápido)
            if (segmentoDestacado.HasValue)
            {
                int si = segmentoDestacado.Value;
                if (si >= 0 && si < coordenadasNoCanvas.Count - 1)
                {
                    using var paintDestaque = new SKPaint
                    {
                        Color = SegmentoDestaqueCor.WithAlpha(200),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = halfPx * 2f + 5f,
                        StrokeCap = SKStrokeCap.Round,
                        IsAntialias = true
                    };
                    canvas.DrawLine(coordenadasNoCanvas[si], coordenadasNoCanvas[si + 1], paintDestaque);
                }
            }

            // Indicar a origem do próximo segmento (fase Desenho do modo rápido)
            if (destacarProximaOrigem && coordenadasNoCanvas.Count > 0)
            {
                var origem = coordenadasNoCanvas[coordenadasNoCanvas.Count - 1];
                float raio = (float)Math.Max(7.0, espessuraPx * 0.7);
                using var paintFundo = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
                using var paintAnel = new SKPaint { Color = SegmentoDestaqueCor, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };
                canvas.DrawCircle(origem, raio, paintFundo);
                canvas.DrawCircle(origem, raio, paintAnel);
            }

            // 6. Desenhar cotas, medidas internas e graus de dobra, se solicitado
            if (mostrarMedidas)
            {
                var medidas = geometryService.GerarMedidasInternaExterna(polar);
                var labelsCotas = DesenharCotas(canvas, coordenadasNoCanvas, polar.SegmentosOriginal, absolutas, medidas, geometryService, fonteCota, espessuraPx, segmentoDestacado);
                DesenharGrausDobra(canvas, coordenadasNoCanvas, absolutas, polar, geometryService, escala, fonteAngulo, labelsCotas);
            }
        }

        // Replica o critério de sentido (horário/anti-horário) usado no GeometryService ao converter a curva em corda,
        // para que o arco desenhado gire para o mesmo lado calculado na planificação.
        private static bool DeterminarSentidoHorarioCurva(int n, List<double> azimutes)
        {
            double diff;
            if (n > 0)
            {
                diff = azimutes[n] - azimutes[n - 1];
            }
            else if (azimutes.Count > 1)
            {
                diff = azimutes[1] - azimutes[0];
            }
            else
            {
                return true;
            }

            diff %= 360.0;
            if (diff < 0) diff += 360.0;
            return diff < 180.0;
        }

        // Azimute tangente no INÍCIO do arco (antes da curva). Para o primeiro segmento da peça não há
        // segmento anterior, então replicamos a mesma estimativa simétrica usada no GeometryService.
        private static double ObterAzimuteInicialCurva(int i, List<double> azimutes, double anguloCurva, bool sentidoHorario)
        {
            if (i > 0) return azimutes[i - 1];

            double aSigned = sentidoHorario ? anguloCurva : -anguloCurva;
            double azStart = (azimutes[0] - aSigned) % 360.0;
            if (azStart < 0) azStart += 360.0;
            return azStart;
        }

        // Subdivide um arco em pequenos trechos retos para que apareça curvado de fato no desenho
        // (tanto no contorno frontal quanto nas faces extrudadas), em vez de uma única corda reta.
        private static List<SKPoint> TesselarArco(SKPoint centro, float raioCanvas, double azCentroGraus, double anguloCurva, bool sentidoHorario)
        {
            int divisoes = Math.Max(6, (int)Math.Ceiling(anguloCurva / 7.5));
            var pontos = new List<SKPoint>(divisoes);
            double azOffsetInicial = azCentroGraus + 180.0;
            double thetaTotal = sentidoHorario ? anguloCurva : -anguloCurva;

            for (int k = 1; k <= divisoes; k++)
            {
                double t = (double)k / divisoes;
                double az = (azOffsetInicial + t * thetaTotal) * Math.PI / 180.0;
                pontos.Add(new SKPoint(
                    centro.X + (float)(raioCanvas * Math.Sin(az)),
                    centro.Y - (float)(raioCanvas * Math.Cos(az))
                ));
            }
            return pontos;
        }

        // Tubo calandrado fechado (360°): desenhado como um cilindro 3D dedicado (face traseira,
        // banda lateral sombreada e face frontal aberta), já que o caminho genérico de polígono
        // degenera quando a corda do segmento tem comprimento zero.
        private static void DesenharTuboRedondo3D(SKCanvas canvas, SKPoint frente, float raio, float dx, float dy)
        {
            var tras = new SKPoint(frente.X + dx, frente.Y + dy);

            double comp = Math.Sqrt(dx * dx + dy * dy);
            float px, py;
            if (comp < 1e-6) { px = 1f; py = 0f; }
            else { px = (float)(-dy / comp); py = (float)(dx / comp); }

            var a0 = new SKPoint(frente.X + px * raio, frente.Y + py * raio);
            var a1 = new SKPoint(tras.X + px * raio, tras.Y + py * raio);
            var b0 = new SKPoint(frente.X - px * raio, frente.Y - py * raio);
            var b1 = new SKPoint(tras.X - px * raio, tras.Y - py * raio);

            using var facePaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            facePaint.Color = AcoFundoCor;
            canvas.DrawCircle(tras, raio, facePaint);

            using var bandaPath = new SKPath();
            bandaPath.MoveTo(a0);
            bandaPath.LineTo(a1);
            bandaPath.LineTo(b1);
            bandaPath.LineTo(b0);
            bandaPath.Close();
            facePaint.Color = AcoBase;
            canvas.DrawPath(bandaPath, facePaint);

            using var arestaPaint = new SKPaint { Color = AcoArestaBack, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            canvas.DrawCircle(tras, raio, arestaPaint);
            canvas.DrawLine(a0, a1, arestaPaint);
            canvas.DrawLine(b0, b1, arestaPaint);

            // Face frontal aberta (vazada), com contorno mestre por cima de tudo
            using var aberturaPaint = new SKPaint { Color = FundoCor, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawCircle(frente, raio, aberturaPaint);

            using var perfilPaint = new SKPaint { Color = PerfilContornoCor, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true };
            canvas.DrawCircle(frente, raio, perfilPaint);
        }

        private static void DesenharChapaLisa(SKCanvas canvas, SKSize size, double medida, double comprimento, float fonteCota)
        {
            float cx = size.Width  / 2f;
            float cy = size.Height / 2f;
            float fonte = Math.Clamp(Math.Min(size.Width / 4.5f, size.Height / 3f), fonteCota * 1.2f, 72f);

            using var dimPaint = new SKPaint
            {
                Color = CotaTextoCor, TextSize = fonte, IsAntialias = true,
                TextAlign = SKTextAlign.Center, Typeface = FonteNegrito
            };
            using var subPaint = new SKPaint
            {
                Color = new SKColor(90, 100, 120), TextSize = Math.Max(7f, fonte * 0.32f),
                IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = FonteNegrito
            };

            string textoUmaLinha = $"{medida:F0} × {comprimento:F0}";
            bool duasLinhas = dimPaint.MeasureText(textoUmaLinha) > size.Width * 0.88f;

            if (!duasLinhas)
            {
                canvas.DrawText(textoUmaLinha, cx, cy + fonte * 0.35f, dimPaint);
                canvas.DrawText("CHAPA PLANA (mm)", cx, cy + fonte * 0.83f, subPaint);
            }
            else
            {
                // Duas linhas: medida em cima, "× comprimento" embaixo — evita overflow em canvases estreitos
                float gap   = fonte * 0.20f;
                float bloco = 2f * fonte + gap;
                float y1    = cy - bloco / 2f + fonte * 0.75f; // baseline linha 1
                float y2    = y1 + fonte + gap;                 // baseline linha 2
                canvas.DrawText($"{medida:F0}", cx, y1, dimPaint);
                canvas.DrawText($"× {comprimento:F0}", cx, y2, dimPaint);
                canvas.DrawText("CHAPA PLANA (mm)", cx, y2 + fonte * 0.30f + subPaint.TextSize + 2f, subPaint);
            }
        }

        private static void DesenharCotaTubo(SKCanvas canvas, SKPoint frente, float raio, float dx, float dy, Segmento.InformacaoCurva info, InstrucoesPolares polar, float fonteCota)
        {
            using var textPaint = new SKPaint { Color = CotaTextoCor, TextSize = fonteCota, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };

            string textoDiametro = $"Ø{info.Raio * 2:F0} ({info.TipoRaio})";
            canvas.DrawText(textoDiametro, frente.X, frente.Y + raio + fonteCota + 6f, textPaint);

            var tras = new SKPoint(frente.X + dx, frente.Y + dy);
            string textoComprimento = $"{polar.Comprimento:F0}";
            canvas.DrawText(textoComprimento, (frente.X + tras.X) / 2f, (frente.Y + tras.Y) / 2f - 10f, textPaint);
        }

        // Computa dois caminhos paralelos ao eixo neutro, deslocados por ±offset em pixels.
        // O ponto de junção nas dobras usa miter (interseção das normais adjacentes),
        // limitado para evitar spikes em ângulos muito agudos.
        private static List<SKPoint> ComputarOffsetPolyline(List<SKPoint> pts, float offset)
        {
            int n = pts.Count;
            var result = new List<SKPoint>(n);
            for (int i = 0; i < n; i++)
            {
                float nx, ny;
                if (i == 0)
                {
                    (nx, ny) = NormalDireita(pts[0], pts[1]);
                }
                else if (i == n - 1)
                {
                    (nx, ny) = NormalDireita(pts[n - 2], pts[n - 1]);
                }
                else
                {
                    var (n1x, n1y) = NormalDireita(pts[i - 1], pts[i]);
                    var (n2x, n2y) = NormalDireita(pts[i], pts[i + 1]);
                    float mx = n1x + n2x, my = n1y + n2y;
                    float ml = MathF.Sqrt(mx * mx + my * my);
                    if (ml < 1e-6f) { nx = n1x; ny = n1y; }
                    else
                    {
                        mx /= ml; my /= ml;
                        float dot = n1x * mx + n1y * my;
                        float scale = dot > 0.2f ? MathF.Min(1f / dot, 3.5f) : 1f;
                        nx = mx * scale; ny = my * scale;
                    }
                }
                result.Add(new SKPoint(pts[i].X + nx * offset, pts[i].Y + ny * offset));
            }
            return result;
        }

        // Normal à direita do vetor a→b, no espaço de tela (Y cresce para baixo).
        private static (float nx, float ny) NormalDireita(SKPoint a, SKPoint b)
        {
            float ddx = b.X - a.X, ddy = b.Y - a.Y;
            float len = MathF.Sqrt(ddx * ddx + ddy * ddy);
            if (len < 1e-6f) return (0f, -1f);
            return (ddy / len, -ddx / len);
        }

        private static SKColor InterpolateColor(SKColor a, SKColor b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new SKColor(
                (byte)(a.Red   + t * (b.Red   - a.Red)),
                (byte)(a.Green + t * (b.Green - a.Green)),
                (byte)(a.Blue  + t * (b.Blue  - a.Blue)));
        }

        private static bool Colide(SKRect a, SKRect b) =>
            a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

        private static List<SKRect> DesenharCotas(
            SKCanvas canvas,
            List<SKPoint> pontos,
            List<Segmento> segmentos,
            List<(double X, double Y)> absolutas,
            List<(double Livre, double Interna, double Externa)> medidas,
            IGeometryService geometryService,
            float fonteCota,
            double espessuraPx,
            int? segmentoDestacado = null)
        {
            using var chamadaPaint = new SKPaint
                { Color = CotaLinhaCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var textoExterna = new SKPaint
                { Color = SKColors.White, TextSize = fonteCota, IsAntialias = true,
                  TextAlign = SKTextAlign.Center, Typeface = FonteNegrito };
            using var textoInterna = new SKPaint
                { Color = CotaInternaTextoCor, TextSize = fonteCota, IsAntialias = true,
                  TextAlign = SKTextAlign.Center, Typeface = FonteNegrito };

            float offsetBase = (float)Math.Max(20.0, espessuraPx * 1.5 + fonteCota * 1.0);

            var labelsColocados = new List<SKRect>();

            float OffsetSemColisao(float mx, float my, float nx, float ny, float offset, string texto, SKPaint paint)
            {
                for (int t = 0; t < 6; t++)
                {
                    float px  = mx + nx * offset;
                    float py  = my + ny * offset - 4f;
                    float larg = paint.MeasureText(texto);
                    float padX = paint.TextSize * 0.28f;
                    float padY = paint.TextSize * 0.14f;
                    var fm = paint.FontMetrics;
                    var test = new SKRect(px - larg / 2f - padX, py + fm.Ascent  - padY,
                                         px + larg / 2f + padX, py + fm.Descent + padY);
                    if (!labelsColocados.Any(r => Colide(r, test)))
                        return offset;
                    offset += offsetBase * 0.65f;
                }
                return offset;
            }

            void DesenharMedida(SKPoint p0, SKPoint p1, float nx, float ny, float baseOffset,
                                 string texto, SKPaint textPaint, SKColor fundoCor, SKColor? bordaCor)
            {
                float mx = (p0.X + p1.X) / 2.0f;
                float my = (p0.Y + p1.Y) / 2.0f;
                float offset = OffsetSemColisao(mx, my, nx, ny, baseOffset, texto, textPaint);
                float lx = mx + nx * offset;
                float ly = my + ny * offset - 4f;
                canvas.DrawLine(new SKPoint(mx, my), new SKPoint(lx, ly), chamadaPaint);
                var bounds = DesenharRotuloComFundo(canvas, new SKPoint(lx, ly),
                    texto, textPaint, fundoCor, bordaCor);
                labelsColocados.Add(bounds);
            }

            for (int i = 0; i < pontos.Count - 1; i++)
            {
                if (i >= segmentos.Count || i >= medidas.Count) break;

                var p0  = pontos[i];
                var p1  = pontos[i + 1];
                var seg = segmentos[i];

                float ddx = p1.X - p0.X, ddy = p1.Y - p0.Y;
                float len = MathF.Sqrt(ddx * ddx + ddy * ddy);
                if (len < 0.5f) continue;

                float nx = -ddy / len, ny = ddx / len;
                int ladoInterno = geometryService.DeterminarLadoInternoSegmento(i, absolutas);
                bool destacado  = segmentoDestacado.HasValue && i == segmentoDestacado.Value;

                // Badge EXTERNO: fundo marinho + texto branco  (P&B: preto/branco = máximo contraste)
                var pTextoExt = destacado
                    ? new SKPaint { Color = SKColors.White, TextSize = fonteCota * 1.15f,
                                    IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = FonteNegrito }
                    : textoExterna;
                SKColor fundoExt = destacado ? SegmentoDestaqueCor : CotaTextoCor;
                string textoExt  = seg.EhCurvo && seg.CurvaInfo != null
                    ? $"{medidas[i].Externa:F0}c" : $"{medidas[i].Externa:F0}";
                DesenharMedida(p0, p1, nx * -ladoInterno, ny * -ladoInterno,
                               offsetBase * (destacado ? 1.4f : 1f), textoExt, pTextoExt, fundoExt, null);
                if (destacado) pTextoExt.Dispose();

                // Badge INTERNO: fundo branco + borda escura + texto preto  (P&B: branco+borda preta = legível)
                if (!seg.EhCurvo)
                {
                    var pTextoInt = destacado
                        ? new SKPaint { Color = SKColors.White, TextSize = fonteCota * 1.15f,
                                        IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = FonteNegrito }
                        : textoInterna;
                    SKColor  fundoInt = destacado ? SegmentoDestaqueCor : SKColors.White;
                    SKColor? bordaInt = destacado ? null : CotaLinhaCor;
                    DesenharMedida(p0, p1, nx * ladoInterno, ny * ladoInterno,
                                   offsetBase * (destacado ? 1.4f : 0.82f),
                                   $"{medidas[i].Interna:F0}", pTextoInt, fundoInt, bordaInt);
                    if (destacado) pTextoInt.Dispose();
                }
            }
            return labelsColocados;
        }

        // Desenha um texto sobre um selo (retângulo arredondado preenchido) e retorna os limites do selo
        // para que o chamador possa registrá-lo na lista de colisão.
        // bordaCor: quando informado, desenha contorno adicional no selo (útil para badges brancos).
        private static SKRect DesenharRotuloComFundo(SKCanvas canvas, SKPoint pos, string texto,
                                                     SKPaint textPaint, SKColor fundoCor,
                                                     SKColor? bordaCor = null)
        {
            float largura = textPaint.MeasureText(texto);
            var metrics = textPaint.FontMetrics;

            float left = textPaint.TextAlign switch
            {
                SKTextAlign.Center => pos.X - largura / 2f,
                SKTextAlign.Right  => pos.X - largura,
                _ => pos.X
            };

            float paddingX = textPaint.TextSize * 0.28f;
            float paddingY = textPaint.TextSize * 0.14f;
            var rect = new SKRect(
                left   - paddingX,
                pos.Y  + metrics.Ascent  - paddingY,
                left   + largura + paddingX,
                pos.Y  + metrics.Descent + paddingY);

            using var fundoPaint = new SKPaint { Color = fundoCor, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRoundRect(rect, 3f, 3f, fundoPaint);

            if (bordaCor.HasValue)
            {
                using var bordaPaint = new SKPaint
                    { Color = bordaCor.Value, Style = SKPaintStyle.Stroke, StrokeWidth = 1.3f, IsAntialias = true };
                canvas.DrawRoundRect(rect, 3f, 3f, bordaPaint);
            }

            canvas.DrawText(texto, pos.X, pos.Y, textPaint);
            return rect;
        }

        private static void DesenharGrausDobra(
            SKCanvas canvas,
            List<SKPoint> pontos,
            List<(double X, double Y)> absolutas,
            InstrucoesPolares polar,
            IGeometryService geometryService,
            double escala,
            float fonteAngulo,
            List<SKRect>? labelsOcupados = null)
        {
            if (pontos.Count < 3) return;

            var azimutes = polar.CoordenadasPolares.Select(c => c.Azimute).ToList();
            var angulosDobra = geometryService.ObterAngulosDobraDeAzimutes(azimutes);
            if (angulosDobra.Count == 0) return;

            using var textPaint = new SKPaint
            {
                Color = AnguloTextoCor,
                TextSize = fonteAngulo,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = FonteNegrito
            };

            using var simboloPaint = new SKPaint
            {
                Color = AnguloTextoCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.8f,
                IsAntialias = true
            };

            double espessuraLinhaPx = Math.Max(2.5, polar.Espessura * escala);
            double distBase = Math.Max(espessuraLinhaPx * 1.2 + fonteAngulo * 1.4, fonteAngulo * 2.4);
            double tamAnguloReto = Math.Max(7.0, Math.Max(fonteAngulo * 0.55, espessuraLinhaPx * 0.55));

            for (int idx = 0; idx < angulosDobra.Count; idx++)
            {
                int v = idx + 1;
                if (v <= 0 || v >= pontos.Count - 1) continue;

                // Vértices adjacentes a um segmento curvo são artefato da representação por corda,
                // não uma dobra real — sem isso, uma curva suave de 90° aparecia como duas dobras de 45°.
                bool segAnteriorCurvo = v - 1 < polar.SegmentosOriginal.Count && polar.SegmentosOriginal[v - 1].EhCurvo;
                bool segPosteriorCurvo = v < polar.SegmentosOriginal.Count && polar.SegmentosOriginal[v].EhCurvo;
                if (segAnteriorCurvo || segPosteriorCurvo) continue;

                var p0 = pontos[v - 1];
                var p1 = pontos[v];
                var p2 = pontos[v + 1];

                double dxIn = p1.X - p0.X, dyIn = p1.Y - p0.Y;
                double dxOut = p2.X - p1.X, dyOut = p2.Y - p1.Y;
                double compIn = Math.Sqrt(dxIn * dxIn + dyIn * dyIn);
                double compOut = Math.Sqrt(dxOut * dxOut + dyOut * dyOut);
                if (compIn < 1e-6 || compOut < 1e-6) continue;

                double tInX = dxIn / compIn, tInY = dyIn / compIn;
                double tOutX = dxOut / compOut, tOutY = dyOut / compOut;

                int lado1 = geometryService.DeterminarLadoInternoSegmento(v - 1, absolutas);
                int lado2 = geometryService.DeterminarLadoInternoSegmento(v, absolutas);

                double n1x = -tInY, n1y = tInX;
                double n2x = -tOutY, n2y = tOutX;

                double bx = n1x * lado1 + n2x * lado2;
                double by = n1y * lado1 + n2y * lado2;
                double comp = Math.Sqrt(bx * bx + by * by);

                double dirX, dirY;
                if (comp < 1e-6)
                {
                    dirX = -n1x * lado1;
                    dirY = -n1y * lado1;
                }
                else
                {
                    dirX = -bx / comp;
                    dirY = -by / comp;
                }

                double angulo = angulosDobra[idx];
                if (Math.Abs(angulo - 90.0) < 0.5)
                {
                    // Símbolo de ângulo reto (esquadro) — apenas os traços, sem fundo
                    var a = new SKPoint((float)(p1.X - tInX  * tamAnguloReto), (float)(p1.Y - tInY  * tamAnguloReto));
                    var b = new SKPoint((float)(p1.X + tOutX * tamAnguloReto), (float)(p1.Y + tOutY * tamAnguloReto));
                    var c = new SKPoint((float)(a.X  + tOutX * tamAnguloReto), (float)(a.Y  + tOutY * tamAnguloReto));
                    canvas.DrawLine(a, c, simboloPaint);
                    canvas.DrawLine(c, b, simboloPaint);
                }
                else
                {
                    string texto = FormatarAnguloDobra(angulo);
                    float dist = (float)distBase;
                    if (labelsOcupados != null)
                    {
                        for (int t = 0; t < 5; t++)
                        {
                            float cx = (float)(p1.X + dirX * dist);
                            float cy = (float)(p1.Y + dirY * dist) - 4f;
                            float lw = textPaint.MeasureText(texto);
                            var fm2 = textPaint.FontMetrics;
                            float px2 = textPaint.TextSize * 0.28f, py2 = textPaint.TextSize * 0.14f;
                            var test = new SKRect(cx - lw/2f - px2, cy + fm2.Ascent - py2,
                                                  cx + lw/2f + px2, cy + fm2.Descent + py2);
                            if (!labelsOcupados.Any(r => Colide(r, test))) break;
                            dist += (float)distBase * 0.6f;
                        }
                    }
                    float tx = (float)(p1.X + dirX * dist);
                    float ty = (float)(p1.Y + dirY * dist) - 4f;
                    var angBounds = DesenharRotuloComFundo(canvas, new SKPoint(tx, ty), texto, textPaint, SKColors.White, CotaLinhaCor);
                    labelsOcupados?.Add(angBounds);
                    float larg = textPaint.MeasureText(texto);
                    float ySub = ty + fonteAngulo * 0.25f;
                    canvas.DrawLine(tx - larg / 2f, ySub, tx + larg / 2f, ySub, simboloPaint);
                }
            }
        }

        private static string FormatarAnguloDobra(double angulo)
        {
            return $"{(long)Math.Round(angulo)}°";
        }

        public void RenderizarPlanificacao(SKCanvas canvas, SKSize size, DadosPlanificacao plano, float fonteAngulo = 10f, float fonteSentido = 10f, float fonteCota = 10f)
        {
            canvas.Clear(FundoCor);
            canvas.ClipRect(new SKRect(0, 0, size.Width, size.Height));

            float margemX     = size.Width * 0.12f;
            float utilW       = size.Width - 2 * margemX;
            float alturaChapa = 28f;

            // Posiciona yBase proporcionalmente: anotações acima precisam de ~50pt além da meia-chapa,
            // anotações abaixo precisam de ~56pt — repartir o canvas nessa proporção evita overflow.
            const float topNeed = 50f;
            const float botNeed = 56f;
            float yBase = size.Height * (alturaChapa / 2f + topNeed) / (alturaChapa + topNeed + botNeed);

            float y0 = yBase - alturaChapa / 2.0f;
            float y1 = yBase + alturaChapa / 2.0f;

            double corteTotal = plano.CorteTotal > 0 ? plano.CorteTotal : 1;
            float EscalaX(double p) => margemX + (float)(p / corteTotal * utilW);

            // — Paints —
            using var chapaPaint  = new SKPaint { Color = PerfilPreenchimentoCor, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var bordaPaint  = new SKPaint { Color = PerfilContornoCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var marcaPaint  = new SKPaint { Color = PerfilContornoCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, IsAntialias = true };
            using var cotaPaint   = new SKPaint { Color = CotaLinhaCor,     Style = SKPaintStyle.Stroke, StrokeWidth = 1.1f, IsAntialias = true };
            using var subPaint    = new SKPaint { Color = CotaLinhaCor,     Style = SKPaintStyle.Stroke, StrokeWidth = 1.1f, IsAntialias = true };

            using var pAngulo  = new SKPaint { Color = AnguloTextoCor, TextSize = fonteAngulo,  IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = FonteNegrito };
            using var pSentido = new SKPaint { Color = AnguloTextoCor, TextSize = fonteSentido, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = FonteNegrito };
            using var pCota    = new SKPaint { Color = CotaLinhaCor,   TextSize = fonteCota,    IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = FonteNegrito };
            using var pCaland  = new SKPaint { Color = CotaTextoCor,   TextSize = Math.Max(fonteCota, 8f), IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = FonteNegrito };

            // — 1. Chapa esticada —
            canvas.DrawRect(margemX, y0, utilW, alturaChapa, chapaPaint);
            canvas.DrawRect(margemX, y0, utilW, alturaChapa, bordaPaint);

            // — 2. Marcas de dobra —
            foreach (var marca in plano.MarcasDobra)
            {
                float x = EscalaX(marca.Posicao);

                marcaPaint.PathEffect = marca.Sentido == "a"
                    ? SKPathEffect.CreateDash(new float[] { 4, 3 }, 0) : null;
                canvas.DrawLine(x, y0 - 3f, x, y1 + 3f, marcaPaint);

                // Ângulo: badge branco + borda escura (visível sobre qualquer fundo)
                string textoAng = $"{marca.AnguloDobra:F0}°";
                DesenharRotuloComFundo(canvas, new SKPoint(x, y0 - 15f), textoAng, pAngulo,
                                       SKColors.White, CotaLinhaCor);
                float largAng = pAngulo.MeasureText(textoAng);
                canvas.DrawLine(x - largAng / 2f, y0 - 13f,
                                x + largAng / 2f, y0 - 13f, subPaint);

                // Sentido: texto simples (está sobre fundo claro)
                string dirText = marca.Sentido == "h" ? "P.Baixo" : "P.Cima";
                canvas.DrawText(dirText, x, y0 - 5f, pSentido);
            }

            // — 3. Calandragem —
            using var calandraPaint = new SKPaint
                { Color = CotaTextoCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.3f, IsAntialias = true };
            calandraPaint.PathEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0);
            foreach (var mc in plano.MarcasCalandragem)
            {
                float xIni  = EscalaX(mc.PosicaoInicio);
                float xFim  = EscalaX(mc.PosicaoFim);
                canvas.DrawLine(xIni, y0 - 2f, xIni, y1 + 2f, calandraPaint);
                canvas.DrawLine(xFim, y0 - 2f, xFim, y1 + 2f, calandraPaint);
                DesenharRotuloComFundo(canvas, new SKPoint((xIni + xFim) / 2f, y0 - 15f),
                    $"CAL  R={mc.Raio:F0}  {mc.AnguloCurva:F0}°", pCaland, SKColors.White, CotaTextoCor);
            }

            // — 4. Cotas acumuladas (topo) — mesmas posições do original, só fonte negrito
            if (plano.PosicoesOrdenadas.Count > 0)
            {
                canvas.DrawLine(EscalaX(plano.PosicoesOrdenadas[0]),    y0 - 28f,
                                EscalaX(plano.PosicoesOrdenadas.Last()), y0 - 28f, cotaPaint);
                foreach (int pos in plano.PosicoesOrdenadas)
                {
                    float x = EscalaX(pos);
                    canvas.DrawLine(x, y0 - 25f, x, y0 - 32f, cotaPaint);
                    canvas.DrawText($"{pos}", x, y0 - 36f, pCota);
                }
            }

            // — 5. Cotas em cadeia (base) — mesmas posições do original, só fonte negrito
            foreach (var trecho in plano.TrechosCadeia)
            {
                float x0c = EscalaX(trecho.Inicio);
                float x1c = EscalaX(trecho.Fim);
                canvas.DrawLine(x0c, y1 + 25f, x0c, y1 + 32f, cotaPaint);
                canvas.DrawLine(x1c, y1 + 25f, x1c, y1 + 32f, cotaPaint);
                canvas.DrawLine(x0c, y1 + 28f, x1c, y1 + 28f, cotaPaint);
                canvas.DrawText($"{trecho.Comprimento}", (x0c + x1c) / 2f, y1 + 42f, pCota);
            }
        }
    }
}
