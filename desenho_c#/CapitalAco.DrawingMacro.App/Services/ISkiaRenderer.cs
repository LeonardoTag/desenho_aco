using SkiaSharp;
using System.Windows.Media;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public interface ISkiaRenderer
    {
        ImageSource RenderToImageSource(
            InstrucoesPolares polar, int width, int height,
            IGeometryService geometryService,
            float fonteCota = 12f, float fonteAngulo = 11f,
            bool mostrarMedidas = true, int? segmentoDestacado = null,
            bool destacarProximaOrigem = false, bool forcarDesenho3D = false);

        void RenderizarPeca(
            SKCanvas canvas, SKSize size,
            InstrucoesPolares polar, DimensoesAcabadas? dimensoes,
            bool mostrarMedidas, IGeometryService geometryService,
            float fonteCota = 12f, float fonteAngulo = 11f,
            int? segmentoDestacado = null,
            bool destacarProximaOrigem = false, bool forcarDesenho3D = false);

        void RenderizarPlanificacao(
            SKCanvas canvas, SKSize size, DadosPlanificacao plano,
            float fonteAngulo = 10f, float fonteSentido = 10f, float fonteCota = 10f);
    }
}
