# Status das mudanças do todo.txt

Última atualização: 2026-06-25 (rodada 2).

## Rodada 1 (concluída)

| # | Item do todo.txt | Status | Onde |
|---|---|---|---|
| 1 | UI dinâmica com o tamanho da aba | Concluído | `EditorPecaView.xaml` (colunas com `*`/`MinWidth` em vez de pixels fixos, `ScrollViewer` no painel direito), `MainWindow.xaml` (`MinWidth`/`MinHeight`) |
| 2 | Demora no launch do programa | Concluído | `App.xaml.cs`: os testes de regressão/integração de PDF (`GeometryTests.ExecutarTestes`) só rodam em build Debug (`#if DEBUG`). Em Release eles custavam ~13s a cada abertura (geração de 2 PDFs de teste em disco) sem nenhum benefício para o usuário final |
| 3 | Botão para abrir a pasta dos arquivos gerados | Concluído | `Services/FileShellHelper.cs` (`AbrirPasta`) + botão "Abrir Pasta de Relatórios" em `EditorPecaView.xaml` e `PedidoView.xaml` |
| 4 | Copiar "link" do arquivo (como Ctrl+C no Explorer) | Concluído | `FileShellHelper.CopiarArquivoParaAreaDeTransferencia` (usa `Clipboard.SetFileDropList`, formato CF_HDROP), chamado ao gerar/abrir a Ficha de Dobra e a Ordem de Produção |
| 5 | Iniciar com Modo Rápido já selecionado | Concluído | `EditorPecaViewModel._modoRapidoAtivo = true` |
| 6 | Ctrl+Z no Editor de Peça | Concluído | `EditorPecaViewModel`: histórico genérico de snapshots (`RegistrarEstadoParaDesfazer`/`DesfazerCommand`), cobrindo adicionar/remover/limpar segmentos, edição de medida/ângulo/direção (inclusive edição direta na tabela), geradores (Boiadeira/Tubo) e troca de modo. `DesfazerModoRapido` (Ctrl+Backspace) passou a reusar o mesmo histórico. Atalho Ctrl+Z adicionado em `EditorPecaView.xaml.cs`. Bônus: corrigido binding `SelectedSegmento` que estava ausente do ViewModel (o botão "Remover Sel." não funcionava) |
| 7 | Todos os ícones do programa = logo.png | Concluído | Gerado `Assets/logo.ico` a partir de `logo.png`; `ApplicationIcon` no `.csproj` e `Icon` na `MainWindow.xaml` |
| 8 | Direção em setas / Tipo de Medida "Interna"/"Externa" | Concluído | `EditorPecaView.xaml`: ComboBox de Direção mostra ↑→↓← (valor real via `Tag`); ComboBox de Tipo de Medida mostra "Externa"/"Interna" |
| 9 | Configurações inacessíveis (sem scroll) + texto branco em fundo claro | Concluído | `ConfiguracaoView.xaml`: `ScrollViewer` adicionado, estilo `FonteBox` com foreground escuro (estava branco sobre fundo claro); `PedidoView.xaml`: cabeçalho da tabela da Ordem de Produção também estava branco sobre fundo claro |
| 10 | Escala do desenho deve considerar só os segmentos | Concluído | `SkiaRenderer.cs`: a escala agora é calculada só a partir do bounding box dos segmentos (`dimX`/`dimY`); o comprimento da peça só afeta a profundidade *estilizada* da extrusão 3D (limitada a uma fração do perfil), nunca a escala da seção transversal |

## Rodada 2 (concluída)

| # | Item do todo.txt | Status | Onde |
|---|---|---|---|
| 1 | Desenho não encaixa nos quadros (Dobra e Ordem) | Concluído | `SkiaRenderer.cs`: a correção da rodada 1 (escala só pelos segmentos) tinha um efeito colateral — a extrusão 3D podia sangrar para fora da caixa, pois a escala passou a usar 100% da largura/altura útil sem reservar espaço para ela. Corrigido reservando espaço para a extrusão *antes* de calcular a escala, mas com a profundidade reservada limitada a uma fração do perfil (não mais proporcional ao comprimento real sem limite) — resolve as duas pontas: cabe na caixa e não encolhe com peças compridas. Verificado visualmente lendo os PDFs gerados |
| 2 | Relatórios mais profissionais e fáceis de visualizar | Concluído | `PdfGeneratorService.cs`: Detalhamento de Dobra ganhou título em azul-marinho, linha divisória sob o cabeçalho e legendas ("DESENHO DA PEÇA" / "PLANIFICAÇÃO (CHAPA ESTICADA)") acima de cada caixa, e um quadro com fundo leve para as instruções. Ordem de Produção ganhou numeração de item, nome da peça em destaque, observação do item exibida, listras zebradas (fundo alternado) e um resumo final (Total de Itens / Total de Peças / Peso Total Estimado) |
| 3 | Sufixo "50i" malvisto → fundo de cor + ângulo sublinhado | Concluído | `SkiaRenderer.cs`: medida externa agora é um selo (retângulo preenchido) azul-escuro com letra branca; medida interna é um selo rosa-claro com letra escura (sem mais sufixo "i"); ângulos de dobra não-retos (tanto no desenho da peça quanto na planificação) agora aparecem sublinhados. Legenda do PDF de Dobra atualizada para descrever a nova convenção |

## Observações
- Build verificado em Debug e Release (`dotnet build`), sem erros, após as duas rodadas.
- Rodada 2 verificada também visualmente: PDFs de teste gerados pelo pre-flight (Debug) foram lidos e inspecionados como imagem — desenho cabe corretamente nas caixas, selos de medida e sublinhado de ângulo aparecem como esperado, resumo da Ordem de Produção correto. Arquivos de teste descartados depois (não fazem parte do repositório).
- Não foi feito `dotnet publish` do `win-x64` (os binários de `bin/Release/.../win-x64/publish` não foram regenerados); rodar isso manualmente se for necessário redistribuir.
