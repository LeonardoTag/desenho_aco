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

tamanho_canvas = (800, 800)
coordenadas_no_canvas, escala = desenhar.desenhar(
    None,
    instrucoes,
    tamanho_canvas,
    margem=0.1,
)

print("Scale:", escala)
print("Canvas Coordinates:")
for idx, pt in enumerate(coordenadas_no_canvas):
    print(f"Vertex {idx}: {pt}")
