using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalAco.DrawingMacro.App.Models;
using Serilog;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class BibliotecaPecasService : IBibliotecaPecasService
    {
        private readonly IConfigService _configService;
        private const int VersaoBiblioteca = 1;

        private BibliotecaPecasDados? _cache;

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
            if (_cache != null) return _cache;

            var filePath = _configService.ObterCaminhoBiblioteca();

            if (!File.Exists(filePath))
            {
                return _cache = new BibliotecaPecasDados();
            }

            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var dados = JsonSerializer.Deserialize<BibliotecaPecasDados>(jsonContent);
                if (dados == null)
                {
                    return _cache = new BibliotecaPecasDados();
                }
                dados.Versao = VersaoBiblioteca;
                return _cache = dados;
            }
            catch (Exception)
            {
                // Preserva o arquivo corrompido antes de sobrescrever com estrutura vazia
                try { File.Move(filePath, filePath + ".corrupt", overwrite: true); } catch { }
                var fallback = new BibliotecaPecasDados();
                GravarDados(fallback);
                return _cache = fallback;
            }
        }

        private void GravarDados(BibliotecaPecasDados dados)
        {
            _cache = null;
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
                // Escrita atômica: gravar em .tmp e renomear para evitar corrupção por interrupção
                var tmpPath = filePath + ".tmp";
                File.WriteAllText(tmpPath, jsonContent);
                File.Move(tmpPath, filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falha ao gravar biblioteca de peças em {FilePath}", filePath);
                throw;
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

            var registroId = id ?? Guid.NewGuid();
            var duplicadoPorNome = dados.Pecas.FirstOrDefault(p =>
                string.Equals(p.Nome, nomeLimpo, StringComparison.OrdinalIgnoreCase) && p.Id != registroId);
            if (duplicadoPorNome != null)
                throw new InvalidOperationException($"Já existe uma peça com o nome \"{nomeLimpo}\" na biblioteca.");

            var registro = new ModeloPeca
            {
                Id = registroId,
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
