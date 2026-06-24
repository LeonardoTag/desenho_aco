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

        public CsvService(IConfigService configService)
        {
            _configService = configService;
        }

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
                return new List<Chapa>(csv.GetRecords<Chapa>());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Erro ao processar chapas.csv: {ex.Message}", ex);
            }
        }
    }
}
