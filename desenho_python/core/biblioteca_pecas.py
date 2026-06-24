from __future__ import annotations

from datetime import datetime, timezone
import json
import uuid

from config_manager import caminho_biblioteca

VERSAO = 1


def _agora_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def _arquivo_biblioteca():
    return caminho_biblioteca()


def _carregar_dados() -> dict:
    arquivo = _arquivo_biblioteca()
    if not arquivo.exists():
        return {"versao": VERSAO, "pecas": []}
    try:
        with open(arquivo, encoding="utf-8") as handle:
            dados = json.load(handle)
    except (json.JSONDecodeError, OSError):
        dados = {"versao": VERSAO, "pecas": []}
        _gravar_dados(dados)
        return dados
    if "pecas" not in dados:
        dados["pecas"] = []
    dados["versao"] = VERSAO
    return dados


def _gravar_dados(dados: dict) -> None:
    dados["versao"] = VERSAO
    arquivo = _arquivo_biblioteca()
    arquivo.parent.mkdir(parents=True, exist_ok=True)
    with open(arquivo, "w", encoding="utf-8") as handle:
        json.dump(dados, handle, ensure_ascii=False, indent=2)


def listar_modelos(filtro: str = "") -> list[dict]:
    filtro = filtro.strip().lower()
    modelos = _carregar_dados()["pecas"]
    if not filtro:
        return sorted(modelos, key=lambda m: m.get("nome", "").lower())
    return sorted(
        [
            modelo
            for modelo in modelos
            if filtro in modelo.get("nome", "").lower()
            or filtro in modelo.get("descricao", "").lower()
            or filtro in modelo.get("chapa", "").lower().lstrip("#")
        ],
        key=lambda m: m.get("nome", "").lower(),
    )


def obter_modelo(modelo_id: str) -> dict | None:
    for modelo in _carregar_dados()["pecas"]:
        if modelo.get("id") == modelo_id:
            return modelo
    return None


def salvar_modelo(
    nome: str,
    chapa: str,
    comprimento: float | None,
    segmentos: list[list],
    modelo_id: str | None = None,
    descricao: str = "",
) -> dict:
    nome = nome.strip()
    if not nome:
        raise ValueError("Informe um nome para a peça.")

    dados = _carregar_dados()
    agora = _agora_iso()
    registro = {
        "id": modelo_id or str(uuid.uuid4()),
        "nome": nome,
        "descricao": descricao.strip(),
        "chapa": chapa,
        "comprimento": comprimento,
        "segmentos": segmentos,
        "atualizado_em": agora,
    }

    for indice, existente in enumerate(dados["pecas"]):
        if existente.get("id") == registro["id"]:
            registro["criado_em"] = existente.get("criado_em", agora)
            dados["pecas"][indice] = registro
            _gravar_dados(dados)
            return registro

    registro["criado_em"] = agora
    dados["pecas"].append(registro)
    _gravar_dados(dados)
    return registro


def excluir_modelo(modelo_id: str) -> bool:
    dados = _carregar_dados()
    tamanho_antes = len(dados["pecas"])
    dados["pecas"] = [m for m in dados["pecas"] if m.get("id") != modelo_id]
    if len(dados["pecas"]) == tamanho_antes:
        return False
    _gravar_dados(dados)
    return True


def resumo_modelo(modelo: dict) -> str:
    chapa = str(modelo.get("chapa", "")).lstrip("#")
    segmentos = modelo.get("segmentos") or []
    comprimento = modelo.get("comprimento")
    comp_txt = f"{comprimento:g} mm" if comprimento else "sem comprimento"
    return f"{modelo.get('nome', '?')}  —  #{chapa}, {len(segmentos)} seg., {comp_txt}"
