from __future__ import annotations

import base64
import json
import logging
import os
import shutil
import subprocess
import sys
from io import BytesIO
from pathlib import Path
from PIL import Image

from config_manager import (
    caminho_chapas,
    caminho_biblioteca,
    caminho_saida_relatorios,
    diretorio_base,
    obter_config,
    gravar_config,
    resolver_caminho,
)
from core import biblioteca_pecas, desenhar
from core.geradores_pecas import gerar_perfil_boiadeira, ErroGeradorPeca


class PecaPedidoWrapper:
    def __init__(self, dados_peca: dict):
        self.codigo_chapa = dados_peca["codigo_chapa"]
        self.comprimento = float(dados_peca["comprimento"])
        self.quantidade = int(dados_peca["quantidade"])
        self.nome_peca = dados_peca.get("nome_peca") or ""
        self.segmentos = dados_peca["segmentos"]
        self.observacao = dados_peca.get("observacao") or ""

    def montar_instrucoes(self) -> dict:
        return {
            "chapa": self.codigo_chapa,
            "comprimento": self.comprimento,
            "segmentos": self.segmentos,
        }


class Api:
    def __init__(self):
        # Atributos com underline inicial para evitar recursão infinita e ignorar acessibilidade do Windows
        self._window = None
        self._chapas_cache = []

    def set_window(self, window):
        self._window = window

    def obter_chapas(self) -> list[dict]:
        try:
            chapas = desenhar.listar_chapas()
            self._chapas_cache = chapas
            return chapas
        except Exception as e:
            logging.exception("Erro ao obter chapas")
            return []

    def listar_modelos(self, filtro: str = "") -> list[dict]:
        try:
            return biblioteca_pecas.listar_modelos(filtro)
        except Exception:
            logging.exception("Erro ao listar modelos")
            return []

    def salvar_modelo(
        self,
        nome: str,
        chapa: str,
        comprimento: float | None,
        segmentos: list[list],
        modelo_id: str | None = None,
        descricao: str = "",
    ) -> dict | None:
        try:
            return biblioteca_pecas.salvar_modelo(
                nome=nome,
                chapa=chapa,
                comprimento=comprimento,
                segmentos=segmentos,
                modelo_id=modelo_id,
                descricao=descricao,
            )
        except Exception as e:
            logging.exception("Erro ao salvar modelo na biblioteca")
            return {"erro": str(e)}

    def excluir_modelo(self, modelo_id: str) -> bool:
        try:
            return biblioteca_pecas.excluir_modelo(modelo_id)
        except Exception:
            logging.exception("Erro ao excluir modelo")
            return False

    def obter_configuracoes(self) -> dict:
        try:
            return obter_config()
        except Exception:
            logging.exception("Erro ao obter configurações")
            return {}

    def salvar_configuracoes(self, novas_config: dict) -> dict:
        try:
            config = obter_config().copy()
            config.update(novas_config)
            gravar_config(config)
            return {"sucesso": True}
        except Exception as e:
            logging.exception("Erro ao salvar configurações")
            return {"sucesso": False, "erro": str(e)}

    def gerar_preview(self, instrucoes_conv: dict, largura: int, altura: int) -> str:
        try:
            # Conversão e renderização da imagem
            instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(
                instrucoes_conv
            )
            tamanho = (max(largura, 400), max(altura, 250))
            imagem = desenhar.renderizar_imagem(
                instrucoes,
                tamanho=tamanho,
                mostrar_medidas=True,
                destino="preview",
            )
            # Converter imagem PIL em base64 PNG
            buffered = BytesIO()
            imagem.save(buffered, format="PNG")
            img_str = base64.b64encode(buffered.getvalue()).decode("utf-8")
            return f"data:image/png;base64,{img_str}"
        except Exception as e:
            logging.exception("Erro ao gerar preview da peça")
            return ""

    def calcular_peso(self, instrucoes_conv: dict, quantidade: int = 1) -> float | None:
        try:
            instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(
                instrucoes_conv
            )
            peso = desenhar.calcular_peso_kg(
                instrucoes,
                quantidade,
                codigo_chapa=instrucoes_conv.get("chapa"),
                arredondar=True,
            )
            return peso
        except Exception:
            logging.exception("Erro ao calcular peso")
            return None

    def calcular_largura_corte(self, instrucoes_conv: dict) -> float | None:
        try:
            instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(
                instrucoes_conv
            )
            return desenhar.calcular_largura_corte(instrucoes)
        except Exception:
            logging.exception("Erro ao calcular largura de corte")
            return None

    def obter_dimensoes_acabadas(self, instrucoes_conv: dict) -> dict | None:
        try:
            instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(
                instrucoes_conv
            )
            dim = desenhar.calcular_dimensoes_acabadas(instrucoes)
            if dim:
                return {"x": round(dim[0], 1), "y": round(dim[1], 1)}
            return None
        except Exception:
            logging.exception("Erro ao obter dimensões acabadas")
            return None

    def verificar_avisos(self, instrucoes_conv: dict) -> list[str]:
        try:
            avisos = []
            instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(
                instrucoes_conv
            )
            # 1. Verificar dobras mínimas
            chapa_codigo = instrucoes_conv.get("chapa")
            avisos_dobra = desenhar.verificar_dobras_abaixo_minima(instrucoes, chapa_codigo)
            avisos.extend(avisos_dobra)
            # 2. Verificar se o perfil cruza a si mesmo
            if desenhar.perfil_cruza_a_si_mesmo(instrucoes_conv):
                avisos.append("O perfil cruza a si mesmo!")
            return avisos
        except Exception as e:
            logging.exception("Erro ao verificar avisos")
            return []

    def gerar_boiadeira(self, params: dict) -> dict:
        try:
            dados = gerar_perfil_boiadeira(
                altura_aba=float(params["altura_aba"]),
                largura_total=float(params["largura_total"]),
                chapa=params["chapa"],
                primeiro_gomo=float(params["primeiro_gomo"]),
                tamanho_gomo_superior=float(params["tamanho_gomo_superior"]),
                tamanho_gomo_inferior=float(params["tamanho_gomo_inferior"]),
                num_gomos=int(params["num_gomos"]),
                comprimento=float(params["comprimento"]),
            )
            return {"sucesso": True, "peca": dados}
        except ErroGeradorPeca as err:
            return {"sucesso": False, "erro": str(err)}
        except Exception as e:
            logging.exception("Erro inesperado no gerador Boiadeira")
            return {"sucesso": False, "erro": "Erro interno no cálculo do perfil."}

    def gerar_relatorio_dobra(self, instrucoes_conv: dict, nome_peca: str) -> dict:
        try:
            from core.relatorio_dobra import (
                gerar_pdf_detalhamento_dobra,
                nome_arquivo_detalhamento_padrao,
            )

            pasta_saida = caminho_saida_relatorios()
            nome_arquivo = nome_arquivo_detalhamento_padrao()
            caminho_pdf = pasta_saida / nome_arquivo

            instrucoes = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(
                instrucoes_conv
            )

            gerar_pdf_detalhamento_dobra(
                instrucoes=instrucoes,
                caminho_saida=caminho_pdf,
                nome_peca=nome_peca,
                codigo_chapa=instrucoes_conv["chapa"],
                comprimento_peca=float(instrucoes_conv["comprimento"]),
            )

            try:
                if sys.platform == "win32":
                    os.startfile(str(caminho_pdf))
                elif sys.platform == "darwin":
                    subprocess.Popen(["open", str(caminho_pdf)])
                else:
                    subprocess.Popen(["xdg-open", str(caminho_pdf)])
            except Exception:
                logging.exception("Erro ao abrir PDF automaticamente")

            return {"sucesso": True, "caminho": str(caminho_pdf), "nome": nome_arquivo}
        except Exception as e:
            logging.exception("Erro ao gerar relatório de dobra")
            return {"sucesso": False, "erro": str(e)}

    def gerar_relatorio_pedido(self, pecas_dados: list[dict], observacao: str = "") -> dict:
        try:
            from core.relatorio_pedido import gerar_pdf_pedido, nome_arquivo_pedido_padrao

            pasta_saida = caminho_saida_relatorios()
            nome_arquivo = nome_arquivo_pedido_padrao()
            caminho_pdf = pasta_saida / nome_arquivo

            pecas = [PecaPedidoWrapper(d) for d in pecas_dados]
            chapas = self.obter_chapas()

            gerar_pdf_pedido(
                pecas=pecas,
                caminho_saida=caminho_pdf,
                chapas=chapas,
                observacao=observacao,
            )

            try:
                if sys.platform == "win32":
                    os.startfile(str(caminho_pdf))
                elif sys.platform == "darwin":
                    subprocess.Popen(["open", str(caminho_pdf)])
                else:
                    subprocess.Popen(["xdg-open", str(caminho_pdf)])
            except Exception:
                logging.exception("Erro ao abrir PDF de ordem de produção automaticamente")

            return {"sucesso": True, "caminho": str(caminho_pdf), "nome": nome_arquivo}
        except Exception as e:
            logging.exception("Erro ao gerar relatório do pedido")
            return {"sucesso": False, "erro": str(e)}

    def abrir_pasta_saida(self) -> bool:
        try:
            pasta = caminho_saida_relatorios()
            if sys.platform == "win32":
                os.startfile(str(pasta))
            elif sys.platform == "darwin":
                subprocess.Popen(["open", str(pasta)])
            else:
                subprocess.Popen(["xdg-open", str(pasta)])
            return True
        except Exception:
            logging.exception("Erro ao abrir pasta de relatórios")
            return False

    def executar_migracao_pasta(self, antiga_rel: str, nova_rel: str, forcar_copia: bool | None = None) -> str:
        try:
            antiga = resolver_caminho(antiga_rel)
            nova = resolver_caminho(nova_rel)
            
            if not antiga.exists() or antiga == nova:
                return "concluido"
                
            nova.mkdir(parents=True, exist_ok=True)
            arquivos_antigos = list(antiga.glob("*.pdf"))
            arquivos_novos = list(nova.glob("*.pdf"))
            
            if not arquivos_novos or forcar_copia is True:
                # Copiar arquivos históricos
                for arq in arquivos_antigos:
                    try:
                        shutil.copy2(arq, nova / arq.name)
                    except Exception:
                        logging.exception(f"Erro ao copiar {arq} para {nova}")
                return "copiado"
            elif forcar_copia is False:
                # Manter novos arquivos e não copiar
                return "concluido"
            else:
                # Necessita confirmação do usuário
                return "confirmacao_necessaria"
        except Exception as e:
            logging.exception("Erro na migração de pastas")
            return "erro"
