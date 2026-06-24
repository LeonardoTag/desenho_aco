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

coordenadas_no_canvas, escala = desenhar.adequar_coordenadas_ao_canvas(
    instrucoes["coordenadas_polares"],
    tamanho_canvas,
    80.0,
)
espessura_linha = 1.5 * escala

comprimento = float(instrucoes.get("comprimento", 0) or 0)
estilo = desenhar._estilo_desenho("preview")
escala_comprimento = espessura_linha / max(float(instrucoes.get("espessura", 1.0)), 1e-6)
comprimento_px = comprimento * escala_comprimento * estilo["fator_comprimento"]

print(f"comprimento = {comprimento}")
print(f"escala_comprimento = {escala_comprimento}")
print(f"fator_comprimento = {estilo['fator_comprimento']}")
print(f"comprimento_px = {comprimento_px}")

dx = comprimento_px * desenhar.cos(desenhar.radians(estilo["angulo_comprimento"]))
dy = -comprimento_px * desenhar.sin(desenhar.radians(estilo["angulo_comprimento"]))
print(f"dx = {dx}, dy = {dy}")
