# Status das mudanças do todo.txt

Última atualização: 2026-06-25.

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

## Observações
- Build verificado em Debug e Release (`dotnet build`), sem erros.
- App testado com `dotnet run` (smoke test de inicialização) após as mudanças — abriu sem exceções.
- Não foi feito `dotnet publish` do `win-x64` (os binários de `bin/Release/.../win-x64/publish` não foram regenerados); rodar isso manualmente se for necessário redistribuir.
