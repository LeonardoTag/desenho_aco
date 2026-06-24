import sys
from pathlib import Path

# Add workspace to path
sys.path.append(r"g:\Meu Drive\1- Capital Aço\AUTOMAÇÕES PYTHON\desenho - antigravity")

from config_manager import carregar_config
carregar_config()

from core import desenhar
from core.geradores_pecas import gerar_perfil_boiadeira
from core.relatorio_dobra import gerar_pdf_detalhamento_dobra

# Generate Boiadeira 230 #14
dados = gerar_perfil_boiadeira(
    altura_aba=20,
    largura_total=230,
    chapa="#14",
    primeiro_gomo=30,
    tamanho_gomo_superior=30,
    tamanho_gomo_inferior=30,
    num_gomos=2,
    comprimento=3000,
)
instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(dados)
output_pdf = Path(r"g:\Meu Drive\1- Capital Aço\AUTOMAÇÕES PYTHON\desenho - antigravity\files\__preview_detalhamento__.pdf")

# If existing, delete it
if output_pdf.exists():
    output_pdf.unlink()

gerar_pdf_detalhamento_dobra(
    instrucoes,
    output_pdf,
    nome_peca="BOIADEIRA 230 #14",
    codigo_chapa="#14",
    comprimento_peca=3000,
)
print("PDF generated successfully at:", output_pdf)
