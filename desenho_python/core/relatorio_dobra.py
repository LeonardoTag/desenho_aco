from __future__ import annotations

from datetime import datetime
from io import BytesIO
from pathlib import Path

from PIL import Image
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.units import mm
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfgen import canvas

from config_manager import obter_config
from core import desenhar


def _mm_int(valor: float) -> int:
    return int(round(valor))


def _formatar_medida(valor: float | int) -> str:
    return str(_mm_int(valor))


def _formatar_espessura(valor: float | int) -> str:
    return f"{float(valor):.2f}".replace(".", ",")


def _formatar_angulo(angulo: float) -> str:
    return f"{_mm_int(angulo)}°"


def _texto_sentido_dobra(sentido: str) -> str:
    if sentido == "h":
        return "Para baixo"
    return "Para cima"


def _largura_texto(texto: str, fonte: str, tamanho: float) -> float:
    return pdfmetrics.stringWidth(texto, fonte, tamanho)


def _bbox_cota_rotacionada(x: float, y: float, texto: str, fonte: str, tamanho: float):
    largura_texto = _largura_texto(texto, fonte, tamanho)
    altura_texto = tamanho * 1.15
    meio = largura_texto / 2
    return (x - altura_texto / 2, y - meio, x + altura_texto / 2, y + meio)


def _bboxes_sobrepoem(a, b, folga: float = 1.5 * mm) -> bool:
    return not (
        a[2] + folga <= b[0]
        or b[2] + folga <= a[0]
        or a[3] + folga <= b[1]
        or b[3] + folga <= a[1]
    )


def _desenhar_texto_rotacionado(
    c: canvas.Canvas, x: float, y: float, texto: str, fonte: str, tamanho: float
):
    c.saveState()
    c.translate(x, y)
    c.rotate(90)
    c.drawString(-_largura_texto(texto, fonte, tamanho) / 2, 0, texto)
    c.restoreState()


def _desenhar_seta_horizontal(c: canvas.Canvas, x: float, y: float, direita: bool):
    tamanho = 1.8 * mm
    if direita:
        c.line(x, y, x + tamanho, y)
        c.line(x + tamanho, y, x + tamanho * 0.55, y + tamanho * 0.35)
        c.line(x + tamanho, y, x + tamanho * 0.55, y - tamanho * 0.35)
    else:
        c.line(x, y, x - tamanho, y)
        c.line(x - tamanho, y, x - tamanho * 0.55, y + tamanho * 0.35)
        c.line(x - tamanho, y, x - tamanho * 0.55, y - tamanho * 0.35)


def _desenhar_cota_cadeia(
    c: canvas.Canvas,
    x1: float,
    x2: float,
    y: float,
    texto: str,
    *,
    desloc_y: float = 0.0,
    tamanho_fonte: float = 9,
):
    y_dim = y + desloc_y
    ext = 3 * mm
    y_ext = y_dim - ext
    c.setLineWidth(0.35)
    c.setStrokeColor(colors.HexColor("#555555"))
    c.line(x1, y, x1, y_ext)
    c.line(x2, y, x2, y_ext)
    c.line(x1, y_ext, x2, y_ext)
    _desenhar_seta_horizontal(c, x1, y_ext, direita=True)
    _desenhar_seta_horizontal(c, x2, y_ext, direita=False)
    c.setFont("Helvetica", tamanho_fonte)
    c.setFillColor(colors.HexColor("#333333"))
    c.drawCentredString((x1 + x2) / 2, y_ext - 3.4 * mm, texto)


def _layout_cotas_ordenadas(
    posicoes_px: list[float],
    valores: list[int],
    y_base: float,
    fonte: str,
    tamanho_fonte: float,
):
    layouts = []
    ocupados: list[tuple[float, float, float, float]] = []
    deslocamento = 0.0

    for x, valor in zip(posicoes_px, valores):
        texto = _formatar_medida(valor)
        y = y_base + deslocamento
        bbox = _bbox_cota_rotacionada(x, y, texto, fonte, tamanho_fonte)
        while any(_bboxes_sobrepoem(bbox, outro) for outro in ocupados):
            deslocamento += tamanho_fonte * 1.35
            y = y_base + deslocamento
            bbox = _bbox_cota_rotacionada(x, y, texto, fonte, tamanho_fonte)
        ocupados.append(bbox)
        layouts.append((x, y, texto, bbox))
    return layouts, deslocamento


def _layout_cotas_cadeia(
    segmentos: list[tuple[float, float, str]],
    y_base: float,
    fonte: str,
    tamanho_fonte: float,
):
    layouts = []
    ocupados: list[tuple[float, float, float, float]] = []

    for x1, x2, texto in segmentos:
        melhor = 0.0
        bbox = None
        for tentativa in range(6):
            desloc = tentativa * (tamanho_fonte * 1.6)
            largura = _largura_texto(texto, fonte, tamanho_fonte)
            cx = (x1 + x2) / 2
            candidato = (
                cx - largura / 2 - 1 * mm,
                y_base - desloc - 4 * mm,
                cx + largura / 2 + 1 * mm,
                y_base - desloc,
            )
            if not any(_bboxes_sobrepoem(candidato, outro) for outro in ocupados):
                melhor = desloc
                bbox = candidato
                break
            melhor = desloc
            bbox = candidato
        ocupados.append(bbox)
        layouts.append((x1, x2, texto, melhor))
    return layouts


def _desenhar_simbolo_angulo_reto_pdf(c: canvas.Canvas, x: float, y: float, tam: float):
    c.setLineWidth(0.55)
    c.setStrokeColor(colors.HexColor("#444444"))
    c.line(x, y, x, y + tam)
    c.line(x, y, x + tam, y)


def _desenhar_planificacao(
    c: canvas.Canvas,
    dados: dict,
    x0: float,
    y0: float,
    largura: float,
    altura_faixa: float,
    *,
    tamanho_fonte_cota: float = 9,
    tamanho_fonte_angulo: float = 8.5,
    tamanho_fonte_sentido: float = 7.5,
) -> float:
    """Desenha a planificação. Retorna a coordenada Y mais alta ocupada."""
    total = dados["corte_total"] or 1
    y_baixo = y0
    y_topo = y0 + altura_faixa
    escala = largura / total
    fonte = "Helvetica"

    def px(posicao: float) -> float:
        return x0 + posicao * escala

    c.setLineWidth(0.8)
    c.setStrokeColor(colors.black)
    c.line(px(0), y_baixo, px(total), y_baixo)
    c.line(px(0), y_topo, px(total), y_topo)
    c.line(px(0), y_baixo, px(0), y_topo)
    c.line(px(total), y_baixo, px(total), y_topo)

    for marca in dados["marcas_dobra"]:
        x = px(marca["posicao"])
        if marca["sentido"] == "a":
            c.setDash(3, 2)
        else:
            c.setDash()
        c.setLineWidth(0.75)
        c.setStrokeColor(colors.HexColor("#222222"))
        c.line(x, y_baixo, x, y_topo)
        c.setDash()

    for mc in dados.get("marcas_calandragem", []):
        c.setDash(4, 2)
        c.setLineWidth(0.8)
        c.setStrokeColor(colors.HexColor("#1565C0")) # Calendering blue
        x_ini = px(mc["posicao_inicio"])
        c.line(x_ini, y_baixo, x_ini, y_topo)
        x_fim = px(mc["posicao_fim"])
        c.line(x_fim, y_baixo, x_fim, y_topo)
        c.setDash()

    posicoes = dados["posicoes_ordenadas"]
    posicoes_px = [px(p) for p in posicoes]
    y_cota_ord_base = y_topo + 6 * mm

    layouts_ord, desloc_ord = _layout_cotas_ordenadas(
        posicoes_px, posicoes, y_cota_ord_base, fonte, tamanho_fonte_cota
    )
    y_linha_ord_topo = y_topo + 4 * mm
    y_linha_ord_fim = y_cota_ord_base + desloc_ord + tamanho_fonte_cota * 2

    for x, y, texto, _bbox in layouts_ord:
        c.setLineWidth(0.35)
        c.setStrokeColor(colors.HexColor("#555555"))
        c.line(x, y_linha_ord_topo, x, y_linha_ord_fim)
        c.setFont(fonte, tamanho_fonte_cota)
        c.setFillColor(colors.HexColor("#333333"))
        _desenhar_texto_rotacionado(c, x, y, texto, fonte, tamanho_fonte_cota)

    segmentos_cadeia = [
        (px(t["inicio"]), px(t["fim"]), _formatar_medida(t["comprimento"]))
        for t in dados["trechos_cadeia"]
    ]
    layouts_cadeia = _layout_cotas_cadeia(
        segmentos_cadeia, y_baixo - 8 * mm, fonte, tamanho_fonte_cota
    )
    for x1, x2, texto, desloc in layouts_cadeia:
        _desenhar_cota_cadeia(
            c,
            x1,
            x2,
            y_baixo,
            texto,
            desloc_y=-8 * mm - desloc,
            tamanho_fonte=tamanho_fonte_cota,
        )

    y_angulo = y_linha_ord_fim + 7 * mm
    y_titulo_ord = y_angulo + 8 * mm

    for marca in dados["marcas_dobra"]:
        x = px(marca["posicao"])
        angulo = marca["angulo_dobra"]
        sentido_txt = _texto_sentido_dobra(marca["sentido"])
        if abs(angulo - 90) < 0.5:
            _desenhar_simbolo_angulo_reto_pdf(c, x, y_angulo, 4 * mm)
        else:
            c.setFont("Helvetica", tamanho_fonte_angulo)
            c.setFillColor(colors.HexColor("#555555"))
            c.drawCentredString(x, y_angulo, _formatar_angulo(angulo))
        c.setFont("Helvetica-Oblique", tamanho_fonte_sentido)
        c.setFillColor(colors.HexColor("#666666"))
        c.drawCentredString(x, y_angulo - 4 * mm, sentido_txt)

    for mc in dados.get("marcas_calandragem", []):
        x_ini = px(mc["posicao_inicio"])
        x_fim = px(mc["posicao_fim"])

        c.setFont("Helvetica-Bold", tamanho_fonte_sentido - 0.5)
        c.setFillColor(colors.HexColor("#1565C0"))
        c.drawCentredString(x_ini, y_angulo, "INÍCIO CALANDRAGEM")

        tipo_str = "R.Int" if mc["tipo_raio"] == "interno" else "R.Ext"
        c.setFont("Helvetica", tamanho_fonte_sentido - 0.5)
        c.drawCentredString(x_ini, y_angulo - 4 * mm, f"{tipo_str}={_formatar_medida(mc['raio'])} A={_formatar_angulo(mc['angulo_curva'])}")

        c.setFont("Helvetica-Bold", tamanho_fonte_sentido - 0.5)
        c.setFillColor(colors.HexColor("#1565C0"))
        c.drawCentredString(x_fim, y_angulo, "FIM CALANDRAGEM")

    c.setFont("Helvetica-Bold", tamanho_fonte_cota)
    c.setFillColor(colors.HexColor("#333333"))
    c.drawString(x0, y_titulo_ord, "Cotas ordenadas (mm)")

    y_titulo_cadeia = y_baixo - 24 * mm
    c.drawString(x0, y_titulo_cadeia, "Cotas em cadeia — entre marcas de dobra (mm)")
    c.setFont("Helvetica", tamanho_fonte_sentido)
    c.setFillColor(colors.HexColor("#777777"))
    c.drawString(
        x0,
        y_baixo - 28 * mm,
        "Linha vertical no centro da dobra (marca do punção)  ·  "
        "contínua = para baixo  ·  tracejada = para cima",
    )

    return y_titulo_ord + tamanho_fonte_cota + 2 * mm


def _encaixar_imagem_no_retangulo(
    largura_slot: float,
    altura_slot: float,
    largura_img: float,
    altura_img: float,
) -> tuple[float, float]:
    if largura_img <= 0 or altura_img <= 0:
        return largura_slot, altura_slot
    escala = min(largura_slot / largura_img, altura_slot / altura_img)
    return largura_img * escala, altura_img * escala


def gerar_pdf_detalhamento_dobra(
    instrucoes: dict,
    caminho_saida: Path | str,
    *,
    nome_peca: str = "",
    codigo_chapa: str = "",
    comprimento_peca: float | None = None,
    imagem_perfil: Image.Image | None = None,
) -> Path:
    dados = desenhar.gerar_dados_planificacao(instrucoes)
    caminho = Path(caminho_saida)
    caminho.parent.mkdir(parents=True, exist_ok=True)

    largura, altura = landscape(A4)
    margem = 10 * mm
    c = canvas.Canvas(str(caminho), pagesize=landscape(A4))
    cfg = obter_config()
    titulo_peca = nome_peca.strip() or "Peça atual"
    c.setTitle(f"Detalhamento de dobra — {titulo_peca}")

    fonte_titulo = float(cfg.get("relatorio_dobra_fonte_titulo", 15.5))
    fonte_texto = float(cfg.get("relatorio_dobra_fonte_texto", 10.0))
    fonte_secao = float(cfg.get("relatorio_dobra_fonte_secao", 10.0))
    fonte_cota = float(cfg.get("relatorio_dobra_fonte_cota", 7.65))
    altura_faixa_plano = 18 * mm
    margem_inferior_plano = 30 * mm

    y_plano_baixo = margem + margem_inferior_plano

    y = altura - margem
    x_texto = margem
    c.setFont("Helvetica-Bold", fonte_titulo)
    c.drawString(x_texto, y, "DETALHAMENTO DE DOBRA")
    y -= 6.5 * mm
    c.setFont("Helvetica", fonte_texto)
    c.drawString(x_texto, y, f"Peça: {titulo_peca}")
    y -= 5 * mm
    chapa_txt = codigo_chapa.lstrip("#") or "—"
    c.drawString(x_texto, y, f"Chapa: #{chapa_txt}")
    y -= 5 * mm
    if comprimento_peca is not None:
        c.drawString(
            x_texto,
            y,
            f"Comprimento da peça: {_formatar_medida(comprimento_peca)} mm",
        )
        y -= 5 * mm
    c.drawString(
        x_texto,
        y,
        f"Desenvolvimento: {_formatar_medida(dados['corte_total'])} mm  ·  "
        f"Esp.: {_formatar_espessura(dados['espessura'])} mm",
    )
    y -= 5 * mm
    c.drawString(
        x_texto,
        y,
        f"Emitido em {datetime.now():%d/%m/%Y %H:%M}  ·  "
        f"{cfg.get('relatorio_nome_responsavel', '')}",
    )

    # 1. Draw planificação FIRST
    y_topo_desenhado = _desenhar_planificacao(
        c,
        dados,
        margem,
        y_plano_baixo,
        largura - 2 * margem,
        altura_faixa_plano,
        tamanho_fonte_cota=fonte_cota,
        tamanho_fonte_angulo=float(cfg.get("relatorio_dobra_fonte_angulo", 7.225)),
        tamanho_fonte_sentido=float(cfg.get("relatorio_dobra_fonte_sentido", 6.375)),
    )

    # 2. Draw Planificação title closer to Cotas ordenadas (4mm above y_topo_desenhado)
    y_titulo_plano = y_topo_desenhado + 4 * mm
    c.setFont("Helvetica-Bold", fonte_secao)
    c.drawString(
        margem,
        y_titulo_plano,
        "Planificação — marcas de dobra no desenvolvimento plano",
    )

    # 3. Draw 3D profile preview (maximize space dynamically)
    y_plano_topo_bloco = y_titulo_plano + 2 * mm

    # 3D Slot geometry: start at X=90mm to allow more width
    x_img = 90 * mm
    area_perfil_w = largura - margem - x_img
    y_slot_topo = altura - margem
    y_slot_base = y_plano_topo_bloco + 8 * mm
    slot_h = y_slot_topo - y_slot_base - 6 * mm
    slot_w = area_perfil_w

    if imagem_perfil is None:
        try:
            tamanho_img = int(cfg.get("relatorio_imagem_tamanho_dobra", 1150))
            imagem_perfil = desenhar.renderizar_imagem(
                instrucoes,
                tamanho=tamanho_img,
                mostrar_medidas=True,
                destino="detalhamento_dobra",
                canvas_adaptativo=True,
            )
        except Exception:
            imagem_perfil = None

    if imagem_perfil is not None:
        buffer = BytesIO()
        imagem_perfil.convert("RGB").save(buffer, format="PNG")
        buffer.seek(0)
        img = ImageReader(buffer)
        img_w, img_h = imagem_perfil.size
        draw_w, draw_h = _encaixar_imagem_no_retangulo(slot_w, slot_h, img_w, img_h)
        x_draw = x_img + (slot_w - draw_w) / 2
        y_draw = y_slot_base + (slot_h - draw_h) / 2
        c.setFont("Helvetica-Bold", fonte_secao)
        c.drawString(x_img, y_slot_topo - 4 * mm, "Perfil (peça atual)")
        try:
            dimensoes_acabadas = desenhar.calcular_dimensoes_acabadas(instrucoes)
        except Exception:
            dimensoes_acabadas = None
        if dimensoes_acabadas is not None:
            largura_x, altura_y = dimensoes_acabadas
            c.setFont("Helvetica", fonte_texto)
            c.setFillColor(colors.black)
            c.drawCentredString(
                x_draw + draw_w / 2,
                y_draw + draw_h + 2.5 * mm,
                f"Dimensões: X = {_formatar_medida(largura_x)} mm  ·  "
                f"Y = {_formatar_medida(altura_y)} mm",
            )
        c.drawImage(
            img,
            x_draw,
            y_draw,
            width=draw_w,
            height=draw_h,
            preserveAspectRatio=True,
            anchor="sw",
        )

    c.showPage()
    c.save()
    return caminho


def nome_arquivo_detalhamento_padrao() -> str:
    return f"DETALHAMENTO_DOBRA_{datetime.now():%Y%m%d_%H%M%S}.pdf"
