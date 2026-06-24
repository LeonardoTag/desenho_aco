from __future__ import annotations

import math
from collections.abc import Callable

from config_manager import tolerancias_gerador_boiadeira
from core import desenhar


def _nelder_mead(func, x0, bounds, tol=1e-12, max_iter=250):
    def objective(x):
        penalty = 0.0
        for i, (low, high) in enumerate(bounds):
            if x[i] < low:
                penalty += (low - x[i]) ** 2 + 1e6
            elif x[i] > high:
                penalty += (x[i] - high) ** 2 + 1e6
        if penalty > 0:
            return 1e12 + penalty
        
        try:
            res = func(x)
            return sum(r ** 2 for r in res)
        except Exception:
            return 1e18

    alpha = 1.0
    gamma = 2.0
    rho = 0.5
    sigma = 0.5

    n = len(x0)
    simplex = [list(x0)]
    for i in range(n):
        vertex = list(x0)
        step = max(abs(x0[i]) * 0.05, 0.1)
        vertex[i] = max(bounds[i][0], min(bounds[i][1], vertex[i] + step))
        simplex.append(vertex)

    f_val = [objective(v) for v in simplex]

    for _ in range(max_iter):
        order = sorted(range(n + 1), key=lambda idx: f_val[idx])
        simplex = [simplex[idx] for idx in order]
        f_val = [f_val[idx] for idx in order]

        centroid = [0.0] * n
        for v in simplex[:-1]:
            for i in range(n):
                centroid[i] += v[i]
        for i in range(n):
            centroid[i] /= n

        size = sum(sum((simplex[j][i] - centroid[i]) ** 2 for i in range(n)) for j in range(n + 1))
        if size < tol or (f_val[-1] - f_val[0]) < tol:
            break

        worst = simplex[-1]
        
        # Reflection
        reflected = [centroid[i] + alpha * (centroid[i] - worst[i]) for i in range(n)]
        for i in range(n):
            reflected[i] = max(bounds[i][0], min(bounds[i][1], reflected[i]))
        f_r = objective(reflected)

        if f_r < f_val[0]:
            # Expansion
            expanded = [centroid[i] + gamma * (reflected[i] - centroid[i]) for i in range(n)]
            for i in range(n):
                expanded[i] = max(bounds[i][0], min(bounds[i][1], expanded[i]))
            f_e = objective(expanded)
            if f_e < f_r:
                simplex[-1] = expanded
                f_val[-1] = f_e
            else:
                simplex[-1] = reflected
                f_val[-1] = f_r
        elif f_r < f_val[-2]:
            simplex[-1] = reflected
            f_val[-1] = f_r
        else:
            # Contraction
            if f_r < f_val[-1]:
                # Outside contraction
                contracted = [centroid[i] + rho * (reflected[i] - centroid[i]) for i in range(n)]
                for i in range(n):
                    contracted[i] = max(bounds[i][0], min(bounds[i][1], contracted[i]))
                f_c = objective(contracted)
                if f_c <= f_r:
                    simplex[-1] = contracted
                    f_val[-1] = f_c
                    continue
            else:
                # Inside contraction
                contracted = [centroid[i] + rho * (worst[i] - centroid[i]) for i in range(n)]
                for i in range(n):
                    contracted[i] = max(bounds[i][0], min(bounds[i][1], contracted[i]))
                f_c = objective(contracted)
                if f_c < f_val[-1]:
                    simplex[-1] = contracted
                    f_val[-1] = f_c
                    continue
            
            # Shrink
            best = simplex[0]
            for j in range(1, n + 1):
                simplex[j] = [best[i] + sigma * (simplex[j][i] - best[i]) for i in range(n)]
                for i in range(n):
                    simplex[j][i] = max(bounds[i][0], min(bounds[i][1], simplex[j][i]))
                f_val[j] = objective(simplex[j])

    return simplex[0]


class ErroGeradorPeca(Exception):
    pass


def _foi_cancelado(cancelar: Callable[[], bool] | None) -> bool:
    return cancelar is not None and cancelar()


def _codigo_chapa(chapa: str) -> str:
    texto = str(chapa).strip()
    if not texto:
        raise ErroGeradorPeca("Selecione uma chapa.")
    return texto if texto.startswith("#") else f"#{texto}"


def _indices_topo_gomos(num_gomos: int) -> list[int]:
    indices = [3]
    for indice in range(1, num_gomos):
        indices.append(3 + 4 * indice)
    return indices


def _montar_segmentos_boiadeira(
    altura_aba: float,
    primeiro_gomo: float,
    tamanho_gomo_superior: float,
    tamanho_gomo_inferior: float,
    tamanho_diagonal: float,
    num_gomos: int,
    angulo_gomo: float,
) -> list[list]:
    segmentos: list[list] = [
        ["S", 90.0, altura_aba, "e"],
        ["E", 90.0, primeiro_gomo, "e"],
        ["N", angulo_gomo, tamanho_diagonal, "e"],
    ]

    for indice in range(1, num_gomos):
        segmentos.extend(
            [
                ["E", angulo_gomo, tamanho_gomo_superior, "e"],
                ["S", angulo_gomo, tamanho_diagonal, "e"],
                ["E", angulo_gomo, tamanho_gomo_inferior, "e"],
                ["N", angulo_gomo, tamanho_diagonal, "e"],
            ]
        )

    segmentos.extend(
        [
            ["E", angulo_gomo, tamanho_gomo_superior, "e"],
            ["S", angulo_gomo, tamanho_diagonal, "e"],
            ["E", 90.0, primeiro_gomo, "e"],
            ["N", 90.0, altura_aba, "e"],
        ]
    )
    return segmentos


def _instrucoes_boiadeira(
    *,
    chapa: str,
    altura_aba: float,
    primeiro_gomo: float,
    tamanho_gomo_superior: float,
    tamanho_gomo_inferior: float,
    tamanho_diagonal: float,
    num_gomos: int,
    comprimento: float,
    angulo: float,
) -> dict:
    return {
        "chapa": chapa,
        "comprimento": comprimento,
        "segmentos": _montar_segmentos_boiadeira(
            altura_aba,
            primeiro_gomo,
            tamanho_gomo_superior,
            tamanho_gomo_inferior,
            tamanho_diagonal,
            num_gomos,
            angulo,
        ),
    }


def _dimensoes_e_topo(
    instrucoes: dict,
    num_gomos: int,
) -> tuple[float, float, float]:
    polares = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(instrucoes)
    espessura = float(polares["espessura"])
    parciais = desenhar.gerar_coordenadas_retangulares_parciais(polares["coordenadas_polares"])
    absolutas = desenhar.gerar_coordenadas_retangulares_absolutas(parciais)
    if espessura > 0 and len(absolutas) >= 2:
        absolutas = desenhar._coordenadas_externas_perfil(absolutas, espessura)
    dimensoes, _ = desenhar.gerar_dimensoes_totais_e_ponto_inicial(absolutas)
    topo_flange = absolutas[0][1]
    indices_topo = _indices_topo_gomos(num_gomos)
    topo_gomos = min(absolutas[indice][1] for indice in indices_topo if indice < len(absolutas))
    return float(dimensoes[0]), float(dimensoes[1]), abs(topo_gomos - topo_flange)


def _metricas_para_parametros(
    *,
    chapa: str,
    altura_aba: float,
    primeiro_gomo: float,
    tamanho_gomo_superior: float,
    tamanho_gomo_inferior: float,
    num_gomos: int,
    comprimento: float,
    angulo: float,
    tamanho_diagonal: float,
) -> tuple[float, float, float]:
    instrucoes = _instrucoes_boiadeira(
        chapa=chapa,
        altura_aba=altura_aba,
        primeiro_gomo=primeiro_gomo,
        tamanho_gomo_superior=tamanho_gomo_superior,
        tamanho_gomo_inferior=tamanho_gomo_inferior,
        tamanho_diagonal=tamanho_diagonal,
        num_gomos=num_gomos,
        comprimento=comprimento,
        angulo=angulo,
    )
    return _dimensoes_e_topo(instrucoes, num_gomos)


def _custo_solucao(
    largura: float,
    altura: float,
    desvio_topo: float,
    *,
    largura_total: float,
    altura_aba: float,
) -> float:
    return max(
        abs(largura - largura_total),
        abs(altura - altura_aba),
        desvio_topo,
    )


def _pontos_iniciais_boiadeira(
    *,
    chapa: str,
    altura_aba: float,
    primeiro_gomo: float,
    tamanho_gomo_superior: float,
    tamanho_gomo_inferior: float,
    num_gomos: int,
    largura_total: float,
    comprimento: float,
) -> list[tuple[float, float]]:
    pontos: list[tuple[float, float, float]] = []

    for angulo in (25.0, 30.0, 35.0, 40.0, 45.0, 50.0, 55.0, 60.0, 65.0, 70.0):
        seno = math.sin(math.radians(angulo))
        diagonal = altura_aba / seno if seno > 0.01 else altura_aba
        largura, altura, desvio_topo = _metricas_para_parametros(
            chapa=chapa,
            altura_aba=altura_aba,
            primeiro_gomo=primeiro_gomo,
            tamanho_gomo_superior=tamanho_gomo_superior,
            tamanho_gomo_inferior=tamanho_gomo_inferior,
            num_gomos=num_gomos,
            comprimento=comprimento,
            angulo=angulo,
            tamanho_diagonal=diagonal,
        )
        custo = _custo_solucao(
            largura,
            altura,
            desvio_topo,
            largura_total=largura_total,
            altura_aba=altura_aba,
        )
        pontos.append((custo, angulo, diagonal))

    pontos.sort(key=lambda item: item[0])
    vistos: set[tuple[float, float]] = set()
    iniciais: list[tuple[float, float]] = []
    for _, angulo, diagonal in pontos:
        chave = (round(angulo, 1), round(diagonal, 1))
        if chave in vistos:
            continue
        vistos.add(chave)
        iniciais.append((angulo, diagonal))
    return iniciais[:6]


def _resolver_solucao_boiadeira(
    *,
    chapa: str,
    altura_aba: float,
    primeiro_gomo: float,
    tamanho_gomo_superior: float,
    tamanho_gomo_inferior: float,
    num_gomos: int,
    largura_total: float,
    comprimento: float,
    cancelar: Callable[[], bool] | None = None,
) -> tuple[float, float, float, float, float]:
    contexto_metricas = {
        "chapa": chapa,
        "altura_aba": altura_aba,
        "primeiro_gomo": primeiro_gomo,
        "tamanho_gomo_superior": tamanho_gomo_superior,
        "tamanho_gomo_inferior": tamanho_gomo_inferior,
        "num_gomos": num_gomos,
        "comprimento": comprimento,
    }

    def residuos(parametros: list[float]) -> list[float]:
        angulo, diagonal = parametros
        largura, altura, desvio_topo = _metricas_para_parametros(
            angulo=angulo,
            tamanho_diagonal=diagonal,
            **contexto_metricas,
        )
        return [
            largura - largura_total,
            altura - altura_aba,
            desvio_topo,
        ]

    limite_diagonal = max(
        altura_aba * 4.0,
        tamanho_gomo_superior * 2.0,
        tamanho_gomo_inferior * 2.0,
        80.0,
    )
    melhor: tuple[float, float, float, float, float, float] | None = None

    for angulo_inicial, diagonal_inicial in _pontos_iniciais_boiadeira(
        largura_total=largura_total,
        **contexto_metricas,
    ):
        if _foi_cancelado(cancelar):
            raise ErroGeradorPeca("Geração cancelada pelo usuário.")
        resultado = _nelder_mead(
            residuos,
            x0=[angulo_inicial, diagonal_inicial],
            bounds=[(0.1, 89.9), (0.1, limite_diagonal)],
            tol=1e-12,
            max_iter=150,
        )

        angulo, diagonal = resultado
        largura, altura, desvio_topo = _metricas_para_parametros(
            angulo=angulo,
            tamanho_diagonal=diagonal,
            **contexto_metricas,
        )
        custo = _custo_solucao(
            largura,
            altura,
            desvio_topo,
            largura_total=largura_total,
            altura_aba=altura_aba,
        )
        if melhor is None or custo < melhor[0]:
            melhor = (custo, angulo, diagonal, largura, altura, desvio_topo)

    if melhor is None:
        raise ErroGeradorPeca(
            "Não foi possível calcular o perfil com os parâmetros informados."
        )

    return melhor[1], melhor[2], melhor[3], melhor[4], melhor[5]


def _polir_valores_arredondados(
    *,
    chapa: str,
    altura_aba: float,
    primeiro_gomo: float,
    tamanho_gomo_superior: float,
    tamanho_gomo_inferior: float,
    num_gomos: int,
    largura_total: float,
    comprimento: float,
    angulo: float,
    tamanho_diagonal: float,
    passos_angulo: float,
    passos_diagonal: float,
    alcance: float,
) -> tuple[float, float]:
    melhor_angulo = round(angulo, 3)
    melhor_diagonal = round(tamanho_diagonal, 3)
    melhor_custo = math.inf
    passos = max(1, int(round(alcance / passos_angulo)))

    for indice_angulo in range(-passos, passos + 1):
        candidato_angulo = round(angulo + indice_angulo * passos_angulo, 3)
        if candidato_angulo <= 0.1 or candidato_angulo >= 89.9:
            continue
        for indice_diagonal in range(-passos, passos + 1):
            candidato_diagonal = round(tamanho_diagonal + indice_diagonal * passos_diagonal, 3)
            if candidato_diagonal <= 0.1:
                continue
            largura, altura, desvio_topo = _metricas_para_parametros(
                chapa=chapa,
                altura_aba=altura_aba,
                primeiro_gomo=primeiro_gomo,
                tamanho_gomo_superior=tamanho_gomo_superior,
                tamanho_gomo_inferior=tamanho_gomo_inferior,
                num_gomos=num_gomos,
                comprimento=comprimento,
                angulo=candidato_angulo,
                tamanho_diagonal=candidato_diagonal,
            )
            custo = _custo_solucao(
                largura,
                altura,
                desvio_topo,
                largura_total=largura_total,
                altura_aba=altura_aba,
            )
            if custo < melhor_custo:
                melhor_custo = custo
                melhor_angulo = candidato_angulo
                melhor_diagonal = candidato_diagonal

    return melhor_angulo, melhor_diagonal


def _solucao_valida(
    largura: float,
    altura: float,
    desvio_topo: float,
    *,
    largura_total: float,
    altura_aba: float,
    tolerancia_largura: float,
    tolerancia_altura: float,
    tolerancia_topo: float,
) -> bool:
    return (
        abs(largura - largura_total) <= tolerancia_largura
        and abs(altura - altura_aba) <= tolerancia_altura
        and desvio_topo <= tolerancia_topo
    )


def gerar_perfil_boiadeira(
    *,
    altura_aba: float,
    largura_total: float,
    chapa: str,
    primeiro_gomo: float,
    tamanho_gomo_superior: float,
    tamanho_gomo_inferior: float,
    num_gomos: int,
    comprimento: float | None = None,
    tolerancia_largura: float | None = None,
    tolerancia_altura: float | None = None,
    tolerancia_topo: float | None = None,
    cancelar: Callable[[], bool] | None = None,
) -> dict:
    """Gera perfil boiadeira com largura acabada dominante e ângulo intermediário calculado."""
    from config_manager import parametros_padrao_gerador_boiadeira

    if _foi_cancelado(cancelar):
        raise ErroGeradorPeca("Geração cancelada pelo usuário.")

    padroes = parametros_padrao_gerador_boiadeira()
    tolerancias = tolerancias_gerador_boiadeira()
    if comprimento is None:
        comprimento = float(padroes["comprimento"])
    if tolerancia_largura is None:
        tolerancia_largura = tolerancias["tolerancia_largura"]
    if tolerancia_altura is None:
        tolerancia_altura = tolerancias["tolerancia_altura"]
    if tolerancia_topo is None:
        tolerancia_topo = tolerancias["tolerancia_topo"]

    altura_aba = float(altura_aba)
    largura_total = float(largura_total)
    primeiro_gomo = float(primeiro_gomo)
    tamanho_gomo_superior = float(tamanho_gomo_superior)
    tamanho_gomo_inferior = float(tamanho_gomo_inferior)
    num_gomos = int(num_gomos)
    comprimento = float(comprimento)
    codigo = _codigo_chapa(chapa)

    if altura_aba <= 0:
        raise ErroGeradorPeca("Informe uma altura de aba maior que zero.")
    if largura_total <= 0:
        raise ErroGeradorPeca("Informe uma largura total acabada maior que zero.")
    if primeiro_gomo <= 0:
        raise ErroGeradorPeca("Informe a dimensão do primeiro gomo maior que zero.")
    if tamanho_gomo_superior <= 0:
        raise ErroGeradorPeca("Informe o tamanho do gomo superior maior que zero.")
    if tamanho_gomo_inferior <= 0:
        raise ErroGeradorPeca("Informe o tamanho do gomo inferior maior que zero.")
    if num_gomos < 1:
        raise ErroGeradorPeca("O número de gomos deve ser pelo menos 1.")
    if comprimento <= 0:
        raise ErroGeradorPeca("Informe um comprimento maior que zero.")

    angulo_gomo, tamanho_diagonal, largura, altura, desvio_topo = _resolver_solucao_boiadeira(
        chapa=codigo,
        altura_aba=altura_aba,
        primeiro_gomo=primeiro_gomo,
        tamanho_gomo_superior=tamanho_gomo_superior,
        tamanho_gomo_inferior=tamanho_gomo_inferior,
        num_gomos=num_gomos,
        largura_total=largura_total,
        comprimento=comprimento,
        cancelar=cancelar,
    )

    contexto_polimento = {
        "chapa": codigo,
        "altura_aba": altura_aba,
        "primeiro_gomo": primeiro_gomo,
        "tamanho_gomo_superior": tamanho_gomo_superior,
        "tamanho_gomo_inferior": tamanho_gomo_inferior,
        "num_gomos": num_gomos,
        "largura_total": largura_total,
        "comprimento": comprimento,
    }

    for passos_angulo, passos_diagonal, alcance in (
        (0.01, 0.01, 0.05),
        (0.005, 0.005, 0.03),
        (0.002, 0.002, 0.012),
    ):
        if _foi_cancelado(cancelar):
            raise ErroGeradorPeca("Geração cancelada pelo usuário.")
        angulo_gomo, tamanho_diagonal = _polir_valores_arredondados(
            angulo=angulo_gomo,
            tamanho_diagonal=tamanho_diagonal,
            passos_angulo=passos_angulo,
            passos_diagonal=passos_diagonal,
            alcance=alcance,
            **contexto_polimento,
        )
        largura, altura, desvio_topo = _metricas_para_parametros(
            angulo=angulo_gomo,
            tamanho_diagonal=tamanho_diagonal,
            chapa=codigo,
            altura_aba=altura_aba,
            primeiro_gomo=primeiro_gomo,
            tamanho_gomo_superior=tamanho_gomo_superior,
            tamanho_gomo_inferior=tamanho_gomo_inferior,
            num_gomos=num_gomos,
            comprimento=comprimento,
        )
        if _solucao_valida(
            largura,
            altura,
            desvio_topo,
            largura_total=largura_total,
            altura_aba=altura_aba,
            tolerancia_largura=tolerancia_largura,
            tolerancia_altura=tolerancia_altura,
            tolerancia_topo=tolerancia_topo,
        ):
            break

    if not _solucao_valida(
        largura,
        altura,
        desvio_topo,
        largura_total=largura_total,
        altura_aba=altura_aba,
        tolerancia_largura=tolerancia_largura,
        tolerancia_altura=tolerancia_altura,
        tolerancia_topo=tolerancia_topo,
    ):
        raise ErroGeradorPeca(
            "Não foi possível fechar largura, altura e alinhamento do topo ao mesmo tempo "
            f"com largura {int(round(largura_total))} mm. "
            f"Melhor aproximação: X = {largura:.2f} mm, Y = {altura:.2f} mm, "
            f"desvio do topo = {desvio_topo:.2f} mm. "
            "Tente outra largura, número de gomos ou tamanho do gomo do meio."
        )

    segmentos = _montar_segmentos_boiadeira(
        altura_aba,
        primeiro_gomo,
        tamanho_gomo_superior,
        tamanho_gomo_inferior,
        tamanho_diagonal,
        num_gomos,
        angulo_gomo,
    )

    nome_largura = int(round(largura_total))
    return {
        "nome": f"Boiadeira {nome_largura} {codigo}",
        "chapa": codigo,
        "comprimento": comprimento,
        "segmentos": segmentos,
        "angulo_gomo": angulo_gomo,
        "tamanho_gomo_superior": tamanho_gomo_superior,
        "tamanho_gomo_inferior": tamanho_gomo_inferior,
        "tamanho_diagonal": tamanho_diagonal,
        "largura_acabada": round(largura, 3),
        "altura_acabada": round(altura, 3),
    }


GERADORES_DISPONIVEIS = (
    {
        "id": "boiadeira",
        "titulo": "Perfil Boiadeira",
        "descricao": (
            "Perfil corrugado simétrico: abas 90° nas pontas, gomos horizontais "
            "superiores e inferiores configuráveis."
        ),
    },
)
