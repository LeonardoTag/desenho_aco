# Status das Mudanças (todo.txt)

Atualizado em: 2026-06-25

## Aba Ordem de Produção

- [x] **Coluna de ilustração do perfil** — adicionada antes de "Nome da Peça" no DataGrid, mostra a forma sem medidas (80×60 px, renderizado com SkiaRenderer sem cotas)
- [x] **Botão "Editar" em Ações** — abre a peça no Editor (muda para aba Editor). O editor mostra "EDITANDO PEÇA DO PEDIDO" em laranja. Ao clicar "Salvar no Pedido", o item é atualizado in-place no carrinho e a aba volta para Ordem de Produção. Há também "Cancelar Edição" para descartar.
- [x] **Ordenação por chapa no PDF** — `GerarRelatorioPedido` agora ordena os itens pela posição da chapa em chapas.csv antes de gerar o PDF.

## Editor de Peça

- [x] **Botão "Nova Peça (Ctrl+N)"** — limpa todos os campos e segmentos. Disponível via botão no painel direito e via atalho Ctrl+N.
- [x] **Nomes únicos na biblioteca** — `BibliotecaPecasService.SalvarModelo` rejeita (com mensagem de erro) qualquer nome já existente em outra peça da biblioteca.
- [x] **Destaque do segmento ativo no modo rápido (fase Medidas)** — o segmento cuja medida está sendo inserida é marcado com linha laranja grossa; as cotas desse segmento (externa e interna) ficam laranja com texto maior, deslocadas para não sobrepor o perfil.
