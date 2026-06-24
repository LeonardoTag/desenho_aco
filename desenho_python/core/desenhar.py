from PIL import Image, ImageDraw, ImageFont
from math import radians, sin, cos, tan, pi, hypot, ceil
from functools import lru_cache
import csv

from config_manager import caminho_chapas, caminho_fonte_desenho, obter_config


def _cfg():
    return obter_config()


def _font_location():
    return caminho_fonte_desenho()


def _margem_desenho():
    return _cfg()["margem_desenho_pct"]


def _margem_fundo():
    return _cfg()["margem_fundo"]


def _casas_decimais_calculo():
    return _cfg()["casas_decimais_calculo"]


def _casas_decimais_mostradas():
    return _cfg()["casas_decimais_mostradas"]


def _supersampling():
    return max(1, int(_cfg().get("desenho_supersampling", 2)))


_PALETA = {
    "fundo_pagina": (246, 248, 251),
    "area_desenho": (255, 255, 255),
    "moldura": (214, 220, 228),
    "perfil_contorno": (38, 50, 66),
    "perfil_preenchimento": (176, 190, 208),
    "perfil_brilho": (210, 220, 232),
    "extrusao_face": (218, 225, 234),
    "extrusao_face_sombra": (198, 206, 218),
    "extrusao_aresta": (130, 142, 158),
    "extrusao_ligacao": (150, 160, 174),
    "extrusao_marca_dobra": (110, 122, 140),
    "cota_externa": (21, 101, 192),
    "cota_externa_fundo": (232, 241, 252),
    "cota_externa_borda": (144, 182, 230),
    "cota_interna": (183, 28, 28),
    "cota_interna_fundo": (253, 236, 236),
    "cota_interna_borda": (229, 154, 154),
    "cota_linha": (160, 170, 184),
    "cota_angulo": (118, 128, 142),
    "cota_angulo_fundo": (245, 247, 250),
    "cota_angulo_borda": (210, 218, 228),
}


def _estilo_desenho(destino: str = "preview"):
    cfg = _cfg()
    return {
        "fator_comprimento": float(cfg.get("desenho_fator_comprimento", 0.35)),
        "angulo_comprimento": float(cfg.get("desenho_angulo_comprimento", 55)),
        "cores": _paleta_cotas(destino),
    }


def _usa_estilo_relatorio(destino: str) -> bool:
    return destino in ("relatorio", "detalhamento_dobra")


def _paleta_cotas(destino: str = "preview") -> dict:
    if not _usa_estilo_relatorio(destino):
        return _PALETA
    return {
        **_PALETA,
        "cota_externa": (18, 55, 105),
        "cota_externa_fundo": (248, 249, 252),
        "cota_externa_borda": (120, 150, 190),
        "cota_interna": (255, 190, 190),
        "cota_interna_fundo": (100, 12, 12),
        "cota_interna_borda": (140, 35, 35),
    }


def _fator_distancia_cotas(destino: str = "preview") -> float:
    cfg = _cfg()
    if _usa_estilo_relatorio(destino):
        return float(cfg.get("desenho_cota_distancia_relatorio", 0.82))
    return float(cfg.get("desenho_cota_distancia_preview", 0.88))


def _tamanho_fonte_cota(tamanho_render: int, destino: str = "preview") -> int:
    cfg = _cfg()
    base_min = float(cfg.get("desenho_fonte_base_minima", 12.0))
    base_fator = float(cfg.get("desenho_fonte_base_fator", 0.028))
    base = max(base_min, int(round(tamanho_render * base_fator)))
    if _usa_estilo_relatorio(destino):
        fator = float(cfg.get("desenho_fonte_relatorio_fator", 1.55))
        if destino == "detalhamento_dobra":
            fator *= float(cfg.get("desenho_fonte_detalhamento_dobra_fator", 0.8)) * 0.85
            dobra_min = float(cfg.get("desenho_fonte_dobra_minima", 11.0))
            return max(int(round(dobra_min)), int(round(base * fator)))
        relatorio_min = float(cfg.get("desenho_fonte_relatorio_minima", 13.0))
        return max(int(round(relatorio_min)), int(round(base * fator)))
    return int(round(base))


def _margens_tentativa_layout(margem_usada: float, mostrar_medidas: bool) -> list[float]:
    if not mostrar_medidas:
        return [margem_usada]
    valores: list[float] = []
    atual = max(margem_usada, 0.12)
    while atual <= 0.48:
        valores.append(round(atual, 4))
        atual += 0.04
    return valores


def _fator_escala_minima_tentativa(indice: int, total: int) -> float:
    if total <= 1:
        return 1.0
    restantes = total - 1 - indice
    if restantes <= 0:
        return 0.72
    if restantes == 1:
        return 0.82
    if restantes == 2:
        return 0.9
    return 1.0


def _flatten(pontos):
    resultado = []
    for ponto in pontos:
        resultado.extend(ponto)
    return resultado


def _polilinha_espessa(draw, pontos, espessura, cor, contorno=None, margem_contorno=1):
    if len(pontos) < 2:
        return
    largura = max(1, int(round(espessura)))
    coords = _flatten(pontos)
    if contorno is not None and margem_contorno > 0:
        draw.line(
            coords,
            fill=contorno,
            width=largura + margem_contorno * 2,
            joint="curve",
        )
    draw.line(coords, fill=cor, width=largura, joint="curve")


def _desenhar_fundo_pagina(draw, tamanho_canvas, cores):
    largura, altura = tamanho_canvas
    draw.rectangle((0, 0, largura, altura), fill=cores["fundo_pagina"])
    margem = max(6, int(min(largura, altura) * 0.018))
    draw.rounded_rectangle(
        (margem, margem, largura - margem, altura - margem),
        radius=max(4, margem // 2),
        fill=cores["area_desenho"],
        outline=cores["moldura"],
        width=1,
    )


def _coordenadas_extrusao(coordenadas_perfil, comprimento_px, angulo_graus):
    angulo = radians(angulo_graus)
    dx = comprimento_px * cos(angulo)
    dy = -comprimento_px * sin(angulo)
    return [(x + dx, y + dy) for x, y in coordenadas_perfil]


def _ponto_interpolado(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def _distancia_ponto_segmento(p, a, b):
    px, py = p
    ax, ay = a
    bx, by = b
    dx = bx - ax
    dy = by - ay
    if dx == 0 and dy == 0:
        return hypot(px - ax, py - ay)
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    proj_x = ax + t * dx
    proj_y = ay + t * dy
    return hypot(px - proj_x, py - proj_y)


def _t_entrada_capsula(origem, destino, sa, sb, raio):
    menor_t = None
    passos = 24
    for i in range(1, passos + 1):
        t = i / passos
        ponto = _ponto_interpolado(origem, destino, t)
        if _distancia_ponto_segmento(ponto, sa, sb) <= raio:
            menor_t = t if menor_t is None else min(menor_t, t)
    if menor_t is None:
        return None
    for _ in range(8):
        passo = 1 / (passos * 2)
        t_teste = max(0.0, menor_t - passo)
        ponto = _ponto_interpolado(origem, destino, t_teste)
        if _distancia_ponto_segmento(ponto, sa, sb) <= raio:
            menor_t = t_teste
        else:
            break
    return menor_t


def _ponto_dentro_perfil(p, coords, raio, vertice_permitido=None, pontos_permitidos=None):
    margem_vertice = raio * 1.35
    permitidos = list(pontos_permitidos or [])
    if vertice_permitido is not None and 0 <= vertice_permitido < len(coords):
        permitidos.append(coords[vertice_permitido])
    for ref in permitidos:
        if hypot(p[0] - ref[0], p[1] - ref[1]) <= margem_vertice:
            return False
    for seg in range(len(coords) - 1):
        if _distancia_ponto_segmento(p, coords[seg], coords[seg + 1]) > raio:
            continue
        return True
    return False


def _ponto_dentro_cavidade_perfil(p, coords, margem=0.5):
    """Região interna aberta entre os segmentos (ex.: vão de um perfil em U)."""
    if len(coords) < 3:
        return False
    xs = [c[0] for c in coords]
    ys = [c[1] for c in coords]
    if not (
        min(xs) - margem <= p[0] <= max(xs) + margem
        and min(ys) - margem <= p[1] <= max(ys) + margem
    ):
        return False
    for n in range(len(coords) - 1):
        x0, y0 = coords[n]
        x1, y1 = coords[n + 1]
        lado = determinar_lado_interno_segmento(n, coords)
        nx, ny = _normal_unitaria_esquerda(x1 - x0, y1 - y0)
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        if (p[0] - mx) * nx * lado + (p[1] - my) * ny * lado < margem:
            return False
    return True


def _ponto_oculto_linha_comprimento(p, coords, raio, a, b, tol_extremo=2.5):
    if hypot(p[0] - a[0], p[1] - a[1]) <= tol_extremo:
        return False
    if hypot(p[0] - b[0], p[1] - b[1]) <= tol_extremo:
        return False
    if _ponto_dentro_cavidade_perfil(p, coords):
        return True
    for seg in range(len(coords) - 1):
        if _distancia_ponto_segmento(p, coords[seg], coords[seg + 1]) <= raio:
            return True
    return False


def _trechos_linha_comprimento(a, b, coords, raio, passos=96):
    if hypot(b[0] - a[0], b[1] - a[1]) < 1:
        return []
    visivel = []
    trecho_inicio = None
    for i in range(passos + 1):
        t = i / passos
        p = _ponto_interpolado(a, b, t)
        oculto = _ponto_oculto_linha_comprimento(p, coords, raio, a, b)
        if not oculto:
            if trecho_inicio is None:
                trecho_inicio = t
        elif trecho_inicio is not None:
            visivel.append((_ponto_interpolado(a, b, trecho_inicio), p))
            trecho_inicio = None
    if trecho_inicio is not None:
        visivel.append((_ponto_interpolado(a, b, trecho_inicio), b))
    return visivel


def _stub_comprimento_visivel(origem, destino, coords, raio, fracao_max=0.5):
    dx, dy = destino[0] - origem[0], destino[1] - origem[1]
    comp = hypot(dx, dy)
    if comp < 1:
        return []
    ux, uy = dx / comp, dy / comp
    limite = comp * fracao_max
    ultimo = origem
    passos = 48
    for i in range(1, passos + 1):
        dist = limite * i / passos
        p = (origem[0] + ux * dist, origem[1] + uy * dist)
        if _ponto_oculto_linha_comprimento(p, coords, raio, origem, destino):
            break
        ultimo = p
    if hypot(ultimo[0] - origem[0], ultimo[1] - origem[1]) < 1.5:
        return []
    return [(origem, ultimo)]


def _trechos_anexados_ancora(trechos, ancora, raio):
    tol = max(8.0, raio * 0.85)
    anexados = []
    for ini, fim in trechos:
        if (
            hypot(ini[0] - ancora[0], ini[1] - ancora[1]) <= tol
            or hypot(fim[0] - ancora[0], fim[1] - ancora[1]) <= tol
        ):
            anexados.append((ini, fim))
    return anexados


def _trechos_comprimento_dobra(
    perfil, extrusao, coordenadas_perfil, espessura_perfil_px
):
    raio = _raio_clip_perfil(espessura_perfil_px)
    trechos = _trechos_anexados_ancora(
        _trechos_linha_comprimento(perfil, extrusao, coordenadas_perfil, raio),
        perfil,
        raio,
    )
    if trechos:
        return trechos
    return _stub_comprimento_visivel(
        perfil, extrusao, coordenadas_perfil, raio, fracao_max=0.5
    )


def _trechos_comprimento_borda(
    inicio, fim, coordenadas_perfil, espessura_perfil_px
):
    raio = _raio_clip_perfil(espessura_perfil_px)
    trechos = _trechos_anexados_ancora(
        _trechos_linha_comprimento(inicio, fim, coordenadas_perfil, raio),
        inicio,
        raio,
    )
    if trechos:
        return trechos
    return _stub_comprimento_visivel(
        inicio, fim, coordenadas_perfil, raio, fracao_max=0.42
    )


def _trechos_linha_fora_perfil(
    a, b, coords, raio, vertice_permitido=None, pontos_permitidos=None, passos=72
):
    if hypot(b[0] - a[0], b[1] - a[1]) < 1:
        return []
    visivel = []
    trecho_inicio = None
    for i in range(passos + 1):
        t = i / passos
        p = _ponto_interpolado(a, b, t)
        oculto = _ponto_dentro_perfil(
            p, coords, raio, vertice_permitido, pontos_permitidos
        )
        if not oculto:
            if trecho_inicio is None:
                trecho_inicio = t
        elif trecho_inicio is not None:
            visivel.append((_ponto_interpolado(a, b, trecho_inicio), p))
            trecho_inicio = None
    if trecho_inicio is not None:
        visivel.append((_ponto_interpolado(a, b, trecho_inicio), b))
    return visivel


def _desenhar_marca_dobra_comprimento(draw, perfil, extrusao, espessura_aresta, cores):
    dx = perfil[0] - extrusao[0]
    dy = perfil[1] - extrusao[1]
    if hypot(dx, dy) < 1:
        return
    tx, ty = dx / hypot(dx, dy), dy / hypot(dx, dy)
    nx, ny = _normal_unitaria_esquerda(tx, ty)
    meia = max(2.5, espessura_aresta * 0.52)
    px, py = perfil
    tick = (
        (px - nx * meia, py - ny * meia),
        (px + nx * meia, py + ny * meia),
    )
    cor = cores.get("extrusao_marca_dobra", cores["extrusao_aresta"])
    draw.line(_flatten(tick), fill=cor, width=max(2, int(round(espessura_aresta * 0.5))))
    raio = max(2.0, espessura_aresta * 0.3)
    draw.ellipse(
        (px - raio, py - raio, px + raio, py + raio),
        fill=cor,
    )


def _eixos_ponta_livre(tangente_x, tangente_y):
    comprimento = hypot(tangente_x, tangente_y)
    if comprimento == 0:
        return None
    tx, ty = tangente_x / comprimento, tangente_y / comprimento
    nx, ny = _normal_unitaria_esquerda(tx, ty)
    return tx, ty, nx, ny


def _face_ponta_livre(ponta, extrusao, tangente_x, tangente_y, espessura_px):
    eixos = _eixos_ponta_livre(tangente_x, tangente_y)
    if eixos is None:
        return None
    tx, ty, nx, ny = eixos
    meia = espessura_px / 2
    pf_m = (ponta[0] - nx * meia, ponta[1] - ny * meia)
    pf_p = (ponta[0] + nx * meia, ponta[1] + ny * meia)
    pb_m = (extrusao[0] - nx * meia, extrusao[1] - ny * meia)
    pb_p = (extrusao[0] + nx * meia, extrusao[1] + ny * meia)
    return pf_m, pf_p, pb_m, pb_p, tx, ty, nx, ny


def _ligacao_comprimento_borda_livre(
    draw,
    ponta,
    extrusao,
    tangente_x,
    tangente_y,
    espessura_px,
    espessura_aresta,
    cores,
    coordenadas_perfil,
):
    face = _face_ponta_livre(ponta, extrusao, tangente_x, tangente_y, espessura_px)
    if face is None:
        return
    pf_m, pf_p, pb_m, pb_p, _, _, _, _ = face
    espessura_borda = max(2, int(round(espessura_aresta * 0.85)))
    trechos_visiveis = []
    for inicio, fim in (
        (pf_m, pb_m),
        (pf_p, pb_p),
    ):
        trechos = _trechos_comprimento_borda(
            inicio, fim, coordenadas_perfil, espessura_px
        )
        trechos_visiveis.extend(trechos)
        for a, b in trechos:
            linha(draw, a, b, espessura_borda, cores["extrusao_aresta"])
    if trechos_visiveis:
        preencher(draw, [pf_m, pf_p, pb_p, pb_m], cores["extrusao_ligacao"])


def _arremate_borda_livre(draw, ponta, tangente_x, tangente_y, espessura_px, cores):
    """Fecha a ponta aberta com um semicírculo na mesma espessura do perfil."""
    eixos = _eixos_ponta_livre(tangente_x, tangente_y)
    if eixos is None:
        return
    tx, ty, nx, ny = eixos
    cx, cy = ponta
    meia = espessura_px / 2

    arco = []
    for i in range(17):
        ang = pi - pi * i / 16
        arco.append(
            (
                cx + meia * (nx * cos(ang) + tx * sin(ang)),
                cy + meia * (ny * cos(ang) + ty * sin(ang)),
            )
        )

    preencher(draw, arco, cores["perfil_preenchimento"])
    contorno = max(1, int(round(meia * 0.28)))
    draw.line(_flatten(arco), fill=cores["perfil_contorno"], width=contorno, joint="curve")


def _desenhar_bordas_livres(
    draw,
    coordenadas_perfil,
    coordenadas_extrusao,
    espessura_px,
    espessura_aresta,
    cores,
):
    if len(coordenadas_perfil) < 2:
        return
    ultimo = len(coordenadas_perfil) - 1
    bordas = [
        (
            0,
            coordenadas_perfil[0][0] - coordenadas_perfil[1][0],
            coordenadas_perfil[0][1] - coordenadas_perfil[1][1],
        ),
        (
            ultimo,
            coordenadas_perfil[ultimo][0] - coordenadas_perfil[ultimo - 1][0],
            coordenadas_perfil[ultimo][1] - coordenadas_perfil[ultimo - 1][1],
        ),
    ]
    dx = coordenadas_extrusao[0][0] - coordenadas_perfil[0][0]
    dy = coordenadas_extrusao[0][1] - coordenadas_perfil[0][1]
    abs_dy = abs(dy)
    # Sort by depth descending (furthest first, closest last)
    bordas = sorted(bordas, key=lambda b: coordenadas_perfil[b[0]][1] * abs_dy - coordenadas_perfil[b[0]][0] * dx, reverse=True)
    
    for indice, tx, ty in bordas:
        perfil = coordenadas_perfil[indice]
        extrusao = coordenadas_extrusao[indice]
        _ligacao_comprimento_borda_livre(
            draw,
            perfil,
            extrusao,
            tx,
            ty,
            espessura_px,
            espessura_aresta,
            cores,
            coordenadas_perfil,
        )
        _arremate_borda_livre(draw, perfil, tx, ty, espessura_px, cores)


def consultar_chapa(chapa):
    with open(caminho_chapas(), newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f, delimiter=",")
        for i in reader:
            if i["codigo"] == chapa:
                chapa_return = {
                    "codigo": i["codigo"],
                    "espessura": float(i["espessura"]),
                    "raio_de_dobra": float(i["raio_de_dobra"]),
                    "k_factor": float(i["k_factor"]),
                    "coeficiente": float(i["coeficiente"]),
                    "dobra_minima": float(i.get("dobra_minima") or 0),
                    "tipo": i["tipo"],
                }
                return chapa_return
        else:
            raise Exception("Chapa não encontrada.")


def gerar_coordenadas_retangulares_parciais(coordenadas_polares):
    coordenadas_retangulares_parciais = list()
    for azimute, dh in coordenadas_polares:
        x_parc = sin(radians(azimute)) * dh
        y_parc = -cos(radians(azimute)) * dh
        coordenadas_retangulares_parciais.append((x_parc, y_parc))
    return coordenadas_retangulares_parciais


def gerar_coordenadas_retangulares_absolutas(coordenadas_retangulares_parciais):
    coordenadas_retangulares_absolutas = [
        (0, 0),
    ]
    for x_parc, y_parc in coordenadas_retangulares_parciais:
        x_atual, y_atual = coordenadas_retangulares_absolutas[-1]
        coordenada_absolute = (x_atual + x_parc, y_atual + y_parc)
        coordenadas_retangulares_absolutas.append(coordenada_absolute)

    return coordenadas_retangulares_absolutas


def _gerar_pontos_arco(x_start, y_start, az_start, R_n, A, is_cw, num_points=30):
    import math
    A_rad = radians(A)
    if is_cw:
        xc = x_start + R_n * cos(radians(az_start))
        yc = y_start + R_n * sin(radians(az_start))
    else:
        xc = x_start - R_n * cos(radians(az_start))
        yc = y_start - R_n * sin(radians(az_start))

    phi_start = math.atan2(y_start - yc, x_start - xc)

    pontos = []
    for i in range(1, num_points + 1):
        t = A_rad * (i / num_points)
        if is_cw:
            angle = phi_start + t
        else:
            angle = phi_start - t
        px = xc + R_n * cos(angle)
        py = yc + R_n * sin(angle)
        pontos.append((px, py))
    return pontos


def _gerar_coordenadas_absolutas_discretas(instrucoes):
    segmentos = instrucoes.get("segmentos_original")
    if not segmentos:
        return None

    espessura = instrucoes.get("espessura", 1.0)
    k_factor = instrucoes.get("k_factor", 0.44)

    # 1. Get azimutes
    azimutes = _azimutes_de_segmentos(segmentos)

    absolutas_discretas = [(0.0, 0.0)]
    x_curr, y_curr = 0.0, 0.0

    import math
    for n, seg in enumerate(segmentos):
        is_curv = _eh_segmento_curvo(seg)
        if is_curv:
            info = seg[5]
            r = float(info["raio"])
            a = float(info["angulo_curva"])
            tipo_r = info["tipo_raio"]

            if tipo_r == "interno":
                r_i = r
            else:
                r_i = r - espessura

            r_n = r_i + k_factor * espessura

            if n > 0:
                diff = (azimutes[n] - azimutes[n-1]) % 360
                is_cw = (diff < 180)
                az_start = azimutes[n-1]
            else:
                if len(segmentos) > 1:
                    diff = (azimutes[1] - azimutes[0]) % 360
                    is_cw = (diff < 180)
                else:
                    is_cw = True
                A_signed = a if is_cw else -a
                az_start = (azimutes[0] - A_signed) % 360

            num_points = 30
            pontos_arco = _gerar_pontos_arco(x_curr, y_curr, az_start, r_n, a, is_cw, num_points)
            absolutas_discretas.extend(pontos_arco)
            x_curr, y_curr = pontos_arco[-1]
        else:
            dh = instrucoes["coordenadas_polares"][n][1]
            az = instrucoes["coordenadas_polares"][n][0]
            x_next = x_curr + sin(radians(az)) * dh
            y_next = y_curr - cos(radians(az)) * dh
            absolutas_discretas.append((x_next, y_next))
            x_curr, y_curr = x_next, y_next

    return absolutas_discretas



def _orientacao2d(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _segmentos_centro_cruzam(a1, a2, b1, b2):
    o1 = _orientacao2d(a1, a2, b1)
    o2 = _orientacao2d(a1, a2, b2)
    o3 = _orientacao2d(b1, b2, a1)
    o4 = _orientacao2d(b1, b2, a2)
    if o1 == 0 and o2 == 0 and o3 == 0 and o4 == 0:
        return False
    return (o1 > 0) != (o2 > 0) and (o3 > 0) != (o4 > 0)


def coordenadas_centro_de_instrucoes_convencionais(instrucoes_convencionais):
    instrucoes = converter_instrucoes_convencionais_para_coordenadas_polares(
        instrucoes_convencionais
    )
    parciais = gerar_coordenadas_retangulares_parciais(instrucoes["coordenadas_polares"])
    return gerar_coordenadas_retangulares_absolutas(parciais)


def perfil_cruza_a_si_mesmo(instrucoes_convencionais) -> bool:
    try:
        coords = coordenadas_centro_de_instrucoes_convencionais(instrucoes_convencionais)
    except Exception:
        return False
    n = len(coords) - 1
    if n < 3:
        return False
    for i in range(n):
        for j in range(i + 2, n):
            if i == 0 and j == n - 1:
                continue
            if _segmentos_centro_cruzam(
                coords[i], coords[i + 1], coords[j], coords[j + 1]
            ):
                return True
    return False


def verificar_dobras_abaixo_minima(instrucoes, codigo_chapa: str) -> list[str]:
    """Avisa quando a medida interna de um segmento fica abaixo de dobra_minima da chapa."""
    chapa = consultar_chapa(
        codigo_chapa if codigo_chapa.startswith("#") else f"#{codigo_chapa.lstrip('#')}"
    )
    minima = chapa.get("dobra_minima") or 0
    if minima <= 0:
        return []
    _, medidas, _ = gerar_corte_medidas_angulos(instrucoes)
    avisos = []
    for indice, medida in enumerate(medidas, start=1):
        interna = medida.get("interna")
        if interna == "" or interna is None:
            continue
        if float(interna) < minima:
            avisos.append(
                f"Segmento {indice}: medida interna {formatar_texto_medida(interna)} mm "
                f"(mínimo da chapa: {formatar_texto_medida(minima)} mm)"
            )
    return avisos


def gerar_dimensoes_totais_e_ponto_inicial(coordenadas_retangulares_absolutas):
    maior_x = menor_x = maior_y = menor_y = 0

    cont = 0
    for x, y in coordenadas_retangulares_absolutas:
        cont += 1
        if cont == 1:
            maior_x = menor_x = x
            maior_y = menor_y = y
        else:
            if x > maior_x:
                maior_x = x
            elif x < menor_x:
                menor_x = x
            if y > maior_y:
                maior_y = y
            elif y < menor_y:
                menor_y = y

    dimensao_horizontal = maior_x - menor_x
    dimensao_vertical = maior_y - menor_y

    return ((dimensao_horizontal, dimensao_vertical), (menor_x, menor_y))


def adequar_coordenadas_ao_canvas(
    coordenadas_polares,
    tamanho_canvas,
    margem,
    escala_minima_segmento_px=22,
    instrucoes=None,
):
    if isinstance(margem, (tuple, list)):
        margem_x, margem_y = margem
    else:
        margem_x = margem
        margem_y = margem

    canvas_util = (tamanho_canvas[0] - 2 * margem_x, tamanho_canvas[1] - 2 * margem_y)

    coordenadas_retangulares_parciais = gerar_coordenadas_retangulares_parciais(
        coordenadas_polares
    )
    coordenadas_retangulares_absolutas = gerar_coordenadas_retangulares_absolutas(
        coordenadas_retangulares_parciais
    )

    absolutas_discretas = None
    if instrucoes:
        absolutas_discretas = _gerar_coordenadas_absolutas_discretas(instrucoes)

    ref_absolutas = absolutas_discretas if absolutas_discretas else coordenadas_retangulares_absolutas
    (
        dimensoes_totais,
        ponto_mais_a_norte_e_oeste,
    ) = gerar_dimensoes_totais_e_ponto_inicial(ref_absolutas)

    dim_x = dimensoes_totais[0] or 1
    dim_y = dimensoes_totais[1] or 1
    escala = min(canvas_util[0] / dim_x, canvas_util[1] / dim_y)

    segmentos_mm = [dh for _, dh in coordenadas_polares if dh > 0]
    if segmentos_mm:
        escala_min = escala_minima_segmento_px / min(segmentos_mm)
        if escala < escala_min:
            escala = escala_min

    # Centering math
    dim_x_scaled = dim_x * escala
    dim_y_scaled = dim_y * escala
    fator_x = (canvas_util[0] - dim_x_scaled) / 2
    fator_y = (canvas_util[1] - dim_y_scaled) / 2
    fator_x += margem_x - ponto_mais_a_norte_e_oeste[0] * escala
    fator_y += margem_y - ponto_mais_a_norte_e_oeste[1] * escala

    coordenadas_no_canvas_high_level = [
        (
            round(x * escala + fator_x, _casas_decimais_calculo()),
            round(y * escala + fator_y, _casas_decimais_calculo()),
        )
        for x, y in coordenadas_retangulares_absolutas
    ]

    if instrucoes:
        instrucoes["coordenadas_no_canvas_high_level"] = coordenadas_no_canvas_high_level

    if absolutas_discretas:
        coordenadas_no_canvas_discretas = [
            (
                round(x * escala + fator_x, _casas_decimais_calculo()),
                round(y * escala + fator_y, _casas_decimais_calculo()),
            )
            for x, y in absolutas_discretas
        ]
        return coordenadas_no_canvas_discretas, escala
    else:
        return coordenadas_no_canvas_high_level, escala


def linha(draw, comeco, fim, espessura, cor=(0, 0, 0)):
    espessura = max(1, int(round(espessura)))
    draw.line(comeco + fim, fill=cor, width=espessura)


def preencher(draw, coordenadas, cor=(0, 0, 0)):
    draw.polygon(coordenadas, fill=cor)


def _desenhar_extrusao_3d(
    draw,
    coordenadas_perfil,
    coordenadas_extrusao,
    espessura_aresta,
    espessura_perfil_px,
    cores,
    instrucoes=None,
):
    dx = coordenadas_extrusao[0][0] - coordenadas_perfil[0][0]
    dy = coordenadas_extrusao[0][1] - coordenadas_perfil[0][1]
    abs_dy = abs(dy)

    ultimo = len(coordenadas_perfil) - 1
    
    # Identify which indices in coordenadas_perfil correspond to sharp high-level folds
    sharp_fold_indices = set()
    segmentos_original = None
    if instrucoes:
        high_level_coords = instrucoes.get("coordenadas_no_canvas_high_level", [])
        segmentos_original = instrucoes.get("segmentos_original")
        
        N_segs = len(instrucoes["coordenadas_polares"])
        is_curved = [False] * N_segs
        if segmentos_original:
            for i in range(min(N_segs, len(segmentos_original))):
                is_curved[i] = _eh_segmento_curvo(segmentos_original[i])
                
        if len(high_level_coords) > 2:
            for v in range(1, len(high_level_coords) - 1):
                if not is_curved[v - 1] and not is_curved[v]:
                    hl_pt = high_level_coords[v]
                    best_idx = None
                    best_dist = float("inf")
                    for idx, pt in enumerate(coordenadas_perfil):
                        dist = hypot(pt[0] - hl_pt[0], pt[1] - hl_pt[1])
                        if dist < best_dist:
                            best_dist = dist
                            best_idx = idx
                    if best_idx is not None and best_idx not in (0, len(coordenadas_perfil) - 1):
                        sharp_fold_indices.add(best_idx)

    for indice, (perfil, extrusao) in enumerate(
        zip(coordenadas_perfil, coordenadas_extrusao)
    ):
        if indice in (0, ultimo):
            continue
        if segmentos_original and (indice not in sharp_fold_indices):
            continue

        trechos = _trechos_comprimento_dobra(
            perfil, extrusao, coordenadas_perfil, espessura_perfil_px
        )
        esp = max(2, espessura_aresta * 0.85)
        for inicio, fim in trechos:
            linha(draw, inicio, fim, esp, cores["extrusao_aresta"])
        _desenhar_marca_dobra_comprimento(
            draw, perfil, extrusao, espessura_aresta, cores
        )

    # 1. Main 3D faces (connecting front to back) - Sorted from bottom-most to top-most
    faces_info = []
    for indice, coord in enumerate(coordenadas_perfil):
        try:
            prox = coordenadas_perfil[indice + 1]
            ext_prox = coordenadas_extrusao[indice + 1]
            ext = coordenadas_extrusao[indice]
            face = [coord, prox, ext_prox, ext]
            cor_face = (
                cores["extrusao_face_sombra"]
                if indice % 2
                else cores["extrusao_face"]
            )
            # Find the depth of this segment's midpoint
            mid_x = (coord[0] + prox[0]) / 2.0
            mid_y = (coord[1] + prox[1]) / 2.0
            depth = mid_y * abs_dy - mid_x * dx
            faces_info.append((depth, face, cor_face))
        except IndexError:
            pass

    # Sort faces by depth descending (furthest first)
    faces_info.sort(key=lambda x: x[0], reverse=True)

    for depth, face, cor_face in faces_info:
        preencher(draw, face, cor=cor_face)

    # 2. Free-edges 3D connections (faces and lines)
    _polilinha_espessa(
        draw,
        coordenadas_extrusao,
        espessura_aresta,
        cores["extrusao_aresta"],
        contorno=(255, 255, 255),
        margem_contorno=0,
    )


def _desenhar_geometria_perfil(
    draw,
    coordenadas_no_canvas,
    instrucoes: dict,
    espessura_linha: float,
    *,
    estilo: dict | None = None,
):
    if len(coordenadas_no_canvas) < 2:
        return

    estilo = estilo or _estilo_desenho()
    cores = estilo["cores"]
    comprimento = float(instrucoes.get("comprimento", 0) or 0)
    escala = espessura_linha / max(float(instrucoes.get("espessura", 1.0)), 1e-6)
    espessura_px = max(2.0, espessura_linha)
    comprimento_px = comprimento * escala * estilo["fator_comprimento"]
    coordenadas_extrusao = _coordenadas_extrusao(
        coordenadas_no_canvas,
        comprimento_px,
        estilo["angulo_comprimento"],
    )
    espessura_aresta = max(1.0, espessura_px * 0.45)

    _desenhar_extrusao_3d(
        draw,
        coordenadas_no_canvas,
        coordenadas_extrusao,
        espessura_aresta,
        espessura_px,
        cores,
        instrucoes=instrucoes,
    )

    _polilinha_espessa(
        draw,
        coordenadas_no_canvas,
        espessura_px + 4,
        cores["area_desenho"],
    )

    _polilinha_espessa(
        draw,
        coordenadas_no_canvas,
        espessura_px + 2,
        cores["perfil_contorno"],
    )
    _polilinha_espessa(
        draw,
        coordenadas_no_canvas,
        espessura_px,
        cores["perfil_preenchimento"],
        contorno=cores["perfil_contorno"],
        margem_contorno=1,
    )
    if espessura_px >= 6:
        _polilinha_espessa(
            draw,
            coordenadas_no_canvas,
            max(1.0, espessura_px * 0.22),
            cores["perfil_brilho"],
        )

    _desenhar_bordas_livres(
        draw,
        coordenadas_no_canvas,
        coordenadas_extrusao,
        espessura_px,
        espessura_aresta,
        cores,
    )


def desenhar(draw, instrucoes, tamanho_canvas, margem, escala_minima_segmento_px=22):
    estilo = _estilo_desenho()
    cores = estilo["cores"]
    if isinstance(margem, (tuple, list)):
        margem_px = margem
    else:
        margem_px = (tamanho_canvas[0] * margem, tamanho_canvas[1] * margem)
    coordenadas_polares = instrucoes["coordenadas_polares"]
    comprimento = instrucoes["comprimento"]
    espessura = instrucoes["espessura"]

    _desenhar_fundo_pagina(draw, tamanho_canvas, cores)

    coordenadas_no_canvas, escala = adequar_coordenadas_ao_canvas(
        coordenadas_polares,
        tamanho_canvas,
        margem_px,
        escala_minima_segmento_px=escala_minima_segmento_px,
    )

    if len(coordenadas_no_canvas) < 2:
        return coordenadas_no_canvas, espessura * escala

    espessura_linha = espessura * escala
    _desenhar_geometria_perfil(
        draw,
        coordenadas_no_canvas,
        instrucoes,
        espessura_linha,
        estilo=estilo,
    )
    return coordenadas_no_canvas, espessura_linha


def get_bend_allowance(angulo_dobra, raio_de_dobra, k_factor, espessura):
    return angulo_dobra * (pi / 180) * (raio_de_dobra + (k_factor * espessura))


def gerar_medida_interna_externa(medida_livre, angulos_dobra, raio_de_dobra, espessura):
    soma_interna = 0
    soma_externa = 0
    for angulo_dobra in angulos_dobra:
        if angulo_dobra < 90:
            soma_interna += (angulo_dobra * pi * raio_de_dobra) / 360
            soma_externa += (angulo_dobra * pi * (raio_de_dobra + espessura)) / 360
        else:
            soma_interna += raio_de_dobra
            soma_externa += raio_de_dobra + espessura

    medida_interna = medida_livre + soma_interna
    medida_externa = medida_livre + soma_externa
    return medida_interna, medida_externa


def _normalizar_medidas_cota_90(medida_interna, medida_externa, angulos_dobra, espessura):
    """Regra de chapa 90°: ponta = externa − t; meio = externa − 2t."""
    if not angulos_dobra or not all(abs(angulo - 90) < 0.5 for angulo in angulos_dobra):
        return medida_interna, medida_externa
    casas = _casas_decimais_calculo()
    if len(angulos_dobra) == 1:
        medida_interna = round(medida_externa - espessura, casas)
    elif len(angulos_dobra) == 2:
        medida_interna = round(medida_externa - 2 * espessura, casas)
    return medida_interna, medida_externa


def gerar_medida_livre(medida, tipo, angulos_dobra, raio_de_dobra, espessura):
    soma_interna = 0
    soma_externa = 0
    for angulo_dobra in angulos_dobra:
        if angulo_dobra < 90:
            soma_interna += (angulo_dobra * pi * raio_de_dobra) / 360
            soma_externa += (angulo_dobra * pi * (raio_de_dobra + espessura)) / 360
        else:
            soma_interna += raio_de_dobra
            soma_externa += raio_de_dobra + espessura

    if tipo == "i":
        medida_livre = medida - soma_interna
    elif tipo == "e":
        medida_livre = medida - soma_externa
    else:
        raise Exception("Tipo de medida inválido.")
    return medida_livre


def _listar_dobras_de_coordenadas(coordenadas_polares):
    dobras = []
    for indice in range(1, len(coordenadas_polares)):
        az_0, az_1 = (
            coordenadas_polares[indice - 1][0],
            coordenadas_polares[indice][0],
        )
        az_1_menos_az_0 = az_1 - az_0
        if az_1_menos_az_0 < 0:
            az_1_menos_az_0 += 360
        if az_1_menos_az_0 < 180:
            sentido = "h"
            angulo_dobra = az_1_menos_az_0
        else:
            sentido = "a"
            angulo_dobra = 360 - az_1_menos_az_0
        dobras.append({"angulo_dobra": angulo_dobra, "sentido": sentido})
    return dobras


def _calcular_seccoes_corte(
    coordenadas_polares, dobras, raio_de_dobra, espessura, k_factor, segmentos_original=None
):
    seccoes_corte = []
    if not coordenadas_polares:
        return seccoes_corte

    N = len(coordenadas_polares)
    bend_allowances = [0.0] * (N - 1)
    anterior_deductions = [0.0] * N
    posterior_deductions = [0.0] * N

    is_curved = [False] * N
    if segmentos_original:
        for i in range(min(N, len(segmentos_original))):
            is_curved[i] = _eh_segmento_curvo(segmentos_original[i])

    for j in range(N - 1):
        if is_curved[j] or is_curved[j + 1]:
            bend_allowances[j] = 0.0
            posterior_deductions[j] = 0.0
            anterior_deductions[j + 1] = 0.0
        else:
            angulo_dobra = dobras[j]["angulo_dobra"]
            bend_allowances[j] = get_bend_allowance(
                angulo_dobra, raio_de_dobra, k_factor, espessura
            )
            desconto = tan(radians(angulo_dobra / 2)) * (raio_de_dobra + espessura / 2)
            posterior_deductions[j] = desconto
            anterior_deductions[j + 1] = desconto

    for i in range(N):
        dh = coordenadas_polares[i][1]
        if is_curved[i]:
            medida = segmentos_original[i][2] if (segmentos_original and i < len(segmentos_original)) else dh
            flat_len = medida
        else:
            flat_len = dh - anterior_deductions[i] - posterior_deductions[i]
        
        seccoes_corte.append(round(flat_len, _casas_decimais_calculo()))
        if i < N - 1:
            seccoes_corte.append(round(bend_allowances[i], _casas_decimais_calculo()))

    return seccoes_corte


def gerar_dados_planificacao(instrucoes) -> dict:
    """Planificação (desenvolvimento plano) com trechos, cadeia e cotas ordenadas."""
    coordenadas_polares = instrucoes["coordenadas_polares"]
    espessura = instrucoes["espessura"]
    raio_de_dobra = instrucoes["raio_de_dobra"]
    k_factor = instrucoes["k_factor"]
    dobras = _listar_dobras_de_coordenadas(coordenadas_polares)
    segmentos_original = instrucoes.get("segmentos_original")
    seccoes_corte = _calcular_seccoes_corte(
        coordenadas_polares, dobras, raio_de_dobra, espessura, k_factor, segmentos_original
    )

    N_segs = len(coordenadas_polares)
    is_curved = [False] * N_segs
    if segmentos_original:
        for i in range(min(N_segs, len(segmentos_original))):
            is_curved[i] = _eh_segmento_curvo(segmentos_original[i])

    trechos = []
    indice_dobra = 0
    for indice, valor in enumerate(seccoes_corte):
        if indice % 2 == 0:
            seg_idx = indice // 2
            if is_curved[seg_idx]:
                trechos.append({
                    "tipo": "curvo", 
                    "comprimento": float(valor),
                    "curva_info": segmentos_original[seg_idx][5]
                })
            else:
                trechos.append({"tipo": "reta", "comprimento": float(valor)})
        else:
            dobra = dobras[indice_dobra]
            trechos.append(
                {
                    "tipo": "dobra",
                    "comprimento": float(valor),
                    "angulo_dobra": float(dobra["angulo_dobra"]),
                    "sentido": dobra["sentido"],
                }
            )
            indice_dobra += 1

    acumulado = 0.0
    marcas_dobra = []
    marcas_calandragem = []
    posicoes_referencia = [0.0]
    for trecho in trechos:
        if trecho["tipo"] == "dobra":
            if trecho["comprimento"] > 0:
                centro = acumulado + trecho["comprimento"] / 2
                marcas_dobra.append(
                    {
                        "posicao": centro,
                        "angulo_dobra": trecho["angulo_dobra"],
                        "sentido": trecho["sentido"],
                    }
                )
                posicoes_referencia.append(centro)
        elif trecho["tipo"] == "curvo":
            start_pos = acumulado
            end_pos = acumulado + trecho["comprimento"]
            marcas_calandragem.append({
                "posicao_inicio": start_pos,
                "posicao_fim": end_pos,
                "raio": trecho["curva_info"]["raio"],
                "angulo_curva": trecho["curva_info"]["angulo_curva"],
                "tipo_raio": trecho["curva_info"]["tipo_raio"]
            })
            posicoes_referencia.append(start_pos)
            posicoes_referencia.append(end_pos)

        acumulado += trecho["comprimento"]

    posicoes_referencia.append(acumulado)
    posicoes_referencia = sorted(list(set(posicoes_referencia)))
    
    trechos_cadeia = []
    for indice in range(len(posicoes_referencia) - 1):
        inicio_f = posicoes_referencia[indice]
        fim_f = posicoes_referencia[indice + 1]
        comprimento = int(round(fim_f - inicio_f))
        if comprimento > 0:
            trechos_cadeia.append(
                {
                    "inicio": 0,
                    "fim": 0,
                    "comprimento": comprimento,
                }
            )

    posicoes_ordenadas = [0]
    for trecho in trechos_cadeia:
        posicoes_ordenadas.append(posicoes_ordenadas[-1] + trecho["comprimento"])
    corte_total = posicoes_ordenadas[-1]

    for indice, trecho in enumerate(trechos_cadeia):
        trecho["inicio"] = posicoes_ordenadas[indice]
        trecho["fim"] = posicoes_ordenadas[indice + 1]

    # Map positions to scaled/discrete ones
    def map_pos(raw_pos):
        if acumulado == 0:
            return 0
        return int(round((raw_pos / acumulado) * corte_total))

    for marca in marcas_dobra:
        marca["posicao"] = map_pos(marca["posicao"])

    for mc in marcas_calandragem:
        mc["posicao_inicio"] = map_pos(mc["posicao_inicio"])
        mc["posicao_fim"] = map_pos(mc["posicao_fim"])

    return {
        "corte_total": corte_total,
        "trechos": trechos,
        "trechos_cadeia": trechos_cadeia,
        "cadeia": [t["comprimento"] for t in trechos_cadeia],
        "posicoes_ordenadas": posicoes_ordenadas,
        "dobras": dobras,
        "marcas_dobra": marcas_dobra,
        "marcas_calandragem": marcas_calandragem,
        "espessura": espessura,
        "raio_de_dobra": raio_de_dobra,
    }


def gerar_corte_medidas_angulos(instrucoes):
    coordenadas_polares = instrucoes["coordenadas_polares"]
    comprimento = instrucoes["comprimento"]
    espessura = instrucoes["espessura"]
    raio_de_dobra = instrucoes["raio_de_dobra"]
    k_factor = instrucoes["k_factor"]
    segmentos_original = instrucoes.get("segmentos_original")

    dobras = _listar_dobras_de_coordenadas(coordenadas_polares)
    seccoes_corte = _calcular_seccoes_corte(
        coordenadas_polares, dobras, raio_de_dobra, espessura, k_factor, segmentos_original
    )
    corte = round(sum(seccoes_corte), _casas_decimais_calculo())

    # Identify curved segments
    N_segs = len(coordenadas_polares)
    is_curved = [False] * N_segs
    if segmentos_original:
        for i in range(min(N_segs, len(segmentos_original))):
            is_curved[i] = _eh_segmento_curvo(segmentos_original[i])

    # Gerar medidas para dobra
    medidas_para_dobra = list()
    for n, medida in enumerate(seccoes_corte):
        if n % 2 != 0:  # É uma medida da curva da dobra
            continue
        if n == 0:
            if len(seccoes_corte) > 1:
                medidas_para_dobra.append(
                    round(medida + seccoes_corte[n + 1] / 2, _casas_decimais_calculo())
                )
            else:
                medidas_para_dobra.append(round(medida, _casas_decimais_calculo()))
        elif n == len(seccoes_corte) - 1:
            medidas_para_dobra.append(
                round(seccoes_corte[n - 1] / 2 + medida, _casas_decimais_calculo())
            )
        else:
            medidas_para_dobra.append(
                round(
                    seccoes_corte[n - 1] / 2 + medida + seccoes_corte[n + 1] / 2,
                    _casas_decimais_calculo(),
                )
            )

    # Gerar medidas internas e externas:
    medidas = list()
    cont = 0
    import math
    for n, medida_livre in enumerate(seccoes_corte):  # Iterar pelas medidas livres
        if n % 2 != 0:  # É uma medida da curva da dobra
            continue
        cont += 1
        
        if is_curved[cont - 1]:
            seg = segmentos_original[cont - 1]
            info = seg[5]
            r = float(info["raio"])
            a = float(info["angulo_curva"])
            tipo_r = info["tipo_raio"]

            if tipo_r == "interno":
                r_i = r
            else:
                r_i = r - espessura

            r_e = r_i + espessura
            a_rad = math.radians(a)

            medida_interna = r_i * a_rad
            medida_externa = r_e * a_rad
            angulos_segmento = ()
        else:
            if len(dobras) == 0:
                medida_interna = medida_livre
                medida_externa = medida_livre
                angulos_segmento = ()
            elif cont == 1:  # Primeira medida
                angulos_segmento = (dobras[0]["angulo_dobra"],)
                medida_interna, medida_externa = gerar_medida_interna_externa(
                    medida_livre, angulos_segmento, raio_de_dobra, espessura
                )
            elif cont == len(coordenadas_polares):  # Ultima medida
                angulos_segmento = (dobras[-1]["angulo_dobra"],)
                medida_interna, medida_externa = gerar_medida_interna_externa(
                    medida_livre, angulos_segmento, raio_de_dobra, espessura
                )
            else:
                angulos_segmento = (
                    dobras[cont - 2]["angulo_dobra"],
                    dobras[cont - 1]["angulo_dobra"],
                )
                medida_interna, medida_externa = gerar_medida_interna_externa(
                    medida_livre,
                    angulos_segmento,
                    raio_de_dobra,
                    espessura,
                )
            medida_interna, medida_externa = _normalizar_medidas_cota_90(
                medida_interna, medida_externa, angulos_segmento, espessura
            )
            
        medidas.append(
            {
                "livre": round(medida_livre, _casas_decimais_calculo()),
                "dobra": round(medidas_para_dobra[cont - 1], _casas_decimais_calculo()),
                "interna": round(medida_interna, _casas_decimais_calculo()),
                "externa": round(medida_externa, _casas_decimais_calculo()),
            }
        )

    if len(medidas) == 0:  # Chapa lisa
        medidas = [{"externa": coordenadas_polares[0][1], "interna": ""}]
    return (corte, medidas, dobras)


def calcular_soma_medidas_internas(instrucoes) -> float | None:
    """Somatório das medidas internas (= largura de corte no chão de fábrica)."""
    _, medidas, _ = gerar_corte_medidas_angulos(instrucoes)
    soma = 0.0
    possui_interna = False
    for medida in medidas:
        interna = medida.get("interna")
        if interna == "" or interna is None:
            continue
        soma += float(interna)
        possui_interna = True
    if not possui_interna:
        return None
    return round(soma, _casas_decimais_calculo())


def calcular_largura_corte(instrucoes) -> float | None:
    """Alias explícito: largura de corte = somatório das medidas internas."""
    return calcular_soma_medidas_internas(instrucoes)


def calcular_peso_kg(
    instrucoes,
    quantidade: int = 1,
    *,
    codigo_chapa: str | None = None,
    arredondar: bool = True,
) -> float | int | None:
    """Peso em kg: soma internas × comprimento × coeficiente / 1 000 000."""
    soma_internas = calcular_largura_corte(instrucoes)
    if soma_internas is None:
        return None
    comprimento = instrucoes.get("comprimento")
    if comprimento is None or comprimento <= 0:
        return None
    if not codigo_chapa:
        return None
    codigo = codigo_chapa if codigo_chapa.startswith("#") else f"#{codigo_chapa.lstrip('#')}"
    chapa = consultar_chapa(codigo)
    coeficiente = chapa["coeficiente"]
    if quantidade < 1:
        quantidade = 1
    peso = soma_internas * comprimento * coeficiente / 1_000_000 * quantidade
    if arredondar:
        return int(ceil(peso))
    return round(peso, _casas_decimais_calculo())


def _arredondar_mm_exibicao(valor: float) -> float | int:
    casas = _casas_decimais_mostradas()
    if casas == 0:
        return int(valor + 0.5)
    return round(valor, casas)


def formatar_texto_medida(text):
    if text == "" or text is None:
        return None
    if type(text) != str:
        text = str(_arredondar_mm_exibicao(float(text)))
    return text.replace(".", ",")


def fonte_medidas(font_size):
    return _fonte_medidas_cached(int(font_size))


@lru_cache(maxsize=16)
def _fonte_medidas_cached(font_size):
    return ImageFont.truetype(str(_font_location()), font_size)


@lru_cache(maxsize=256)
def _dimensoes_texto_medida(texto, font_size):
    font = _fonte_medidas_cached(font_size)
    texto_fmt = formatar_texto_medida(texto)
    if texto_fmt is None:
        return None
    textbbox = font.getbbox(texto_fmt)
    return (
        textbbox[2] - textbbox[0],
        textbbox[3] - textbbox[1],
        textbbox[0],
        textbbox[1],
        textbbox[2],
        textbbox[3],
        texto_fmt,
    )


def _texto_cache_key(texto):
    if type(texto) != str:
        return str(_arredondar_mm_exibicao(float(texto)))
    return texto


def calcular_bbox_texto_medida(centro, text, font, x_offset=0, y_offset=0, margem_fundo=1):
    font_size = getattr(font, "size", 18)
    dim = _dimensoes_texto_medida(_texto_cache_key(text), font_size)
    if dim is None:
        return None
    text_width, text_height, tb0, tb1, tb2, tb3, _texto_fmt = dim
    xy = (
        centro[0] + x_offset - text_width // 2,
        centro[1] + y_offset - text_height * 1.5 // 2,
    )
    return (
        xy[0] + tb0 - margem_fundo,
        xy[1] + tb1 - margem_fundo,
        xy[0] + tb2 + margem_fundo,
        xy[1] + tb3 + margem_fundo,
    )


def _bbox_rotulo(centro, texto, font_size, x_offset, y_offset, margem_fundo=1):
    dim = _dimensoes_texto_medida(_texto_cache_key(texto), font_size)
    if dim is None:
        return None
    text_width, text_height, tb0, tb1, tb2, tb3, _ = dim
    xy = (
        centro[0] + x_offset - text_width // 2,
        centro[1] + y_offset - text_height * 1.5 // 2,
    )
    return (
        xy[0] + tb0 - margem_fundo,
        xy[1] + tb1 - margem_fundo,
        xy[0] + tb2 + margem_fundo,
        xy[1] + tb3 + margem_fundo,
    )


def write_using_center_loc(
    draw,
    xy,
    text,
    font,
    color,
    x_offset=0,
    y_offset=0,
    fundo=True,
    cor_fundo=(255, 255, 255),
    cor_borda=None,
    margem_fundo=1,
):
    dim = _dimensoes_texto_medida(_texto_cache_key(text), getattr(font, "size", 18))
    if dim is None:
        return
    text_width, text_height, tb0, tb1, tb2, tb3, texto_fmt = dim
    xy = (xy[0] + x_offset - text_width // 2, xy[1] + y_offset - text_height * 1.5 // 2)
    if fundo:
        caixa = (
            xy[0] + tb0 - margem_fundo,
            xy[1] + tb1 - margem_fundo,
            xy[0] + tb2 + margem_fundo,
            xy[1] + tb3 + margem_fundo,
        )
        raio = max(2, margem_fundo)
        draw.rounded_rectangle(
            caixa,
            radius=raio,
            fill=cor_fundo,
            outline=cor_borda or cor_fundo,
            width=1,
        )
    draw.text(xy, texto_fmt, fill=color, font=font)


def acima_da_funcao(funcao, ponto):
    if ponto[1] > (float(funcao[0]) * float(ponto[0]) + float(funcao[1])):
        return True
    else:
        return False


def calcular_dimensoes_acabadas(instrucoes) -> tuple[float, float] | None:
    coordenadas_polares = instrucoes.get("coordenadas_polares")
    if not coordenadas_polares:
        return None
    espessura = float(instrucoes.get("espessura") or 0)
    parciais = gerar_coordenadas_retangulares_parciais(coordenadas_polares)
    absolutas = gerar_coordenadas_retangulares_absolutas(parciais)
    if espessura > 0 and len(absolutas) >= 2:
        absolutas = _coordenadas_externas_perfil(absolutas, espessura)
    dimensoes, _ = gerar_dimensoes_totais_e_ponto_inicial(absolutas)
    return dimensoes


def _coordenadas_externas_perfil(coordenadas, espessura):
    meia_espessura = espessura / 2
    externas = []

    for indice, (x, y) in enumerate(coordenadas):
        deslocamentos = []
        if indice > 0:
            deslocamentos.append(
                _deslocamento_externo_segmento(indice - 1, coordenadas, meia_espessura)
            )
        if indice < len(coordenadas) - 1:
            deslocamentos.append(
                _deslocamento_externo_segmento(indice, coordenadas, meia_espessura)
            )
        if not deslocamentos:
            externas.append((x, y))
            continue
        if len(deslocamentos) == 1:
            ox, oy = deslocamentos[0]
        else:
            d0_x, d0_y = deslocamentos[0]
            d1_x, d1_y = deslocamentos[1]
            if meia_espessura > 0:
                n0_x, n0_y = d0_x / meia_espessura, d0_y / meia_espessura
                n1_x, n1_y = d1_x / meia_espessura, d1_y / meia_espessura
                dot = n0_x * n1_x + n0_y * n1_y
                if 1.0 + dot > 0.001:
                    factor = meia_espessura / (1.0 + dot)
                    ox = (n0_x + n1_x) * factor
                    oy = (n0_y + n1_y) * factor
                else:
                    ox, oy = d0_x, d0_y
            else:
                ox, oy = 0.0, 0.0
        externas.append((x + ox, y + oy))

    return externas


def _deslocamento_externo_segmento(indice_segmento, coordenadas, meia_espessura):
    x0, y0 = coordenadas[indice_segmento]
    x1, y1 = coordenadas[indice_segmento + 1]
    nx, ny = _normal_unitaria_esquerda(x1 - x0, y1 - y0)
    lado_interno = determinar_lado_interno_segmento(indice_segmento, coordenadas)
    fator = -lado_interno * meia_espessura
    return nx * fator, ny * fator


def _produto_vetorial(ax, ay, bx, by):
    return ax * by - ay * bx


def _normal_unitaria_esquerda(dx, dy):
    comprimento = hypot(dx, dy)
    if comprimento == 0:
        return 0.0, 0.0
    return -dy / comprimento, dx / comprimento


def _normal_unitaria_tangente(dx, dy):
    comprimento = hypot(dx, dy)
    if comprimento == 0:
        return 0.0, 0.0
    return dx / comprimento, dy / comprimento


def _cross_no_vertice(coordenadas, indice):
    if indice <= 0 or indice >= len(coordenadas) - 1:
        return 0.0
    x0, y0 = coordenadas[indice - 1]
    x1, y1 = coordenadas[indice]
    x2, y2 = coordenadas[indice + 1]
    return _produto_vetorial(x1 - x0, y1 - y0, x2 - x1, y2 - y1)


def segmento_entre_dobras_opostas(n, coordenadas):
    if n == 0 or n + 2 >= len(coordenadas):
        return False
    c_in = _cross_no_vertice(coordenadas, n)
    c_out = _cross_no_vertice(coordenadas, n + 1)
    if c_in == 0 or c_out == 0:
        return False
    return (c_in > 0) != (c_out > 0)


def determinar_lado_interno_segmento(n, coordenadas):
    x0, y0 = coordenadas[n]
    x1, y1 = coordenadas[n + 1]
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    dx, dy = x1 - x0, y1 - y0
    nx, ny = _normal_unitaria_esquerda(dx, dy)

    scores = []
    if n > 0:
        scores.append(
            (coordenadas[n - 1][0] - mx) * nx + (coordenadas[n - 1][1] - my) * ny
        )
    if n + 2 < len(coordenadas):
        scores.append(
            (coordenadas[n + 2][0] - mx) * nx + (coordenadas[n + 2][1] - my) * ny
        )

    if not scores:
        return 1
    return 1 if sum(scores) / len(scores) >= 0 else -1


def calcular_posicoes_medidas_segmento(
    n,
    coordenadas_no_canvas,
    espessura_linha,
    font_size,
    *,
    fator_distancia: float = 1.0,
):
    x0, y0 = coordenadas_no_canvas[n]
    x1, y1 = coordenadas_no_canvas[n + 1]
    dx, dy = x1 - x0, y1 - y0
    nx, ny = _normal_unitaria_esquerda(dx, dy)
    tx, ty = _normal_unitaria_tangente(dx, dy)

    centro = ((x0 + x1) / 2, (y0 + y1) / 2)
    lado = determinar_lado_interno_segmento(n, coordenadas_no_canvas)
    dist_base = (
        max(0.38 * espessura_linha + font_size * 0.95, font_size * 1.45)
        * fator_distancia
    )

    if segmento_entre_dobras_opostas(n, coordenadas_no_canvas):
        dist_int = dist_base * 0.88
        dist_ext = dist_base * 1.22
        desloc_tang = font_size * 0.32
        offset_int_x = nx * dist_int * lado + tx * desloc_tang
        offset_int_y = ny * dist_int * lado + ty * desloc_tang
        offset_ext_x = -nx * dist_ext * lado - tx * desloc_tang
        offset_ext_y = -ny * dist_ext * lado - ty * desloc_tang
    else:
        offset_int_x = nx * dist_base * lado
        offset_int_y = ny * dist_base * lado
        offset_ext_x = -nx * dist_base * lado
        offset_ext_y = -ny * dist_base * lado

    return centro, offset_int_x, offset_int_y, offset_ext_x, offset_ext_y


def _bboxes_sobrepoem(bbox_a, bbox_b, gap=2):
    if bbox_a is None or bbox_b is None:
        return False
    return not (
        bbox_a[2] + gap <= bbox_b[0]
        or bbox_b[2] + gap <= bbox_a[0]
        or bbox_a[3] + gap <= bbox_b[1]
        or bbox_b[3] + gap <= bbox_a[1]
    )


def _area_sobreposicao(bbox_a, bbox_b):
    if not _bboxes_sobrepoem(bbox_a, bbox_b, gap=0):
        return 0.0
    x0 = max(bbox_a[0], bbox_b[0])
    y0 = max(bbox_a[1], bbox_b[1])
    x1 = min(bbox_a[2], bbox_b[2])
    y1 = min(bbox_a[3], bbox_b[3])
    return max(0.0, x1 - x0) * max(0.0, y1 - y0)


def _pontos_amostra_bbox(bbox):
    x0, y0, x1, y1 = bbox
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    return (
        (x0, y0),
        (x1, y0),
        (x0, y1),
        (x1, y1),
        (mx, y0),
        (mx, y1),
        (x0, my),
        (x1, my),
        (mx, my),
    )


def _distancia_ponto_polilinha(p, coords):
    if len(coords) < 2:
        return float("inf")
    return min(
        _distancia_ponto_segmento(p, coords[i], coords[i + 1])
        for i in range(len(coords) - 1)
    )


def _bbox_sobrepoe_perfil(bbox, coords, meia_espessura, margem=3):
    if bbox is None or len(coords) < 2:
        return False
    limite = meia_espessura + margem
    for ponto in _pontos_amostra_bbox(bbox):
        if _distancia_ponto_polilinha(ponto, coords) <= limite:
            return True
    return False


def _raio_clip_perfil(espessura_linha):
    return espessura_linha / 2 + max(5.0, espessura_linha * 0.22)


def _info_geometrica_segmento(
    n, coordenadas_no_canvas, espessura_linha, font_size, *, fator_distancia: float = 1.0
):
    x0, y0 = coordenadas_no_canvas[n]
    x1, y1 = coordenadas_no_canvas[n + 1]
    dx, dy = x1 - x0, y1 - y0
    nx, ny = _normal_unitaria_esquerda(dx, dy)
    tx, ty = _normal_unitaria_tangente(dx, dy)
    centro = ((x0 + x1) / 2, (y0 + y1) / 2)
    lado = determinar_lado_interno_segmento(n, coordenadas_no_canvas)
    dist_base = (
        max(0.38 * espessura_linha + font_size * 0.95, font_size * 1.45)
        * fator_distancia
    )
    entre_opostas = segmento_entre_dobras_opostas(n, coordenadas_no_canvas)
    return centro, nx, ny, tx, ty, lado, dist_base, entre_opostas


def montar_rotulos_medidas(
    coordenadas_no_canvas,
    espessura_linha,
    medidas,
    font_size,
    *,
    destino: str = "preview",
):
    rotulos = []
    cores = _paleta_cotas(destino)
    fator_distancia = _fator_distancia_cotas(destino)
    especificacoes = (
        (
            "externa",
            cores["cota_externa"],
            cores["cota_externa_fundo"],
            cores["cota_externa_borda"],
        ),
        (
            "interna",
            cores["cota_interna"],
            cores["cota_interna_fundo"],
            cores["cota_interna_borda"],
        ),
    )

    for n in range(len(coordenadas_no_canvas) - 1):
        if n >= len(medidas):
            break

        centro, nx, ny, tx, ty, lado, dist_base, entre_opostas = _info_geometrica_segmento(
            n,
            coordenadas_no_canvas,
            espessura_linha,
            font_size,
            fator_distancia=fator_distancia,
        )
        _, ox_int, oy_int, ox_ext, oy_ext = calcular_posicoes_medidas_segmento(
            n,
            coordenadas_no_canvas,
            espessura_linha,
            font_size,
            fator_distancia=fator_distancia,
        )

        for chave, cor, cor_fundo, cor_borda in especificacoes:
            texto = medidas[n].get(chave)
            if texto == "" or texto is None:
                continue
            ox_base, oy_base = (ox_ext, oy_ext) if chave == "externa" else (ox_int, oy_int)
            rotulos.append(
                {
                    "segmento": n,
                    "tipo": chave,
                    "centro": centro,
                    "texto": texto,
                    "cor": cor,
                    "cor_fundo": cor_fundo,
                    "cor_borda": cor_borda,
                    "nx": nx,
                    "ny": ny,
                    "tx": tx,
                    "ty": ty,
                    "lado": lado,
                    "dist_base": dist_base,
                    "entre_opostas": entre_opostas,
                    "ox_base": ox_base,
                    "oy_base": oy_base,
                    "offset_x": ox_base,
                    "offset_y": oy_base,
                    "bbox": None,
                }
            )

    return rotulos


def _gerar_candidatos_rotulo(rotulo, font_size):
    ox0 = rotulo["ox_base"]
    oy0 = rotulo["oy_base"]
    tx = rotulo["tx"]
    ty = rotulo["ty"]
    centro = rotulo["centro"]
    texto = rotulo["texto"]

    candidatos = []
    visto = set()

    def registrar(pontuacao, ox, oy):
        chave = (round(ox, 1), round(oy, 1))
        if chave in visto:
            return
        bbox = _bbox_rotulo(centro, texto, font_size, ox, oy, margem_fundo=_margem_fundo())
        if bbox is None:
            return
        visto.add(chave)
        candidatos.append((pontuacao, ox, oy, bbox))

    registrar(0, ox0, oy0)

    for mult in (0.92, 1.0, 1.08, 1.16, 1.26, 1.38, 1.52, 1.68, 1.9, 2.15):
        registrar(abs(mult - 1) * 22, ox0 * mult, oy0 * mult)
        for tang in (0.35, -0.35, 0.7, -0.7, 1.0, -1.0, 1.4, -1.4):
            ox = ox0 * mult + tx * tang * font_size
            oy = oy0 * mult + ty * tang * font_size
            registrar(abs(mult - 1) * 22 + abs(tang) * 5, ox, oy)

    candidatos.sort(key=lambda item: item[0])
    return candidatos


def _pontuacao_sobreposicao(bbox, bboxes_ocupados):
    penalidade = 0.0
    for ocupado in bboxes_ocupados:
        if _bboxes_sobrepoem(bbox, ocupado, gap=0):
            penalidade += _area_sobreposicao(bbox, ocupado)
    return penalidade


def _pontuacao_sobreposicao_perfil(bbox, coords, meia_espessura):
    if not _bbox_sobrepoe_perfil(bbox, coords, meia_espessura):
        return 0.0
    limite = meia_espessura + 3
    excesso = 0.0
    for ponto in _pontos_amostra_bbox(bbox):
        dist = _distancia_ponto_polilinha(ponto, coords)
        if dist <= limite:
            excesso += (limite - dist + 1) ** 2
    return excesso * 250


def _posicao_rotulo_valida(bbox, bboxes_ocupados, coords, meia_espessura):
    if _bbox_sobrepoe_perfil(bbox, coords, meia_espessura):
        return False
    return not any(_bboxes_sobrepoem(bbox, ocupado) for ocupado in bboxes_ocupados)


def otimizar_posicoes_rotulos(
    rotulos, font_size, coordenadas_no_canvas=None, espessura_linha=None
):
    if not rotulos:
        return rotulos

    meia_espessura = None
    if coordenadas_no_canvas is not None and espessura_linha is not None:
        meia_espessura = espessura_linha / 2

    ordem = sorted(
        rotulos,
        key=lambda r: (r["segmento"], 0 if r["tipo"] == "externa" else 1),
    )
    bboxes_ocupados = []

    for rotulo in ordem:
        melhor_livre = None
        melhor_penalidade = float("inf")
        melhor_escolha = None

        for pontuacao, ox, oy, bbox in _gerar_candidatos_rotulo(rotulo, font_size):
            if meia_espessura is not None:
                valida = _posicao_rotulo_valida(
                    bbox, bboxes_ocupados, coordenadas_no_canvas, meia_espessura
                )
            else:
                valida = not any(
                    _bboxes_sobrepoem(bbox, ocupado) for ocupado in bboxes_ocupados
                )
            if valida:
                melhor_livre = (ox, oy, bbox)
                break
            penalidade = _pontuacao_sobreposicao(bbox, bboxes_ocupados) + pontuacao
            if meia_espessura is not None:
                penalidade += _pontuacao_sobreposicao_perfil(
                    bbox, coordenadas_no_canvas, meia_espessura
                )
            if penalidade < melhor_penalidade:
                melhor_penalidade = penalidade
                melhor_escolha = (ox, oy, bbox)

        if melhor_livre is not None:
            ox, oy, bbox = melhor_livre
        else:
            ox, oy, bbox = melhor_escolha

        rotulo["offset_x"] = ox
        rotulo["offset_y"] = oy
        rotulo["bbox"] = bbox
        bboxes_ocupados.append(bbox)

    return rotulos


def calcular_layout_medidas(
    coordenadas_no_canvas,
    espessura_linha,
    medidas,
    font_size,
    *,
    destino: str = "preview",
):
    font = fonte_medidas(font_size)
    rotulos = montar_rotulos_medidas(
        coordenadas_no_canvas,
        espessura_linha,
        medidas,
        font_size,
        destino=destino,
    )
    otimizar_posicoes_rotulos(
        rotulos, font_size, coordenadas_no_canvas, espessura_linha
    )
    return rotulos, font


def _formatar_angulo_dobra(angulo):
    arred = round(float(angulo), 1)
    if abs(arred - round(arred)) < 0.05:
        return f"{int(round(arred))}°"
    return f"{formatar_texto_medida(arred)}°"


def _geometria_vertice_dobra(indice_vertice, coordenadas):
    if indice_vertice <= 0 or indice_vertice >= len(coordenadas) - 1:
        return None
    x0, y0 = coordenadas[indice_vertice - 1]
    x1, y1 = coordenadas[indice_vertice]
    x2, y2 = coordenadas[indice_vertice + 1]
    dx_in, dy_in = x1 - x0, y1 - y0
    dx_out, dy_out = x2 - x1, y2 - y1
    comp_in = hypot(dx_in, dy_in)
    comp_out = hypot(dx_out, dy_out)
    if comp_in < 1e-6 or comp_out < 1e-6:
        return None
    t_in = (dx_in / comp_in, dy_in / comp_in)
    t_out = (dx_out / comp_out, dy_out / comp_out)
    return (x1, y1), t_in, t_out


def _direcao_rotulo_angulo_dobra(indice_vertice, coordenadas):
    geom = _geometria_vertice_dobra(indice_vertice, coordenadas)
    if geom is None:
        return (0.0, -1.0)
    _, t_in, t_out = geom
    lado1 = determinar_lado_interno_segmento(indice_vertice - 1, coordenadas)
    lado2 = determinar_lado_interno_segmento(indice_vertice, coordenadas)
    n1x, n1y = _normal_unitaria_esquerda(t_in[0], t_in[1])
    n2x, n2y = _normal_unitaria_esquerda(t_out[0], t_out[1])
    bx = n1x * lado1 + n2x * lado2
    by = n1y * lado1 + n2y * lado2
    comp = hypot(bx, by)
    if comp < 1e-6:
        # Invert to point to the outside
        return (-n1x * lado1, -n1y * lado1)
    # Invert to point to the outside
    return (-bx / comp, -by / comp)


def _bbox_simbolo_angulo_reto(vertex, t_in, t_out, tam, margem=2):
    vx, vy = vertex
    pontos = (
        vertex,
        (vx - t_in[0] * tam, vy - t_in[1] * tam),
        (vx + t_out[0] * tam, vy + t_out[1] * tam),
        (vx - t_in[0] * tam + t_out[0] * tam, vy - t_in[1] * tam + t_out[1] * tam),
    )
    xs = [p[0] for p in pontos]
    ys = [p[1] for p in pontos]
    return (min(xs) - margem, min(ys) - margem, max(xs) + margem, max(ys) + margem)


def _desenhar_simbolo_angulo_reto(draw, vertex, t_in, t_out, tam, cor, largura=1):
    vx, vy = vertex
    ax, ay = vx - t_in[0] * tam, vy - t_in[1] * tam
    bx, by = vx + t_out[0] * tam, vy + t_out[1] * tam
    cx, cy = ax + t_out[0] * tam, ay + t_out[1] * tam
    draw.line((ax, ay, cx, cy), fill=cor, width=largura)
    draw.line((cx, cy, bx, by), fill=cor, width=largura)


def montar_marcadores_angulos_dobra(
    coordenadas_no_canvas, dobras, espessura_linha, font_size
):
    if not dobras or len(coordenadas_no_canvas) < 3:
        return []

    dist_base = max(espessura_linha * 0.8 + font_size * 0.5, font_size * 1.1)
    tam_reto = max(5.0, font_size * 0.38, espessura_linha * 0.42)
    font_angulo = max(9, int(round(font_size * 0.72)))
    marcadores = []

    for indice, dobra in enumerate(dobras):
        v = indice + 1
        geom = _geometria_vertice_dobra(v, coordenadas_no_canvas)
        if geom is None:
            continue
        vertice, t_in, t_out = geom
        angulo = float(dobra["angulo_dobra"])
        dir_x, dir_y = _direcao_rotulo_angulo_dobra(v, coordenadas_no_canvas)

        if abs(angulo - 90) < 0.5:
            marcadores.append(
                {
                    "tipo": "reto",
                    "vertice": vertice,
                    "t_in": t_in,
                    "t_out": t_out,
                    "tam": tam_reto,
                    "vertice_indice": v,
                    "bbox": _bbox_simbolo_angulo_reto(vertice, t_in, t_out, tam_reto),
                }
            )
        else:
            ox, oy = dir_x * dist_base, dir_y * dist_base
            texto = _formatar_angulo_dobra(angulo)
            bbox = _bbox_rotulo(
                vertice,
                texto,
                font_angulo,
                ox,
                oy,
                margem_fundo=max(2, _margem_fundo()),
            )
            marcadores.append(
                {
                    "tipo": "texto",
                    "centro": vertice,
                    "texto": texto,
                    "offset_x": ox,
                    "offset_y": oy,
                    "ox_base": ox,
                    "oy_base": oy,
                    "vertice_indice": v,
                    "font_size": font_angulo,
                    "bbox": bbox,
                }
            )
    return marcadores


def otimizar_posicoes_marcadores_angulos(marcadores, bboxes_ocupados):
    for marcador in marcadores:
        if marcador["tipo"] != "texto":
            continue
        font_size = marcador["font_size"]
        ox0, oy0 = marcador["ox_base"], marcador["oy_base"]
        centro = marcador["centro"]
        texto = marcador["texto"]
        melhor = (ox0, oy0, marcador.get("bbox"))

        for mult in (1.0, 1.2, 1.45, 1.75, 2.1):
            ox, oy = ox0 * mult, oy0 * mult
            bbox = _bbox_rotulo(
                centro,
                texto,
                font_size,
                ox,
                oy,
                margem_fundo=max(2, _margem_fundo()),
            )
            if bbox is None:
                continue
            if not any(_bboxes_sobrepoem(bbox, ocupado) for ocupado in bboxes_ocupados):
                melhor = (ox, oy, bbox)
                break
            melhor = (ox, oy, bbox)

        marcador["offset_x"], marcador["offset_y"], marcador["bbox"] = melhor
        if marcador["bbox"] is not None:
            bboxes_ocupados.append(marcador["bbox"])


def _desenhar_chamada_angulo_dobra(
    draw,
    vertice,
    offset_x,
    offset_y,
    coordenadas_no_canvas,
    espessura_linha,
    vertice_permitido=None,
):
    linha_cota = _PALETA["cota_linha"]
    vx, vy = vertice
    comprimento = hypot(offset_x, offset_y)
    if comprimento <= 8:
        return
    fator = max(0.35, 1 - (12 / comprimento))
    origem = (vx + offset_x * fator, vy + offset_y * fator)
    destino = (vx, vy)
    raio_perfil = _raio_clip_perfil(espessura_linha)
    trechos = _trechos_linha_fora_perfil(
        origem,
        destino,
        coordenadas_no_canvas,
        raio_perfil,
        vertice_permitido=vertice_permitido,
    )
    for inicio, fim in trechos:
        draw.line((*inicio, *fim), fill=linha_cota, width=1)
    draw.ellipse((vx - 2, vy - 2, vx + 2, vy + 2), fill=linha_cota)


def desenhar_marcadores_angulos_dobra(
    draw, marcadores, coordenadas_no_canvas, espessura_linha
):
    if not marcadores:
        return
    cor = _PALETA["cota_angulo"]
    margem = max(2, _margem_fundo())

    for marcador in marcadores:
        if marcador["tipo"] == "reto":
            largura = max(1, int(round(espessura_linha * 0.14)))
            _desenhar_simbolo_angulo_reto(
                draw,
                marcador["vertice"],
                marcador["t_in"],
                marcador["t_out"],
                marcador["tam"],
                cor,
                largura=largura,
            )
            continue

        font = fonte_medidas(marcador["font_size"])
        _desenhar_chamada_angulo_dobra(
            draw,
            marcador["centro"],
            marcador["offset_x"],
            marcador["offset_y"],
            coordenadas_no_canvas,
            espessura_linha,
            vertice_permitido=marcador.get("vertice_indice"),
        )
        write_using_center_loc(
            draw,
            marcador["centro"],
            marcador["texto"],
            font=font,
            color=cor,
            x_offset=marcador["offset_x"],
            y_offset=marcador["offset_y"],
            fundo=True,
            cor_fundo=_PALETA["cota_angulo_fundo"],
            cor_borda=_PALETA["cota_angulo_borda"],
            margem_fundo=margem,
        )


def desenhar_rotulos_medidas(draw, rotulos, font, coordenadas_no_canvas=None, espessura_linha=None):
    margem = max(3, _margem_fundo() + 1)
    linha_cota = _PALETA["cota_linha"]
    raio_perfil = None
    meia_espessura = None
    if coordenadas_no_canvas is not None and espessura_linha is not None:
        raio_perfil = _raio_clip_perfil(espessura_linha)
        meia_espessura = espessura_linha / 2

    for rotulo in rotulos:
        cx, cy = rotulo["centro"]
        ox = rotulo["offset_x"]
        oy = rotulo["offset_y"]

        comprimento = hypot(ox, oy)
        if comprimento > 8:
            fator = max(0.35, 1 - (12 / comprimento))
            ax = cx + ox * fator
            ay = cy + oy * fator
            if meia_espessura is not None and comprimento > 0:
                ux, uy = ox / comprimento, oy / comprimento
                borda = meia_espessura + max(2.0, meia_espessura * 0.15)
                origem = (cx + ux * borda, cy + uy * borda)
            else:
                origem = (cx, cy)
            destino = (ax, ay)
            if raio_perfil is not None:
                vertice = rotulo.get("segmento")
                if vertice is not None and 0 <= vertice < len(coordenadas_no_canvas):
                    vertice_permitido = vertice
                    if vertice + 1 < len(coordenadas_no_canvas):
                        vx = coordenadas_no_canvas[vertice + 1]
                        if hypot(destino[0] - vx[0], destino[1] - vx[1]) < raio_perfil * 1.5:
                            vertice_permitido = vertice + 1
                else:
                    vertice_permitido = None
                trechos = _trechos_linha_fora_perfil(
                    origem,
                    destino,
                    coordenadas_no_canvas,
                    raio_perfil,
                    vertice_permitido=vertice_permitido,
                )
                for inicio, fim in trechos:
                    draw.line((*inicio, *fim), fill=linha_cota, width=1)
            else:
                origem = (cx, cy)
                draw.line((*origem, ax, ay), fill=linha_cota, width=1)
            ox0, oy0 = origem
            draw.ellipse((ox0 - 2, oy0 - 2, ox0 + 2, oy0 + 2), fill=linha_cota)

        write_using_center_loc(
            draw,
            rotulo["centro"],
            rotulo["texto"],
            font=font,
            color=rotulo["cor"],
            x_offset=rotulo["offset_x"],
            y_offset=rotulo["offset_y"],
            fundo=True,
            cor_fundo=rotulo["cor_fundo"],
            cor_borda=rotulo.get("cor_borda"),
            margem_fundo=margem,
        )


def calcular_centros_e_offsets(coordenadas_no_canvas, espessura_linha):
    offset_base = 0.33 * espessura_linha + 31
    centros_e_offsets = list()

    for n, xy in enumerate(coordenadas_no_canvas):
        try:
            x_atual = xy[0]
            y_atual = xy[1]
            x_prox = coordenadas_no_canvas[n + 1][0]
            y_prox = coordenadas_no_canvas[n + 1][1]
        except IndexError:
            break

        centro = [(x_atual + x_prox) / 2, (y_atual + y_prox) / 2]
        dx, dy = x_prox - x_atual, y_prox - y_atual
        nx, ny = _normal_unitaria_esquerda(dx, dy)
        offset_x = nx * offset_base
        offset_y = ny * offset_base

        centros_e_offsets.append(
            [centro, x_atual, y_atual, x_prox, y_prox, offset_x, offset_y]
        )

    return centros_e_offsets


def calcular_fatores_medida_segmento(n, centros_e_offsets, coordenadas_no_canvas):
    centro, x_atual, y_atual, x_prox, y_prox, offset_x, offset_y = centros_e_offsets[n]
    lado = determinar_lado_interno_segmento(n, coordenadas_no_canvas)
    fator_x_int = 1 if offset_x >= 0 else -1
    fator_y_int = 1 if offset_y >= 0 else -1
    if lado < 0:
        fator_x_int *= -1
        fator_y_int *= -1
    return fator_x_int, fator_y_int, abs(offset_x), abs(offset_y), centro


def pontos_comprimento_no_canvas(coordenadas_no_canvas, comprimento, espessura_linha, espessura):
    if espessura <= 0:
        return []
    escala = espessura_linha / espessura
    comprimento_canvas = comprimento * escala * 0.4
    angulo_comprimento = radians(60)
    return [
        (
            coordenada[0] + comprimento_canvas * cos(angulo_comprimento),
            coordenada[1] - comprimento_canvas * sin(angulo_comprimento),
        )
        for coordenada in coordenadas_no_canvas
    ]


def _bbox_desenho_e_rotulos(
    coordenadas_no_canvas,
    rotulos,
    marcadores_angulos=None,
    pontos_extra=None,
):
    xs = [ponto[0] for ponto in coordenadas_no_canvas]
    ys = [ponto[1] for ponto in coordenadas_no_canvas]
    for ponto in pontos_extra or ():
        xs.append(ponto[0])
        ys.append(ponto[1])
    for rotulo in rotulos:
        bbox = rotulo.get("bbox")
        if bbox is None:
            continue
        xs.extend((bbox[0], bbox[2]))
        ys.extend((bbox[1], bbox[3]))
    for marcador in marcadores_angulos or ():
        bbox = marcador.get("bbox")
        if bbox is None:
            continue
        xs.extend((bbox[0], bbox[2]))
        ys.extend((bbox[1], bbox[3]))
    if not xs or not ys:
        return (0, 0, 0, 0)
    return (min(xs), min(ys), max(xs), max(ys))


def calcular_layout_desenho_com_anotacoes(
    coordenadas_no_canvas,
    espessura_linha,
    medidas,
    dobras,
    font_size,
    *,
    destino: str = "preview",
):
    rotulos, font = calcular_layout_medidas(
        coordenadas_no_canvas,
        espessura_linha,
        medidas,
        font_size,
        destino=destino,
    )
    marcadores = montar_marcadores_angulos_dobra(
        coordenadas_no_canvas, dobras, espessura_linha, font_size
    )
    bboxes_ocupados = [r["bbox"] for r in rotulos if r.get("bbox")]
    otimizar_posicoes_marcadores_angulos(marcadores, bboxes_ocupados)
    return rotulos, font, marcadores


def calcular_bbox_desenho_com_medidas(
    coordenadas_no_canvas, espessura_linha, medidas, instrucoes, font_size
):
    _, _, dobras = gerar_corte_medidas_angulos(instrucoes)
    coordenadas_high_level = instrucoes.get("coordenadas_no_canvas_high_level", coordenadas_no_canvas)
    rotulos, _font, marcadores = calcular_layout_desenho_com_anotacoes(
        coordenadas_high_level, espessura_linha, medidas, dobras, font_size
    )
    return _bbox_desenho_e_rotulos(coordenadas_no_canvas, rotulos, marcadores)


def _normalizar_layout_no_canvas(
    coordenadas_no_canvas,
    rotulos,
    marcadores_angulos,
    bbox,
    tamanho_canvas,
    padding,
    font_size,
    instrucoes=None,
):
    min_x, min_y, max_x, max_y = bbox
    largura_canvas, altura_canvas = tamanho_canvas
    largura_bbox = max(max_x - min_x, 1.0)
    altura_bbox = max(max_y - min_y, 1.0)
    alvo_largura = max(1.0, largura_canvas - 2 * padding)
    alvo_altura = max(1.0, altura_canvas - 2 * padding)
    escala = min(alvo_largura / largura_bbox, alvo_altura / altura_bbox, 1.0)

    centro_bbox_x = (min_x + max_x) / 2
    centro_bbox_y = (min_y + max_y) / 2
    centro_canvas_x = largura_canvas / 2
    centro_canvas_y = altura_canvas / 2

    def map_xy(x, y):
        return (
            centro_canvas_x + (x - centro_bbox_x) * escala,
            centro_canvas_y + (y - centro_bbox_y) * escala,
        )

    novas_coords = [map_xy(x, y) for x, y in coordenadas_no_canvas]

    if instrucoes and "coordenadas_no_canvas_high_level" in instrucoes:
        instrucoes["coordenadas_no_canvas_high_level"] = [
            map_xy(x, y) for x, y in instrucoes["coordenadas_no_canvas_high_level"]
        ]

    for rotulo in rotulos:
        rotulo["centro"] = map_xy(*rotulo["centro"])
        rotulo["offset_x"] *= escala
        rotulo["offset_y"] *= escala
        if "ox_base" in rotulo:
            rotulo["ox_base"] *= escala
        if "oy_base" in rotulo:
            rotulo["oy_base"] *= escala
        rotulo["bbox"] = _bbox_rotulo(
            rotulo["centro"],
            rotulo["texto"],
            font_size,
            rotulo["offset_x"],
            rotulo["offset_y"],
            margem_fundo=_margem_fundo(),
        )

    for marcador in marcadores_angulos or ():
        if marcador["tipo"] == "texto":
            marcador["centro"] = map_xy(*marcador["centro"])
            marcador["offset_x"] *= escala
            marcador["offset_y"] *= escala
            marcador["ox_base"] *= escala
            marcador["oy_base"] *= escala
            marcador["bbox"] = _bbox_rotulo(
                marcador["centro"],
                marcador["texto"],
                marcador["font_size"],
                marcador["offset_x"],
                marcador["offset_y"],
                margem_fundo=max(2, _margem_fundo()),
            )
        elif marcador["tipo"] == "reto":
            marcador["vertice"] = map_xy(*marcador["vertice"])
            marcador["tam"] *= escala
            marcador["bbox"] = _bbox_simbolo_angulo_reto(
                marcador["vertice"],
                marcador["t_in"],
                marcador["t_out"],
                marcador["tam"],
            )

    return novas_coords, rotulos, marcadores_angulos, escala


def conteudo_cabe_no_canvas(bbox, tamanho_canvas, padding=8):
    min_x, min_y, max_x, max_y = bbox
    largura, altura = tamanho_canvas
    return (
        min_x >= padding
        and min_y >= padding
        and max_x <= largura - padding
        and max_y <= altura - padding
    )


def adicionar_medidas(
    draw, coordenadas_no_canvas, espessura_linha, medidas, dobras, instrucoes, font_size
):
    coordenadas_high_level = instrucoes.get("coordenadas_no_canvas_high_level", coordenadas_no_canvas)
    rotulos, font, marcadores = calcular_layout_desenho_com_anotacoes(
        coordenadas_high_level, espessura_linha, medidas, dobras, font_size
    )
    desenhar_rotulos_medidas(
        draw,
        rotulos,
        font,
        coordenadas_no_canvas=coordenadas_no_canvas,
        espessura_linha=espessura_linha,
    )
    desenhar_marcadores_angulos_dobra(
        draw, marcadores, coordenadas_no_canvas, espessura_linha
    )


def definir_azimute(direcao, grau, azimute_anterior):
    match direcao:
        case "N":
            azimute_direcao = 0
        case "S":
            azimute_direcao = 180
        case "E":
            azimute_direcao = 90
        case "W":
            azimute_direcao = 270
    if grau == 90:
        azimute = azimute_direcao
    else:
        if azimute_anterior == None:
            azimute = azimute_direcao - (90 - grau)
        else:
            match azimute_anterior:
                case 0:
                    match azimute_direcao:
                        case 0 | 90 | 180:
                            azimute = grau
                        case 270:
                            azimute = 360 - grau
                case 90:
                    match azimute_direcao:
                        case 90 | 180 | 270:
                            azimute = 90 + grau
                        case 0:
                            azimute = 90 - grau
                case 180:
                    match azimute_direcao:
                        case 0 | 90 | 270:
                            azimute = 180 + grau
                        case 180:
                            azimute = 180 - grau
                case 270:
                    match azimute_direcao:
                        case 0 | 90 | 270:
                            azimute = 270 + grau
                        case 180:
                            azimute = 270 - grau
                case _:
                    if 90 > azimute_anterior > 0:
                        match azimute_direcao:
                            case 0 | 270:
                                azimute = azimute_anterior - grau
                            case 90 | 180:
                                azimute = azimute_anterior + grau
                    elif 180 > azimute_anterior > 90:
                        match azimute_direcao:
                            case 0 | 90:
                                azimute = azimute_anterior - grau
                            case 180 | 270:
                                azimute = azimute_anterior + grau
                    elif 270 > azimute_anterior > 180:
                        match azimute_direcao:
                            case 0 | 270:
                                azimute = azimute_anterior + grau
                            case 90 | 180:
                                azimute = azimute_anterior - grau
                    else:  # 360 > azimute_anterior > 270
                        match azimute_direcao:
                            case 0 | 90:
                                azimute = azimute_anterior + grau
                            case 180 | 270:
                                azimute = azimute_anterior - grau
    while azimute < 0:
        azimute += 360
    return azimute


def medida_centro_de_interna(medida_interna, grau, espessura, raio_de_dobra):
    if grau == 90:
        medida = medida_interna + espessura / 2
    else:
        medida = (
            medida_interna
            - (tan(radians(grau / 2)) * raio_de_dobra)
            + (tan(radians(grau / 2)) * (raio_de_dobra + (espessura / 2)))
        )
    return medida


def medida_centro_de_externa(medida_externa, grau, espessura, raio_de_dobra):
    if grau == 90:
        medida = medida_externa - espessura / 2
    else:
        medida = (
            medida_externa
            - (sin(radians(grau / 2)) * (raio_de_dobra + espessura))
            + tan(radians(grau / 2)) * (raio_de_dobra + (espessura / 2))
        )
    return medida


def _eh_segmento_curvo(seg):
    if not seg or len(seg) < 5:
        return False
    curvo = seg[4]
    if not curvo:
        return False
    if len(seg) < 6 or not isinstance(seg[5], dict):
        return False
    info = seg[5]
    if info.get("raio") is None or info.get("comprimento_curva") is None or info.get("angulo_curva") is None:
        return False
    return True


def _azimutes_de_segmentos(segmentos):
    azimutes = []
    azimute_anterior = None
    for seg in segmentos:
        direcao = seg[0]
        angulo = seg[1]
        azimute = definir_azimute(direcao, angulo, azimute_anterior)
        azimutes.append(azimute)
        azimute_anterior = azimute
    return azimutes


def _angulos_dobra_de_azimutes(azimutes):
    angulos = []
    for indice in range(1, len(azimutes)):
        az_0, az_1 = azimutes[indice - 1], azimutes[indice]
        diff = az_1 - az_0
        if diff < 0:
            diff += 360
        if diff < 180:
            angulos.append(diff)
        else:
            angulos.append(360 - diff)
    return angulos


def converter_instrucoes_convencionais_para_coordenadas_polares(
    instrucoes_convencionais,
):
    chapa = instrucoes_convencionais["chapa"]
    comprimento = instrucoes_convencionais["comprimento"]
    segmentos = instrucoes_convencionais["segmentos"]

    chapa = consultar_chapa(chapa)
    espessura = chapa["espessura"]
    raio_de_dobra = chapa["raio_de_dobra"]
    k_factor = chapa["k_factor"]

    espessura_informada = instrucoes_convencionais.get("espessura")
    if espessura_informada is not None:
        espessura_informada = float(espessura_informada)
        if espessura_informada > 0 and espessura_informada != espessura:
            proporcao = espessura_informada / espessura
            espessura = espessura_informada
            raio_de_dobra = raio_de_dobra * proporcao

    coordenadas_polares = []
    total_segmentos = len(segmentos)
    azimutes = _azimutes_de_segmentos(segmentos)
    angulos_dobra = _angulos_dobra_de_azimutes(azimutes)

    for n, seg in enumerate(segmentos):
        direcao = seg[0]
        angulo = seg[1]
        medida = seg[2]
        tipo_de_medida = seg[3]
        azimute = azimutes[n]

        is_curv = _eh_segmento_curvo(seg)
        if is_curv:
            info = seg[5]
            r = float(info["raio"])
            a = float(info["angulo_curva"])
            tipo_r = info["tipo_raio"]

            if tipo_r == "interno":
                r_i = r
            else:
                r_i = r - espessura

            r_n = r_i + k_factor * espessura
            a_rad = radians(a)

            if a == 360:
                L_chord = 0.0
            else:
                L_chord = 2 * r_n * sin(a_rad / 2)

            if n > 0:
                diff = (azimutes[n] - azimutes[n-1]) % 360
                is_cw = (diff < 180)
                az_start = azimutes[n-1]
            else:
                if len(segmentos) > 1:
                    diff = (azimutes[1] - azimutes[0]) % 360
                    is_cw = (diff < 180)
                else:
                    is_cw = True
                A_signed = a if is_cw else -a
                az_start = (azimutes[0] - A_signed) % 360

            azimute_chord = (az_start + (a if is_cw else -a) / 2) % 360
            dh = L_chord
            azimute = azimute_chord
        else:
            grau_entrada = 0.0
            if n > 0:
                if not _eh_segmento_curvo(segmentos[n - 1]):
                    grau_entrada = angulos_dobra[n - 1]

            grau_saida = 0.0
            if n < total_segmentos - 1:
                if not _eh_segmento_curvo(segmentos[n + 1]):
                    grau_saida = angulos_dobra[n]

            if grau_entrada == 0.0 and grau_saida == 0.0:
                dh = medida
            else:
                if tipo_de_medida == "e":
                    medida_temp = medida
                    if grau_entrada != 0.0:
                        medida_temp = medida_centro_de_externa(
                            medida_temp, grau_entrada, espessura, raio_de_dobra
                        )
                    if grau_saida != 0.0:
                        medida_temp = medida_centro_de_externa(
                            medida_temp, grau_saida, espessura, raio_de_dobra
                        )
                    dh = medida_temp
                else:  # tipo_de_medida == "i"
                    medida_temp = medida
                    if grau_entrada != 0.0:
                        medida_temp = medida_centro_de_interna(
                            medida_temp, grau_entrada, espessura, raio_de_dobra
                        )
                    if grau_saida != 0.0:
                        medida_temp = medida_centro_de_interna(
                            medida_temp, grau_saida, espessura, raio_de_dobra
                        )
                    dh = medida_temp

        coordenadas_polares.append((azimute, dh))

    if len(coordenadas_polares) == 1 and not _eh_segmento_curvo(segmentos[0]):  # Chapa lisa
        coordenadas_polares = [(coordenadas_polares[0][0], segmentos[0][2])]

    instrucoes = {
        "coordenadas_polares": coordenadas_polares,
        "comprimento": comprimento,
        "espessura": espessura,
        "raio_de_dobra": raio_de_dobra,
        "k_factor": k_factor,
        "segmentos_original": segmentos,
    }

    return instrucoes



def renderizar_imagem(
    instrucoes,
    tamanho=800,
    mostrar_medidas=True,
    margem_canvas=None,
    destino: str = "preview",
    canvas_adaptativo: bool = False,
):
    fator_aa = _supersampling() if destino == "preview" else max(1, _supersampling())
    if isinstance(tamanho, (tuple, list)):
        tamanho_base_x = tamanho[0] * fator_aa
        tamanho_base_y = tamanho[1] * fator_aa
        tamanho_base = max(tamanho[0], tamanho[1]) * fator_aa
    else:
        tamanho_base_x = tamanho * fator_aa
        tamanho_base_y = tamanho * fator_aa
        tamanho_base = tamanho * fator_aa

    margem_usada = _margem_desenho() if margem_canvas is None else margem_canvas

    medidas = None
    dobras = None
    if mostrar_medidas:
        _, medidas, dobras = gerar_corte_medidas_angulos(instrucoes)

    def _dimensoes_canvas_adaptativo(bbox: tuple, referencia: int) -> tuple[int, int]:
        bx0, by0, bx1, by1 = bbox
        pad = max(10, int(round(referencia * 0.012)))
        largura_conteudo = max(bx1 - bx0 + 2 * pad, 1)
        altura_conteudo = max(by1 - by0 + 2 * pad, 1)
        proporcao = largura_conteudo / altura_conteudo
        minimo = max(int(referencia * 0.32), 120)
        if proporcao >= 1:
            return referencia, max(int(referencia / proporcao), minimo)
        return max(int(referencia * proporcao), minimo), referencia

    def _montar(tamanho_canvas: tuple[int, int], margem_usar: float) -> tuple | None:
        margens_tentativa = _margens_tentativa_layout(margem_usar, mostrar_medidas)
        dimensao_ref = max(tamanho_canvas)
        font_size = _tamanho_fonte_cota(dimensao_ref, destino=destino)
        padding = max(10, int(round(min(tamanho_canvas) * 0.012)))
        escala_minima_segmento_px = max(16, int(round(font_size * 1.4)))

        image_local = None
        draw_local = None
        coordenadas_no_canvas_local = None
        espessura_linha_local = None
        rotulos_local = None
        font_local = None
        marcadores_angulos_local = None
        bbox_local = None
        layout_cabendo_local = False

        for indice, margem_tentativa in enumerate(margens_tentativa):
            escala_min_atual = max(
                10,
                int(
                    round(
                        escala_minima_segmento_px
                        * _fator_escala_minima_tentativa(indice, len(margens_tentativa))
                    )
                ),
            )
            image_local = Image.new("RGBA", tamanho_canvas, color=(255, 255, 255, 0))
            draw_local = ImageDraw.Draw(image_local, "RGBA")
            coordenadas_no_canvas_local, espessura_linha_local = desenhar(
                draw_local,
                instrucoes,
                tamanho_canvas,
                margem=margem_tentativa,
                escala_minima_segmento_px=escala_min_atual,
            )

            if not mostrar_medidas:
                break

            rotulos_local, font_local, marcadores_angulos_local = (
                calcular_layout_desenho_com_anotacoes(
                    coordenadas_no_canvas_local,
                    espessura_linha_local,
                    medidas,
                    dobras,
                    font_size,
                    destino=destino,
                )
            )
            bbox_local = _bbox_desenho_e_rotulos(
                coordenadas_no_canvas_local,
                rotulos_local,
                marcadores_angulos_local,
            )
            if conteudo_cabe_no_canvas(bbox_local, tamanho_canvas, padding=padding):
                layout_cabendo_local = True
                break

        if (
            mostrar_medidas
            and bbox_local is not None
            and not layout_cabendo_local
            and coordenadas_no_canvas_local is not None
            and rotulos_local is not None
        ):
            coordenadas_no_canvas_local, rotulos_local, marcadores_angulos_local, escala_layout = (
                _normalizar_layout_no_canvas(
                    coordenadas_no_canvas_local,
                    rotulos_local,
                    marcadores_angulos_local,
                    bbox_local,
                    tamanho_canvas,
                    padding,
                    font_size,
                    instrucoes=instrucoes,
                )
            )
            espessura_linha_local *= escala_layout
            bbox_local = _bbox_desenho_e_rotulos(
                coordenadas_no_canvas_local,
                rotulos_local,
                marcadores_angulos_local,
            )
            image_local = Image.new("RGBA", tamanho_canvas, color=(255, 255, 255, 0))
            draw_local = ImageDraw.Draw(image_local, "RGBA")
            _desenhar_fundo_pagina(
                draw_local, tamanho_canvas, _estilo_desenho(destino)["cores"]
            )
            _desenhar_geometria_perfil(
                draw_local,
                coordenadas_no_canvas_local,
                instrucoes,
                espessura_linha_local,
                estilo=_estilo_desenho(destino),
            )

        if (
            mostrar_medidas
            and draw_local is not None
            and rotulos_local is not None
            and font_local is not None
        ):
            desenhar_rotulos_medidas(
                draw_local,
                rotulos_local,
                font_local,
                coordenadas_no_canvas=coordenadas_no_canvas_local,
                espessura_linha=espessura_linha_local,
            )
            if marcadores_angulos_local is not None:
                desenhar_marcadores_angulos_dobra(
                    draw_local,
                    marcadores_angulos_local,
                    coordenadas_no_canvas_local,
                    espessura_linha_local,
                )

        return image_local, bbox_local

    tamanho_canvas = (tamanho_base_x, tamanho_base_y)
    image, bbox_final = _montar(tamanho_canvas, margem_usada)

    if canvas_adaptativo and mostrar_medidas and bbox_final is not None:
        novo_canvas = _dimensoes_canvas_adaptativo(bbox_final, tamanho_base)
        if novo_canvas != tamanho_canvas:
            tamanho_canvas = novo_canvas
            image, bbox_final = _montar(tamanho_canvas, 0.01)

    if fator_aa > 1 and image is not None:
        destino_px = (
            max(1, tamanho_canvas[0] // fator_aa),
            max(1, tamanho_canvas[1] // fator_aa),
        )
        image = image.resize(destino_px, Image.Resampling.LANCZOS)

    if image is not None:
        fundo = Image.new("RGB", image.size, _PALETA["fundo_pagina"])
        if image.mode == "RGBA":
            fundo.paste(image, mask=image.split()[3])
        else:
            fundo.paste(image)
        image = fundo

    return image


def gerar_imagem(instrucoes, tamanho, mostrar_medidas=True):
    image = renderizar_imagem(instrucoes, tamanho, mostrar_medidas)
    image.save(str(tamanho) + ".png")


def listar_chapas():
    chapas = []
    with open(caminho_chapas(), newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f, delimiter=",")
        for row in reader:
            chapas.append(
                {
                    "codigo": row["codigo"],
                    "espessura": float(row["espessura"]),
                    "coeficiente": float(row["coeficiente"]),
                    "dobra_minima": float(row.get("dobra_minima") or 0),
                    "tipo": row["tipo"],
                }
            )
    return chapas
