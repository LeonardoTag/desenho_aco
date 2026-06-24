import sys
from pathlib import Path

# Add project base to path
sys.path.append(str(Path(__file__).parent.parent))

from config_manager import carregar_config
from core import desenhar

carregar_config()

# U-profile: 50mm, 50mm, 50mm
inst = {
    "chapa": "#16", # thickness = 1.5mm, radio = 0.9mm
    "comprimento": 500,
    "segmentos": [
        ["S", 90, 50, "e"],
        ["E", 90, 50, "e"],
        ["N", 90, 50, "e"]
    ]
}

pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
dim = desenhar.calcular_dimensoes_acabadas(pol)
print("Calculated dimensions (current):", dim)
