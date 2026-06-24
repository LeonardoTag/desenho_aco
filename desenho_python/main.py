from __future__ import annotations

import logging
import sys
from datetime import date

import webview

from config_manager import (
    caminho_chapas,
    caminho_fonte_desenho,
    caminho_log,
    carregar_config,
    diretorio_base,
    obter_config,
    resolver_caminho,
    titulo_aplicacao,
)


def configurar_logging() -> None:
    config = obter_config()
    pasta_logs = resolver_caminho(config["pasta_logs"])
    pasta_logs.mkdir(parents=True, exist_ok=True)
    arquivo_log = caminho_log()

    cabecalho = (
        f"=== {titulo_aplicacao()} — sessão {date.today().isoformat()} ==="
    )

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(message)s",
        handlers=[
            logging.FileHandler(arquivo_log, encoding="utf-8"),
            logging.StreamHandler(sys.stderr),
        ],
        force=True,
    )
    logging.info(cabecalho)


def pre_flight_check() -> list[str]:
    erros: list[str] = []
    config = obter_config()

    chapas = caminho_chapas()
    if not chapas.is_file():
        erros.append(
            f"Arquivo de chapas não encontrado: {chapas.name}\n"
            f"Esperado em: {chapas}"
        )

    fonte = caminho_fonte_desenho()
    if not fonte.is_file():
        erros.append(
            f"Fonte de desenho não encontrada: {fonte.name}\n"
            f"Esperado em: {fonte}"
        )

    for chave in ("arquivo_biblioteca", "pasta_logs"):
        pasta = resolver_caminho(config[chave if chave != "pasta_logs" else "pasta_logs"])
        if chave == "arquivo_biblioteca":
            pasta = pasta.parent
        try:
            pasta.mkdir(parents=True, exist_ok=True)
            probe = pasta / ".perm_test"
            probe.write_text("", encoding="utf-8")
            probe.unlink(missing_ok=True)
        except OSError:
            erros.append(f"Sem permissão de escrita em: {pasta}")

    if not diretorio_base().is_dir():
        erros.append("Pasta base do aplicativo não está acessível.")

    return erros


def executar() -> None:
    carregar_config()
    configurar_logging()
    logging.info("Iniciando aplicativo WebView em %s", diretorio_base())

    erros = pre_flight_check()
    if erros:
        from tkinter import messagebox
        import tkinter as tk

        root = tk.Tk()
        root.withdraw()
        messagebox.showerror(
            "Não foi possível iniciar",
            "Corrija os problemas abaixo antes de continuar:\n\n"
            + "\n\n".join(erros),
        )
        root.destroy()
        logging.error("Pré-verificação falhou: %s", erros)
        sys.exit(1)

    logging.info("Pré-verificação concluída com sucesso. Inicializando pywebview.")

    from api import Api
    config = obter_config()

    largura = config.get("janela_largura", 1280)
    altura = config.get("janela_altura", 720)
    largura_min = config.get("janela_largura_min", 1024)
    altura_min = config.get("janela_altura_min", 640)
    titulo = titulo_aplicacao()

    api = Api()
    url_index = resolver_caminho("web/index.html")

    window = webview.create_window(
        title=titulo,
        url=str(url_index),
        js_api=api,
        width=largura,
        height=altura,
        min_size=(largura_min, altura_min),
    )
    api.set_window(window)

    webview.start(debug=False)
