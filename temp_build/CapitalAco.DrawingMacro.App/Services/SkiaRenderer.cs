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

        public static System.Windows.Media.ImageSource RenderToImageSource(InstrucoesPolares polar, int width, int height, IGeometryService geometryService)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(FundoCor);

            var dim = geometryService.CalcularDimensoesAcabadas(polar);
            RenderizarPeca(canvas, new SKSize(width, height), polar, dim, true, geometryService);

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
            IGeometryService geometryService)
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

            double minX = absolutas.Min(p => p.X);
            double maxX = absolutas.Max(p => p.X);
            double minY = absolutas.Min(p => p.Y);
            double maxY = absolutas.Max(p => p.Y);

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

                var pathPoints = new[] { p0, p1, pe1, pe0 };
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

            for (int i = 0; i < coordenadasNoCanvas.Count - 1; i++)
            {
                canvas.DrawLine(coordenadasNoCanvas[i], coordenadasNoCanvas[i + 1], perfilPaint);
            }

            // 6. Desenhar cotas e medidas se solicitado
            if (mostrarMedidas)
            {
                DesenharCotas(canvas, coordenadasNoCanvas, polar.SegmentosOriginal, escala);
            }
        }

        private static void DesenharCotas(SKCanvas canvas, List<SKPoint> pontos, List<Segmento> segmentos, double escala)
        {
            using var cotaPaint = new SKPaint
            {
                Color = CotaLinhaCor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                IsAntialias = true
            };

            using var textPaint = new SKPaint
            {
                Color = CotaTextoCor,
                TextSize = 12f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            // Para cada segmento desenhamos uma linha de cota paralela offsetada
            for (int i = 0; i < pontos.Count - 1; i++)
            {
                if (i >= segmentos.Count) break;

                var p0 = pontos[i];
                var p1 = pontos[i + 1];
                var seg = segmentos[i];

                // Calcular vetor normal unitário apontando para fora
                float dx = p1.X - p0.X;
                float dy = p1.Y - p0.Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len == 0) continue;

                float nx = -dy / len;
                float ny = dx / len;

                // Deslocamento de cota
                float offset = 18f;
                var pc0 = new SKPoint(p0.X + nx * offset, p0.Y + ny * offset);
                var pc1 = new SKPoint(p1.X + nx * offset, p1.Y + ny * offset);

                // Desenhar linha de cota e linhas auxiliares
                canvas.DrawLine(p0, pc0, cotaPaint);
                canvas.DrawLine(p1, pc1, cotaPaint);
                canvas.DrawLine(pc0, pc1, cotaPaint);

                // Desenhar texto
                float mx = (pc0.X + pc1.X) / 2.0f;
                float my = (pc0.Y + pc1.Y) / 2.0f;
                string texto = seg.EhCurvo && seg.CurvaInfo != null
                    ? $"{seg.CurvaInfo.ComprimentoCurva:F0} (Curva)"
                    : $"{seg.Medida:F0}";

                canvas.DrawText(texto, mx, my - 4f, textPaint);
            }
        }

        public static void RenderizarPlanificacao(SKCanvas canvas, SKSize size, DadosPlanificacao plano)
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
                TextSize = 10f,
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
                canvas.DrawText($"{marca.AnguloDobra:F0}°", x, y0 - 15, textPaint);
                string dirText = marca.Sentido == "h" ? "P.Baixo" : "P.Cima";
                canvas.DrawText(dirText, x, y0 - 5, textPaint);
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
