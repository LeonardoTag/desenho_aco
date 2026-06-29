using System;
using System.IO;
using System.Text.Json;
using CapitalAco.DrawingMacro.App.Models;
using Serilog;

namespace CapitalAco.DrawingMacro.App.Services
{
    public class ConfigService : IConfigService
    {
        private readonly string _configFilePath;
        private Configuracao _configuracao = null!;

        // Pasta de dados do usuário: %APPDATA%\CapitalAco\DrawingMacro\
        // Arquivos de configuração do deployment (config.json) ficam perto do exe;
        // dados mutáveis do usuário (biblioteca, logs, PDFs) ficam em AppData.
        public static readonly string UserDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CapitalAco", "DrawingMacro");

        public ConfigService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _configFilePath = Path.Combine(baseDir, "config.json");

            // Garantir que a pasta de dados do usuário exista
            Directory.CreateDirectory(UserDataDir);

            // Migração única: se arquivos existem em BaseDir mas não em UserDataDir, copiar
            MigrarArquivosParaUserDataDir(baseDir);

            CarregarConfiguracao();
        }

        private static void MigrarArquivosParaUserDataDir(string baseDir)
        {
            // Preserva a mesma estrutura relativa (data/) ao migrar para UserDataDir
            var legados = new[]
            {
                "data/biblioteca_pecas.json",
                "data/chapas.csv",
            };

            foreach (var rel in legados)
            {
                var srcPath = Path.GetFullPath(Path.Combine(baseDir, rel.Replace('/', Path.DirectorySeparatorChar)));
                var dstPath = Path.Combine(UserDataDir, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(srcPath) && !File.Exists(dstPath))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);
                        File.Copy(srcPath, dstPath);
                        Log.Information("Migração: {Src} → {Dst}", srcPath, dstPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Falha ao migrar {Src} para UserDataDir", srcPath);
                    }
                }
            }
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

                    const int SchemaAtual = 1;
                    if (_configuracao.VersaoSchema < SchemaAtual)
                    {
                        Log.Warning(
                            "config.json tem versão de schema {Old} (atual: {New}). " +
                            "Novos campos usarão valores padrão. Salve as configurações para atualizar.",
                            _configuracao.VersaoSchema, SchemaAtual);
                        _configuracao.VersaoSchema = SchemaAtual;
                    }

                    _configuracao.Sanitizar();
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
                // Escrita atômica: gravar em .tmp e renomear para evitar corrupção por interrupção
                var tmpPath = _configFilePath + ".tmp";
                File.WriteAllText(tmpPath, jsonContent);
                File.Move(tmpPath, _configFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falha ao gravar configuração em {FilePath}", _configFilePath);
                throw;
            }
        }

        public string ResolverCaminho(string caminhoRelativo)
        {
            // Caminhos absolutos são usados como estão; relativos resolvem contra UserDataDir
            // para manter dados mutáveis do usuário fora da pasta do executável (UAC-safe).
            var normalizado = caminhoRelativo.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizado))
                return Path.GetFullPath(normalizado);
            return Path.GetFullPath(Path.Combine(UserDataDir, normalizado));
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
            // Retorna o caminho base sem data; o Serilog adiciona a data ao fazer rolling diário.
            return Path.Combine(logFolder, "app_log.txt");
        }

        public string ObterCaminhoSaidaRelatorios()
        {
            var path = ResolverCaminho(_configuracao.PastaSaidaRelatorios);
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Não foi possível criar a pasta de relatórios em {Path}; usando pasta temp", path);
                path = Path.GetTempPath();
            }
            return path;
        }

        public string ObterTituloAplicacao(string nomePeca = "")
        {
            var baseTitle = $"{_configuracao.TituloApp} v{_configuracao.VersaoApp}";
            return string.IsNullOrWhiteSpace(nomePeca) ? baseTitle : $"{baseTitle} — {nomePeca}";
        }
    }
}
