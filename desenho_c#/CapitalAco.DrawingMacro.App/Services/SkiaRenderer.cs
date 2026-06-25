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
    public static class SkiaRenderer
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
        private static readonly SKColor CotaLinhaCor        = new SKColor(160, 170, 184);
        private static readonly SKColor CotaTextoCor        = new SKColor(21,  101, 192);
        private static readonly SKColor CotaInternaLinhaCor = new SKColor(229, 154, 154);
        private static readonly SKColor CotaInternaTextoCor = new SKColor(183, 28,  28);
        private static readonly SKColor AnguloTextoCor      = new SKColor(46,  125, 50);
        private static readonly SKColor SegmentoDestaqueCor = new SKColor(255, 120, 0);

        public static System.Windows.Media.ImageSource RenderToImageSource(InstrucoesPolares polar, int width, int height, IGeometryService geometryService, float fonteCota = 12f, float fonteAngulo = 11f, bool mostrarMedidas = true, int? segmentoDestacado = null, bool destacarProximaOrigem = false)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(FundoCor);

            var dim = geometryService.CalcularDimensoesAcabadas(polar);
            RenderizarPeca(canvas, new SKSize(width, height), polar, dim, mostrarMedidas, geometryService, fonteCota, fonteAngulo, segmentoDestacado, destacarProximaOrigem);

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

        public static void RenderizarPeca(
            SKCanvas canvas,
            SKSize size,
            InstrucoesPolares polar,
            DimensoesAcabadas? dimensoes,
            bool mostrarMedidas,
            IGeometryService geometryService,
            float fonteCota = 12f,
            float fonteAngulo = 11f,
            int? segmentoDestacado = null,
            bool destacarProximaOrigem = false)
        {
            if (polar.CoordenadasPolares.Count == 0)
                return;

            // 1. Calcular coordenadas retangulares
            var parciais = ((GeometryService)geometryService).GerarCoordenadasRetangularesParciais(polar.CoordenadasPolares);
            var absolutas = ((GeometryService)geometryService).GerarCoordenadasRetangularesAbsolutas(parciais);

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
                double rNeutroBbox = rInternoBbox + polar.KFactor * polar.Espessura;
                var (cx, cy) = absolutas[i];
                pontosParaEscala.Add((cx - rNeutroBbox, cy - rNeutroBbox));
                pontosParaEscala.Add((cx + rNeutroBbox, cy + rNeutroBbox));
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
                DesenharCotas(canvas, coordenadasNoCanvas, polar.SegmentosOriginal, absolutas, medidas, geometryService, fonteCota, espessuraPx, segmentoDestacado);
                DesenharGrausDobra(canvas, coordenadasNoCanvas, absolutas, polar, geometryService, escala, fonteAngulo);
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

        private static void DesenharCotas(
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
            // Sem caixas de cota (sem linhas de extensão nem linha dupla): só uma linha de chamada curta
            // do meio do segmento até o texto. O deslocamento cresce com a espessura da chapa para a
            // medida nunca ficar escondida atrás do traço espesso do perfil em chapas grossas.
            using var chamadaPaintExterna = new SKPaint { Color = CotaLinhaCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f, IsAntialias = true };
            using var chamadaPaintInterna = new SKPaint { Color = CotaInternaLinhaCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f, IsAntialias = true };
            // Em vez de diferenciar interna/externa só pela cor do texto (ou um sufixo "i"), cada medida
            // ganha um "selo" com fundo preenchido: externa = letra branca em fundo escuro (azul), interna =
            // letra escura em fundo claro (rosa) — funciona mesmo em impressão P&B, pela diferença de tom.
            using var textoExterna = new SKPaint { Color = SKColors.White, TextSize = fonteCota, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };
            using var textoInterna = new SKPaint { Color = CotaInternaTextoCor, TextSize = fonteCota, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };

            float offsetExterna = (float)Math.Max(16.0, espessuraPx * 1.3 + fonteCota * 0.6);
            float offsetInterna = (float)Math.Max(12.0, espessuraPx * 1.1 + fonteCota * 0.4);

            void DesenharMedida(SKPoint p0, SKPoint p1, float nx, float ny, float offset, string texto, SKPaint chamadaPaint, SKPaint textPaint, SKColor fundoCor)
            {
                float mx = (p0.X + p1.X) / 2.0f;
                float my = (p0.Y + p1.Y) / 2.0f;
                var pTexto = new SKPoint(mx + nx * offset, my + ny * offset);

                canvas.DrawLine(new SKPoint(mx, my), pTexto, chamadaPaint);
                DesenharRotuloComFundo(canvas, new SKPoint(pTexto.X, pTexto.Y - 4f), texto, textPaint, fundoCor);
            }

            // Para cada segmento desenhamos a medida externa (selo azul escuro, lado de fora) e a interna
            // (selo rosa claro, lado de dentro).
            for (int i = 0; i < pontos.Count - 1; i++)
            {
                if (i >= segmentos.Count || i >= medidas.Count) break;

                var p0 = pontos[i];
                var p1 = pontos[i + 1];
                var seg = segmentos[i];

                float dx = p1.X - p0.X;
                float dy = p1.Y - p0.Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len == 0) continue;

                float nx = -dy / len;
                float ny = dx / len;

                int ladoInterno = geometryService.DeterminarLadoInternoSegmento(i, absolutas);

                bool destacado = segmentoDestacado.HasValue && i == segmentoDestacado.Value;
                SKColor fundoExt = destacado ? SegmentoDestaqueCor : CotaTextoCor;
                SKPaint chamadaExt = destacado
                    ? new SKPaint { Color = SegmentoDestaqueCor, Style = SKPaintStyle.Stroke, StrokeWidth = 2.0f, IsAntialias = true }
                    : chamadaPaintExterna;
                SKPaint textoExt2 = destacado
                    ? new SKPaint { Color = SKColors.White, TextSize = fonteCota * 1.15f, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true }
                    : textoExterna;

                string textoExtStr = seg.EhCurvo && seg.CurvaInfo != null
                    ? $"{medidas[i].Externa:F0} (Curva)"
                    : $"{medidas[i].Externa:F0}";
                DesenharMedida(p0, p1, nx * -ladoInterno, ny * -ladoInterno, offsetExterna * (destacado ? 1.3f : 1f), textoExtStr, chamadaExt, textoExt2, fundoExt);

                if (destacado) { chamadaExt.Dispose(); textoExt2.Dispose(); }

                if (!seg.EhCurvo)
                {
                    string textoIntStr = $"{medidas[i].Interna:F0}";
                    SKColor fundoInt = destacado ? SegmentoDestaqueCor.WithAlpha(180) : CotaInternaLinhaCor;
                    SKPaint chamadaInt = destacado
                        ? new SKPaint { Color = SegmentoDestaqueCor, Style = SKPaintStyle.Stroke, StrokeWidth = 2.0f, IsAntialias = true }
                        : chamadaPaintInterna;
                    SKPaint textoInt2 = destacado
                        ? new SKPaint { Color = SKColors.White, TextSize = fonteCota * 1.15f, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true }
                        : textoInterna;
                    DesenharMedida(p0, p1, nx * ladoInterno, ny * ladoInterno, offsetInterna * (destacado ? 1.3f : 1f), textoIntStr, chamadaInt, textoInt2, fundoInt);
                    if (destacado) { chamadaInt.Dispose(); textoInt2.Dispose(); }
                }
            }
        }

        // Desenha um texto centrado em "pos" sobre um selo (retângulo arredondado preenchido), usado para
        // diferenciar medida externa/interna por contraste de fundo em vez de cor de texto ou sufixo.
        private static void DesenharRotuloComFundo(SKCanvas canvas, SKPoint pos, string texto, SKPaint textPaint, SKColor fundoCor)
        {
            // MeasureText(text, ref bounds) devolve os limites de tinta como se o texto fosse desenhado
            // alinhado à esquerda a partir de "pos", ignorando o TextAlign do paint — por isso calculamos a
            // borda esquerda manualmente de acordo com o alinhamento, em vez de usar bounds.Left/Right.
            float largura = textPaint.MeasureText(texto);
            var metrics = textPaint.FontMetrics;

            float left = textPaint.TextAlign switch
            {
                SKTextAlign.Center => pos.X - largura / 2f,
                SKTextAlign.Right => pos.X - largura,
                _ => pos.X
            };

            float paddingX = textPaint.TextSize * 0.32f;
            float paddingY = textPaint.TextSize * 0.16f;
            var rect = new SKRect(
                left - paddingX,
                pos.Y + metrics.Ascent - paddingY,
                left + largura + paddingX,
                pos.Y + metrics.Descent + paddingY);

            using var fundoPaint = new SKPaint { Color = fundoCor, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRoundRect(rect, 3f, 3f, fundoPaint);
            canvas.DrawText(texto, pos.X, pos.Y, textPaint);
        }

        private static void DesenharGrausDobra(
            SKCanvas canvas,
            List<SKPoint> pontos,
            List<(double X, double Y)> absolutas,
            InstrucoesPolares polar,
            IGeometryService geometryService,
            double escala,
            float fonteAngulo)
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
                FakeBoldText = true
            };

            using var anguloRetoPaint = new SKPaint
            {
                Color = AnguloTextoCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.3f,
                IsAntialias = true
            };

            double espessuraLinhaPx = Math.Max(2.5, polar.Espessura * escala);
            double distBase = Math.Max(espessuraLinhaPx * 0.8 + fonteAngulo * 0.5, fonteAngulo * 1.1);
            double tamAnguloReto = Math.Max(5.0, Math.Max(fonteAngulo * 0.38, espessuraLinhaPx * 0.42));

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
                    // Dobras de 90° são indicadas por um símbolo de esquadro (ângulo reto), não por texto.
                    var a = new SKPoint((float)(p1.X - tInX * tamAnguloReto), (float)(p1.Y - tInY * tamAnguloReto));
                    var b = new SKPoint((float)(p1.X + tOutX * tamAnguloReto), (float)(p1.Y + tOutY * tamAnguloReto));
                    var c = new SKPoint((float)(a.X + tOutX * tamAnguloReto), (float)(a.Y + tOutY * tamAnguloReto));
                    canvas.DrawLine(a, c, anguloRetoPaint);
                    canvas.DrawLine(c, b, anguloRetoPaint);
                }
                else
                {
                    string texto = FormatarAnguloDobra(angulo);
                    float tx = (float)(p1.X + dirX * distBase);
                    float ty = (float)(p1.Y + dirY * distBase + fonteAngulo * 0.3);
                    canvas.DrawText(texto, tx, ty, textPaint);

                    // Ângulos não-retos são sublinhados, para diferenciar visualmente do esquadro de 90°.
                    float larguraTexto = textPaint.MeasureText(texto);
                    float ySublinhado = ty + fonteAngulo * 0.18f;
                    canvas.DrawLine(tx - larguraTexto / 2f, ySublinhado, tx + larguraTexto / 2f, ySublinhado, anguloRetoPaint);
                }
            }
        }

        private static string FormatarAnguloDobra(double angulo)
        {
            return $"{(long)Math.Round(angulo)}°";
        }

        public static void RenderizarPlanificacao(SKCanvas canvas, SKSize size, DadosPlanificacao plano, float fonteAngulo = 10f, float fonteSentido = 10f, float fonteCota = 10f)
        {
            canvas.Clear(FundoCor);

            float margemX = size.Width * 0.12f;
            float utilW = size.Width - 2 * margemX;

            float yBase = size.Height / 2.0f;
            float alturaChapa = 28f;

            float y0 = yBase - alturaChapa / 2.0f;
            float y1 = yBase + alturaChapa / 2.0f;

            double corteTotal = plano.CorteTotal;
            if (corteTotal == 0) corteTotal = 1;

            float EscalaX(double rawPos) => margemX + (float)(rawPos / corteTotal * utilW);

            // 1. Desenhar retângulo da chapa esticada
            using var chapaPaint = new SKPaint
            {
                Color = PerfilPreenchimentoCor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRect(margemX, y0, utilW, alturaChapa, chapaPaint);

            using var bordaPaint = new SKPaint
            {
                Color = PerfilContornoCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };
            canvas.DrawRect(margemX, y0, utilW, alturaChapa, bordaPaint);

            // 2. Desenhar marcas de dobras
            using var marcaDobraPaint = new SKPaint
            {
                Color = PerfilContornoCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                IsAntialias = true
            };

            using var sublinhadoAnguloPaint = new SKPaint
            {
                Color = PerfilContornoCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                IsAntialias = true
            };

            using var textPaint = new SKPaint
            {
                Color = PerfilContornoCor,
                TextSize = fonteCota,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };

            using var textPaintAngulo = new SKPaint
            {
                Color = PerfilContornoCor,
                TextSize = fonteAngulo,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };

            using var textPaintSentido = new SKPaint
            {
                Color = PerfilContornoCor,
                TextSize = fonteSentido,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };

            using var textBluePaint = new SKPaint
            {
                Color = new SKColor(21, 101, 192),
                TextSize = 10f,
                IsAntialias = true,
                FakeBoldText = true,
                TextAlign = SKTextAlign.Center
            };

            foreach (var marca in plano.MarcasDobra)
            {
                float x = EscalaX(marca.Posicao);

                // Dobra contínua ou tracejada dependendo do sentido
                if (marca.Sentido == "a")
                {
                    marcaDobraPaint.PathEffect = SKPathEffect.CreateDash(new float[] { 4, 3 }, 0);
                }
                else
                {
                    marcaDobraPaint.PathEffect = null;
                }
                canvas.DrawLine(x, y0 - 3, x, y1 + 3, marcaDobraPaint);

                // Ângulo (sublinhado, mesma convenção do desenho da peça) e sentido
                string textoAngulo = $"{marca.AnguloDobra:F0}°";
                canvas.DrawText(textoAngulo, x, y0 - 15, textPaintAngulo);
                float larguraAngulo = textPaintAngulo.MeasureText(textoAngulo);
                canvas.DrawLine(x - larguraAngulo / 2f, y0 - 13, x + larguraAngulo / 2f, y0 - 13, sublinhadoAnguloPaint);
                string dirText = marca.Sentido == "h" ? "P.Baixo" : "P.Cima";
                canvas.DrawText(dirText, x, y0 - 5, textPaintSentido);
            }

            // 3. Desenhar marcas de calandragem (curvas)
            using var calandraPaint = new SKPaint
            {
                Color = new SKColor(21, 101, 192),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.2f,
                IsAntialias = true
            };
            calandraPaint.PathEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0);

            foreach (var mc in plano.MarcasCalandragem)
            {
                float xIni = EscalaX(mc.PosicaoInicio);
                float xFim = EscalaX(mc.PosicaoFim);

                canvas.DrawLine(xIni, y0 - 2, xIni, y1 + 2, calandraPaint);
                canvas.DrawLine(xFim, y0 - 2, xFim, y1 + 2, calandraPaint);

                // Rótulo
                float xMeio = (xIni + xFim) / 2.0f;
                canvas.DrawText("CALANDRAGEM", xMeio, y0 - 15, textBluePaint);
                canvas.DrawText($"R={mc.Raio:F0} / A={mc.AnguloCurva:F0}°", xMeio, y0 - 5, textBluePaint);
            }

            // 4. Desenhar cotas ordenadas no topo
            using var cotaPaint = new SKPaint
            {
                Color = CotaLinhaCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.8f,
                IsAntialias = true
            };

            for (int i = 0; i < plano.PosicoesOrdenadas.Count; i++)
            {
                double pos = plano.PosicoesOrdenadas[i];
                float x = EscalaX(pos);
                canvas.DrawLine(x, y0 - 25, x, y0 - 32, cotaPaint);

                canvas.DrawText($"{pos:F0}", x, y0 - 36, textPaint);
            }
            canvas.DrawLine(EscalaX(plano.PosicoesOrdenadas[0]), y0 - 28, EscalaX(plano.PosicoesOrdenadas.Last()), y0 - 28, cotaPaint);

            // 5. Desenhar cotas em cadeia na base
            for (int i = 0; i < plano.TrechosCadeia.Count; i++)
            {
                var trecho = plano.TrechosCadeia[i];
                float x0 = EscalaX(trecho.Inicio);
                float x1 = EscalaX(trecho.Fim);

                canvas.DrawLine(x0, y1 + 25, x0, y1 + 32, cotaPaint);
                canvas.DrawLine(x1, y1 + 25, x1, y1 + 32, cotaPaint);
                canvas.DrawLine(x0, y1 + 28, x1, y1 + 28, cotaPaint);

                canvas.DrawText($"{trecho.Comprimento:F0}", (x0 + x1) / 2.0f, y1 + 42, textPaint);
            }
        }
    }
}
