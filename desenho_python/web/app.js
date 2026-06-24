/**
 * Capital Aço — Webview Frontend Controller
 */

// Mock API for testing outside pywebview environment
const mockApi = {
  _isMock: true,
  obter_chapas: async () => [
    {codigo: "#20", espessura: 0.90, raio_de_dobra: 0.54, k_factor: 0.165, coeficiente: 7.2},
    {codigo: "#18", espessura: 1.20, raio_de_dobra: 0.72, k_factor: 0.165, coeficiente: 9.6},
    {codigo: "#16", espessura: 1.50, raio_de_dobra: 0.90, k_factor: 0.165, coeficiente: 12.0},
    {codigo: "#14", espessura: 2.00, raio_de_dobra: 1.20, k_factor: 0.165, coeficiente: 16.0},
    {codigo: "#13", espessura: 2.25, raio_de_dobra: 1.35, k_factor: 0.165, coeficiente: 18.0},
    {codigo: "#12", espessura: 2.65, raio_de_dobra: 1.59, k_factor: 0.165, coeficiente: 21.2},
    {codigo: "#11", espessura: 3.00, raio_de_dobra: 1.80, k_factor: 0.165, coeficiente: 24.0},
    {codigo: "#10", espessura: 3.35, raio_de_dobra: 2.01, k_factor: 0.165, coeficiente: 26.8},
    {codigo: "#9", espessura: 3.75, raio_de_dobra: 2.25, k_factor: 0.165, coeficiente: 30.0},
    {codigo: "#8", espessura: 4.25, raio_de_dobra: 2.55, k_factor: 0.165, coeficiente: 34.0},
    {codigo: "#3/16", espessura: 4.75, raio_de_dobra: 2.85, k_factor: 0.165, coeficiente: 38.0},
    {codigo: "#1/4", espessura: 6.30, raio_de_dobra: 3.78, k_factor: 0.165, coeficiente: 50.4},
    {codigo: "#5/16", espessura: 8.00, raio_de_dobra: 4.80, k_factor: 0.165, coeficiente: 64.0},
    {codigo: "#3/8", espessura: 9.50, raio_de_dobra: 5.70, k_factor: 0.165, coeficiente: 76.0},
    {codigo: "#1/2", espessura: 12.50, raio_de_dobra: 7.50, k_factor: 0.165, coeficiente: 100.0},
    {codigo: "#5/8", espessura: 16.00, raio_de_dobra: 9.60, k_factor: 0.165, coeficiente: 128.0},
    {codigo: "#3/4", espessura: 19.00, raio_de_dobra: 11.40, k_factor: 0.165, coeficiente: 152.0},
    {codigo: "#16BZ", espessura: 1.50, raio_de_dobra: 0.90, k_factor: 0.165, coeficiente: 12.0},
    {codigo: "#14BZ", espessura: 2.00, raio_de_dobra: 1.20, k_factor: 0.165, coeficiente: 16.0},
    {codigo: "#12BZ", espessura: 2.65, raio_de_dobra: 1.59, k_factor: 0.165, coeficiente: 21.2},
    {codigo: "#12XDZ", espessura: 2.65, raio_de_dobra: 2.19, k_factor: 0.165, coeficiente: 24.2},
    {codigo: "#11XDZ", espessura: 3.00, raio_de_dobra: 2.40, k_factor: 0.165, coeficiente: 27.0},
    {codigo: "#3/16XDZ", espessura: 4.75, raio_de_dobra: 3.45, k_factor: 0.165, coeficiente: 41.0}
  ],
  obter_configuracoes: async () => ({
    titulo_app: "Capital Aço — MOCK PREVIEW",
    comprimento_preview_placeholder: 3000,
    medida_placeholder: 50,
    pasta_saida_relatorios: "files",
    relatorio_nome_responsavel: "Leonardo Mock"
  }),
  listar_modelos: async (filtro) => [
    {id: "m1", nome: "Mock Perfil U", chapa: "#16", comprimento: 3000, segmentos: [["E", 90, 50, "e"], ["N", 90, 50, "e"], ["W", 90, 50, "e"]]}
  ],
  salvar_modelo: async (nome, chapa, comprimento, segmentos, id, desc) => ({nome, id: id || "m2"}),
  excluir_modelo: async () => true,
  salvar_configuracoes: async () => ({sucesso: true}),
  gerar_preview: async () => {
    // Return a mock tiny transparent PNG base64
    return "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
  },
  calcular_peso: async () => 45,
  calcular_largura_corte: async () => 150,
  obter_dimensoes_acabadas: async () => ({x: 100, y: 50}),
  verificar_avisos: async () => [],
  gerar_boiadeira: async (p) => ({
    sucesso: true,
    peca: {
      nome: `Boiadeira ${p.largura_total}`,
      chapa: p.chapa,
      comprimento: p.comprimento,
      segmentos: [["S", 90, p.altura_aba, "e"], ["E", 90, p.primeiro_gomo, "e"], ["N", 45, 28, "e"], ["E", 45, p.tamanho_gomo_superior, "e"], ["S", 45, 28, "e"], ["E", 90, p.primeiro_gomo, "e"], ["N", 90, p.altura_aba, "e"]]
    }
  }),
  gerar_relatorio_dobra: async () => ({sucesso: true, nome: "MOCK_DOBRA.pdf"}),
  gerar_relatorio_pedido: async () => ({sucesso: true, nome: "MOCK_PEDIDO.pdf"}),
  abrir_pasta_saida: async () => true,
  executar_migracao_pasta: async () => "concluido"
};

// Application State
const state = {
  pecaNome: "",
  chapaCodigo: "",
  comprimento: 3000,
  quantidade: 1,
  segmentos: [], // Array of { direcao, angulo, medida, tipo }
  ordemPecas: [], // Array of saved pieces in session
  modoAtual: "rapida", // "classica" or "rapida"
  chapas: [],
  configuracoes: {},
  
  // Quick Mode state variables
  faseRapida: "desenho", // "desenho" or "medidas"
  indiceMedidaRapida: 0,
  proximoGrauRapido: null,
  
  // Editing state
  segmentoEditandoIndice: null,
  
  // Guard flag to prevent Enter double-fire after grau confirmation
  _grauAcabouDeConfirmar: false,
  
  // Debounce timers
  previewTimer: null,
};

// Map direction codes to arrows and UI descriptions
const DIRECOES_UI = {
  "W": "← Oeste",
  "E": "→ Leste",
  "N": "↑ Norte",
  "S": "↓ Sul"
};

const TECLAS_DIRECAO = {
  "ArrowLeft": "W", "KeyA": "W", "a": "W", "Left": "W",
  "ArrowRight": "E", "KeyD": "E", "d": "E", "Right": "E",
  "ArrowUp": "N", "KeyW": "N", "w": "N", "Up": "N",
  "ArrowDown": "S", "KeyS": "S", "s": "S", "Down": "S"
};

// Resilient API initialization
document.addEventListener("DOMContentLoaded", () => {
  inicializarPollerApi();
  configurarEventosGerais();
});

function inicializarPollerApi() {
  const limiteTentativas = 20; // 2 seconds limit to detect native webview backend
  let tentativa = 0;
  
  const poller = setInterval(() => {
    tentativa++;
    if (window.pywebview && window.pywebview.api && !window.pywebview.api._isMock) {
      clearInterval(poller);
      console.log("Conectado com o Python com sucesso.");
      inicializarAplicativo();
    } else if (tentativa >= limiteTentativas) {
      clearInterval(poller);
      console.log("Conectado com o Mock API.");
      window.pywebview = {
        api: mockApi
      };
      inicializarAplicativo();
    }
  }, 100);
}

// Wrap localStorage safely to comply with file:// protocol try/catch rule
function obterDoLocalStorage(chave) {
  try {
    return localStorage.getItem(chave);
  } catch (e) {
    console.warn("localStorage inacessível:", e);
    return null;
  }
}

function salvarNoLocalStorage(chave, valor) {
  try {
    localStorage.setItem(chave, valor);
  } catch (e) {
    console.warn("localStorage inacessível para gravação:", e);
  }
}

async function inicializarAplicativo() {
  // Load Theme
  const temaSalvo = obterDoLocalStorage("app-theme") || "dark";
  if (temaSalvo === "light") {
    document.body.classList.remove("dark");
  } else {
    document.body.classList.add("dark");
  }
  atualizarLogoTema();

  // Load configuration from Python
  state.configuracoes = await window.pywebview.api.obter_configuracoes();
  
  // Load chapas list
  state.chapas = await window.pywebview.api.obter_chapas();
  popularDropdownChapas();
  
  // Load saved session pieces if any
  const pecasSalvas = obterDoLocalStorage("session-pecas");
  if (pecasSalvas) {
    try {
      state.ordemPecas = JSON.parse(pecasSalvas);
      atualizarTabelaOrdem();
    } catch (e) {
      state.ordemPecas = [];
    }
  }

  // Set default values from configuration
  state.comprimento = state.configuracoes.comprimento_preview_placeholder || 3000;
  document.getElementById("peca-comprimento").value = state.comprimento;
  document.getElementById("peca-quantidade").value = state.quantidade;

  if (state.chapas.length > 0) {
    state.chapaCodigo = state.chapas[0].codigo;
    document.getElementById("peca-chapa").value = state.chapaCodigo;
  }
  
  aplicarModo("rapida");
  novaPeca();
}

function atualizarLogoTema() {
  const isDark = document.body.classList.contains("dark");
  const logoImg = document.querySelector(".logo-img");
  if (logoImg) {
    if (isDark) {
      logoImg.src = "images/s%C3%B3%20logo%20branco.png";
    } else {
      logoImg.src = "images/s%C3%B3%20logo.png";
    }
  }
}

function popularDropdownChapas() {
  const dropdownEditor = document.getElementById("peca-chapa");
  const dropdownBoiadeira = document.getElementById("boiadeira-chapa");
  const dropdownTubo = document.getElementById("tubo-chapa");
  
  dropdownEditor.innerHTML = "";
  dropdownBoiadeira.innerHTML = "";
  if (dropdownTubo) dropdownTubo.innerHTML = "";
  
  state.chapas.forEach(chapa => {
    const option = document.createElement("option");
    option.value = chapa.codigo;
    option.textContent = `${chapa.codigo.replace("#", "")} (${formatarDecimal(chapa.espessura)} mm)`;
    dropdownEditor.appendChild(option.cloneNode(true));
    dropdownBoiadeira.appendChild(option.cloneNode(true));
    if (dropdownTubo) dropdownTubo.appendChild(option.cloneNode(true));
  });
}

function formatarDecimal(valor) {
  if (valor === undefined || valor === null) return "";
  return String(valor).replace(".", ",");
}

function converterVirgulaPonto(texto) {
  return parseFloat(String(texto).replace(/\s+/g, "").replace(",", "."));
}

function jsDefinirAzimute(direcao, grau, azimuteAnterior) {
  let azimuteDirecao = 0;
  if (direcao === "N") azimuteDirecao = 0;
  else if (direcao === "S") azimuteDirecao = 180;
  else if (direcao === "E") azimuteDirecao = 90;
  else if (direcao === "W") azimuteDirecao = 270;

  let azimute = 0;
  if (grau === 90) {
    azimute = azimuteDirecao;
  } else {
    if (azimuteAnterior === null || azimuteAnterior === undefined) {
      azimute = azimuteDirecao - (90 - grau);
    } else {
      switch (azimuteAnterior) {
        case 0:
          if (azimuteDirecao === 0 || azimuteDirecao === 90 || azimuteDirecao === 180) {
            azimute = grau;
          } else {
            azimute = 360 - grau;
          }
          break;
        case 90:
          if (azimuteDirecao === 90 || azimuteDirecao === 180 || azimuteDirecao === 270) {
            azimute = 90 + grau;
          } else {
            azimute = 90 - grau;
          }
          break;
        case 180:
          if (azimuteDirecao === 0 || azimuteDirecao === 90 || azimuteDirecao === 270) {
            azimute = 180 + grau;
          } else {
            azimute = 180 - grau;
          }
          break;
        case 270:
          if (azimuteDirecao === 0 || azimuteDirecao === 90 || azimuteDirecao === 270) {
            azimute = 270 + grau;
          } else {
            azimute = 270 - grau;
          }
          break;
        default:
          if (azimuteAnterior > 0 && azimuteAnterior < 90) {
            if (azimuteDirecao === 0 || azimuteDirecao === 270) {
              azimute = azimuteAnterior - grau;
            } else {
              azimute = azimuteAnterior + grau;
            }
          } else if (azimuteAnterior > 90 && azimuteAnterior < 180) {
            if (azimuteDirecao === 0 || azimuteDirecao === 90) {
              azimute = azimuteAnterior - grau;
            } else {
              azimute = azimuteAnterior + grau;
            }
          } else if (azimuteAnterior > 180 && azimuteAnterior < 270) {
            if (azimuteDirecao === 0 || azimuteDirecao === 270) {
              azimute = azimuteAnterior + grau;
            } else {
              azimute = azimuteAnterior - grau;
            }
          } else { // 270 < azimuteAnterior < 360
            if (azimuteDirecao === 0 || azimuteDirecao === 90) {
              azimute = azimuteAnterior + grau;
            } else {
              azimute = azimuteAnterior - grau;
            }
          }
      }
    }
  }
  while (azimute < 0) azimute += 360;
  return azimute % 360;
}

function obterAzimutesDoDesenho() {
  let azimutes = [];
  let azimuteAnterior = null;
  state.segmentos.forEach(seg => {
    let az = jsDefinirAzimute(seg.direcao, seg.angulo || 90, azimuteAnterior);
    azimutes.push(az);
    azimuteAnterior = az;
  });
  return azimutes;
}

function parseMultiplesComprimentos(texto) {
  const partes = texto.split(",");
  const resultados = [];
  for (let parte of partes) {
    parte = parte.trim();
    if (!parte) continue;
    
    // Match something like "10x3000" or "10*3000" or "10 x 3000" or "3000"
    const regex = /^(\d+)\s*(?:[xX*]|\s+)\s*(\d+(?:[.,]\d+)?)$/;
    const match = parte.match(regex);
    if (match) {
      const qtd = parseInt(match[1]);
      const comp = converterVirgulaPonto(match[2]);
      if (isNaN(comp) || comp <= 0 || isNaN(qtd) || qtd <= 0) {
        throw new Error(`Item inválido: "${parte}"`);
      }
      resultados.push({ comprimento: comp, quantidade: qtd });
    } else {
      const comp = converterVirgulaPonto(parte);
      if (isNaN(comp) || comp <= 0) {
        throw new Error(`Item inválido: "${parte}"`);
      }
      resultados.push({ comprimento: comp, quantidade: 1 });
    }
  }
  return resultados;
}

// Mode Selection F1/F2
function aplicarModo(modo) {
  state.modoAtual = modo;
  
  const badgeClassico = document.getElementById("badge-modo-classico");
  const badgeRapido = document.getElementById("badge-modo-rapido");
  const arrowsGrid = document.getElementById("manual-segment-controls");
  const instructions = document.getElementById("rapida-instructions");
  const editorTitle = document.getElementById("segment-editor-title");
  
  if (modo === "classica") {
    badgeClassico.classList.add("active");
    badgeRapido.classList.remove("active");
    arrowsGrid.classList.remove("hidden");
    instructions.classList.add("hidden");
    editorTitle.textContent = "Adicionar Segmento";
    cancelarEdicaoSegmento();
  } else {
    badgeClassico.classList.remove("active");
    badgeRapido.classList.add("active");
    instructions.classList.remove("hidden");
    
    if (state.faseRapida === "desenho" || state.faseRapida === "grau") {
      if (state.faseRapida === "desenho") {
        editorTitle.textContent = "Modo Rápido: Forma";
        arrowsGrid.classList.add("hidden");
      } else {
        editorTitle.textContent = "Definir Ângulo de Dobra";
        arrowsGrid.classList.add("hidden");
      }
    } else if (state.segmentos.length > 0) {
      const temIncompleto = state.segmentos.some(s => s.medida === null);
      if (temIncompleto) {
        state.faseRapida = "medidas";
        state.indiceMedidaRapida = state.segmentos.findIndex(s => s.medida === null);
        editorTitle.textContent = `Modo Rápido: Medida ${state.indiceMedidaRapida + 1}/${state.segmentos.length}`;
        arrowsGrid.classList.remove("hidden");
      } else {
        state.faseRapida = "concluido";
        editorTitle.textContent = "Desenho concluído";
        arrowsGrid.classList.add("hidden");
        instructions.classList.add("hidden");
      }
    } else {
      state.faseRapida = "desenho";
      editorTitle.textContent = "Modo Rápido: Forma";
      arrowsGrid.classList.add("hidden");
    }
    atualizarListaSegmentos();
    atualizarPreview();
  }
  atualizarIndicadorModo();
}

function atualizarIndicadorModo() {
  const stripe = document.getElementById("mode-status-stripe");
  const titleSpan = document.getElementById("mode-stripe-title");
  const dicaSpan = document.getElementById("mode-stripe-dica");
  
  if (!stripe || !titleSpan || !dicaSpan) return;
  
  // Clear existing classes
  stripe.className = "mode-stripe";
  
  let titulo = "";
  let dica = "";
  let classeCor = "";
  let usaHtml = false;
  
  if (state.modoAtual === "classica") {
    if (state.segmentoEditandoIndice !== null) {
      titulo = `EDITAR SEGMENTO ${state.segmentoEditandoIndice + 1}`;
      dica = "Altere os campos · Setas direção · Enter/Aplicar · Esc cancelar";
      classeCor = "stripe-edicao";
    } else {
      titulo = "CLÁSSICO";
      dica = "Setas direção · Enter adicionar segmento · F1/F2 trocar modo";
      classeCor = "stripe-classico";
    }
  } else { // Rápido
    if (state.faseRapida === "desenho") {
      titulo = "RÁPIDO — DESENHAR";
      dica = "Setas segmentos · Enter confirmar forma · F1/F2 trocar modo";
      classeCor = "stripe-desenho-rapido";
    } else if (state.faseRapida === "grau") {
      titulo = "DEFINIR ÂNGULO DE DOBRA";
      const valAtual = state.proximoGrauRapido !== null ? state.proximoGrauRapido : 90;
      dica = `Digite o ângulo de deflexão e Enter: <input type="number" id="input-grau-rapido" class="stripe-inline-input" value="${valAtual}" step="any" min="0.1" max="179.9">`;
      classeCor = "stripe-grau-rapido";
      usaHtml = true;
    } else if (state.faseRapida === "medidas") {
      const total = state.segmentos.length;
      const atual = state.indiceMedidaRapida + 1;
      titulo = `RÁPIDO — MEDIDAS (${atual}/${total})`;
      dica = "Digite a medida e Enter · Esc voltar ao desenho";
      classeCor = "stripe-medidas-rapido";
    } else if (state.faseRapida === "concluido") {
      titulo = "RÁPIDO — CONCLUÍDO";
      dica = "Ctrl+S ou botão Adicionar · Ctrl+N nova peça · F1/F2 trocar modo";
      classeCor = "stripe-concluido";
    }
  }
  
  titleSpan.textContent = titulo;
  if (usaHtml) {
    dicaSpan.innerHTML = dica;
    const input = document.getElementById("input-grau-rapido");
    if (input) {
      input.focus();
      input.select();
      input.addEventListener("keydown", e => {
        if (e.key === "Enter") {
          e.preventDefault();
          e.stopPropagation();
          const val = parseFloat(input.value);
          if (!isNaN(val) && val > 0 && val < 180) {
            state.proximoGrauRapido = val;
            state._grauAcabouDeConfirmar = true;
            setTimeout(() => { state._grauAcabouDeConfirmar = false; }, 0);
            state.faseRapida = "desenho";
            aplicarModo("rapida");
          } else {
            alert("Ângulo inválido. Deve ser entre 0 e 180 graus.");
          }
        } else if (e.key === "Escape") {
          e.preventDefault();
          e.stopPropagation();
          state.faseRapida = "desenho";
          aplicarModo("rapida");
        }
      });
    }
  } else {
    dicaSpan.textContent = dica;
  }
  stripe.classList.add(classeCor);
}

// Generate image preview of drawing
function agendarPreview(imediato = false) {
  if (state.previewTimer) {
    clearTimeout(state.previewTimer);
  }
  
  const delay = imediato ? 0 : (state.configuracoes.preview_debounce_ms || 150);
  
  state.previewTimer = setTimeout(async () => {
    const badgeDim = document.getElementById("badge-dimensoes-acabadas");
    if (state.segmentos.length === 0) {
      document.getElementById("preview-img").classList.add("hidden");
      document.getElementById("preview-placeholder-text").classList.remove("hidden");
      document.getElementById("badge-largura-corte").textContent = "Corte: -";
      document.getElementById("badge-peso-peca").textContent = "Peso: -";
      if (badgeDim) badgeDim.textContent = "Dimensões: -";
      document.getElementById("btn-pdf-dobra").disabled = true;
      document.getElementById("avisos-container").classList.add("hidden");
      return;
    }

    const canvasDiv = document.getElementById("preview-image-container");
    const larguraCanvas = canvasDiv.clientWidth;
    const alturaCanvas = canvasDiv.clientHeight;
    
    // Prepare instructions conv for rendering
    const segmentosInstrucoes = state.segmentos.map(s => {
      // If segment is part of skeleton in quick mode and has no measure yet
      const medidaVal = s.medida === null ? (state.configuracoes.medida_placeholder || 50) : s.medida;
      return [
        s.direcao,
        s.angulo,
        medidaVal,
        s.tipo,
        s.curvo || false,
        s.curva_info || null
      ];
    });

    let comprimentoVal = state.comprimento || 3000;
    let quantidadeVal = state.quantidade || 1;

    const chkMultiplos = document.getElementById("chk-multiplos-comprimentos");
    if (chkMultiplos && chkMultiplos.checked) {
      const txtMultiplos = document.getElementById("peca-comprimentos-multiplos").value;
      try {
        const parsed = parseMultiplesComprimentos(txtMultiplos);
        if (parsed.length > 0) {
          comprimentoVal = parsed[0].comprimento;
          quantidadeVal = parsed[0].quantidade;
        }
      } catch (err) {
        comprimentoVal = state.configuracoes.comprimento_preview_placeholder || 3000;
        quantidadeVal = 1;
      }
    } else {
      comprimentoVal = converterVirgulaPonto(document.getElementById("peca-comprimento").value) || state.configuracoes.comprimento_preview_placeholder || 3000;
      quantidadeVal = parseInt(document.getElementById("peca-quantidade").value) || 1;
    }

    const instrucoes = {
      chapa: state.chapaCodigo,
      comprimento: comprimentoVal,
      segmentos: segmentosInstrucoes
    };

    // 1. Get Preview Base64 Image
    const base64Uri = await window.pywebview.api.gerar_preview(instrucoes, larguraCanvas, alturaCanvas);
    if (base64Uri) {
      const img = document.getElementById("preview-img");
      img.src = base64Uri;
      img.classList.remove("hidden");
      document.getElementById("preview-placeholder-text").classList.add("hidden");
    }

    // 2. Get Geometric Info (corte, peso, warnings, dimensões acabadas)
    const corte = await window.pywebview.api.calcular_largura_corte(instrucoes);
    const peso = await window.pywebview.api.calcular_peso(instrucoes, quantidadeVal);
    const dim = await window.pywebview.api.obter_dimensoes_acabadas(instrucoes);
    
    document.getElementById("badge-largura-corte").textContent = `Corte: ${corte ? Math.round(corte) : "-"} mm`;
    document.getElementById("badge-peso-peca").textContent = `Peso: ${peso ? Math.round(peso) : "-"} kg`;
    
    if (badgeDim) {
      if (dim) {
        badgeDim.textContent = `Dimensões: ${formatarDecimal(dim.x)} x ${formatarDecimal(dim.y)} mm`;
      } else {
        badgeDim.textContent = "Dimensões: -";
      }
    }
    
    // Enable pdf generation only if drawing is complete
    const incompleta = state.segmentos.some(s => s.medida === null);
    document.getElementById("btn-pdf-dobra").disabled = incompleta;

    // 3. Warnings
    const avisos = await window.pywebview.api.verificar_avisos(instrucoes);
    const warningsBox = document.getElementById("avisos-container");
    if (avisos.length > 0) {
      warningsBox.innerHTML = avisos.map(aviso => `<div>⚠️ ${aviso}</div>`).join("");
      warningsBox.classList.remove("hidden");
    } else {
      warningsBox.classList.add("hidden");
    }

  }, delay);
}

function atualizarPreview() {
  agendarPreview(true);
}

// Add Segment manually in Classic Mode
function adicionarSegmentoClassico() {
  const inputMedida = document.getElementById("seg-medida");
  const inputAngulo = document.getElementById("seg-angulo");
  const radCota = document.querySelector('input[name="cota-tipo"]:checked');
  const activeArrow = document.querySelector(".arrow-btn.active");
  
  if (!activeArrow) {
    alert("Selecione uma direção (Setas ou W, E, N, S) primeiro.");
    return;
  }
  
  const direcao = activeArrow.getAttribute("data-dir");
  const medida = converterVirgulaPonto(inputMedida.value);
  const angulo = parseFloat(inputAngulo.value) || 90;
  const tipo = radCota.value;
  
  if (isNaN(medida) || medida <= 0) {
    alert("Informe uma medida de segmento válida maior que zero.");
    inputMedida.focus();
    return;
  }

  const curvo = document.getElementById("chk-seg-curvo").checked;
  let curva_info = null;
  if (curvo) {
    const raio = parseFloat(document.getElementById("curva-raio").value);
    const comprimento_curva = parseFloat(document.getElementById("curva-comprimento").value);
    const angulo_curva = parseFloat(document.getElementById("curva-angulo").value);
    const tipo_raio = document.getElementById("curva-tipo-raio").value;
    curva_info = { raio, comprimento_curva, angulo_curva, tipo_raio };
  }
  
  if (state.segmentoEditandoIndice !== null) {
    // Save edit
    state.segmentos[state.segmentoEditandoIndice] = { direcao, angulo, medida, tipo, curvo, curva_info };
    cancelarEdicaoSegmento();
  } else {
    // Add new
    state.segmentos.push({ direcao, angulo, medida, tipo, curvo, curva_info });
  }

  // Clear inputs
  inputMedida.value = "";
  inputAngulo.value = "90";
  document.getElementById("chk-seg-curvo").checked = false;
  document.getElementById("chk-seg-curvo").dispatchEvent(new Event("change"));
  document.getElementById("curva-raio").value = "";
  document.getElementById("curva-comprimento").value = "";
  document.getElementById("curva-angulo").value = "";
  camposCurvaModificados = [];
  
  atualizarListaSegmentos();
  atualizarPreview();
}

function atualizarListaSegmentos() {
  const container = document.getElementById("segmentos-lista");
  container.innerHTML = "";
  
  if (state.segmentos.length === 0) {
    container.innerHTML = '<div class="empty-list-state">Nenhum segmento adicionado.</div>';
    return;
  }
  
  state.segmentos.forEach((seg, idx) => {
    const item = document.createElement("div");
    item.className = "segment-item";
    if (state.segmentoEditandoIndice === idx) {
      item.classList.add("editing");
    }
    
    // If in quick mode measures phase, highlight current
    if (state.modoAtual === "rapida" && state.faseRapida === "medidas" && state.indiceMedidaRapida === idx) {
      item.classList.add("editing");
    }
    
    const tipoTxt = seg.tipo === "e" ? "Ext" : "Int";
    const medidaTxt = seg.medida === null ? "?" : `${formatarDecimal(seg.medida)} mm`;
    
    item.innerHTML = `
      <div class="segment-item-left">
        <span class="segment-badge ${seg.tipo === "e" ? "ext" : "int"}">${tipoTxt}</span>
        <span><strong>${DIRECOES_UI[seg.direcao]}</strong> | ${formatarDecimal(seg.angulo)}° | ${medidaTxt}</span>
      </div>
      <button type="button" class="segment-delete" onclick="event.stopPropagation(); excluirSegmento(${idx})">&times;</button>
    `;
    
    item.addEventListener("click", () => iniciarEdicaoSegmento(idx));
    container.appendChild(item);
  });
}

function excluirSegmento(idx) {
  state.segmentos.splice(idx, 1);
  if (state.segmentoEditandoIndice === idx) {
    cancelarEdicaoSegmento();
  } else if (state.segmentoEditandoIndice !== null && state.segmentoEditandoIndice > idx) {
    state.segmentoEditandoIndice--;
  }
  
  if (state.modoAtual === "rapida") {
    if (state.segmentos.length === 0) {
      state.faseRapida = "desenho";
      document.getElementById("manual-segment-controls").classList.add("hidden");
      document.getElementById("rapida-instructions").classList.remove("hidden");
      document.getElementById("segment-editor-title").textContent = "Modo Rápido: Forma";
    } else {
      if (state.faseRapida === "medidas") {
        if (state.indiceMedidaRapida >= state.segmentos.length) {
          state.indiceMedidaRapida = state.segmentos.length - 1;
        }
        if (state.indiceMedidaRapida < 0) {
          state.faseRapida = "desenho";
          document.getElementById("manual-segment-controls").classList.add("hidden");
          document.getElementById("rapida-instructions").classList.remove("hidden");
          document.getElementById("segment-editor-title").textContent = "Modo Rápido: Forma";
        } else {
          atualizarNomeFaseRapida();
        }
      } else if (state.faseRapida === "concluido") {
        const incompleta = state.segmentos.some(s => s.medida === null);
        if (incompleta) {
          state.faseRapida = "medidas";
          state.indiceMedidaRapida = state.segmentos.findIndex(s => s.medida === null);
          if (state.indiceMedidaRapida === -1) state.indiceMedidaRapida = 0;
          document.getElementById("manual-segment-controls").classList.remove("hidden");
          document.getElementById("rapida-instructions").classList.remove("hidden");
          atualizarNomeFaseRapida();
        } else {
          state.faseRapida = "concluido";
          document.getElementById("manual-segment-controls").classList.add("hidden");
          document.getElementById("segment-editor-title").textContent = "Desenho concluído";
          document.getElementById("rapida-instructions").classList.add("hidden");
        }
      } else { // "desenho"
        document.getElementById("manual-segment-controls").classList.add("hidden");
        document.getElementById("rapida-instructions").classList.remove("hidden");
        document.getElementById("segment-editor-title").textContent = "Modo Rápido: Forma";
      }
    }
  }
  
  atualizarListaSegmentos();
  atualizarPreview();
  atualizarIndicadorModo();
}

function desfazerUltimoSegmento() {
  if (state.segmentos.length > 0) {
    excluirSegmento(state.segmentos.length - 1);
  }
}

function iniciarEdicaoSegmento(idx) {
  if (state.modoAtual === "rapida") {
    // Quick mode editing works by jumping to measure phase of this index
    state.faseRapida = "medidas";
    state.indiceMedidaRapida = idx;
    document.getElementById("segment-editor-title").textContent = `Modo Rápido: Medida ${idx + 1}/${state.segmentos.length}`;
    document.getElementById("seg-medida").focus();
    atualizarListaSegmentos();
    atualizarIndicadorModo();
    return;
  }
  
  state.segmentoEditandoIndice = idx;
  const seg = state.segmentos[idx];
  
  // Set Arrow Active
  document.querySelectorAll(".arrow-btn").forEach(btn => {
    if (btn.getAttribute("data-dir") === seg.direcao) {
      btn.classList.add("active");
    } else {
      btn.classList.remove("active");
    }
  });
  
  document.getElementById("seg-medida").value = formatarDecimal(seg.medida);
  document.getElementById("seg-angulo").value = seg.angulo;
  document.getElementById(`cota-${seg.tipo === "e" ? "ext" : "int"}`).checked = true;

  if (seg.curvo) {
    document.getElementById("chk-seg-curvo").checked = true;
    document.getElementById("chk-seg-curvo").dispatchEvent(new Event("change"));
    document.getElementById("curva-raio").value = seg.curva_info.raio;
    document.getElementById("curva-comprimento").value = seg.curva_info.comprimento_curva;
    document.getElementById("curva-angulo").value = seg.curva_info.angulo_curva;
    document.getElementById("curva-tipo-raio").value = seg.curva_info.tipo_raio;
    camposCurvaModificados = ["raio", "angulo", "comprimento"];
  } else {
    document.getElementById("chk-seg-curvo").checked = false;
    document.getElementById("chk-seg-curvo").dispatchEvent(new Event("change"));
    document.getElementById("curva-raio").value = "";
    document.getElementById("curva-comprimento").value = "";
    document.getElementById("curva-angulo").value = "";
    camposCurvaModificados = [];
  }
  
  document.getElementById("btn-add-segmento").textContent = "Salvar Alt.";
  document.getElementById("segment-editor-title").textContent = `Editando Segmento ${idx + 1}`;
  
  atualizarListaSegmentos();
  atualizarIndicadorModo();
}

function cancelarEdicaoSegmento() {
  state.segmentoEditandoIndice = null;
  document.getElementById("btn-add-segmento").textContent = "Adicionar";
  document.getElementById("segment-editor-title").textContent = "Adicionar Segmento";
  
  document.querySelectorAll(".arrow-btn").forEach(btn => btn.classList.remove("active"));
  document.getElementById("seg-medida").value = "";
  document.getElementById("seg-angulo").value = "90";
  document.getElementById("cota-ext").checked = true;

  document.getElementById("chk-seg-curvo").checked = false;
  document.getElementById("chk-seg-curvo").dispatchEvent(new Event("change"));
  document.getElementById("curva-raio").value = "";
  document.getElementById("curva-comprimento").value = "";
  document.getElementById("curva-angulo").value = "";
  camposCurvaModificados = [];
  
  atualizarListaSegmentos();
  atualizarIndicadorModo();
}

// Quick Mode (F2) flow control
function adicionarEsqueletoRapido(direcao) {
  if (state.modoAtual !== "rapida" || state.faseRapida !== "desenho") return;
  
  const radCota = document.querySelector('input[name="cota-tipo"]:checked');
  const tipo = radCota ? radCota.value : "e";
  
  let angulo = 90;
  let direcaoFinal = direcao;
  
  if (state.proximoGrauRapido !== null && state.proximoGrauRapido !== undefined) {
    angulo = state.proximoGrauRapido;
    state.proximoGrauRapido = null; // reset after applying
    
    if (state.segmentos.length > 0) {
      const azs = obterAzimutesDoDesenho();
      const azPrev = azs[azs.length - 1];
      
      let azTarget = 0;
      if (direcao === "N") azTarget = 0;
      else if (direcao === "E") azTarget = 90;
      else if (direcao === "S") azTarget = 180;
      else if (direcao === "W") azTarget = 270;
      
      const options = ["N", "E", "S", "W"].map(d => {
        const azRes = jsDefinirAzimute(d, angulo, azPrev);
        let diff = Math.abs(azRes - azTarget) % 360;
        let dist = diff < 180 ? diff : 360 - diff;
        return { direcao: d, dist: dist };
      });
      
      options.sort((a, b) => a.dist - b.dist);
      direcaoFinal = options[0].direcao;
    }
  }
  
  state.segmentos.push({
    direcao: direcaoFinal,
    angulo: angulo,
    medida: null, // Pending input
    tipo: tipo
  });
  
  atualizarListaSegmentos();
  atualizarPreview();
  atualizarIndicadorModo();
}

function confirmarEsqueletoRapido() {
  if (state.segmentos.length === 0) return;
  state.faseRapida = "medidas";
  state.indiceMedidaRapida = 0;
  
  // Enable the manual input form temporarily for inputting measures
  document.getElementById("manual-segment-controls").classList.remove("hidden");
  document.getElementById("seg-medida").focus();
  
  atualizarNomeFaseRapida();
  atualizarListaSegmentos();
  atualizarIndicadorModo();
}

function atualizarNomeFaseRapida() {
  const total = state.segmentos.length;
  const atual = state.indiceMedidaRapida + 1;
  document.getElementById("segment-editor-title").textContent = `Modo Rápido: Medida ${atual}/${total}`;
  
  // Auto set arrow indicator corresponding to current editing segment direction
  const seg = state.segmentos[state.indiceMedidaRapida];
  document.querySelectorAll(".arrow-btn").forEach(btn => {
    if (btn.getAttribute("data-dir") === seg.direcao) {
      btn.classList.add("active");
    } else {
      btn.classList.remove("active");
    }
  });
}

function confirmarMedidaRapida() {
  const inputMedida = document.getElementById("seg-medida");
  const medida = converterVirgulaPonto(inputMedida.value);
  
  if (isNaN(medida) || medida <= 0) {
    alert("Informe uma medida de segmento válida maior que zero.");
    inputMedida.focus();
    return;
  }
  
  // Set measure on current index
  state.segmentos[state.indiceMedidaRapida].medida = medida;
  inputMedida.value = "";
  
  state.indiceMedidaRapida++;
  if (state.indiceMedidaRapida >= state.segmentos.length) {
    // All measures collected
    state.faseRapida = "concluido";
    // Hide controls back
    document.getElementById("manual-segment-controls").classList.add("hidden");
    document.getElementById("segment-editor-title").textContent = "Desenho concluído";
    document.getElementById("rapida-instructions").classList.add("hidden");
    atualizarListaSegmentos();
    atualizarPreview();
  } else {
    atualizarNomeFaseRapida();
    atualizarListaSegmentos();
    atualizarPreview();
  }
  atualizarIndicadorModo();
}

// Session Parts List (Ordem de Produção)
function novaPeca() {
  state.pecaNome = "";
  state.segmentos = [];
  state.segmentoEditandoIndice = null;
  
  document.getElementById("peca-nome").value = "";
  const inputObs = document.getElementById("peca-observacao");
  if (inputObs) inputObs.value = "";
  
  const chkMultiplos = document.getElementById("chk-multiplos-comprimentos");
  if (chkMultiplos) {
    chkMultiplos.checked = false;
    document.getElementById("peca-comprimentos-multiplos").value = "";
    document.getElementById("group-comprimentos-multiplos").classList.add("hidden");
    document.getElementById("group-comprimento-single").classList.remove("hidden");
    document.getElementById("group-quantidade-single").classList.remove("hidden");
  }
  
  cancelarEdicaoSegmento();
  aplicarModo(state.modoAtual);
  atualizarListaSegmentos();
  atualizarPreview();
}

function guardarPecaAtual() {
  if (state.segmentos.length === 0) {
    alert("Não é possível adicionar uma peça sem segmentos.");
    return;
  }
  
  if (state.segmentos.some(s => s.medida === null)) {
    alert("A peça está incompleta. Informe as medidas de todos os segmentos.");
    return;
  }

  const inputNome = document.getElementById("peca-nome");
  const inputObs = document.getElementById("peca-observacao");
  state.pecaNome = inputNome.value.trim();
  const pecaObsVal = inputObs ? inputObs.value.trim() : "";
  const segmentsData = state.segmentos.map(s => [
    s.direcao,
    s.angulo,
    s.medida,
    s.tipo,
    s.curvo || false,
    s.curva_info || null
  ]);
  
  const chkMultiplos = document.getElementById("chk-multiplos-comprimentos");
  let parsedItems = [];
  
  if (chkMultiplos && chkMultiplos.checked) {
    const txtMultiplos = document.getElementById("peca-comprimentos-multiplos").value.trim();
    if (!txtMultiplos) {
      alert("Informe pelo menos um comprimento e quantidade.");
      document.getElementById("peca-comprimentos-multiplos").focus();
      return;
    }
    try {
      parsedItems = parseMultiplesComprimentos(txtMultiplos);
      if (parsedItems.length === 0) {
        alert("Informe pelo menos um comprimento e quantidade válido.");
        document.getElementById("peca-comprimentos-multiplos").focus();
        return;
      }
    } catch (err) {
      alert(err.message || "Erro ao processar a lista de comprimentos.");
      document.getElementById("peca-comprimentos-multiplos").focus();
      return;
    }
  } else {
    const comprimentoVal = converterVirgulaPonto(document.getElementById("peca-comprimento").value);
    const quantidadeVal = parseInt(document.getElementById("peca-quantidade").value) || 1;
    
    if (isNaN(comprimentoVal) || comprimentoVal <= 0) {
      alert("Informe um comprimento de peça válido maior que zero.");
      document.getElementById("peca-comprimento").focus();
      return;
    }
    parsedItems = [{ comprimento: comprimentoVal, quantidade: quantidadeVal }];
  }

  const baseNome = state.pecaNome || `Peça ${state.ordemPecas.length + 1}`;
  
  parsedItems.forEach(item => {
    const nameSuffix = parsedItems.length > 1 ? ` (${Math.round(item.comprimento)})` : "";
    const nome_peca = baseNome + nameSuffix;
    
    const novaPecaDados = {
      nome_peca: nome_peca,
      codigo_chapa: state.chapaCodigo,
      comprimento: item.comprimento,
      quantidade: item.quantidade,
      segmentos: segmentsData,
      observacao: pecaObsVal
    };
    state.ordemPecas.push(novaPecaDados);
  });

  salvarNoLocalStorage("session-pecas", JSON.stringify(state.ordemPecas));
  
  atualizarTabelaOrdem();
  novaPeca();
}

function atualizarTabelaOrdem() {
  const tbody = document.getElementById("orders-list-body");
  const countSpan = document.getElementById("orders-count");
  const totalWeightSpan = document.getElementById("total-peso-ordem");
  
  tbody.innerHTML = "";
  countSpan.textContent = state.ordemPecas.length;
  
  if (state.ordemPecas.length === 0) {
    tbody.innerHTML = '<tr><td colspan="6" class="table-empty-state">Nenhuma peça na ordem de produção.</td></tr>';
    totalWeightSpan.textContent = "Total: 0 kg";
    document.getElementById("btn-gerar-folha-pedido").disabled = true;
    return;
  }
  
  document.getElementById("btn-gerar-folha-pedido").disabled = false;
  let totalPeso = 0;
  
  state.ordemPecas.forEach((peca, idx) => {
    const tr = document.createElement("tr");
    
    // Approximate weight calculation for table
    // We fetch chapas metadata to calculate locally
    const chapaInfo = state.chapas.find(c => c.codigo === peca.codigo_chapa);
    const coef = chapaInfo ? chapaInfo.coeficiente : 12;
    // Local calculation of cut width (approx sum of segments since they are final)
    const corte = peca.segmentos.reduce((acc, curr) => acc + curr[2], 0);
    const peso = Math.ceil(corte * peca.comprimento * coef / 1000000 * peca.quantidade);
    totalPeso += peso;
    
    tr.innerHTML = `
      <td><strong>${peca.nome_peca}</strong></td>
      <td>${peca.codigo_chapa.replace("#", "")}</td>
      <td class="font-mono">${Math.round(corte)} mm</td>
      <td class="font-mono">${Math.round(peca.comprimento)} mm</td>
      <td class="font-mono">x${peca.quantidade}</td>
      <td class="orders-action-cell">
        <button class="orders-action-btn" title="Editar peça" onclick="editarPecaOrdem(${idx})">✏️</button>
        <button class="orders-action-btn" title="Duplicar" onclick="duplicarPecaOrdem(${idx})">📋</button>
        <button class="orders-action-btn" title="Excluir" onclick="excluirPecaOrdem(${idx})">🗑️</button>
      </td>
    `;
    tbody.appendChild(tr);
  });
  
  totalWeightSpan.textContent = `Total: ${totalPeso} kg`;
}

function excluirPecaOrdem(idx) {
  state.ordemPecas.splice(idx, 1);
  salvarNoLocalStorage("session-pecas", JSON.stringify(state.ordemPecas));
  atualizarTabelaOrdem();
}

function duplicarPecaOrdem(idx) {
  const clone = JSON.parse(JSON.stringify(state.ordemPecas[idx]));
  clone.nome_peca += " (Cópia)";
  state.ordemPecas.push(clone);
  salvarNoLocalStorage("session-pecas", JSON.stringify(state.ordemPecas));
  atualizarTabelaOrdem();
}

function editarPecaOrdem(idx) {
  const peca = state.ordemPecas[idx];
  
  const chkMultiplos = document.getElementById("chk-multiplos-comprimentos");
  if (chkMultiplos) {
    chkMultiplos.checked = false;
    document.getElementById("group-comprimentos-multiplos").classList.add("hidden");
    document.getElementById("group-comprimento-single").classList.remove("hidden");
    document.getElementById("group-quantidade-single").classList.remove("hidden");
  }

  document.getElementById("peca-nome").value = peca.nome_peca;
  document.getElementById("peca-chapa").value = peca.codigo_chapa;
  document.getElementById("peca-comprimento").value = peca.comprimento;
  document.getElementById("peca-quantidade").value = peca.quantidade;
  const inputObs = document.getElementById("peca-observacao");
  if (inputObs) inputObs.value = peca.observacao || "";
  
  state.chapaCodigo = peca.codigo_chapa;
  state.comprimento = peca.comprimento;
  state.quantidade = peca.quantidade;
  
  // Reconstruct segments list
  state.segmentos = peca.segmentos.map(s => ({
    direcao: s[0],
    angulo: s[1],
    medida: s[2],
    tipo: s[3],
    curvo: s[4] || false,
    curva_info: s[5] || null
  }));
  
  aplicarModo("classica");
  
  // Remove it from the order session so that saving it "replaces" it
  state.ordemPecas.splice(idx, 1);
  salvarNoLocalStorage("session-pecas", JSON.stringify(state.ordemPecas));
  atualizarTabelaOrdem();
  
  atualizarListaSegmentos();
  atualizarPreview();
}

// Generate PDF reports
async function gerarPdfDobra() {
  if (state.segmentos.length === 0) return;
  const inputNome = document.getElementById("peca-nome");
  const nomePeca = inputNome.value.trim() || "Peça Desenho";
  
  const instrucoes = {
    chapa: state.chapaCodigo,
    comprimento: state.comprimento || 3000,
    segmentos: state.segmentos.map(s => [s.direcao, s.angulo, s.medida, s.tipo])
  };
  
  const res = await window.pywebview.api.gerar_relatorio_dobra(instrucoes, nomePeca);
  if (res && res.sucesso) {
    alert(`Relatório gerado com sucesso!\nSalvo em: ${res.nome}`);
    window.pywebview.api.abrir_pasta_saida();
  } else {
    alert(`Erro ao gerar relatório: ${res.erro}`);
  }
}

async function gerarPdfPedido() {
  if (state.ordemPecas.length === 0) return;
  const obs = document.getElementById("relatorio-observacao").value.trim();
  
  const res = await window.pywebview.api.gerar_relatorio_pedido(state.ordemPecas, obs);
  if (res && res.sucesso) {
    alert(`Ordem de Produção gerada com sucesso!\nSalvo em: ${res.nome}`);
    window.pywebview.api.abrir_pasta_saida();
    
    // Clear session after successful print
    if (confirm("Deseja limpar as peças da lista atual de produção?")) {
      state.ordemPecas = [];
      salvarNoLocalStorage("session-pecas", JSON.stringify([]));
      atualizarTabelaOrdem();
    }
  } else {
    alert(`Erro ao gerar PDF: ${res.erro}`);
  }
}

// Modals display handling
function abrirModal(modalId) {
  document.getElementById(modalId).classList.remove("hidden");
}

function fecharModais() {
  document.querySelectorAll(".modal-overlay").forEach(overlay => {
    // Only close if it's not the critical folder migration confirmation modal
    if (overlay.id !== "modal-confirmacao-migracao") {
      overlay.classList.add("hidden");
    }
  });
}

// Biblioteca templates operations
async function abrirBiblioteca() {
  abrirModal("modal-biblioteca");
  document.getElementById("search-biblioteca").value = "";
  carregarListaBiblioteca("");
  // Enable/disable the "add current piece" button based on whether segments exist
  const btnAdicionar = document.getElementById("btn-adicionar-peca-atual-biblioteca");
  if (btnAdicionar) {
    btnAdicionar.disabled = state.segmentos.length === 0;
  }
  document.getElementById("search-biblioteca").focus();
}

async function carregarListaBiblioteca(filtro) {
  const container = document.getElementById("library-list");
  container.innerHTML = '<div class="empty-list-state">Pesquisando biblioteca...</div>';
  
  const modelos = await window.pywebview.api.listar_modelos(filtro);
  container.innerHTML = "";
  
  if (modelos.length === 0) {
    container.innerHTML = '<div class="empty-list-state">Nenhum modelo encontrado na biblioteca.</div>';
    return;
  }
  
  modelos.forEach(modelo => {
    const row = document.createElement("div");
    row.className = "library-row";
    
    const chapaTxt = modelo.chapa.replace("#", "");
    const compTxt = modelo.comprimento ? `${Math.round(modelo.comprimento)} mm` : "variável";
    const specs = `#${chapaTxt}, ${modelo.segmentos.length} seg., ${compTxt}`;
    
    row.innerHTML = `
      <div class="lib-col-name">${modelo.nome}</div>
      <div class="lib-col-spec">${specs}</div>
      <div class="lib-col-actions">
        <button class="action-btn small" onclick="abrirModeloBiblioteca('${modelo.id}')" title="Carregar modelo">Abrir</button>
        <button class="danger-btn small" onclick="excluirModeloBiblioteca('${modelo.id}', '${modelo.nome}')" title="Excluir da biblioteca">Excluir</button>
      </div>
    `;
    container.appendChild(row);
  });
}

async function abrirModeloBiblioteca(modeloId) {
  const modelos = await window.pywebview.api.listar_modelos("");
  const modelo = modelos.find(m => m.id === modeloId);
  if (!modelo) return;
  
  document.getElementById("peca-nome").value = modelo.nome;
  document.getElementById("peca-chapa").value = modelo.chapa;
  const inputObs = document.getElementById("peca-observacao");
  if (inputObs) inputObs.value = modelo.descricao || "";
  
  state.chapaCodigo = modelo.chapa;
  if (modelo.comprimento) {
    document.getElementById("peca-comprimento").value = modelo.comprimento;
    state.comprimento = modelo.comprimento;
  }
  
  state.segmentos = modelo.segmentos.map(s => ({
    direcao: s[0],
    angulo: s[1],
    medida: s[2],
    tipo: s[3],
    curvo: s[4] || false,
    curva_info: s[5] || null
  }));
  
  aplicarModo("classica");
  fecharModais();
  atualizarListaSegmentos();
  atualizarPreview();
}

async function excluirModeloBiblioteca(modeloId, nome) {
  if (confirm(`Tem certeza de que deseja excluir o modelo "${nome}" da biblioteca?\nEsta ação não poderá ser desfeita.`)) {
    const ok = await window.pywebview.api.excluir_modelo(modeloId);
    if (ok) {
      carregarListaBiblioteca(document.getElementById("search-biblioteca").value);
    } else {
      alert("Erro ao excluir modelo.");
    }
  }
}

async function abrirSalvarBiblioteca() {
  if (state.segmentos.length === 0) {
    alert("Desenhe a peça antes de adicioná-la à biblioteca.");
    return;
  }
  const inputNome = document.getElementById("peca-nome");
  const inputObs = document.getElementById("peca-observacao");
  document.getElementById("lib-salvar-nome").value = inputNome.value.trim();
  document.getElementById("lib-salvar-descricao").value = inputObs ? inputObs.value.trim() : "";
  abrirModal("modal-salvar-biblioteca");
}

async function salvarNaBiblioteca() {
  const nome = document.getElementById("lib-salvar-nome").value.trim();
  const desc = document.getElementById("lib-salvar-descricao").value.trim();
  const atualizar = document.getElementById("lib-salvar-atualizar").checked;
  
  if (!nome) {
    alert("Informe o nome do modelo.");
    return;
  }

  // Find id if updating existing model with same name
  let modeloId = null;
  if (atualizar) {
    const todos = await window.pywebview.api.listar_modelos("");
    const existente = todos.find(m => m.nome.toLowerCase() === nome.toLowerCase());
    if (existente) {
      modeloId = existente.id;
    }
  }

  const segmentosData = state.segmentos.map(s => [
    s.direcao,
    s.angulo,
    s.medida,
    s.tipo,
    s.curvo || false,
    s.curva_info || null
  ]);

  const res = await window.pywebview.api.salvar_modelo(
    nome,
    state.chapaCodigo,
    state.comprimento,
    segmentosData,
    modeloId,
    desc
  );

  if (res && !res.erro) {
    alert(`Modelo "${nome}" salvo com sucesso na biblioteca.`);
    document.getElementById("peca-nome").value = nome;
    fecharModais();
  } else {
    alert(`Erro ao salvar na biblioteca: ${res ? res.erro : "Erro desconhecido"}`);
  }
}

// Generator selector and Boiadeira profile generator operations
function abrirModalGeradores() {
  abrirModal("modal-geradores");
}

function abrirGeradorBoiadeira() {
  fecharModais();
  abrirModal("modal-gerador-boiadeira");
  
  // Pre-fill inputs with default settings
  document.getElementById("boiadeira-altura").value = state.configuracoes.boiadeira_altura_aba_padrao || 20.0;
  document.getElementById("boiadeira-largura").value = state.configuracoes.boiadeira_largura_total_padrao || 230.0;
  document.getElementById("boiadeira-primeiro").value = state.configuracoes.boiadeira_primeiro_gomo_padrao || 30.0;
  document.getElementById("boiadeira-superior").value = state.configuracoes.boiadeira_gomo_superior_padrao || 30.0;
  document.getElementById("boiadeira-inferior").value = state.configuracoes.boiadeira_gomo_inferior_padrao || 30.0;
  document.getElementById("boiadeira-num").value = state.configuracoes.boiadeira_num_gomos_padrao || 2;
  document.getElementById("boiadeira-comprimento").value = state.configuracoes.boiadeira_comprimento_padrao || 3000.0;
  
  if (state.chapas.length > 0) {
    document.getElementById("boiadeira-chapa").value = state.chapaCodigo || state.chapas[0].codigo;
  }
}

async function abrirConfiguracoesBoiadeira() {
  fecharModais();
  abrirModal("modal-config-boiadeira");
  
  state.configuracoes = await window.pywebview.api.obter_configuracoes();
  
  document.getElementById("cfg-boiadeira-altura").value = state.configuracoes.boiadeira_altura_aba_padrao !== undefined ? state.configuracoes.boiadeira_altura_aba_padrao : 20.0;
  document.getElementById("cfg-boiadeira-largura").value = state.configuracoes.boiadeira_largura_total_padrao !== undefined ? state.configuracoes.boiadeira_largura_total_padrao : 230.0;
  document.getElementById("cfg-boiadeira-primeiro").value = state.configuracoes.boiadeira_primeiro_gomo_padrao !== undefined ? state.configuracoes.boiadeira_primeiro_gomo_padrao : 30.0;
  document.getElementById("cfg-boiadeira-superior").value = state.configuracoes.boiadeira_gomo_superior_padrao !== undefined ? state.configuracoes.boiadeira_gomo_superior_padrao : 30.0;
  document.getElementById("cfg-boiadeira-inferior").value = state.configuracoes.boiadeira_gomo_inferior_padrao !== undefined ? state.configuracoes.boiadeira_gomo_inferior_padrao : 30.0;
  document.getElementById("cfg-boiadeira-num").value = state.configuracoes.boiadeira_num_gomos_padrao !== undefined ? state.configuracoes.boiadeira_num_gomos_padrao : 2;
  document.getElementById("cfg-boiadeira-comprimento").value = state.configuracoes.boiadeira_comprimento_padrao !== undefined ? state.configuracoes.boiadeira_comprimento_padrao : 3000.0;
  document.getElementById("cfg-boiadeira-tol-largura").value = state.configuracoes.boiadeira_tolerancia_largura !== undefined ? state.configuracoes.boiadeira_tolerancia_largura : 0.5;
  document.getElementById("cfg-boiadeira-tol-altura").value = state.configuracoes.boiadeira_tolerancia_altura !== undefined ? state.configuracoes.boiadeira_tolerancia_altura : 0.5;
  document.getElementById("cfg-boiadeira-tol-topo").value = state.configuracoes.boiadeira_tolerancia_topo !== undefined ? state.configuracoes.boiadeira_tolerancia_topo : 0.5;
}

async function salvarConfiguracoesBoiadeira() {
  const configs = {
    boiadeira_altura_aba_padrao: parseFloat(document.getElementById("cfg-boiadeira-altura").value) || 20.0,
    boiadeira_largura_total_padrao: parseFloat(document.getElementById("cfg-boiadeira-largura").value) || 230.0,
    boiadeira_primeiro_gomo_padrao: parseFloat(document.getElementById("cfg-boiadeira-primeiro").value) || 30.0,
    boiadeira_gomo_superior_padrao: parseFloat(document.getElementById("cfg-boiadeira-superior").value) || 30.0,
    boiadeira_gomo_inferior_padrao: parseFloat(document.getElementById("cfg-boiadeira-inferior").value) || 30.0,
    boiadeira_num_gomos_padrao: parseInt(document.getElementById("cfg-boiadeira-num").value) || 2,
    boiadeira_comprimento_padrao: parseFloat(document.getElementById("cfg-boiadeira-comprimento").value) || 3000.0,
    boiadeira_tolerancia_largura: parseFloat(document.getElementById("cfg-boiadeira-tol-largura").value) || 0.5,
    boiadeira_tolerancia_altura: parseFloat(document.getElementById("cfg-boiadeira-tol-altura").value) || 0.5,
    boiadeira_tolerancia_topo: parseFloat(document.getElementById("cfg-boiadeira-tol-topo").value) || 0.5
  };
  
  const res = await window.pywebview.api.salvar_configuracoes(configs);
  if (res && res.sucesso) {
    state.configuracoes = await window.pywebview.api.obter_configuracoes();
    fecharModais();
    alert("Configurações do gerador Boiadeira salvas com sucesso!");
  } else {
    alert(`Erro ao salvar configurações da Boiadeira: ${res ? res.erro : "Erro interno"}`);
  }
}

async function gerarBoiadeira() {
  const chapa = document.getElementById("boiadeira-chapa").value;
  const comprimento = converterVirgulaPonto(document.getElementById("boiadeira-comprimento").value);
  const largura = converterVirgulaPonto(document.getElementById("boiadeira-largura").value);
  const altura = converterVirgulaPonto(document.getElementById("boiadeira-altura").value);
  const primeiro = converterVirgulaPonto(document.getElementById("boiadeira-primeiro").value);
  const superior = converterVirgulaPonto(document.getElementById("boiadeira-superior").value);
  const inferior = converterVirgulaPonto(document.getElementById("boiadeira-inferior").value);
  const num = parseInt(document.getElementById("boiadeira-num").value) || 2;

  if ([comprimento, largura, altura, primeiro, superior, inferior, num].some(val => isNaN(val) || val <= 0)) {
    alert("Todos os campos do gerador devem ser preenchidos com valores numéricos maiores que zero.");
    return;
  }

  document.getElementById("boiadeira-calculando").classList.remove("hidden");
  document.getElementById("btn-confirmar-boiadeira").disabled = true;

  const res = await window.pywebview.api.gerar_boiadeira({
    chapa, comprimento, largura_total: largura, altura_aba: altura,
    primeiro_gomo: primeiro, tamanho_gomo_superior: superior,
    tamanho_gomo_inferior: inferior, num_gomos: num
  });

  document.getElementById("boiadeira-calculando").classList.add("hidden");
  document.getElementById("btn-confirmar-boiadeira").disabled = false;

  if (res && res.sucesso) {
    const peca = res.peca;
    document.getElementById("peca-nome").value = peca.nome;
    document.getElementById("peca-chapa").value = peca.chapa;
    document.getElementById("peca-comprimento").value = peca.comprimento;
    document.getElementById("peca-quantidade").value = 1;

    state.chapaCodigo = peca.chapa;
    state.comprimento = peca.comprimento;
    state.quantidade = 1;

    state.segmentos = peca.segmentos.map(s => ({
      direcao: s[0],
      angulo: s[1],
      medida: s[2],
      tipo: s[3],
      curvo: s[4] || false,
      curva_info: s[5] || null
    }));

    aplicarModo("classica");
    fecharModais();
    atualizarListaSegmentos();
    atualizarPreview();
  } else {
    alert(`Erro ao gerar perfil boiadeira:\n${res ? res.erro : "Erro desconhecido"}`);
  }
}

// Config Panel operations
let configSalvarFila = null; // Stored config migration state

async function abrirConfiguracoes() {
  abrirModal("modal-config");
  const container = document.getElementById("config-form");
  container.innerHTML = "Carregando configurações...";
  
  state.configuracoes = await window.pywebview.api.obter_configuracoes();
  
  // Field labels and keys
  const configFields = [
    { label: "Geral", isHeader: true },
    { key: "relatorio_nome_responsavel", label: "Responsável pelo Relatório", type: "text" },
    { key: "comprimento_preview_placeholder", label: "Comp. Padrão da Prévia (mm)", type: "number" },
    { key: "medida_placeholder", label: "Medida Prov. do Segmento (mm)", type: "number" },
    { key: "pasta_saida_relatorios", label: "Pasta de Saída dos PDFs (Caminho Relativo)", type: "text" },

    { label: "Fontes: Desenho 3D e Prévia", isHeader: true },
    { key: "desenho_fonte_base_fator", label: "Fator de Escala da Fonte no Preview", type: "number" },
    { key: "desenho_fonte_base_minima", label: "Tamanho de Fonte Mínimo no Preview", type: "number" },
    { key: "desenho_fonte_relatorio_fator", label: "Fator de Escala das Medidas no PDF (Pedido)", type: "number" },
    { key: "desenho_fonte_detalhamento_dobra_fator", label: "Fator de Escala das Medidas no PDF (Dobra)", type: "number" },
    { key: "desenho_fonte_relatorio_minima", label: "Tamanho Mínimo das Medidas no PDF (Pedido)", type: "number" },
    { key: "desenho_fonte_dobra_minima", label: "Tamanho Mínimo das Medidas no PDF (Dobra)", type: "number" },

    { label: "Fontes: PDF Detalhe de Dobra (Textos)", isHeader: true },
    { key: "relatorio_dobra_fonte_titulo", label: "Fonte do Título Principal", type: "number" },
    { key: "relatorio_dobra_fonte_secao", label: "Fonte de Seções e Legendas", type: "number" },
    { key: "relatorio_dobra_fonte_texto", label: "Fonte de Informações e Metadados", type: "number" },
    { key: "relatorio_dobra_fonte_cota", label: "Fonte das Cotas da Planificação", type: "number" },
    { key: "relatorio_dobra_fonte_angulo", label: "Fonte dos Ângulos de Dobra", type: "number" },
    { key: "relatorio_dobra_fonte_sentido", label: "Fonte do Sentido de Dobra (Cima/Baixo)", type: "number" },

    { label: "Fontes: PDF Ordem de Produção (Textos)", isHeader: true },
    { key: "relatorio_pedido_fonte_titulo", label: "Fonte do Título Principal (Responsável)", type: "number" },
    { key: "relatorio_pedido_fonte_subtitulo", label: "Fonte de 'Ordem de Produção'", type: "number" },
    { key: "relatorio_pedido_fonte_texto", label: "Fonte de Textos Gerais (Obs/Prazo)", type: "number" },
    { key: "relatorio_pedido_fonte_destaque", label: "Fonte de Destaques (Quant/Chapa/Corte)", type: "number" },
    { key: "relatorio_pedido_fonte_rotulo_peca", label: "Fonte do Nome da Peça", type: "number" },
    { key: "relatorio_pedido_fonte_rotulo_campo", label: "Fonte de Rótulos de Tabela (Corte/Comp/Kg)", type: "number" }
  ];

  container.innerHTML = "";
  configFields.forEach(field => {
    if (field.isHeader) {
      const header = document.createElement("h4");
      header.className = "config-section-title span-2";
      header.style.marginTop = "18px";
      header.style.marginBottom = "8px";
      header.style.borderBottom = "1px solid var(--border-color, #444)";
      header.style.paddingBottom = "5px";
      header.style.color = "var(--primary-color, #3b82f6)";
      header.style.fontSize = "1.05rem";
      header.textContent = field.label;
      container.appendChild(header);
      return;
    }
    const group = document.createElement("div");
    group.className = "form-group";
    const val = state.configuracoes[field.key] !== undefined ? state.configuracoes[field.key] : "";
    group.innerHTML = `
      <label for="cfg-${field.key}">${field.label}</label>
      <input type="${field.type}" id="cfg-${field.key}" value="${val}" data-key="${field.key}">
    `;
    container.appendChild(group);
  });
}

async function salvarConfiguracoes() {
  const inputs = document.querySelectorAll("#config-form input");
  const novasConfig = {};
  
  inputs.forEach(input => {
    const key = input.getAttribute("data-key");
    const type = input.getAttribute("type");
    let val = input.value.trim();
    if (type === "number") {
      val = parseFloat(val);
    }
    novasConfig[key] = val;
  });

  const pastaAntiga = state.configuracoes.pasta_saida_relatorios;
  const pastaNova = novasConfig.pasta_saida_relatorios;

  configSalvarFila = novasConfig;

  // Folder migration logic check
  if (pastaAntiga && pastaNova && pastaAntiga !== pastaNova) {
    const status = await window.pywebview.api.executar_migracao_pasta(pastaAntiga, pastaNova);
    if (status === "confirmacao_necessaria") {
      // Open migration dialog overlay
      abrirModal("modal-confirmacao-migracao");
      return;
    } else if (status === "copiado") {
      alert("Pasta de relatórios alterada. Arquivos históricos copiados com sucesso!");
    }
  }

  confirmarSalvarConfigFinal(novasConfig);
}

async function confirmarSalvarConfigFinal(config) {
  const res = await window.pywebview.api.salvar_configuracoes(config);
  if (res && res.sucesso) {
    state.configuracoes = await window.pywebview.api.obter_configuracoes();
    fecharModais();
    alert("Configurações salvas com sucesso!");
    
    // Apply changes locally
    state.comprimento = state.configuracoes.comprimento_preview_placeholder || 3000;
    document.getElementById("peca-comprimento").value = state.comprimento;
    atualizarPreview();
  } else {
    alert(`Erro ao salvar configurações: ${res ? res.erro : "Erro interno"}`);
  }
}

async function processarConfirmacaoMigracao(forcarCopia) {
  const pastaAntiga = state.configuracoes.pasta_saida_relatorios;
  const pastaNova = configSalvarFila.pasta_saida_relatorios;
  
  await window.pywebview.api.executar_migracao_pasta(pastaAntiga, pastaNova, forcarCopia);
  document.getElementById("modal-confirmacao-migracao").classList.add("hidden");
  
  if (forcarCopia) {
    alert("Arquivos copiados e mesclados com sucesso!");
  }
  
  confirmarSalvarConfigFinal(configSalvarFila);
}

async function restaurarConfiguracoesPadrao() {
  if (confirm("Isso redefinirá todas as configurações para os valores de fábrica originais. Deseja continuar?")) {
    await window.pywebview.api.salvar_configuracoes(state.configuracoes = {});
    abrirConfiguracoes();
    alert("Padrões restaurados.");
  }
}

// Event Bindings
function configurarEventosGerais() {
  // Input fields changes
  document.getElementById("peca-chapa").addEventListener("change", e => {
    state.chapaCodigo = e.target.value;
    atualizarPreview();
  });

  // Mode selection badge clicks
  document.getElementById("badge-modo-classico").addEventListener("click", () => aplicarModo("classica"));
  document.getElementById("badge-modo-rapido").addEventListener("click", () => aplicarModo("rapida"));

  const triggerPreviewChange = () => {
    state.comprimento = converterVirgulaPonto(document.getElementById("peca-comprimento").value) || 3000;
    state.quantidade = parseInt(document.getElementById("peca-quantidade").value) || 1;
    
    // Stagger changes (debounce) when typing numbers
    if (state.previewTimer) clearTimeout(state.previewTimer);
    state.previewTimer = setTimeout(() => agendarPreview(true), 350);
  };

  document.getElementById("peca-comprimento").addEventListener("input", triggerPreviewChange);
  document.getElementById("peca-quantidade").addEventListener("input", triggerPreviewChange);

  document.getElementById("chk-multiplos-comprimentos").addEventListener("change", e => {
    const isMulti = e.target.checked;
    const groupMulti = document.getElementById("group-comprimentos-multiplos");
    const groupComp = document.getElementById("group-comprimento-single");
    const groupQtd = document.getElementById("group-quantidade-single");
    
    if (isMulti) {
      groupMulti.classList.remove("hidden");
      groupComp.classList.add("hidden");
      groupQtd.classList.add("hidden");
    } else {
      groupMulti.classList.add("hidden");
      groupComp.classList.remove("hidden");
      groupQtd.classList.remove("hidden");
    }
    agendarPreview(true);
  });

  document.getElementById("peca-comprimentos-multiplos").addEventListener("input", () => {
    if (state.previewTimer) clearTimeout(state.previewTimer);
    state.previewTimer = setTimeout(() => agendarPreview(true), 350);
  });

  // Manual segment addition bindings
  document.getElementById("btn-add-segmento").addEventListener("click", adicionarSegmentoClassico);
  document.getElementById("btn-desfazer-segmento").addEventListener("click", desfazerUltimoSegmento);
  document.getElementById("btn-nova-peca").addEventListener("click", () => {
    if (confirm("Limpar desenho atual e iniciar nova peça?")) {
      novaPeca();
    }
  });
  
  document.getElementById("btn-guardar-peca").addEventListener("click", guardarPecaAtual);
  document.getElementById("btn-pdf-dobra").addEventListener("click", gerarPdfDobra);
  document.getElementById("btn-gerar-folha-pedido").addEventListener("click", gerarPdfPedido);
  
  // Directions arrow keys click
  document.querySelectorAll(".arrow-btn").forEach(btn => {
    btn.addEventListener("click", e => {
      document.querySelectorAll(".arrow-btn").forEach(b => b.classList.remove("active"));
      e.target.classList.add("active");
      
      // If in quick mode, clicking immediately adds skeleton segment
      if (state.modoAtual === "rapida" && state.faseRapida === "desenho") {
        adicionarEsqueletoRapido(e.target.getAttribute("data-dir"));
      }
    });
  });

  // Modal Buttons
  document.getElementById("btn-biblioteca").addEventListener("click", abrirBiblioteca);
  document.getElementById("btn-geradores").addEventListener("click", abrirModalGeradores);
  document.getElementById("btn-config").addEventListener("click", abrirConfiguracoes);
  document.getElementById("btn-pasta-saida").addEventListener("click", () => window.pywebview.api.abrir_pasta_saida());
  
  // Selector dialog inside modal bindings
  document.getElementById("btn-abrir-boiadeira").addEventListener("click", abrirGeradorBoiadeira);
  document.getElementById("btn-config-boiadeira-gear").addEventListener("click", abrirConfiguracoesBoiadeira);
  document.getElementById("btn-salvar-config-boiadeira").addEventListener("click", salvarConfiguracoesBoiadeira);
  
  document.getElementById("btn-confirmar-salvar-biblioteca").addEventListener("click", salvarNaBiblioteca);
  document.getElementById("btn-adicionar-peca-atual-biblioteca").addEventListener("click", () => {
    fecharModais();
    abrirSalvarBiblioteca();
  });
  document.getElementById("btn-confirmar-boiadeira").addEventListener("click", gerarBoiadeira);
  document.getElementById("btn-confirmar-config").addEventListener("click", salvarConfiguracoes);
  document.getElementById("btn-restaurar-config").addEventListener("click", restaurarConfiguracoesPadrao);
  
  document.getElementById("btn-migrar-copiar").addEventListener("click", () => processarConfirmacaoMigracao(true));
  document.getElementById("btn-migrar-manter").addEventListener("click", () => processarConfirmacaoMigracao(false));

  // Curved segment event listeners
  const chkSegCurvo = document.getElementById("chk-seg-curvo");
  if (chkSegCurvo) {
    chkSegCurvo.addEventListener("change", e => {
      const panel = document.getElementById("panel-calandragem");
      const inputMedida = document.getElementById("seg-medida");
      const inputAngulo = document.getElementById("seg-angulo");
      if (e.target.checked) {
        panel.classList.remove("hidden");
        inputMedida.readOnly = true;
        inputAngulo.readOnly = true;
        inputMedida.style.opacity = 0.6;
        inputAngulo.style.opacity = 0.6;
      } else {
        panel.classList.add("hidden");
        inputMedida.readOnly = false;
        inputAngulo.readOnly = false;
        inputMedida.style.opacity = 1;
        inputAngulo.style.opacity = 1;
      }
    });
  }

  const inputRaio = document.getElementById("curva-raio");
  const inputCompCurva = document.getElementById("curva-comprimento");
  const inputAngCurva = document.getElementById("curva-angulo");
  const selectTipoCurva = document.getElementById("curva-tipo-raio");

  if (inputRaio) inputRaio.addEventListener("input", () => registrarModificacaoCampoCurva("raio"));
  if (inputCompCurva) inputCompCurva.addEventListener("input", () => registrarModificacaoCampoCurva("comprimento"));
  if (inputAngCurva) inputAngCurva.addEventListener("input", () => registrarModificacaoCampoCurva("angulo"));
  if (selectTipoCurva) selectTipoCurva.addEventListener("change", calcularCamposCalandragem);
  document.getElementById("peca-chapa").addEventListener("change", calcularCamposCalandragem);

  // Tubo Calandrado buttons
  const btnAbrirTubo = document.getElementById("btn-abrir-tubo");
  if (btnAbrirTubo) btnAbrirTubo.addEventListener("click", abrirGeradorTubo);
  const btnConfirmarTubo = document.getElementById("btn-confirmar-tubo");
  if (btnConfirmarTubo) btnConfirmarTubo.addEventListener("click", gerarTubo);

  // Biblioteca Search Filter
  document.getElementById("search-biblioteca").addEventListener("input", e => {
    carregarListaBiblioteca(e.target.value);
  });

  // Theme Toggler
  document.getElementById("btn-toggle-theme").addEventListener("click", () => {
    document.body.classList.toggle("dark");
    const tema = document.body.classList.contains("dark") ? "dark" : "light";
    salvarNoLocalStorage("app-theme", tema);
    atualizarLogoTema();
  });

  // Modal Closers
  document.querySelectorAll(".modal-close, .modal-close-btn").forEach(closer => {
    closer.addEventListener("click", fecharModais);
  });

  // Keyboard Shortcuts Handler
  window.addEventListener("keydown", e => {
    // Esc closes modals or cancels segment edits
    if (e.key === "Escape") {
      fecharModais();
      if (state.segmentoEditandoIndice !== null) {
        cancelarEdicaoSegmento();
      }
      return;
    }

    // Modal triggers with Ctrl
    if (e.ctrlKey) {
      if (e.key.toLowerCase() === "s") {
        e.preventDefault();
        if (e.shiftKey) {
          abrirSalvarBiblioteca(); // Ctrl+Shift+S (Biblioteca save)
        } else {
          guardarPecaAtual(); // Ctrl+S (Session save)
        }
      }
      if (e.key.toLowerCase() === "a") {
        e.preventDefault();
        abrirBiblioteca(); // Ctrl+A (Biblioteca list)
      }
      if (e.key.toLowerCase() === "p") {
        e.preventDefault();
        abrirModalGeradores(); // Ctrl+P (Gerador boiadeira)
      }
      if (e.key.toLowerCase() === "z") {
        e.preventDefault();
        desfazerUltimoSegmento(); // Ctrl+Z (Desfazer)
      }
      if (e.key === "Backspace" || e.keyCode === 8) {
        e.preventDefault();
        e.stopPropagation();
        
        if (state.modoAtual === "classica") {
          if (state.segmentoEditandoIndice !== null) {
            cancelarEdicaoSegmento();
          } else {
            desfazerUltimoSegmento();
          }
        } else { // Rápido
          if (state.faseRapida === "desenho") {
            desfazerUltimoSegmento();
          } else if (state.faseRapida === "medidas") {
            if (state.indiceMedidaRapida > 0) {
              state.indiceMedidaRapida--;
              state.segmentos[state.indiceMedidaRapida].medida = null;
              document.getElementById("seg-medida").value = "";
              atualizarNomeFaseRapida();
              atualizarListaSegmentos();
              atualizarPreview();
              atualizarIndicadorModo();
              document.getElementById("seg-medida").focus();
            } else {
              // Revert to drawing phase
              state.faseRapida = "desenho";
              document.getElementById("manual-segment-controls").classList.add("hidden");
              document.getElementById("rapida-instructions").classList.remove("hidden");
              document.getElementById("segment-editor-title").textContent = "Modo Rápido: Forma";
              atualizarListaSegmentos();
              atualizarPreview();
              atualizarIndicadorModo();
            }
          } else if (state.faseRapida === "concluido") {
            // Revert to editing the last measure
            state.faseRapida = "medidas";
            state.indiceMedidaRapida = state.segmentos.length - 1;
            state.segmentos[state.indiceMedidaRapida].medida = null;
            document.getElementById("manual-segment-controls").classList.remove("hidden");
            document.getElementById("rapida-instructions").classList.remove("hidden");
            document.getElementById("seg-medida").value = "";
            atualizarNomeFaseRapida();
            atualizarListaSegmentos();
            atualizarPreview();
            atualizarIndicadorModo();
            document.getElementById("seg-medida").focus();
          }
        }
        return;
      }
      return;
    }

    // F1/F2 Modos
    if (e.key === "F1") {
      e.preventDefault();
      aplicarModo("classica");
      return;
    }
    if (e.key === "F2") {
      e.preventDefault();
      aplicarModo("rapida");
      return;
    }

    // Prevent arrow bindings if an input/select is focused
    const elem = document.activeElement;
    const inputs = ["INPUT", "SELECT", "TEXTAREA"];
    if (inputs.includes(elem.tagName)) {
      // Enter in inputs confirms values
      if (e.key === "Enter") {
        e.preventDefault();
        if (elem.id === "seg-medida") {
          if (state.modoAtual === "rapida" && state.faseRapida === "medidas") {
            confirmarMedidaRapida();
          } else {
            adicionarSegmentoClassico();
          }
        } else if (elem.id === "peca-nome") {
          const obsEl = document.getElementById("peca-observacao");
          if (obsEl) obsEl.focus();
          else document.getElementById("peca-comprimento").focus();
        } else if (elem.id === "peca-observacao") {
          document.getElementById("peca-comprimento").focus();
        } else if (elem.id === "peca-comprimento") {
          document.getElementById("peca-quantidade").focus();
        } else if (elem.id === "peca-quantidade") {
          guardarPecaAtual();
        } else if (elem.id === "search-biblioteca") {
          // If search is active and has results, load first
          const firstRow = document.querySelector("#library-list .library-row button");
          if (firstRow) firstRow.click();
        }
      }
      return;
    }

    // Global keyboard keys (outside inputs)
    if (e.key === "Enter") {
      e.preventDefault();
      if (state._grauAcabouDeConfirmar) return;
      if (state.modoAtual === "rapida") {
        if (state.faseRapida === "desenho") {
          confirmarEsqueletoRapido();
        }
      } else {
        adicionarSegmentoClassico();
      }
      return;
    }

    // Define custom deflection angle in Quick Mode using shortcut key "g"
    if (e.key.toLowerCase() === "g" && !e.ctrlKey && !e.altKey && !e.metaKey) {
      if (state.modoAtual === "rapida" && state.faseRapida === "desenho") {
        e.preventDefault();
        state.faseRapida = "grau";
        atualizarIndicadorModo();
        return;
      }
    }

    // Focus thickness/plate selector using shortcut "E"
    if (e.key.toLowerCase() === "e") {
      e.preventDefault();
      document.getElementById("peca-chapa").focus();
      return;
    }

    // Focus length input using shortcut "C"
    if (e.key.toLowerCase() === "c") {
      e.preventDefault();
      const el = document.getElementById("peca-comprimento");
      el.focus();
      el.select();
      return;
    }

    // Direction arrows / keys N, S, E, W
    const dir = TECLAS_DIRECAO[e.code] || TECLAS_DIRECAO[e.key];
    if (dir) {
      e.preventDefault();
      document.querySelectorAll(".arrow-btn").forEach(btn => {
        if (btn.getAttribute("data-dir") === dir) {
          btn.classList.add("active");
        } else {
          btn.classList.remove("active");
        }
      });
      
      if (state.modoAtual === "rapida" && state.faseRapida === "desenho") {
        adicionarEsqueletoRapido(dir);
      }
      return;
    }

    // Toggle cota type using shortcut key "x" (externa) or "i" (interna)
    if (e.key.toLowerCase() === "x") {
      document.getElementById("cota-ext").checked = true;
      if (state.segmentoEditandoIndice !== null) {
        state.segmentos[state.segmentoEditandoIndice].tipo = "e";
        atualizarListaSegmentos();
        atualizarPreview();
      }
    }
    if (e.key.toLowerCase() === "i") {
      document.getElementById("cota-int").checked = true;
      if (state.segmentoEditandoIndice !== null) {
        state.segmentos[state.segmentoEditandoIndice].tipo = "i";
        atualizarListaSegmentos();
        atualizarPreview();
      }
    }
  });
}

// Curved segment & Calendering dynamic calculations
let camposCurvaModificados = [];

function registrarModificacaoCampoCurva(idCampo) {
  camposCurvaModificados = camposCurvaModificados.filter(id => id !== idCampo);
  camposCurvaModificados.push(idCampo);
  if (camposCurvaModificados.length > 2) {
    camposCurvaModificados.shift();
  }
  if (camposCurvaModificados.length === 2) {
    calcularCamposCalandragem();
  }
}

function obterChapaAtiva() {
  const cod = document.getElementById("peca-chapa").value;
  return state.chapas.find(c => c.codigo === cod) || { espessura: 2.0, k_factor: 0.44 };
}

function calcularCamposCalandragem() {
  const inputRaio = document.getElementById("curva-raio");
  const inputComprimento = document.getElementById("curva-comprimento");
  const inputAngulo = document.getElementById("curva-angulo");
  const selectTipo = document.getElementById("curva-tipo-raio");
  
  if (!inputRaio || !inputComprimento || !inputAngulo) return;

  const R = parseFloat(inputRaio.value);
  const C = parseFloat(inputComprimento.value);
  const A = parseFloat(inputAngulo.value);
  const tipoRaio = selectTipo ? selectTipo.value : "interno";
  
  const campos = ["raio", "comprimento", "angulo"];
  const target = campos.find(c => !camposCurvaModificados.includes(c));
  
  if (!target) return;
  
  let R_val = R;
  let C_val = C;
  let A_val = A;
  
  if (target === "comprimento") {
    if (!isNaN(R) && !isNaN(A)) {
      C_val = R * (A * Math.PI / 180);
      inputComprimento.value = C_val.toFixed(2);
    }
  } else if (target === "angulo") {
    if (!isNaN(R) && !isNaN(C) && R > 0) {
      A_val = (C / R) * 180 / Math.PI;
      inputAngulo.value = A_val.toFixed(2);
    }
  } else if (target === "raio") {
    if (!isNaN(C) && !isNaN(A) && A > 0) {
      R_val = C / (A * Math.PI / 180);
      inputRaio.value = R_val.toFixed(2);
    }
  }
  
  if (!isNaN(R_val) && !isNaN(A_val)) {
    const chapa = obterChapaAtiva();
    const espessura = chapa.espessura;
    const k_factor = chapa.k_factor;
    
    let Ri = R_val;
    if (tipoRaio === "externo") {
      Ri = R_val - espessura;
    }
    const Rn = Ri + k_factor * espessura;
    const C_desenvolvimento = Rn * (A_val * Math.PI / 180);
    
    document.getElementById("seg-medida").value = C_desenvolvimento.toFixed(1);
    document.getElementById("seg-angulo").value = A_val.toFixed(1);
  }
}

// Tubo Calandrado (360°) Generator
function abrirGeradorTubo() {
  fecharModais();
  abrirModal("modal-gerador-tubo");
  
  document.getElementById("tubo-comprimento").value = 3000;
  document.getElementById("tubo-diametro").value = 200;
  document.getElementById("tubo-tipo-diametro").value = "interno";
  
  if (state.chapas.length > 0) {
    document.getElementById("tubo-chapa").value = state.chapaCodigo || state.chapas[0].codigo;
  }
}

function gerarTubo() {
  const chapaCodigo = document.getElementById("tubo-chapa").value;
  const comprimento = converterVirgulaPonto(document.getElementById("tubo-comprimento").value);
  const diametro = converterVirgulaPonto(document.getElementById("tubo-diametro").value);
  const tipoDiametro = document.getElementById("tubo-tipo-diametro").value;

  if (isNaN(comprimento) || comprimento <= 0 || isNaN(diametro) || diametro <= 0) {
    alert("Todos os campos do gerador devem ser preenchidos com valores maiores que zero.");
    return;
  }

  const chapa = state.chapas.find(c => c.codigo === chapaCodigo);
  const espessura = chapa ? chapa.espessura : 2.0;
  const k_factor = chapa ? chapa.k_factor : 0.44;

  let Ri = diametro / 2;
  if (tipoDiametro === "externo") {
    Ri = diametro / 2 - espessura;
  }
  const Rn = Ri + k_factor * espessura;
  const C_desenvolvimento = Rn * 2 * Math.PI;

  const peca = {
    nome: `Tubo Calandrado D${tipoDiametro === "interno" ? "i" : "e"}${diametro}`,
    chapa: chapaCodigo,
    comprimento: comprimento,
    segmentos: [
      {
        direcao: "E",
        angulo: 360,
        medida: parseFloat(C_desenvolvimento.toFixed(1)),
        tipo: "e",
        curvo: true,
        curva_info: {
          raio: Ri,
          comprimento_curva: Ri * 2 * Math.PI,
          angulo_curva: 360,
          tipo_raio: "interno"
        }
      }
    ]
  };

  document.getElementById("peca-nome").value = peca.nome;
  document.getElementById("peca-chapa").value = peca.chapa;
  document.getElementById("peca-comprimento").value = peca.comprimento;
  document.getElementById("peca-quantidade").value = 1;

  state.chapaCodigo = peca.chapa;
  state.comprimento = peca.comprimento;
  state.quantidade = 1;
  state.segmentos = peca.segmentos;

  aplicarModo("classica");
  fecharModais();
  atualizarListaSegmentos();
  atualizarPreview();
}
