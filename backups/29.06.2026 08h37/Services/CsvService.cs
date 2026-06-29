using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CapitalAco.DrawingMacro.App.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class CsvService : ICsvService
    {
        private readonly IConfigService _configService;
        private List<Chapa>? _cache;

        public CsvService(IConfigService configService)
        {
            _configService = configService;
        }

        public void InvalidarCache() => _cache = null;

        private sealed class ChapaMap : ClassMap<Chapa>
        {
            public ChapaMap()
            {
                Map(m => m.Codigo).Name("codigo");
                Map(m => m.Espessura).Name("espessura");
                Map(m => m.RaioDeDobra).Name("raio_de_dobra");
                Map(m => m.KFactor).Name("k_factor");
                Map(m => m.Coeficiente).Name("coeficiente");
                // Algumas linhas podem ter dobra_minima em branco; trata de forma segura
                Map(m => m.DobraMinima).Name("dobra_minima").Default(0.0);
                Map(m => m.Tipo).Name("tipo");
            }
        }

        public List<Chapa> CarregarChapas()
        {
            if (_cache != null)
                return _cache;

            var filePath = _configService.ObterCaminhoChapas();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Arquivo de chapas não encontrado em: {filePath}");
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                Delimiter = ","
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<ChapaMap>();
            
            try
            {
                var chapas = new List<Chapa>(csv.GetRecords<Chapa>());
                ValidarChapas(chapas, filePath);
                _cache = chapas;
                return _cache;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Erro ao processar chapas.csv: {ex.Message}", ex);
            }
        }
        private static void ValidarChapas(List<Chapa> chapas, string filePath)
        {
            if (chapas.Count == 0)
                throw new InvalidOperationException($"chapas.csv não contém nenhuma linha de dados em: {filePath}");

            var erros = new List<string>();
            var codigosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < chapas.Count; i++)
            {
                var c = chapas[i];
                int linha = i + 2; // linha 1 = cabeçalho

                if (string.IsNullOrWhiteSpace(c.Codigo))
                    erros.Add($"Linha {linha}: código vazio.");
                else if (!codigosVistos.Add(c.Codigo))
                    erros.Add($"Linha {linha}: código '{c.Codigo}' duplicado.");

                if (c.Espessura <= 0)
                    erros.Add($"Linha {linha} ({c.Codigo}): espessura inválida ({c.Espessura}).");
                if (c.RaioDeDobra < 0)
                    erros.Add($"Linha {linha} ({c.Codigo}): raio_de_dobra negativo ({c.RaioDeDobra}).");
                if (c.KFactor <= 0 || c.KFactor >= 1)
                    erros.Add($"Linha {linha} ({c.Codigo}): k_factor fora do intervalo (0,1): {c.KFactor}.");
            }

            if (erros.Count > 0)
                throw new InvalidOperationException(
                    $"chapas.csv contém {erros.Count} erro(s):\n" + string.Join("\n", erros));
        }
    }
}
