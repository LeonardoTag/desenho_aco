# Estado da Migração: Python para C# (.NET 8)

Este documento registra o status atual, as decisões de design, a arquitetura adotada e o cronograma do processo de migração do sistema local de detalhamento de dobras, projetos estruturais e geração de ordens de produção de Python para C# (.NET 8 / WPF).

---

## 1. Visão Geral da Arquitetura C# (.NET 8 WPF)

A nova aplicação adota o padrão **MVVM (Model-View-ViewModel)** puro com injeção de dependências, garantindo separação clara entre a interface de usuário (View), lógica de apresentação (ViewModel), regras de negócio (Services) e dados (Models).

### Mapeamento de Componentes (Python ➔ C#)

| Componente Python | Componente C# (.NET 8) | Responsabilidade |
| :--- | :--- | :--- |
| `main.py` / `main.pyw` | `App.xaml` / `App.xaml.cs` | Ponto de entrada do sistema, inicialização de dependências, logging e verificação de integridade (Pre-flight checks). |
| `api.py` | Camada de `ViewModels` & `Services` | Intermediação entre a interface gráfica e o motor de cálculos/banco local. |
| `config_manager.py` | `ConfigService` (`IConfigService`) | Leitura e escrita resiliente do arquivo `config.json` no diretório executável. |
| `core/numeros.py` | `NumericUtils` | Validação e formatação de números com suporte a vírgula como separador decimal. |
| `core/biblioteca_pecas.py` | `BibliotecaPecasService` (`IBibliotecaPecasService`) | Gerenciamento de persistência de modelos de peças em `biblioteca_pecas.json`. |
| `core/geradores_pecas.py` | `GeradorPecaService` (`IGeradorPecaService`) | Motores de geração procedural (Boiadeira). Implementa o solver de otimização Nelder-Mead traduzido para C#. |
| `core/desenhar.py` | `GeometryService` (`IGeometryService`) & `DrawingEngine` | Cálculo de coordenadas (polares/cartesianas), compensações de dobras (K-Factor), largura de corte flat, dimensões acabadas e renderização de desenhos usando SkiaSharp. |
| `core/relatorio_pedido.py` | `PdfGeneratorService` | Geração do PDF de Ordens de Produção usando QuestPDF (substituindo ReportLab). |
| `core/relatorio_dobra.py` | `PdfGeneratorService` | Geração do PDF de Detalhamento de Dobra com planificação e cotas usando QuestPDF (substituindo ReportLab). |
| `web/` (HTML/JS/CSS) | XAML Views (`Views/`) | Telas nativas de alta performance em WPF. |

### Decisão Arquitetural Crítica: Renderização Unificada com SkiaSharp
Para garantir 100% de paridade visual e matemática entre o desenho exibido na tela (Preview) e o desenho gerado nos relatórios PDF, usaremos **SkiaSharp** como o motor gráfico comum. A mesma rotina de desenho será compartilhada no C#, renderizando para um bitmap no WPF e para a canvas vetorial no QuestPDF.

---

## 2. Dependências Adotadas (NuGet)

*   **`QuestPDF`**: Geração de documentos PDF moderna, rápida e baseada em layout declarativo.
*   **`Serilog`** + **`Serilog.Sinks.File`**: Registro robusto de logs em arquivos diários locais com rotação inteligente.
*   **`CommunityToolkit.Mvvm`**: Biblioteca padrão do ecossistema .NET para MVVM (gera código de propriedades notificáveis e comandos automaticamente por meio de Source Generators).
*   **`CsvHelper`**: Leitura rápida e fortemente tipada das especificações de chapa em `chapas.csv`.
*   **`System.Text.Json`** *(Nativo do .NET)*: Serialização/desserialização de alta performance para `config.json` e `biblioteca_pecas.json`.
*   **`SkiaSharp`**: Desenho 2D de alta precisão para geração de previews e imagens de planificação e dobras estruturais.

---

## 3. Checklist de Conclusão

- [x] **Fase 1: Planejamento e Estruturação do Diário de Bordo**
    - [x] Análise estrutural da pasta Python original
    - [x] Criação do arquivo `ESTADO_MIGRACAO.md` na raiz
    - [x] Obtenção da aprovação para o primeiro commit
- [x] **Fase 2: Scaffolding e Infraestrutura Inicial (C#)**
    - [x] Criação do arquivo de Solução `.sln` e do projeto WPF (.NET 8)
    - [x] Instalação de todos os pacotes NuGet necessários
    - [x] Configuração do Serilog (logging diário local)
    - [x] Criação dos Models (`Chapa`, `ModeloPeca`, `Segmento`, `Configuracao`)
    - [x] Carregamento inicial de `chapas.csv` e `biblioteca_pecas.json`
- [ ] **Fase 3: Camada Core - Matemática e Geometria**
    - [ ] Portabilidade de `NumericUtils` e manipulação de strings/valores decimais
    - [ ] Implementação das fórmulas geométricas base (Azimutes, deduções e compensações de dobras)
    - [ ] Conversão de instruções convencionais para polares e cartesianas discretas
    - [ ] Portabilidade do algoritmo solver Nelder-Mead e gerador Boiadeira
    - [ ] Criação de testes unitários para validar a exatidão matemática contra o Python
- [ ] **Fase 4: Geração de Relatórios e PDFs (QuestPDF)**
    - [ ] Implementação da geração de imagens via SkiaSharp (anotações de cotas e ângulos)
    - [ ] Recriação da Ordem de Produção (antigo `relatorio_pedido.py`) com QuestPDF
    - [ ] Recriação do Detalhamento de Dobra (antigo `relatorio_dobra.py`) com QuestPDF
- [ ] **Fase 5: Interface Gráfica (WPF/XAML) e ViewModels**
    - [ ] Design da interface moderna baseada em Grid/Tabs e estilos consistentes (Dark/Light mode via HSL)
    - [ ] Implementação de ViewModels usando CommunityToolkit.Mvvm
    - [ ] Tela de desenhar peça interativa (atualização de preview em tempo real)
    - [ ] Integração com Biblioteca de Peças e Configurações

---

## 4. Pendências e Próximos Passos (Backlog)

### Próximo Passo Imediato
**Fase 3 - Matemática e Geometria:** Implementar as lógicas de geometria base, conversão polar/cartesiana e o otimizador Nelder-Mead no C#.

### Arquivos Python a Migrar:
*   `desenho_python/config_manager.py` ➔ C# Service (Concluído)
*   `desenho_python/core/biblioteca_pecas.py` ➔ C# Service (Concluído)
*   `desenho_python/core/numeros.py` ➔ C# Util
*   `desenho_python/core/geradores_pecas.py` ➔ C# Service/Solver
*   `desenho_python/core/desenhar.py` ➔ C# GeometryEngine + SkiaRenderer
*   `desenho_python/core/relatorio_pedido.py` ➔ C# QuestPDF Component
*   `desenho_python/core/relatorio_dobra.py` ➔ C# QuestPDF Component
*   `desenho_python/api.py` ➔ C# ViewModels
*   `desenho_python/main.py` ➔ C# `App.xaml.cs` e `MainWindow.xaml` (Concluído)
