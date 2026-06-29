using System;
using System.Globalization;

namespace CapitalAco.DrawingMacro.App.Services
{
    public static class NumericUtils
    {
        public static double? ParseNumero(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            var limpo = texto.Trim().Replace(" ", "");

            // Tratamento de pontuação híbrida (PT-BR vs EN)
            if (limpo.Contains(',') && limpo.Contains('.'))
            {
                limpo = limpo.Replace(".", "").Replace(",", ".");
            }
            else
            {
                limpo = limpo.Replace(",", ".");
            }

            if (double.TryParse(limpo, NumberStyles.Any, CultureInfo.InvariantCulture, out double valor))
            {
                return valor;
            }

            return null;
        }

        public static int? ParseInteiro(string? texto)
        {
            var valor = ParseNumero(texto);
            if (valor == null)
                return null;

            try
            {
                return Convert.ToInt32(valor.Value);
            }
            catch
            {
                return null;
            }
        }

        public static string FormatarNumero(double? valor, int? casasDecimais = null, int casasPadrao = 0)
        {
            if (valor == null)
                return string.Empty;

            int precisao = casasDecimais ?? casasPadrao;
            double numero = valor.Value;

            string texto;
            if (precisao == 0)
            {
                texto = Math.Round(numero).ToString("F0", CultureInfo.InvariantCulture);
            }
            else
            {
                // Formato padrão G ou F, removendo zeros à direita extras e o ponto se desnecessário
                texto = numero.ToString($"F{precisao}", CultureInfo.InvariantCulture)
                    .TrimEnd('0')
                    .TrimEnd('.');
            }

            return texto.Replace(".", ",");
        }

        public static string FormatarCompacto(double? valor)
        {
            if (valor == null)
                return string.Empty;

            double numero = valor.Value;

            // Se for inteiro, retorna sem casas decimais
            if (Math.Abs(numero - Math.Truncate(numero)) < 1e-9)
            {
                return ((int)numero).ToString(CultureInfo.InvariantCulture);
            }

            string texto = numero.ToString("G", CultureInfo.InvariantCulture);
            return texto.Replace(".", ",");
        }
    }
}
