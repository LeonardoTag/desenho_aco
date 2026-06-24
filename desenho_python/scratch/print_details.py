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
polares = instrucoes["coordenadas_polares"]

print("1. Polares:")
for idx, pt in enumerate(polares):
    print(f"  {idx}: {pt}")

parciais = desenhar.gerar_coordenadas_retangulares_parciais(polares)
print("\n2. Parciais:")
for idx, pt in enumerate(parciais):
    print(f"  {idx}: {pt}")

absolutas = desenhar.gerar_coordenadas_retangulares_absolutas(parciais)
print("\n3. Absolutas:")
for idx, pt in enumerate(absolutas):
    print(f"  {idx}: {pt}")

tamanho_canvas = (800, 800)
margem_px = 80.0
coordenadas_no_canvas, escala = desenhar.adequar_coordenadas_ao_canvas(
    polares,
    tamanho_canvas,
    margem_px,
)
print(f"\n4. Escala: {escala}")
print("5. Canvas Coordinates:")
for idx, pt in enumerate(coordenadas_no_canvas):
    print(f"  {idx}: {pt}")
