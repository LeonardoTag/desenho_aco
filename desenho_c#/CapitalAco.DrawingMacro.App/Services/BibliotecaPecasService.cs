using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class BibliotecaPecasService : IBibliotecaPecasService
    {
        private readonly IConfigService _configService;
        private const int VersaoBiblioteca = 1;

        public BibliotecaPecasService(IConfigService configService)
        {
            _configService = configService;
        }

        private class BibliotecaPecasDados
        {
            [JsonPropertyName("versao")]
            public int Versao { get; set; } = VersaoBiblioteca;

            [JsonPropertyName("pecas")]
            public List<ModeloPeca> Pecas { get; set; } = new();
        }

        private BibliotecaPecasDados CarregarDados()
        {
            var filePath = _configService.ObterCaminhoBiblioteca();

            if (!File.Exists(filePath))
            {
                return new BibliotecaPecasDados();
            }

            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var dados = JsonSerializer.Deserialize<BibliotecaPecasDados>(jsonContent);
                if (dados == null)
                {
                    return new BibliotecaPecasDados();
                }
                dados.Versao = VersaoBiblioteca;
                return dados;
            }
            catch (Exception)
            {
                var fallback = new BibliotecaPecasDados();
                GravarDados(fallback);
                return fallback;
            }
        }

        private void GravarDados(BibliotecaPecasDados dados)
        {
            var filePath = _configService.ObterCaminhoBiblioteca();
            try
            {
                var folder = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                dados.Versao = VersaoBiblioteca;
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var jsonContent = JsonSerializer.Serialize(dados, options);
                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception)
            {
                // Silencia erros de gravação ou delega para logging mais tarde
            }
        }

        public List<ModeloPeca> ListarModelos(string filtro = "")
        {
            var filtroNormalizado = (filtro ?? "").Trim().ToLower();
            var pecas = CarregarDados().Pecas;

            if (string.IsNullOrEmpty(filtroNormalizado))
            {
                return pecas.OrderBy(p => p.Nome.ToLower()).ToList();
            }

            return pecas
                .Where(p => p.Nome.ToLower().Contains(filtroNormalizado) ||
                            p.Descricao.ToLower().Contains(filtroNormalizado) ||
                            p.Chapa.ToLower().Replace("#", "").Contains(filtroNormalizado))
                .OrderBy(p => p.Nome.ToLower())
                .ToList();
        }

        public ModeloPeca? ObterModelo(Guid id)
        {
            return CarregarDados().Pecas.FirstOrDefault(p => p.Id == id);
        }

        public ModeloPeca SalvarModelo(string nome, string chapa, double? comprimento, List<Segmento> segmentos, Guid? id = null, string descricao = "")
        {
            var nomeLimpo = (nome ?? "").Trim();
            if (string.IsNullOrEmpty(nomeLimpo))
            {
                throw new ArgumentException("Informe um nome para a peça.");
            }

            var dados = CarregarDados();
            var agora = DateTime.UtcNow;
            
            var registro = new ModeloPeca
            {
                Id = id ?? Guid.NewGuid(),
                Nome = nomeLimpo,
                Descricao = (descricao ?? "").Trim(),
                Chapa = chapa,
                Comprimento = comprimento,
                Segmentos = segmentos ?? new List<Segmento>(),
                AtualizadoEm = agora
            };

            var indexExistente = dados.Pecas.FindIndex(p => p.Id == registro.Id);
            if (indexExistente >= 0)
            {
                var existente = dados.Pecas[indexExistente];
                registro.CriadoEm = existente.CriadoEm == default ? agora : existente.CriadoEm;
                dados.Pecas[indexExistente] = registro;
            }
            else
            {
                registro.CriadoEm = agora;
                dados.Pecas.Add(registro);
            }

            GravarDados(dados);
            return registro;
        }

        public bool ExcluirModelo(Guid id)
        {
            var dados = CarregarDados();
            var pecaParaRemover = dados.Pecas.FirstOrDefault(p => p.Id == id);
            if (pecaParaRemover == null)
            {
                return false;
            }

            dados.Pecas.Remove(pecaParaRemover);
            GravarDados(dados);
            return true;
        }
    }
}
