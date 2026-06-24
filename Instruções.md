# CONTEXTO E DIRETRIZES PARA MIGRAÇÃO AUTÔNOMA: PYTHON PARA C# (.NET)

## 1. OBJETIVO GERAL E CONTINUIDADE MULTI-AGENTES
Atue como um Arquiteto de Software Sênior e Especialista em .NET/C# (WPF ou WinUI 3). O objetivo é migrar este projeto local — focado no detalhamento de peças, cálculo de dobras e geração de pedidos em PDF — de Python para uma aplicação nativa Windows em C# (.NET 8 ou superior).

**MUITO IMPORTANTE:** Esta transição será feita em várias sessões e potencialmente por diferentes modelos de IA. Portanto, **toda a memória do projeto deve ser registrada em arquivos físicos**. O chat atual não deve ser usado como histórico confiável.

## 2. DIRETRIZES ARQUITETURAIS E LIBERDADE DE REFATORAÇÃO
* **Foco no Resultado, não na Cópia:** A estrutura de arquivos, as classes e as funções não precisam ser uma cópia exata de como eram no Python (ex: 1 arquivo `.py` não precisa virar 1 arquivo `.cs`). Use as melhores práticas, os padrões de projeto mais limpos e a forma mais idiomática do C# para atingir a **mesma funcionalidade e o mesmo resultado final**.
* **Modernidade e Performance:** Utilize as bibliotecas mais modernas, enxutas e performáticas do ecossistema .NET.
* **Padrão de Projeto:** Implementar o padrão **MVVM (Model-View-ViewModel)**. A comunicação via `api.py` será substituída por chamadas nativas em C# na camada de ViewModels ou Services.
* **Geração de PDF:** Utilize o **QuestPDF** para a geração de relatórios e detalhamentos, focando em layouts desenhados via código com precisão e alta performance, abandonando qualquer dependência de renderizadores lentos.

## 3. PROTOCOLO DE ESTADO E REGISTRO CONTÍNUO (OBRIGATÓRIO)
Como diferentes IAs lerão o diretório, o estado da migração deve ser imutável no disco.

1. **Criação do Diário de Bordo:** A sua primeira tarefa, após analisar a pasta `core/` e os demais arquivos, é criar e manter rigorosamente atualizado um arquivo na raiz chamado `ESTADO_MIGRACAO.md`.
2. **Estrutura do `ESTADO_MIGRACAO.md`:** Este arquivo deve conter sempre:
    * **Visão Geral da Arquitetura:** Como o sistema Python foi mapeado para o novo sistema C#.
    * **Dependências (NuGet):** Lista das bibliotecas adotadas (ex: `QuestPDF`, `CsvHelper`, `CommunityToolkit.Mvvm`).
    * **Checklist de Conclusão:** O que EXATAMENTE já foi totalmente migrado, testado e está pronto.
    * **Pendências (Backlog):** O que falta fazer, qual o próximo passo imediato e quais os arquivos originais em Python que ainda não foram convertidos.
3. **Atualização Constante:** Toda vez que você concluir a tradução de um módulo, classe, interface ou rotina, **você deve, obrigatoriamente, reescrever o arquivo `ESTADO_MIGRACAO.md`** atualizando o checklist antes de me avisar que terminou.

## 4. FASES DE EXECUÇÃO
* **Fase 1:** Análise geral, definição das bibliotecas e criação do `ESTADO_MIGRACAO.md` com o plano de ação. Aguarde aprovação.
* **Fase 2:** Scaffolding da solução `.sln`, instalação de dependências e criação dos `Models` de base (lendo as regras de `data/chapas.csv` e `biblioteca_pecas.json`).
* **Fase 3 (Core):** Migração das lógicas de geometria e cálculos (ex: módulos de `core/`). Garantir que os cálculos matemáticos mantenham estrita paridade com o original.
* **Fase 4 (PDFs):** Recriação completa dos documentos gerados em `relatorio_pedido.py` e `relatorio_dobra.py` utilizando QuestPDF.
* **Fase 5 (Interface):** Criação da interface gráfica nativa em XAML e ligação com as ViewModels.

## 5. REGRAS DE COMPORTAMENTO
* Seja proativo, leia os arquivos no disco.
* Se um módulo for muito complexo, quebre a execução em partes menores, mas documente isso no arquivo de estado.
* Após cada etapa, informe de maneira concisa o que foi feito e declare o que consta como próximo passo no `ESTADO_MIGRACAO.md`.