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
        private static readonly SKColor FundoCor = new SKColor(255, 255, 255);
        private static readonly SKColor PerfilContornoCor = new SKColor(38, 50, 66);
        private static readonly SKColor PerfilPreenchimentoCor = new SKColor(176, 190, 208);
        private static readonly SKColor ExtrusaoFaceCor = new SKColor(218, 225, 234);
        private static readonly SKColor ExtrusaoFaceSombraCor = new SKColor(198, 206, 218);
        private static readonly SKColor ExtrusaoArestaCor = new SKColor(130, 142, 158);
        private static readonly SKColor CotaLinhaCor = new SKColor(160, 170, 184);
        private static readonly SKColor CotaTextoCor = new SKColor(21, 101, 192);
        private static readonly SKColor CotaInternaLinhaCor = new SKColor(229, 154, 154);
        private static readonly SKColor CotaInternaTextoCor = new SKColor(183, 28, 28);
        private static readonly SKColor AnguloTextoCor = new SKColor(46, 125, 50);

        public static System.Windows.Media.ImageSource RenderToImageSource(InstrucoesPolares polar, int width, int height, IGeometryService geometryService, float fonteCota = 12f, float fonteAngulo = 11f)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(FundoCor);

            var dim = geometryService.CalcularDimensoesAcabadas(polar);
            RenderizarPeca(canvas, new SKSize(width, height), polar, dim, true, geometryService, fonteCota, fonteAngulo);

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
            float fonteAngulo = 11f)
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

            double escala = Math.Min(utilW / dimX, utilH / dimY);

            // Translação para centralizar
            double dimXScaled = dimX * escala;
            double dimYScaled = dimY * escala;
            double fatorX = (canvasW - dimXScaled) / 2.0 - minX * escala;
            double fatorY = (canvasH - dimYScaled) / 2.0 - minY * escala;

            var coordenadasNoCanvas = absolutas.Select(p => new SKPoint(
                (float)(p.X * escala + fatorX),
                (float)(p.Y * escala + fatorY)
            )).ToList();

            // 2. Projetar extrusão 3D
            double comprimento = polar.Comprimento;
            // Reduz escala do 3D para caber
            double compEsboco = comprimento * escala * 0.25; 
            double angulo3D = 55.0 * (Math.PI / 180.0);
            float dx = (float)(compEsboco * Math.Cos(angulo3D));
            float dy = (float)(-compEsboco * Math.Sin(angulo3D));

            var coordenadasExtrusao = coordenadasNoCanvas.Select(p => new SKPoint(p.X + dx, p.Y + dy)).ToList();

            // 3. Desenhar faces 3D (Painters Algorithm)
            var facesInfo = new List<(double Depth, SKPoint[] Path, SKColor Cor)>();
            for (int i = 0; i < coordenadasNoCanvas.Count - 1; i++)
            {
                var p0 = coordenadasNoCanvas[i];
                var p1 = coordenadasNoCanvas[i + 1];
                var pe0 = coordenadasExtrusao[i];
                var pe1 = coordenadasExtrusao[i + 1];

                var pathPoints = new SKPoint[] { p0, p1, pe1, pe0 };
                double midX = (p0.X + p1.X) / 2.0;
                double midY = (p0.Y + p1.Y) / 2.0;
                double depth = midY * Math.Abs(dy) - midX * dx;

                var cor = i % 2 == 0 ? ExtrusaoFaceCor : ExtrusaoFaceSombraCor;
                facesInfo.Add((depth, pathPoints, cor));
            }

            // Ordenar por profundidade (mais distantes primeiro)
            facesInfo = facesInfo.OrderByDescending(f => f.Depth).ToList();

            using var facePaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            foreach (var face in facesInfo)
            {
                using var path = new SKPath();
                path.MoveTo(face.Path[0]);
                path.LineTo(face.Path[1]);
                path.LineTo(face.Path[2]);
                path.LineTo(face.Path[3]);
                path.Close();

                facePaint.Color = face.Cor;
                canvas.DrawPath(path, facePaint);
            }

            // 4. Desenhar arestas traseiras e conexões
            using var arestaPaint = new SKPaint
            {
                Color = ExtrusaoArestaCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };

            // Desenhar linhas de dobra/conexão
            for (int i = 0; i < coordenadasNoCanvas.Count; i++)
            {
                canvas.DrawLine(coordenadasNoCanvas[i], coordenadasExtrusao[i], arestaPaint);
            }

            // Desenhar perfil traseiro
            for (int i = 0; i < coordenadasExtrusao.Count - 1; i++)
            {
                canvas.DrawLine(coordenadasExtrusao[i], coordenadasExtrusao[i + 1], arestaPaint);
            }

            // 5. Desenhar perfil frontal mestre
            using var perfilPaint = new SKPaint
            {
                Color = PerfilContornoCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)Math.Max(2.5, polar.Espessura * escala),
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true
            };

            var azimutesOriginais = geometryService.ObterAzimutesDeSegmentos(polar.SegmentosOriginal);
            using var perfilPath = new SKPath();
            perfilPath.MoveTo(coordenadasNoCanvas[0]);
            for (int i = 0; i < coordenadasNoCanvas.Count - 1; i++)
            {
                var p1 = coordenadasNoCanvas[i + 1];
                var segOriginal = i < polar.SegmentosOriginal.Count ? polar.SegmentosOriginal[i] : null;

                if (segOriginal is { EhCurvo: true, CurvaInfo: not null } && i < azimutesOriginais.Count)
                {
                    var info = segOriginal.CurvaInfo;
                    double rInterno = info.TipoRaio == "interno" ? info.Raio : info.Raio - polar.Espessura;
                    double rNeutro = rInterno + polar.KFactor * polar.Espessura;
                    float raioCanvas = (float)Math.Max(0.5, rNeutro * escala);

                    if (info.AnguloCurva >= 359.5)
                    {
                        // Tubo calandrado fechado (360°): corda de comprimento zero, desenhamos o círculo completo.
                        perfilPath.AddCircle(coordenadasNoCanvas[i].X, coordenadasNoCanvas[i].Y, raioCanvas);
                        perfilPath.MoveTo(p1);
                    }
                    else
                    {
                        bool sentidoHorario = DeterminarSentidoHorarioCurva(i, azimutesOriginais);
                        var tamanhoArco = info.AnguloCurva > 180.0 ? SKPathArcSize.Large : SKPathArcSize.Small;
                        var direcao = sentidoHorario ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise;
                        perfilPath.ArcTo(new SKPoint(raioCanvas, raioCanvas), 0, tamanhoArco, direcao, p1);
                    }
                }
                else
                {
                    perfilPath.LineTo(p1);
                }
            }
            canvas.DrawPath(perfilPath, perfilPaint);

            // 6. Desenhar cotas, medidas internas e graus de dobra, se solicitado
            if (mostrarMedidas)
            {
                var medidas = geometryService.GerarMedidasInternaExterna(polar);
                DesenharCotas(canvas, coordenadasNoCanvas, polar.SegmentosOriginal, absolutas, medidas, geometryService, fonteCota);
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

        private static void DesenharCotas(
            SKCanvas canvas,
            List<SKPoint> pontos,
            List<Segmento> segmentos,
            List<(double X, double Y)> absolutas,
            List<(double Livre, double Interna, double Externa)> medidas,
            IGeometryService geometryService,
            float fonteCota)
        {
            using var cotaPaintExterna = new SKPaint { Color = CotaLinhaCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f, IsAntialias = true };
            using var cotaPaintInterna = new SKPaint { Color = CotaInternaLinhaCor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f, IsAntialias = true };
            using var textoExterna = new SKPaint { Color = CotaTextoCor, TextSize = fonteCota, IsAntialias = true, TextAlign = SKTextAlign.Center };
            using var textoInterna = new SKPaint { Color = CotaInternaTextoCor, TextSize = fonteCota, IsAntialias = true, TextAlign = SKTextAlign.Center };

            void DesenharLado(SKPoint p0, SKPoint p1, float nx, float ny, float offset, string texto, SKPaint linhaPaint, SKPaint textPaint)
            {
                var pc0 = new SKPoint(p0.X + nx * offset, p0.Y + ny * offset);
                var pc1 = new SKPoint(p1.X + nx * offset, p1.Y + ny * offset);

                canvas.DrawLine(p0, pc0, linhaPaint);
                canvas.DrawLine(p1, pc1, linhaPaint);
                canvas.DrawLine(pc0, pc1, linhaPaint);

                float mx = (pc0.X + pc1.X) / 2.0f;
                float my = (pc0.Y + pc1.Y) / 2.0f;
                canvas.DrawText(texto, mx, my - 4f, textPaint);
            }

            // Para cada segmento desenhamos a cota externa (azul, lado de fora) e a interna (vermelha, lado de dentro)
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

                string textoExt = seg.EhCurvo && seg.CurvaInfo != null
                    ? $"{medidas[i].Externa:F0} (Curva)"
                    : $"{medidas[i].Externa:F0}";
                DesenharLado(p0, p1, nx * -ladoInterno, ny * -ladoInterno, 20f, textoExt, cotaPaintExterna, textoExterna);

                if (!seg.EhCurvo)
                {
                    string textoInt = $"{medidas[i].Interna:F0}";
                    DesenharLado(p0, p1, nx * ladoInterno, ny * ladoInterno, 14f, textoInt, cotaPaintInterna, textoInterna);
                }
            }
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
                TextAlign = SKTextAlign.Center
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

            using var textPaint = new SKPaint
            {
                Color = PerfilContornoCor,
                TextSize = fonteCota,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            using var textPaintAngulo = new SKPaint
            {
                Color = PerfilContornoCor,
                TextSize = fonteAngulo,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            using var textPaintSentido = new SKPaint
            {
                Color = PerfilContornoCor,
                TextSize = fonteSentido,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            using var textBluePaint = new SKPaint
            {
                Color = new SKColor(21, 101, 192),
                TextSize = 10f,
                IsAntialias = true,
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

                // Ângulo e sentido
                canvas.DrawText($"{marca.AnguloDobra:F0}°", x, y0 - 15, textPaintAngulo);
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
