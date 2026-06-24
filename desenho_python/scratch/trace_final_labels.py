import sys
from pathlib import Path

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

# Run renderizar_imagem step-by-step
tamanho = 800
fator_aa = desenhar._supersampling()
tamanho_base = tamanho * fator_aa
tamanho_canvas = (tamanho_base, tamanho_base)

_, medidas, dobras = desenhar.gerar_corte_medidas_angulos(instrucoes)
font_size = desenhar._tamanho_fonte_cota(tamanho_base, "preview")

# 1. First attempt of drawing
coordenadas_no_canvas, espessura_linha = desenhar.desenhar(
    None,
    instrucoes,
    tamanho_canvas,
    margem=0.1,
)

rotulos, font = desenhar.calcular_layout_medidas(
    coordenadas_no_canvas,
    espessura_linha,
    medidas,
    font_size,
    destino="preview",
)

# Check if it fits
bbox = desenhar._bbox_desenho_e_rotulos(
    coordenadas_no_canvas,
    rotulos,
    None,
    pontos_extra=None,
)

padding = max(10, int(round(min(tamanho_canvas) * 0.012)))
fits = desenhar.conteudo_cabe_no_canvas(bbox, tamanho_canvas, padding=padding)
print(f"Fits: {fits}")

# Apply normalization if needed
if not fits:
    coordenadas_no_canvas, rotulos, _, escala_layout = desenhar._normalizar_layout_no_canvas(
        coordenadas_no_canvas,
        rotulos,
        None,
        bbox,
        tamanho_canvas,
        padding,
        font_size,
    )
    print(f"Scaled layout by: {escala_layout}")

print("\n--- FINAL LABELS ---")
for r in rotulos:
    final_x = r["centro"][0] + r["offset_x"]
    final_y = r["centro"][1] + r["offset_y"]
    print(f"Seg {r['segmento']} ({r['tipo']}): texto={r['texto']}, centro={r['centro']}, offset=({r['offset_x']:.2f}, {r['offset_y']:.2f}) => final=({final_x:.2f}, {final_y:.2f})")
