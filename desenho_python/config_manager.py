from __future__ import annotations

import copy
import json
import logging
import sys
from pathlib import Path

DEFAULT_CONFIG: dict = {
    "versao_app": "1.2.4",
    "titulo_app": "Capital Aço — Construção de Perfil",
    "janela_largura": 1280,
    "janela_altura": 720,
    "janela_largura_min": 1024,
    "janela_altura_min": 640,
    "preview_tamanho_max": 520,
    "preview_tamanho_min": 400,
    "preview_debounce_ms": 100,
    "preview_debounce_alteracao_ms": 350,
    "comprimento_preview_placeholder": 500.0,
    "medida_placeholder": 50.0,
    "margem_canvas": 0.04,
    "margem_desenho_pct": 0.15,
    "margem_fundo": 3,
    "desenho_supersampling": 2,
    "desenho_fator_comprimento": 0.35,
    "desenho_angulo_comprimento": 55,
    "casas_decimais_calculo": 3,
    "casas_decimais_mostradas": 0,
    "arquivo_chapas": "data/chapas.csv",
    "arquivo_biblioteca": "data/biblioteca_pecas.json",
    "pasta_fontes": "assets/Noto_Sans",
    "fonte_desenho": "NotoSans-Black.ttf",
    "pasta_logs": "logs",
    "nome_log": "app_log.txt",
    "relatorio_nome_responsavel": "Leonardo",
    "relatorio_observacao": "",
    "relatorio_pasta_imagens": "images",
    "relatorio_logo_principal": "Capital Azul Recortado.png",
    "relatorio_logo_secundario": "só logo 100px.png",
    "relatorio_pecas_por_pagina": 9,
    "pasta_saida_relatorios": "files",
    "boiadeira_altura_aba_padrao": 20.0,
    "boiadeira_largura_total_padrao": 230.0,
    "boiadeira_primeiro_gomo_padrao": 30.0,
    "boiadeira_gomo_superior_padrao": 30.0,
    "boiadeira_gomo_inferior_padrao": 30.0,
    "boiadeira_num_gomos_padrao": 2,
    "boiadeira_comprimento_padrao": 3000.0,
    "boiadeira_tolerancia_largura": 0.5,
    "boiadeira_tolerancia_altura": 0.5,
    "boiadeira_tolerancia_topo": 0.5,
    "desenho_cota_distancia_preview": 0.88,
    "desenho_cota_distancia_relatorio": 0.82,
    "desenho_fonte_relatorio_fator": 1.55,
    "desenho_fonte_detalhamento_dobra_fator": 0.8,
    "relatorio_imagem_tamanho_pedido": 680,
    "relatorio_imagem_tamanho_dobra": 1150,
    "relatorio_largura_desenho_pct": 0.64,
    "relatorio_dobra_altura_imagem_pct": 0.50,
    "relatorio_dobra_largura_perfil_pct": 0.50,
    "desenho_fonte_base_fator": 0.028,
    "desenho_fonte_base_minima": 12.0,
    "desenho_fonte_relatorio_minima": 13.0,
    "desenho_fonte_dobra_minima": 11.0,
    "relatorio_dobra_fonte_titulo": 15.5,
    "relatorio_dobra_fonte_secao": 10.0,
    "relatorio_dobra_fonte_texto": 10.0,
    "relatorio_dobra_fonte_cota": 7.65,
    "relatorio_dobra_fonte_angulo": 7.225,
    "relatorio_dobra_fonte_sentido": 6.375,
    "relatorio_pedido_fonte_titulo": 16.0,
    "relatorio_pedido_fonte_subtitulo": 11.0,
    "relatorio_pedido_fonte_texto": 9.0,
    "relatorio_pedido_fonte_destaque": 10.0,
    "relatorio_pedido_fonte_rotulo_peca": 7.0,
    "relatorio_pedido_fonte_rotulo_campo": 8.0,
}


def parametros_padrao_gerador_boiadeira() -> dict[str, float | int]:
    cfg = obter_config()
    return {
        "altura_aba": float(cfg["boiadeira_altura_aba_padrao"]),
        "largura_total": float(cfg["boiadeira_largura_total_padrao"]),
        "primeiro_gomo": float(cfg["boiadeira_primeiro_gomo_padrao"]),
        "tamanho_gomo_superior": float(cfg["boiadeira_gomo_superior_padrao"]),
        "tamanho_gomo_inferior": float(cfg["boiadeira_gomo_inferior_padrao"]),
        "num_gomos": int(cfg["boiadeira_num_gomos_padrao"]),
        "comprimento": float(cfg["boiadeira_comprimento_padrao"]),
    }


def tolerancias_gerador_boiadeira() -> dict[str, float]:
    cfg = obter_config()
    return {
        "tolerancia_largura": float(cfg["boiadeira_tolerancia_largura"]),
        "tolerancia_altura": float(cfg["boiadeira_tolerancia_altura"]),
        "tolerancia_topo": float(cfg["boiadeira_tolerancia_topo"]),
    }

_config: dict | None = None


def diretorio_base() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parent


def caminho_config() -> Path:
    return diretorio_base() / "config.json"


def carregar_config(recriar_se_invalido: bool = True) -> dict:
    global _config
    caminho = caminho_config()
    dados = copy.deepcopy(DEFAULT_CONFIG)

    if caminho.exists():
        try:
            with open(caminho, encoding="utf-8") as arquivo:
                salvo = json.load(arquivo)
            if isinstance(salvo, dict):
                dados.update(salvo)
            elif recriar_se_invalido:
                gravar_config(dados)
        except (json.JSONDecodeError, OSError) as erro:
            logging.warning("config.json inválido; recriando padrões: %s", erro)
            gravar_config(dados)
    else:
        gravar_config(dados)

    _config = dados
    return dados


def gravar_config(config: dict | None = None) -> dict:
    global _config
    if config is None:
        config = obter_config()
    else:
        _config = copy.deepcopy(config)
    caminho = caminho_config()
    caminho.parent.mkdir(parents=True, exist_ok=True)
    with open(caminho, "w", encoding="utf-8") as arquivo:
        json.dump(_config, arquivo, ensure_ascii=False, indent=2)
    return _config


def restaurar_config_padrao() -> dict:
    global _config
    _config = copy.deepcopy(DEFAULT_CONFIG)
    gravar_config(_config)
    return _config


def obter_config() -> dict:
    if _config is None:
        return carregar_config()
    return _config


def resolver_caminho(relativo: str) -> Path:
    return diretorio_base() / relativo


def caminho_chapas() -> Path:
    return resolver_caminho(obter_config()["arquivo_chapas"])


def caminho_biblioteca() -> Path:
    return resolver_caminho(obter_config()["arquivo_biblioteca"])


def caminho_fonte_desenho() -> Path:
    cfg = obter_config()
    return resolver_caminho(cfg["pasta_fontes"]) / cfg["fonte_desenho"]


def caminho_log() -> Path:
    cfg = obter_config()
    from datetime import date
    nome_log_diario = f"app_log_{date.today().isoformat()}.txt"
    return resolver_caminho(cfg["pasta_logs"]) / nome_log_diario


def caminho_pasta_imagens_relatorio() -> Path:
    return resolver_caminho(obter_config()["relatorio_pasta_imagens"])


def caminho_logo_relatorio(principal: bool = True) -> Path:
    cfg = obter_config()
    nome = cfg["relatorio_logo_principal"] if principal else cfg["relatorio_logo_secundario"]
    return caminho_pasta_imagens_relatorio() / nome


def caminho_saida_relatorios() -> Path:
    caminho = resolver_caminho(obter_config()["pasta_saida_relatorios"])
    caminho.mkdir(parents=True, exist_ok=True)
    return caminho


def titulo_aplicacao(nome_peca: str = "") -> str:
    cfg = obter_config()
    base = f"{cfg['titulo_app']} v{cfg['versao_app']}"
    if nome_peca:
        return f"{base} — {nome_peca}"
    return base
