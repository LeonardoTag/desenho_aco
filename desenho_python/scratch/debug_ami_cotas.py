import sys
from pathlib import Path

# Add workspace to path
sys.path.append(r"g:\Meu Drive\1- Capital Aço\AUTOMAÇÕES PYTHON\desenho - antigravity")

from config_manager import carregar_config
carregar_config()

from core import desenhar

instrucoes_conv = {
    "chapa": "#16",
    "comprimento": 2300,
    "segmentos": [
        ["W", 90.0, 60.0, "e"],
        ["S", 90.0, 30.0, "e"],
        ["E", 90.0, 22.0, "e"],
        ["S", 90.0, 50.0, "e"],
        ["W", 90.0, 22.0, "e"],
        ["S", 90.0, 30.0, "e"],
        ["E", 90.0, 60.0, "e"]
    ]
}

instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(instrucoes_conv)

# We want to mimic renderizar_imagem step-by-step
tamanho_canvas = (800, 800)
coordenadas_no_canvas, espessura_linha = desenhar.desenhar(
    None,
    instrucoes,
    tamanho_canvas,
    margem=0.1,
)

_, medidas, dobras = desenhar.gerar_corte_medidas_angulos(instrucoes)
font_size = desenhar._tamanho_fonte_cota(800, "preview")

rotulos = desenhar.montar_rotulos_medidas(
    coordenadas_no_canvas,
    espessura_linha,
    medidas,
    font_size,
    destino="preview",
)

print("--- BEFORE OPTIMIZATION ---")
for r in rotulos:
    print(f"Seg {r['segmento']} ({r['tipo']}): centro={r['centro']}, ox_base={r['ox_base']:.2f}, oy_base={r['oy_base']:.2f}")

# Now run optimization but trace it
meia_espessura = espessura_linha / 2
bboxes_ocupados = []
ordem = sorted(
    rotulos,
    key=lambda r: (r["segmento"], 0 if r["tipo"] == "externa" else 1),
)

print("\n--- OPTIMIZATION TRACE ---")
for rotulo in ordem:
    print(f"\nOptimizing Seg {rotulo['segmento']} ({rotulo['tipo']}):")
    melhor_livre = None
    melhor_penalidade = float("inf")
    melhor_escolha = None

    candidatos = desenhar._gerar_candidatos_rotulo(rotulo, font_size)
    print(f"  Generated {len(candidatos)} candidates.")
    
    for idx, (pontuacao, ox, oy, bbox) in enumerate(candidatos):
        valida = desenhar._posicao_rotulo_valida(
            bbox, bboxes_ocupados, coordenadas_no_canvas, meia_espessura
        )
        sobrepoe_perfil = desenhar._bbox_sobrepoe_perfil(bbox, coordenadas_no_canvas, meia_espessura)
        sobrepoe_ocupados = any(desenhar._bboxes_sobrepoem(bbox, ocupado) for ocupado in bboxes_ocupados)
        
        if idx < 5 or valida:
            print(f"    Cand {idx}: ox={ox:.2f}, oy={oy:.2f}, pont={pontuacao:.2f}, valida={valida} (sobrepoe_perfil={sobrepoe_perfil}, sobrepoe_ocupados={sobrepoe_ocupados})")
            
        if valida:
            melhor_livre = (ox, oy, bbox)
            break
            
        penalidade = desenhar._pontuacao_sobreposicao(bbox, bboxes_ocupados) + pontuacao
        penalidade += desenhar._pontuacao_sobreposicao_perfil(
            bbox, coordenadas_no_canvas, meia_espessura
        )
        if penalidade < melhor_penalidade:
            melhor_penalidade = penalidade
            melhor_escolha = (ox, oy, bbox)

    if melhor_livre is not None:
        ox, oy, bbox = melhor_livre
        print(f"  => Chosen VALID: ox={ox:.2f}, oy={oy:.2f}")
    else:
        ox, oy, bbox = melhor_escolha
        print(f"  => Chosen PENALIZED (pen={melhor_penalidade:.2f}): ox={ox:.2f}, oy={oy:.2f}")
        
    rotulo["offset_x"] = ox
    rotulo["offset_y"] = oy
    rotulo["bbox"] = bbox
    bboxes_ocupados.append(bbox)
