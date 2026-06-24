using System;
using System.IO;
using System.Text.Json;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class ConfigService : IConfigService
    {
        private readonly string _configFilePath;
        private Configuracao _configuracao = null!;

        public ConfigService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _configFilePath = Path.Combine(baseDir, "config.json");
            CarregarConfiguracao();
        }

        public void CarregarConfiguracao()
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(_configFilePath);
                    var parsed = JsonSerializer.Deserialize<Configuracao>(jsonContent);
                    _configuracao = parsed ?? Configuracao.CreateDefault();
                }
                catch (Exception)
                {
                    // Se corrompido, recria os padrões
                    _configuracao = Configuracao.CreateDefault();
                    SalvarConfiguracao(_configuracao);
                }
            }
            else
            {
                _configuracao = Configuracao.CreateDefault();
                SalvarConfiguracao(_configuracao);
            }
        }

        public Configuracao ObterConfiguracao()
        {
            return _configuracao;
        }

        public void SalvarConfiguracao(Configuracao novaConfig)
        {
            _configuracao = novaConfig;
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonContent = JsonSerializer.Serialize(_configuracao, options);
                File.WriteAllText(_configFilePath, jsonContent);
            }
            catch (Exception)
            {
                // Erros silenciosos no salvamento podem ser logados se o logger já estiver configurado
            }
        }

        public string ResolverCaminho(string caminhoRelativo)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Para manter compatibilidade com caminhos em python (ex. data/chapas.csv), normaliza as barras
            var caminhoNormalizado = caminhoRelativo.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(baseDir, caminhoNormalizado));
        }

        public string ObterCaminhoChapas()
        {
            return ResolverCaminho(_configuracao.ArquivoChapas);
        }

        public string ObterCaminhoBiblioteca()
        {
            return ResolverCaminho(_configuracao.ArquivoBiblioteca);
        }

        public string ObterCaminhoLog()
        {
            var logFolder = ResolverCaminho(_configuracao.PastaLogs);
            Directory.CreateDirectory(logFolder);
            var logFileName = $"app_log_{DateTime.Today:yyyy-MM-dd}.txt";
            return Path.Combine(logFolder, logFileName);
        }

        public string ObterCaminhoSaidaRelatorios()
        {
            var path = ResolverCaminho(_configuracao.PastaSaidaRelatorios);
            Directory.CreateDirectory(path);
            return path;
        }

        public string ObterTituloAplicacao(string nomePeca = "")
        {
            var baseTitle = $"{_configuracao.TituloApp} v{_configuracao.VersaoApp}";
            return string.IsNullOrWhiteSpace(nomePeca) ? baseTitle : $"{baseTitle} — {nomePeca}";
        }
    }
}
