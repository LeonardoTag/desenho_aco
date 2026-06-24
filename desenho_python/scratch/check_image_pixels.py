import sys
from pathlib import Path
from PIL import Image

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

# Render
image = desenhar.renderizar_imagem(
    instrucoes,
    tamanho=800,
    mostrar_medidas=True,
    destino="preview",
    canvas_adaptativo=True,
)

# Find blue pixels and red pixels
# Let's inspect colors
# Blue label text color is _PALETA["cota_externa"] which is (21, 101, 192) or in report style (18, 55, 105)
# Red label text color is _PALETA["cota_interna"] which is (183, 28, 28) or in report style (255, 190, 190) or similar.
# Let's find any pixels close to these colors.

pixels = image.load()
width, height = image.size

blue_pixels = []
red_pixels = []

for y in range(height):
    for x in range(width):
        r, g, b = pixels[x, y][:3]
        # Check for blue (b > 150 and r < 100)
        if b > 150 and r < 100 and g < 150:
            blue_pixels.append((x, y))
        # Check for red (r > 150 and b < 100 and g < 100)
        if r > 180 and b < 100 and g < 100:
            red_pixels.append((x, y))

print(f"Image size: {width}x{height}")
print(f"Found {len(blue_pixels)} blue pixels and {len(red_pixels)} red_pixels.")

if blue_pixels:
    min_bx = min(p[0] for p in blue_pixels)
    max_bx = max(p[0] for p in blue_pixels)
    min_by = min(p[1] for p in blue_pixels)
    max_by = max(p[1] for p in blue_pixels)
    print(f"Blue bounding box: [{min_bx}, {min_by}] to [{max_bx}, {max_by}]")

if red_pixels:
    min_rx = min(p[0] for p in red_pixels)
    max_rx = max(p[0] for p in red_pixels)
    min_ry = min(p[1] for p in red_pixels)
    max_ry = max(p[1] for p in red_pixels)
    print(f"Red bounding box: [{min_rx}, {min_ry}] to [{max_rx}, {max_by}]")
