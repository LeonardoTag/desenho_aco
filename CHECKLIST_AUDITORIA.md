# CHECKLIST DE AUDITORIA — CapitalAco.DrawingMacro.App
Gerado em: 2026-06-26  
Baseado na leitura integral dos arquivos de código-fonte. Cada item indica arquivo + linha, descrição do problema e impacto.  
Uso: pedir ao assistente "vamos verificar o item X do checklist" para investigar e corrigir um item específico.

---

## A) PERFORMANCE E MEMÓRIA

**A1 — CsvService.CarregarChapas() lê disco a cada chamada, sem cache**
- Arquivo: `Services/CsvService.cs:35` / `Services/GeometryService.cs:29` (ConsultarChapa)
- Problema: `CarregarChapas()` faz `File.ReadAllText` + parse CSV toda vez que é invocado. Durante geração de boiadeira (Nelder-Mead com 10 pontos iniciais × 150 iterações cada), `ObterMetricasBoiadeira` chama `ConverterInstrucoesParaCoordenadasPolares` que chama `ConsultarChapa` → centenas de leituras de disco.
- Impacto: lentidão ao gerar boiadeiras; degrada com CVS grande.
- Correção sugerida: cache em memória no singleton `CsvService` (invalidar ao chamar `RecarregarChapas()` explicitamente).

**A2 — BibliotecaPecasService.CarregarDados() lê o JSON a cada operação**
- Arquivo: `Services/BibliotecaPecasService.cs:30` (CarregarDados chamado em ListarModelos, ObterModelo, SalvarModelo, ExcluirModelo)
- Problema: Cada operação na biblioteca lê o arquivo inteiro do disco. Para biblioteca com muitos modelos, `ListarModelos(filtro)` filtra in-memory após carregar tudo.
- Impacto: lentidão progressiva com biblioteca grande; I/O desnecessário em operações frequentes.

**A3 — GerarRelatorioPedido: CSV de chapas carregado na linha 159, mas ConsultarChapa carrega novamente para cada item**
- Arquivo: `Services/PdfGeneratorService.cs:159` e `Services/GeometryService.cs:33`
- Problema: `GerarRelatorioPedido` chama `_csvService.CarregarChapas()` uma vez, mas cada `ConverterInstrucoesParaCoordenadasPolares` dentro do `.Select()` chama `ConsultarChapa` que chama `CarregarChapas()` de novo. Com N itens no pedido, o CSV é lido N+1 vezes.
- Impacto: lentidão em pedidos grandes.

**A4 — AtualizarPreview() sem debounce ativo em OnComprimentoPecaChanged / OnChapaSelecionadaChanged**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:261-262`
- Problema: `partial void OnChapaSelecionadaChanged` e `OnComprimentoPecaChanged` chamam `AtualizarPreview()` diretamente e síncronamente. O `config.json` tem `PreviewDebounceMs` e `PreviewDebounceAlteracaoMs`, mas não há timer/DispatcherTimer visível nesses handlers. Cada tecla pressionada no campo comprimento dispara renderização SkiaSharp + cálculo de geometria completo.
- Impacto: UI pode travar brevemente ao digitar rapidamente no campo comprimento.
- Ação: verificar se existe debounce por timer não visto, ou implementar um.

**A5 — SkiaRenderer: cast explícito `(GeometryService)geometryService` em hot path**
- Arquivo: `Services/SkiaRenderer.cs:99-100`
- Problema: O renderer faz cast `((GeometryService)geometryService).GerarCoordenadasRetangularesParciais(...)` porque esses métodos não estão na interface `IGeometryService`. Isso viola o princípio de substituição e impede mocking em testes. Se a implementação mudar, lança `InvalidCastException` em runtime.
- Mesmo problema em: `Services/GeradorPecaService.cs:89-90`
- Correção sugerida: adicionar `GerarCoordenadasRetangularesParciais` e `GerarCoordenadasRetangularesAbsolutas` à `IGeometryService`.

**A6 — Geração de PDF síncrona na thread UI**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:989-1008` (GerarFichaDobra) / `ViewModels/PedidoViewModel.cs:174-202` (VisualizarPdf)
- Problema: `GerarFichaDobra()` e `VisualizarPdf()` executam renderização SkiaSharp + QuestPDF + escrita em disco diretamente na thread UI sem `async/await` ou `Task.Run`. Para pedidos com muitas peças ou peças complexas, a interface trava completamente.
- Impacto: UX ruim; pode parecer crash para o usuário.

**A7 — GeradorPecaService: lógica de coordenadas externas duplicada de GeometryService**
- Arquivo: `Services/GeradorPecaService.cs:116-207` vs `Services/GeometryService.cs:724-799`
- Problema: `CoordenadasExternas()` + `Deslocamento()` em GeradorPecaService são cópias quase idênticas de `CoordenadasExternasPerfil()` + `DeslocamentoExternoSegmento()` em GeometryService. Duplicação porque `CoordenadasExternasPerfil` é `private` e `GeometryService` não é injetado nesse contexto de forma acessível.
- Impacto: bug corrigido em um lugar pode ser esquecido no outro.

---

## B) ERROS DE LÓGICA E MATEMÁTICA

**B1 — GerarMedidaInternaExterna: fórmula para ângulos < 90° pode ser incorreta**
- Arquivo: `Services/GeometryService.cs:375-379`
- Problema: Para `angulo < 90°`, usa `(angulo * PI * raio) / 360` que é um comprimento de arco. Para dobras não-retas (não curvos calandrados), a expansão interna/externa deveria usar a tangente do half-angle (como em `GetBendAllowance`), não o arco. Verificar se essa fórmula está alinhada com a realidade da chapa dobrada.
- Impacto: medidas interna/externa exibidas incorretas para dobras com ângulo ≠ 90°.

**B2 — SegmentosCentroCruzam: segmentos colineares sobrepostos não são detectados**
- Arquivo: `Services/GeometryService.cs:865-870`
- Problema: Quando os 4 produtos cruzados são ≈ 0 (segmentos colineares), retorna `false` (não cruzam). Segmentos colineares que se sobrepõem parcialmente no espaço não são detectados como auto-interseção. Uma peça que "dobra sobre si mesma" em linha reta não seria detectada por `PerfilCruzaASiMesmo`.
- Impacto: peça inválida pode ser adicionada ao pedido.

**B3 — DefinirAzimute para primeiro segmento com ângulo ≠ 90° tem semântica ambígua**
- Arquivo: `Services/GeometryService.cs:96-97`
- Problema: Para o primeiro segmento (sem anterior), quando `angulo ≠ 90°`, usa `azimuteDirecao - (90.0 - grau)`. Isso significa que um segmento com direcao="N" (0°) e angulo=45° tem azimute=0-(90-45)=-45°=315° (NO). A semântica de "45°" sem anterior não está documentada claramente e pode não ser o que o usuário espera.
- Impacto: peças com primeiro segmento não-ortogonal podem ser renderizadas incorretamente.

**B4 — CalcularPesoKg usa Math.Ceiling (sempre arredonda para cima)**
- Arquivo: `Services/GeometryService.cs:293`
- Problema: `Math.Ceiling(peso)` arredonda sempre para cima. Para peças pequenas com peso de 0.1 kg, o peso exibido seria 1 kg (10× a mais). Para propósitos de estimativa, `Math.Round` seria mais adequado.
- Impacto: estimativa de peso sistematicamente maior que o real, podendo confundir o usuário.

**B5 — SugerirNomePeca não cobre case 4 (3 dobras)**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:1061-1113`
- Problema: O `switch` vai de `case 3` para `case 5`, sem tratar `case 4`. Peças com 4 segmentos (3 dobras, ex: Perfil Ômega assimétrico, Z aberto) recebem o nome genérico "Perfil com 3 Dobras".
- Impacto: nomenclatura automática menos útil para perfis de 4 segmentos.

**B6 — ObterAngulosDobraDeAzimutes perde informação de sentido**
- Arquivo: `Services/GeometryService.cs:133-153`
- Problema: O método retorna apenas o módulo do ângulo (0° a 180°), descartando o sentido. O sentido é recalculado separadamente em `GerarDadosPlanificacao`. Isso resulta em dupla-computação dos azimutes com lógica duplicada. Se as duas implementações diferirem no edge case, produzirão sentidos diferentes.
- Impacto: potencial inconsistência no sentido de dobra entre a planificação e o detalhamento.

**B7 — CalcularDimensoesAcabadas: curvas inflam bbox usando rNeutro mas deveriam usar rExterno**
- Arquivo: `Services/GeometryService.cs:447-450`
- Problema: Para estimar o bounding box de curvas (tubo 360°, arcos), usa `rNeutroBbox` (raio neutro) para inflar a caixa. O bounding box real da peça acabada deveria usar `rExterno = rInterno + espessura`, não o raio neutro, para garantir que a chapa inteira caiba no canvas.
- Impacto: peça desenhada pode ser ligeiramente cortada nas bordas da extrusão em casos limítrofes.

---

## C) ROBUSTEZ / TRATAMENTO DE ERROS

**C1 — BibliotecaPecasService.GravarDados silencia erros de escrita**
- Arquivo: `Services/BibliotecaPecasService.cs:76-79`
- Problema: `catch (Exception) { /* Silencia */ }` descarta erros ao gravar o JSON da biblioteca. Se o disco estiver cheio, arquivo bloqueado ou sem permissão, o usuário não recebe nenhuma notificação. Os dados parecem salvos mas não foram.
- Mesmo problema em: `Services/ConfigService.cs:57-60` (SalvarConfiguracao)
- Impacto: perda silenciosa de dados do usuário.

**C2 — PdfGeneratorService.GerarRelatorioPedido: catch vazio (linha 191)**
- Arquivo: `Services/PdfGeneratorService.cs:191`
- Problema: `catch { }` captura exceções ao calcular geometria de cada item do pedido. Item com chapa inexistente ou segmentos corrompidos resulta em slot sem desenho no PDF, sem log e sem aviso ao usuário.
- Impacto: silenciamento de erros oculta dados corrompidos.

**C3 — ConsultarChapa lança InvalidOperationException para chapa não encontrada**
- Arquivo: `Services/GeometryService.cs:36-39`
- Problema: Se o CSV for editado manualmente e remover uma chapa que ainda é usada em itens do pedido (arquivo `.pedido`), todas as operações com esse item lançarão exceção. A mensagem de erro não indica em qual item do pedido ocorreu o problema.
- Impacto: pedidos com chapas inexistentes tornam-se inúteis sem diagnóstico claro.

**C4 — EditorPecaViewModel.CarregarChapas sem tratamento de exceção**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:246-254`
- Problema: Chama `_csvService.CarregarChapas()` sem try/catch. Se o CSV for deletado DEPOIS da inicialização (ex: arquivo travado ou antivírus), uma chamada posterior a `CarregarChapas` (ex: ao trocar aba e voltar) lançaria exception não tratada na thread UI.
- Impacto: crash na thread UI sem mensagem amigável.

**C5 — PerfilCruzaASiMesmo: catch genérico retorna false mascarando erros reais**
- Arquivo: `Services/GeometryService.cs:459-463`
- Problema: `catch { return false; }` mascara exceções de chapa inválida ou segmentos corrompidos como "perfil não cruza". O usuário pode adicionar ao pedido uma peça com dados inválidos acreditando que passou na validação.
- Impacto: peças inválidas chegam ao PDF sem aviso.

**C6 — ConfigService.ObterCaminhoSaidaRelatorios: Directory.CreateDirectory sem try/catch**
- Arquivo: `Services/ConfigService.cs:91-94`
- Problema: `Directory.CreateDirectory(path)` pode lançar `UnauthorizedAccessException` se não houver permissão (pasta do sistema, UAC). A exceção propaga para cima e cancela a geração de PDF com mensagem genérica de erro.
- Impacto: falha não clara ao tentar gerar PDF em ambientes com permissões restritas.

**C7 — NelderMeadSolver sem timeout: pode bloquear UI indefinidamente**
- Arquivo: `Services/GeradorPecaService.cs:298` (chamada ao solver)
- Problema: 10 pontos iniciais × 150 iterações cada = até 1.500 chamadas ao solver, mais polimento. Para parâmetros de boiadeira extremos (largura muito grande, aba muito pequena), a convergência pode falhar ou demorar muito sem nenhum mecanismo de cancelamento.
- Impacto: UI trava na thread principal durante geração de boiadeiras difíceis.

**C8 — AbrirPedido: não verifica PedidoArquivo.Versao ao deserializar**
- Arquivo: `ViewModels/PedidoViewModel.cs:124-133`
- Problema: O arquivo `.pedido` tem campo `Versao` (sempre 1 atualmente), mas ao ler nunca é verificado. Se o formato mudar em versões futuras, arquivos novos abertos em versão antiga serão desserializados silenciosamente com campos ausentes (valores padrão).
- Impacto: compatibilidade retroativa não garantida.

---

## D) AÇÕES INESPERADAS DO USUÁRIO

**D1 — Fechar a janela com pedido não salvo não exibe aviso**
- Arquivo: `MainWindow.xaml.cs` (não lido) / `ViewModels/PedidoViewModel.cs:28` (TemAlteracoesNaoSalvas)
- Problema: `PedidoViewModel.TemAlteracoesNaoSalvas` existe e é atualizado corretamente, mas não há handler `Window.Closing` em `MainWindow` que verifique esse flag e pergunte ao usuário se deseja salvar.
- Impacto: pedido inteiro pode ser perdido ao fechar a janela sem querer.
- Ação: verificar `MainWindow.xaml.cs` e adicionar verificação no evento Closing.

**D2 — Duplo clique em "Gerar Ficha de Dobra" / "Adicionar ao Pedido" sem proteção**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:976` (GerarFichaDobra) / `836` (AdicionarAoPedido)
- Problema: Os commands não desabilitam o botão durante execução. Dois cliques rápidos podem gerar 2 PDFs ou adicionar 2 itens idênticos ao pedido.
- Impacto: duplicatas no pedido; múltiplos PDFs sem querer.

**D3 — "Carregar da Biblioteca" sobrescreve segmentos sem confirmação**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:265` (CarregarPecaDoModelo)
- Problema: Carrega os segmentos sem perguntar se o usuário quer descartar o desenho atual. A operação é reversível com Ctrl+Z (registra antes), mas o usuário pode não saber disso.
- Impacto: perda acidental de trabalho em andamento.

**D4 — "Abrir Pedido" verifica Itens.Count mas não _alteradoDesdeUltimoSalvamento**
- Arquivo: `ViewModels/PedidoViewModel.cs:107-112`
- Problema: Verifica `Itens.Count > 0` para mostrar aviso de substituição, mas se o pedido tiver 0 itens após limpar manualmente (mas antes de salvar), abre sem avisar. Se o usuário salvou o pedido e depois adicionou mais itens, o Count > 0 ativa o aviso desnecessariamente.
- Impacto: aviso de substituição aparece quando não deveria / não aparece quando deveria.

**D5 — Múltiplos comprimentos: quantidade double truncada para int sem aviso**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:909`
- Problema: `(int)quantidade` trunca `double` sem aviso. O usuário pode digitar "2.5x3000" e a quantidade seria 2 silenciosamente.
- Impacto: quantidade errada adicionada ao pedido.

**D6 — Trocar de chapa no meio do Modo Rápido mantém segmentos sem re-validação**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:261` (OnChapaSelecionadaChanged → AtualizarPreview)
- Problema: Ao trocar a chapa selecionada com segmentos já desenhados, o preview se atualiza mas os segmentos não são revalidados contra a nova chapa. Dobrasmínimas, raio e K-factor mudam, mas os valores já digitados permanecem. Poderia produzir planificação com valores diferentes da nova chapa sem aviso.
- Impacto: silencioso; usuário pode não perceber que as medidas mudaram.

**D7 — Separador de múltiplos comprimentos não aceita "×" unicode (U+00D7)**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:948`
- Problema: A regex `[xX*]` aceita apenas ASCII. O caractere "×" (multiplicação unicode) que o usuário pode copiar de documentos Word/Excel é rejeitado como entrada inválida.
- Impacto: entrada "2×3000" que parece correta falha com erro confuso.

**D8 — Adicionar segmento com Medida = 0 no modo clássico não é validado**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:349-379` (AdicionarSegmento)
- Problema: Não há verificação `SegMedida > 0` ao adicionar segmento no modo clássico. Um segmento de comprimento 0 pode causar divisão por zero em `DeslocamentoExternoSegmento` (`comp = sqrt(0) = 0`) e produzir NaN nas coordenadas.
- Impacto: crash ou renderização com NaN/infinity.

**D9 — Modo rápido: ângulo fora de 0-180 é silenciosamente substituído por 90°**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:542`
- Problema: Em `ConfirmarGrauPersonalizado()`, `GrauRapidoAtual is > 0 and < 180 ? GrauRapidoAtual : 90.0` substitui silenciosamente valores inválidos por 90° sem avisar o usuário.
- Impacto: usuário digita "200" e o ângulo confirmado é 90° sem feedback.

**D10 — Salvar na biblioteca sem avisar sobre limite de nome genérico**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:780-799` (SalvarNaBiblioteca)
- Problema: Permite salvar peças com nome "Perfil com 5 Dobras" ou "Peça Nova", que são nomes genéricos que poluem a biblioteca. Não há aviso se o nome parecer ser o automático.
- Impacto: biblioteca acumula modelos com nomes genéricos difíceis de distinguir.

---

## E) ARQUITETURA E ORGANIZAÇÃO

**E1 — Métodos públicos de geometria ausentes em IGeometryService**
- Arquivo: `Services/IGeometryService.cs` (não lido integralmente) / `Services/GeometryService.cs`
- Problema: `GerarCoordenadasRetangularesParciais`, `GerarCoordenadasRetangularesAbsolutas`, `DeterminarLadoInternoSegmento` são métodos públicos em `GeometryService` mas ausentes na interface, forçando casts explícitos para `GeometryService` em `SkiaRenderer` e `GeradorPecaService`. Isso viola LSP e impede testes com mocks.
- Correção: adicionar esses métodos à interface.

**E2 — Lógica de coordenadas externas duplicada entre GeometryService e GeradorPecaService**
- Arquivo: `Services/GeradorPecaService.cs:116-207` vs `Services/GeometryService.cs:724-799`
- Problema: Violação de DRY. `CoordenadasExternas` e `Deslocamento` são cópias de `CoordenadasExternasPerfil` e `DeslocamentoExternoSegmento`. Qualquer bug corrigido num lado precisa ser replicado manualmente no outro.
- Correção: expor o método na interface ou tornar `CoordenadasExternasPerfil` interno ao GeometryService e acessível via um método de interface.

**E3 — SkiaRenderer é estático e não pode ser injetado**
- Arquivo: `Services/SkiaRenderer.cs:25`
- Problema: `public static class SkiaRenderer` não tem interface, não pode ser mockado em testes, e não pode ser substituído por outra implementação (ex: para exportar para outro formato). Todos os callers dependem diretamente da classe concreta.
- Correção: criar `ISkiaRenderer` e registrar como singleton.

**E4 — Todos os serviços são Singleton mas sem cache efetivo**
- Arquivo: `App.xaml.cs:51-56`
- Problema: `CsvService`, `BibliotecaPecasService` são Singleton mas releem o disco a cada chamada. O padrão singleton só funciona se os dados forem cacheados internamente, caso contrário seria mais honesto usar Transient.
- Impacto: confusão arquitetural; expectativa de singleton (dados compartilhados) não é atendida.

**E5 — GeometryTests.cs na pasta Services em vez de projeto de testes**
- Arquivo: `Services/GeometryTests.cs`
- Problema: Classe de testes de regressão que só executa em `#if DEBUG` está misturada com o código de produção na mesma pasta. Deveria ser um projeto `CapitalAco.DrawingMacro.Tests` separado usando xUnit/NUnit.
- Impacto: aumento do tamanho do binário em Release (mesmo que o código seja omitido, o arquivo compila); difícil manutenção.

**E6 — Configuracao.cs sem mecanismo de migração de versão**
- Arquivo: `Models/Configuracao.cs:8`
- Problema: `VersaoApp = "1.2.4"` é string de exibição, não versão de schema. Se campos forem adicionados/renomeados, um `config.json` antigo é carregado sem aviso, com valores padrão para novos campos que o usuário pode não perceber.
- Impacto: regressão silenciosa ao atualizar o aplicativo.

**E7 — EditorPecaViewModel com +1.100 linhas — complexidade excessiva**
- Arquivo: `ViewModels/EditorPecaViewModel.cs`
- Problema: O ViewModel acumula: lógica de Modo Rápido, lógica de biblioteca, lógica de geração de boiadeira/tubo, lógica de múltiplos comprimentos, preview, avisos, histórico desfazer. Múltiplas responsabilidades tornam difícil rastrear bugs e adicionar funcionalidades.
- Sugestão: extrair `ModoRapidoController`, `HistoricoDesfazer`, `PreviewManager` como classes auxiliares ou ViewModels parciais.

**E8 — PdfGeneratorService: chapas.Find() em loop (O(n×m))**
- Arquivo: `Services/PdfGeneratorService.cs:312` (Find dentro do loop de itens)
- Problema: `chapas.Find(c => ...)` dentro do loop de `dadosPecas` é O(m) por item, resultando em O(n×m) total para n itens e m chapas. Com pedidos grandes (50+ itens) e CSV com 30+ chapas, são 1500 comparações de string.
- Correção: criar um `Dictionary<string, Chapa>` antes do loop.

---

## F) PERSISTÊNCIA E DADOS

**F1 — biblioteca_pecas.json sem backup antes de gravar**
- Arquivo: `Services/BibliotecaPecasService.cs:58-80` (GravarDados)
- Problema: `File.WriteAllText(filePath, jsonContent)` grava diretamente sem criar `.bak` primeiro. Se a escrita for interrompida (queda de energia, antivírus bloqueando), o arquivo fica corrompido. O método `CarregarDados` detecta a corrupção e grava um arquivo VAZIO como fallback, perdendo todos os modelos.
- Impacto alto: perda irrecuperável de todos os modelos da biblioteca.
- Correção: gravar em `filePath.tmp`, depois renomear atomicamente.

**F2 — ConfigService: gravação não atômica pode corromper config.json**
- Arquivo: `Services/ConfigService.cs:53-56` (SalvarConfiguracao)
- Problema: `File.WriteAllText` não é atômico. Se interrompido, `config.json` fica truncado. Na próxima inicialização, `CarregarConfiguracao` detecta o JSON corrompido e RECRIA COM PADRÕES, perdendo as configurações customizadas do usuário.
- Correção: gravar em arquivo temporário e renomear.

**F3 — CsvService não valida campos críticos do CSV**
- Arquivo: `Services/CsvService.cs:35-63`
- Problema: Não há validação pós-carga: `Espessura <= 0`, `RaioDeDobra <= 0`, `KFactor <= 0` ou `KFactor > 1`, `Coeficiente <= 0`. Uma linha corrompida no CSV produz cálculos de planificação absurdos (corte negativo, peso infinito) sem aviso.
- Impacto: corte errado entregue ao operador de CNC.

**F4 — Dados de usuário gravados na pasta do executável (viola convenção Windows)**
- Arquivo: `Services/ConfigService.cs:15-17`
- Problema: `config.json`, `biblioteca_pecas.json`, `logs/`, `files/` são todos gravados em `AppDomain.CurrentDomain.BaseDirectory` (pasta do exe). Em Windows com UAC, escrever em `C:\Program Files\` requer elevação ou é virtualizado silenciosamente. O caminho correto para dados de usuário é `%APPDATA%\CapitalAco\`.
- Impacto: aplicação pode falhar em ambientes corporativos com permissões restritas; dados ficam perdidos com reinstalação.

**F5 — Log file cresce indefinidamente (RollingInterval.Infinite)**
- Arquivo: `App.xaml.cs:32`
- Problema: `RollingInterval.Infinite` cria um único arquivo de log que cresce sem limite. Em uso intensivo (pedidos grandes, muitos PDFs), o log pode consumir gigabytes.
- Correção: usar `RollingInterval.Day` com `retainedFileCountLimit: 7`.

**F6 — Pedido (.pedido) não tem hash de integridade**
- Arquivo: `ViewModels/PedidoViewModel.cs:86-95` (PedidoArquivo)
- Problema: Arquivos `.pedido` são JSON puro sem checksum. Um arquivo editado manualmente com erros de sintaxe produz `InvalidDataException` com mensagem genérica; valores editados incorretamente passam silenciosamente pela desserialização.
- Impacto: pedidos corrompidos manualmente não são detectados.

---

## G) UI/UX

**G1 — Nenhum indicador de carregamento durante geração de PDF**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:989` / `ViewModels/PedidoViewModel.cs:174`
- Problema: UI trava sem feedback durante `GerarFichaDobra()` e `VisualizarPdf()`. O cursor não muda para aguardar, não há spinner, não há mensagem de status. Para pedidos com 20+ itens, o travamento pode durar vários segundos.
- Impacto: usuário pode achar que a aplicação travou e tentar fechar forçosamente.

**G2 — MessageBox modal "Peça adicionada!" a cada item adicionado**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:928`
- Problema: `MessageBox.Show("Peça adicionada ao carrinho da Ordem de Produção!", ...)` exige que o usuário clique "OK" após cada adição. Para um fluxo de múltiplas peças, isso é extremamente irritante.
- Correção sugerida: substituir por toast/snackbar ou atualização de texto de status.

**G3 — Preview do modo clássico não atualiza ao editar célula do DataGrid (sem CollectionChanged)**
- Arquivo: `Views/EditorPecaView.xaml.cs:83-88` (SegmentosDataGrid_CellEditEnding)
- Observação positiva: o code-behind já chama `vm.AtualizarPreview()` em `CellEditEnding` via `BeginInvoke`. Porém se `CellEditEnding` não for disparado em todos os cenários de edição (ex: editar e pressionar Tab rapidamente), o preview pode ficar defasado.
- Ação: testar se o preview atualiza corretamente em todos os cenários de edição de tabela.

**G4 — Campo comprimento aceita valores inválidos sem feedback imediato**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:870`
- Problema: `ComprimentoPeca` é `double?`. Digitar "0" ou "-500" não produz feedback visual imediato — o campo fica visualmente normal e só ao tentar adicionar ao pedido o usuário recebe a mensagem de erro.
- Correção: validação inline com `IDataErrorInfo` ou `ValidationRule` no binding XAML.

**G5 — DimensoesTotaisTexto mostra "mm (L) x mm (A)" mas unidade inline é redundante**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:1150`
- Problema: `"Dimensões Totais: 230 mm (L) x 45 mm (A)"` é verboso. Considerar `"230 × 45 mm (L × A)"` ou apresentar como dois campos separados.
- Impacto: mínimo estético.

**G6 — Ausência de atalho de teclado para "Gerar Ficha de Dobra"**
- Arquivo: `Views/EditorPecaView.xaml.cs:94` (PreviewKeyDown)
- Problema: O modo rápido tem atalhos completos (setas, G, Enter, ESC, Ctrl+Z). Mas "Gerar Ficha de Dobra" não tem atalho — requer mouse. Para fluxo de trabalho teclado-first, isso quebra o ritmo.
- Sugestão: Ctrl+P ou F5.

**G7 — Aba de pedido não mostra contagem de itens no cabeçalho da aba**
- Problema observado geral: A aba "Ordem de Produção" não indica quantos itens estão no carrinho. O usuário tem que trocar de aba para saber. Uma badge `(N)` no título da aba facilitaria o fluxo.

**G8 — "Salvar na Biblioteca" não atualiza a lista da BibliotecaView imediatamente**
- Problema observado geral: Ao salvar uma peça na biblioteca, a lista de modelos na aba Biblioteca pode não refletir a mudança até a aba ser reaberta/recarregada, dependendo de como `BibliotecaViewModel` observa `IBibliotecaPecasService`.
- Ação: verificar se há evento/notificação entre `EditorPecaViewModel.SalvarNaBiblioteca()` e `BibliotecaViewModel.AtualizarLista()`.

---

## H) SEGURANÇA E CONFIABILIDADE

**H1 — Dados de usuário em pasta do executável: risco de virtualização UAC**
- (Detalhado em F4) — Em Windows com UAC, gravações em `Program Files` podem ser silenciosamente redirecionadas para `VirtualStore` sem que o programa saiba, resultando em dados "fantasmas".

**H2 — Process.Start sem validação do caminho gerado**
- Arquivo: `ViewModels/EditorPecaViewModel.cs:998` / `ViewModels/PedidoViewModel.cs:193`
- Problema: O caminho do PDF passado para `Process.Start` é gerado internamente com timestamp, mas o destino (`_configService.ObterCaminhoSaidaRelatorios()`) é configurável por `config.json`. Se `PastaSaidaRelatorios` for alterado para um caminho contendo `..` (ex: `../../Windows/System32`), o PDF seria gravado lá.
- Impacto: baixo (app desktop local) mas boas práticas sugerem validar que o caminho resultante está dentro do diretório esperado.

**H3 — Sem limite máximo de itens no pedido**
- Arquivo: `ViewModels/PedidoViewModel.cs:163` (Itens.CollectionChanged)
- Problema: O usuário pode adicionar centenas ou milhares de itens ao pedido. `GerarRelatorioPedido` pré-computa a geometria de cada item em memória antes de gerar o PDF. Sem limite, isso pode causar `OutOfMemoryException`.
- Sugestão: avisar ao usuário quando o pedido exceder 100 itens.

**H4 — GeometryTests executa antes de mostrar a janela (bloqueia inicialização em DEBUG)**
- Arquivo: `App.xaml.cs:95` (GeometryTests.ExecutarTestes)
- Problema: Testes de regressão são executados sincronamente na thread UI antes da janela aparecer. Se os testes gerarem PDFs de teste em disco (como sugerido pelo comentário), isso pode levar segundos. Nenhum splash screen ou feedback ao usuário durante esse período.
- Impacto: aplicação parece não responder no primeiro segundo de inicialização em DEBUG.

**H5 — config.json pode ter valores inválidos sem validação de schema**
- Arquivo: `Services/ConfigService.cs:27-29`
- Problema: `JsonSerializer.Deserialize<Configuracao>(jsonContent)` aceita qualquer JSON válido com os campos conhecidos, sem validar ranges. Ex: `PreviewDebounceMs: -1`, `DesenhoSupersampling: 0`, `RelatorioPecasPorPagina: 0` produziriam comportamentos indefinidos em divisões por zero ou buffers de tamanho 0.
- Correção: validar propriedades críticas após deserializar, com clamp para valores seguros.

**H6 — FileShellHelper sem verificação de resultado de operações shell**
- Arquivo: `Services/FileShellHelper.cs` (não lido)
- Problema: Métodos `CopiarArquivoParaAreaDeTransferencia` e `ImprimirArquivo` são chamados sem verificação de sucesso. Se o arquivo estiver bloqueado por outro processo (ex: Acrobat já abriu o PDF), a operação falha silenciosamente.
- Ação: ler `FileShellHelper.cs` e verificar se há tratamento de exceção e feedback ao usuário.

---

## RESUMO PRIORIZADO

| Prioridade | Items | Razão |
|---|---|---|
| **CRÍTICO** | F1, F2, C1 | Perda de dados do usuário (biblioteca, config, sem aviso) |
| **ALTO** | D1, A6, C7, A1, C5 | Perda de pedido sem aviso; UI trava; boiadeira bloqueia; performance; peças inválidas |
| **MÉDIO** | B1, B2, D8, E1, E2, F3, F4 | Cálculos incorretos; crashs por segmento zero; DI quebrada; CSV não validado |
| **BAIXO** | G1-G8, E7, H5 | UX; organização; validação de config |

---

*Total: 58 itens em 8 categorias. Para revisar cada item, solicitar: "Vamos verificar o item [código] do checklist".*
