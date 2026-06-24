from __future__ import annotations

from config_manager import obter_config


def _casas_mostradas() -> int:
    return int(obter_config().get("casas_decimais_mostradas", 0))


def parse_numero(texto: str) -> float | None:
    texto = str(texto).strip().replace(" ", "")
    if not texto:
        return None
    if "," in texto and "." in texto:
        texto = texto.replace(".", "").replace(",", ".")
    else:
        texto = texto.replace(",", ".")
    try:
        return float(texto)
    except ValueError:
        return None


def parse_inteiro(texto: str) -> int | None:
    valor = parse_numero(texto)
    if valor is None:
        return None
    try:
        return int(valor)
    except (TypeError, ValueError):
        return None


def formatar_numero(valor, casas_decimais: int | None = None) -> str:
    if valor is None:
        return ""
    if casas_decimais is None:
        casas_decimais = _casas_mostradas()
    numero = float(valor)
    if casas_decimais == 0:
        texto = str(int(round(numero)))
    else:
        texto = f"{numero:.{casas_decimais}f}".rstrip("0").rstrip(".")
    return texto.replace(".", ",")


def formatar_compacto(valor) -> str:
    """Equivalente legível ao :g, com vírgula decimal."""
    if valor is None:
        return ""
    numero = float(valor)
    if numero.is_integer():
        return str(int(numero))
    texto = f"{numero:g}"
    if "e" in texto or "E" in texto:
        return texto.replace(".", ",")
    return texto.replace(".", ",")
