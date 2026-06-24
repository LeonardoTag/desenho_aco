from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from io import BytesIO
from pathlib import Path

from PIL import Image
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.utils import ImageReader
from reportlab.pdfgen import canvas

from config_manager import (
    caminho_logo_relatorio,
    caminho_pasta_imagens_relatorio,
    obter_config,
)
from core import desenhar
from core.numeros import formatar_compacto


@dataclass
class LinhaRelatorio:
    quantidade: int
    codigo_chapa: str
    corte_mm: float | None
    comprimento_mm: float
    peso_kg: int | None
    nome_peca: str
    imagem: Image.Image | None = None
    observacao: str = ""


def _resolver_logo(caminho: Path) -> Path | None:
    if caminho.exists():
        return caminho
    pasta = caminho_pasta_imagens_relatorio()
    if not pasta.is_dir():
        return None
    for candidato in pasta.glob("*.png"):
        if candidato.name.lower() == caminho.name.lower():
            return candidato
    logos = sorted(pasta.glob("*.png"))
    return logos[0] if logos else None


def _formatar_numero(valor: float | None) -> str:
    if valor is None:
        return ""
    return formatar_compacto(valor)


def montar_linha_relatorio(peca, chapas: list[dict]) -> LinhaRelatorio | None:
    instrucoes_conv = peca.montar_instrucoes()
    if instrucoes_conv is None:
        return None
    try:
        instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(
            instrucoes_conv
        )
    except Exception:
        return None

    corte = desenhar.calcular_largura_corte(instrucoes)
    peso = desenhar.calcular_peso_kg(
        instrucoes,
        peca.quantidade,
        codigo_chapa=peca.codigo_chapa,
        arredondar=True,
    )  # kg total = todas as peças (quantidade × peso unitário, arredondado)
    codigo = peca.codigo_chapa.lstrip("#")
    cfg = obter_config()

    imagem = None
    try:
        tamanho_img = int(cfg.get("relatorio_imagem_tamanho_pedido", 680))
        imagem = desenhar.renderizar_imagem(
            instrucoes,
            tamanho=tamanho_img,
            mostrar_medidas=True,
            destino="relatorio",
        )
    except Exception:
        imagem = None

    return LinhaRelatorio(
        quantidade=max(1, int(peca.quantidade)),
        codigo_chapa=codigo,
        corte_mm=corte,
        comprimento_mm=float(peca.comprimento),
        peso_kg=peso,
        nome_peca=getattr(peca, "nome_peca", "") or "",
        imagem=imagem,
        observacao=getattr(peca, "observacao", "") or "",
    )


def _desenhar_cabecalho(
    c: canvas.Canvas,
    largura: float,
    altura: float,
    cfg: dict,
    pagina_atual: int,
    total_paginas: int,
):
    margem = 12 * mm
    topo = altura - margem

    logo_esq = _resolver_logo(caminho_logo_relatorio(principal=True))
    if logo_esq:
        try:
            c.drawImage(
                str(logo_esq),
                margem,
                topo - 22 * mm,
                width=48 * mm,
                height=18 * mm,
                preserveAspectRatio=True,
                anchor="sw",
                mask="auto",
            )
        except Exception:
            pass

    logo_dir = _resolver_logo(caminho_logo_relatorio(principal=False))
    if logo_dir:
        try:
            c.drawImage(
                str(logo_dir),
                largura - margem - 22 * mm,
                topo - 20 * mm,
                width=22 * mm,
                height=18 * mm,
                preserveAspectRatio=True,
                anchor="sw",
                mask="auto",
            )
        except Exception:
            pass

    nome = str(cfg.get("relatorio_nome_responsavel", "Leonardo")).strip().upper()
    fonte_titulo = float(cfg.get("relatorio_pedido_fonte_titulo", 16.0))
    fonte_subtitulo = float(cfg.get("relatorio_pedido_fonte_subtitulo", 11.0))
    fonte_texto = float(cfg.get("relatorio_pedido_fonte_texto", 9.0))

    c.setFont("Helvetica-Bold", fonte_titulo)
    c.drawCentredString(largura / 2, topo - 8 * mm, nome)
    c.setFont("Helvetica-Bold", fonte_subtitulo)
    c.drawCentredString(largura / 2, topo - 14 * mm, "ORDEM DE PRODUÇÃO")

    observacao = str(cfg.get("relatorio_observacao", "")).strip()
    y_obs = topo - 22 * mm
    c.setFont("Helvetica", fonte_texto)
    c.drawString(margem, y_obs, "Observação:")
    if observacao:
        c.setFont("Helvetica-Bold", fonte_texto)
        c.drawString(margem + 22 * mm, y_obs, observacao)
    else:
        c.setLineWidth(0.4)
        c.line(margem + 22 * mm, y_obs - 1, largura - margem - 62 * mm, y_obs - 1)

    # Emission details and page number right-aligned
    data_emissao = datetime.now().strftime("%d/%m/%Y %H:%M")
    texto_cabecalho_dir = f"Emissão: {data_emissao}  ·  Pág: {pagina_atual}/{total_paginas}"
    c.setFont("Helvetica", fonte_texto)
    c.drawRightString(largura - margem, y_obs, texto_cabecalho_dir)

    y_linha = y_obs - 5 * mm
    c.setLineWidth(0.8)
    c.line(margem, y_linha, largura - margem, y_linha)

    y_prazo = y_linha - 7 * mm
    c.setFont("Helvetica-Bold", fonte_texto)
    c.drawString(margem, y_prazo, "PRAZO DE ENTREGA:")
    c.setLineWidth(0.4)
    c.line(margem + 38 * mm, y_prazo - 1, largura - margem, y_prazo - 1)

    c.setLineWidth(0.8)
    c.line(margem, y_prazo - 4 * mm, largura - margem, y_prazo - 4 * mm)
    return y_prazo - 8 * mm


def _desenhar_linha(
    c: canvas.Canvas,
    linha: LinhaRelatorio | None,
    x0: float,
    y_topo: float,
    largura_util: float,
    altura_linha: float,
    *,
    largura_desenho_pct: float = 0.64,
):
    largura_desenho = largura_util * largura_desenho_pct
    largura_dados = largura_util * 0.38
    x_dados = x0 + largura_desenho + 4 * mm
    y_base = y_topo - altura_linha

    cfg = obter_config()
    fonte_texto = float(cfg.get("relatorio_pedido_fonte_texto", 9.0))
    fonte_destaque = float(cfg.get("relatorio_pedido_fonte_destaque", 10.0))
    fonte_rotulo_peca = float(cfg.get("relatorio_pedido_fonte_rotulo_peca", 7.0))
    fonte_rotulo_campo = float(cfg.get("relatorio_pedido_fonte_rotulo_campo", 8.0))

    c.setLineWidth(0.6)
    c.setStrokeColor(colors.HexColor("#888888"))
    c.rect(x0, y_base, largura_desenho, altura_linha - 2 * mm, stroke=1, fill=0)

    if linha and linha.imagem is not None:
        buffer = BytesIO()
        imagem_rgb = linha.imagem.convert("RGB")
        imagem_rgb.save(buffer, format="PNG")
        buffer.seek(0)
        img = ImageReader(buffer)
        padding = 1 * mm
        box_w = largura_desenho - 2 * padding
        box_h = altura_linha - 2 * mm - 2 * padding
        box_x = x0 + padding
        box_y = y_base + padding
        c.drawImage(
            img,
            box_x,
            box_y,
            width=box_w,
            height=box_h,
            preserveAspectRatio=True,
            anchor="c",
        )
        if linha.nome_peca:
            c.setFont("Helvetica-Oblique", fonte_rotulo_peca)
            c.setFillColor(colors.HexColor("#444444"))
            c.drawString(x0 + 2 * mm, y_base + altura_linha - 5 * mm, linha.nome_peca[:40])
            c.setFillColor(colors.black)
        if getattr(linha, "observacao", ""):
            c.setFont("Helvetica-Oblique", fonte_rotulo_peca)
            c.setFillColor(colors.HexColor("#555555"))
            c.drawString(x0 + 2 * mm, y_base + 2 * mm, f"Obs: {linha.observacao[:65]}")
            c.setFillColor(colors.black)

    if linha is None:
        return

    c.setStrokeColor(colors.black)
    c.setFont("Helvetica-Bold", fonte_texto)
    y = y_topo - 5 * mm
    c.drawString(x_dados, y, "x")
    c.setFont("Helvetica", fonte_destaque)
    c.drawString(x_dados + 5 * mm, y, _formatar_numero(linha.quantidade))

    c.setFont("Helvetica-Bold", fonte_texto)
    c.drawString(x_dados + 18 * mm, y, "#")
    c.setFont("Helvetica", fonte_destaque)
    c.drawString(x_dados + 22 * mm, y, linha.codigo_chapa)

    campos = [
        ("Corte:", _formatar_numero(linha.corte_mm), True),
        ("Comp:", _formatar_numero(linha.comprimento_mm), False),
        ("Kg:", "" if linha.peso_kg is None else str(linha.peso_kg), False),
    ]
    y_campo = y - 7 * mm
    for rotulo, valor, destaque_vermelho in campos:
        c.setFont("Helvetica-Bold", fonte_rotulo_campo)
        c.setFillColor(colors.black)
        c.drawString(x_dados, y_campo, rotulo)
        if destaque_vermelho:
            c.setFont("Helvetica-Bold", fonte_destaque)
            c.setFillColor(colors.red)
        else:
            c.setFont("Helvetica", fonte_texto)
            c.setFillColor(colors.black)
        c.drawString(x_dados + 14 * mm, y_campo, valor)
        y_campo -= 6 * mm

    c.setFillColor(colors.black)

    c.setFont("Helvetica", fonte_texto)
    c.drawString(x_dados + largura_dados - 18 * mm, y - 7 * mm, "[")
    c.drawString(x_dados + largura_dados - 14 * mm, y - 7 * mm, " ] Cort.")
    c.setLineWidth(0.5)
    c.rect(x_dados + largura_dados - 16.5 * mm, y - 8.5 * mm, 3.5 * mm, 3.5 * mm)


def gerar_pdf_pedido(
    pecas: list,
    caminho_saida: Path | str,
    *,
    chapas: list[dict] | None = None,
    observacao: str | None = None,
) -> Path:
    cfg = obter_config().copy()
    if observacao is not None:
        cfg["relatorio_observacao"] = observacao

    linhas: list[LinhaRelatorio] = []
    for peca in pecas:
        linha = montar_linha_relatorio(peca, chapas or [])
        if linha is not None:
            linhas.append(linha)

    caminho = Path(caminho_saida)
    caminho.parent.mkdir(parents=True, exist_ok=True)

    largura, altura = A4
    pecas_por_pagina = int(cfg.get("relatorio_pecas_por_pagina", 9))
    largura_desenho_pct = float(cfg.get("relatorio_largura_desenho_pct", 0.64))
    margem = 12 * mm
    largura_util = largura - 2 * margem

    c = canvas.Canvas(str(caminho), pagesize=A4)
    c.setTitle(f"Ordem de produção — {cfg.get('relatorio_nome_responsavel', '')}")

    if not linhas:
        raise ValueError("Nenhuma peça válida para o relatório.")

    import math
    total = len(linhas)
    total_paginas = math.ceil(total / pecas_por_pagina)
    pagina_atual = 1

    indice = 0
    total = len(linhas)
    while True:
        y_topo = _desenhar_cabecalho(c, largura, altura, cfg, pagina_atual, total_paginas)
        altura_linhas = y_topo - margem
        altura_linha = altura_linhas / pecas_por_pagina

        for slot in range(pecas_por_pagina):
            linha = linhas[indice] if indice < total else None
            y_slot = y_topo - slot * altura_linha
            _desenhar_linha(
                c,
                linha,
                margem,
                y_slot,
                largura_util,
                altura_linha,
                largura_desenho_pct=largura_desenho_pct,
            )
            if indice < total:
                indice += 1

        c.showPage()
        pagina_atual += 1
        if indice >= total:
            break

    c.save()
    return caminho


def nome_arquivo_pedido_padrao() -> str:
    agora = datetime.now()
    return f"ORDEM_PRODUCAO_{agora:%Y%m%d_%H%M%S}.pdf"
