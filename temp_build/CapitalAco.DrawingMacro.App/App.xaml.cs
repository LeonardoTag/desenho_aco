using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Configurar Container de Injeção de Dependências
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // 2. Configurar o Serilog Logging
            var configService = ServiceProvider.GetRequiredService<IConfigService>();
            var logPath = configService.ObterCaminhoLog();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(logPath, 
                    rollingInterval: RollingInterval.Infinite, 
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("=== Capital Aço Detalhador — Sessão Inicializada ===");
            Log.Information("Diretório Base: {BaseDir}", AppDomain.CurrentDomain.BaseDirectory);
            Log.Information("Arquivo de Log configurado em: {LogPath}", logPath);

            // 3. Executar Teste de Carga de Dados (Pre-flight checks e verificação da Fase 2)
            ExecutarPreFlightCheck();

            // 4. Inicializar MainWindow
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Title = configService.ObterTituloAplicacao();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Registrar Serviços
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<ICsvService, CsvService>();
            services.AddSingleton<IBibliotecaPecasService, BibliotecaPecasService>();
            services.AddSingleton<IGeometryService, GeometryService>();
            services.AddSingleton<IGeradorPecaService, GeradorPecaService>();
            services.AddSingleton<IPdfGeneratorService, PdfGeneratorService>();

            // Registrar ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<EditorPecaViewModel>();
            services.AddSingleton<BibliotecaViewModel>();
            services.AddSingleton<PedidoViewModel>();
            services.AddSingleton<ConfiguracaoViewModel>();

            // Registrar Views
            services.AddSingleton<MainWindow>();
        }

        private void ExecutarPreFlightCheck()
        {
            var csvService = ServiceProvider.GetRequiredService<ICsvService>();
            var bibliotecaService = ServiceProvider.GetRequiredService<IBibliotecaPecasService>();
            var configService = ServiceProvider.GetRequiredService<IConfigService>();
            var geometryService = ServiceProvider.GetRequiredService<IGeometryService>();
            var geradorPecaService = ServiceProvider.GetRequiredService<IGeradorPecaService>();

            try
            {
                // Copiar arquivos de teste se eles não existirem no diretório de execução para facilitar desenvolvimento local
                CopiarArquivosDeDadosLocais(configService);

                // Carregar chapas
                var chapas = csvService.CarregarChapas();
                Log.Information("Pré-carregamento: {Count} especificações de chapa lidas de chapas.csv com sucesso.", chapas.Count);

                // Carregar biblioteca
                var biblioteca = bibliotecaService.ListarModelos();
                Log.Information("Pré-carregamento: {Count} modelos de peça carregados da biblioteca JSON com sucesso.", biblioteca.Count);

                var pdfGeneratorService = ServiceProvider.GetRequiredService<IPdfGeneratorService>();

                // Executar Testes de Regressão da Fase 3 e Integração da Fase 4
                GeometryTests.ExecutarTestes(geometryService, geradorPecaService, pdfGeneratorService);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro crítico no pré-carregamento dos arquivos de dados ou na validação matemática");
                MessageBox.Show(
                    $"Não foi possível iniciar o aplicativo.\nErro: {ex.Message}\nConsulte o arquivo de log para mais detalhes.",
                    "Erro de Inicialização",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                Shutdown(1);
            }
        }

        private void CopiarArquivosDeDadosLocais(IConfigService configService)
        {
            // Como as configurações padrão apontam para "data/chapas.csv" e "data/biblioteca_pecas.json",
            // tentamos localizar a pasta "data" do projeto e copiar para a pasta de execução caso não exista lá.
            var targetChapas = configService.ObterCaminhoChapas();
            var targetBiblioteca = configService.ObterCaminhoBiblioteca();

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // O repositório contém a pasta desenho_python/data com os arquivos originais.
            // Tentamos encontrar esta pasta subindo níveis a partir do executável.
            string sourceDataDir = "";
            var current = new DirectoryInfo(baseDir);
            for (int i = 0; i < 5; i++)
            {
                if (current == null) break;
                
                var candidato = Path.Combine(current.FullName, "desenho_python", "data");
                if (Directory.Exists(candidato))
                {
                    sourceDataDir = candidato;
                    break;
                }
                current = current.Parent!;
            }

            if (!string.IsNullOrEmpty(sourceDataDir))
            {
                var dirChapas = Path.GetDirectoryName(targetChapas);
                if (dirChapas != null) Directory.CreateDirectory(dirChapas);

                var dirBib = Path.GetDirectoryName(targetBiblioteca);
                if (dirBib != null) Directory.CreateDirectory(dirBib);

                if (!File.Exists(targetChapas))
                {
                    var sourceChapasFile = Path.Combine(sourceDataDir, "chapas.csv");
                    if (File.Exists(sourceChapasFile))
                    {
                        File.Copy(sourceChapasFile, targetChapas);
                    }
                }

                if (!File.Exists(targetBiblioteca))
                {
                    var sourceBibFile = Path.Combine(sourceDataDir, "biblioteca_pecas.json");
                    if (File.Exists(sourceBibFile))
                    {
                        File.Copy(sourceBibFile, targetBiblioteca);
                    }
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("=== Capital Aço Detalhador — Aplicação Finalizada ===");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
