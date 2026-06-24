namespace CapitalAco.DrawingMacro.App.Models
{
    public class Chapa
    {
        public string Codigo { get; set; } = string.Empty;
        public double Espessura { get; set; }
        public double RaioDeDobra { get; set; }
        public double KFactor { get; set; }
        public double Coeficiente { get; set; }
        public double DobraMinima { get; set; }
        public string Tipo { get; set; } = string.Empty;
    }
}
