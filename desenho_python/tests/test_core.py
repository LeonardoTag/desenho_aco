import unittest

from config_manager import carregar_config, diretorio_base
from core import biblioteca_pecas, desenhar


class TestCoreDesenho(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        carregar_config()

    def test_listar_chapas_nao_vazio(self):
        chapas = desenhar.listar_chapas()
        self.assertGreater(len(chapas), 0)
        self.assertIn("codigo", chapas[0])
        self.assertIn("espessura", chapas[0])

    def test_dimensoes_acabadas_incluem_espessura(self):
        inst = {
            "chapa": desenhar.listar_chapas()[0]["codigo"],
            "comprimento": 500,
            "segmentos": [["E", 90, 100, "e"], ["N", 90, 50, "e"]],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        sem = desenhar.calcular_dimensoes_acabadas({**pol, "espessura": 0})
        com = desenhar.calcular_dimensoes_acabadas(pol)
        self.assertIsNotNone(sem)
        self.assertIsNotNone(com)
        self.assertGreaterEqual(com[0], sem[0])
        self.assertGreaterEqual(com[1], sem[1])

    def test_soma_internas_sanity(self):
        inst = {
            "chapa": desenhar.listar_chapas()[0]["codigo"],
            "comprimento": 500,
            "segmentos": [["E", 90, 100, "i"], ["N", 90, 50, "i"]],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        soma = desenhar.calcular_soma_medidas_internas(pol)
        self.assertIsNotNone(soma)
        self.assertGreater(soma, 0)

    def test_peso_kg_arredonda_para_cima(self):
        chapa = desenhar.listar_chapas()[0]["codigo"]
        inst = {
            "chapa": chapa,
            "comprimento": 500,
            "segmentos": [["E", 90, 100, "i"], ["N", 90, 50, "i"]],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        chapa_dados = desenhar.consultar_chapa(chapa)
        largura = desenhar.calcular_largura_corte(pol)
        esperado_bruto = largura * 500 * chapa_dados["coeficiente"] / 1_000_000
        peso = desenhar.calcular_peso_kg(
            pol, quantidade=1, codigo_chapa=chapa, arredondar=True
        )
        self.assertIsNotNone(peso)
        self.assertIsInstance(peso, int)
        self.assertEqual(peso, int(__import__("math").ceil(esperado_bruto)))

    def test_peso_kg_total_com_quantidade(self):
        chapa = desenhar.listar_chapas()[0]["codigo"]
        inst = {
            "chapa": chapa,
            "comprimento": 500,
            "segmentos": [["E", 90, 100, "i"], ["N", 90, 50, "i"]],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        chapa_dados = desenhar.consultar_chapa(chapa)
        largura = desenhar.calcular_largura_corte(pol)
        unitario = largura * 500 * chapa_dados["coeficiente"] / 1_000_000
        total = desenhar.calcular_peso_kg(
            pol, quantidade=10, codigo_chapa=chapa, arredondar=True
        )
        self.assertEqual(total, int(__import__("math").ceil(unitario * 10)))

    def test_perfil_cruza_detecta_intersecao(self):
        self.assertTrue(
            desenhar._segmentos_centro_cruzam((0, 0), (100, 0), (50, -10), (50, 10))
        )
        inst = {
            "chapa": desenhar.listar_chapas()[0]["codigo"],
            "comprimento": 500,
            "segmentos": [["E", 90, 100, "i"], ["N", 90, 50, "i"]],
        }
        self.assertFalse(desenhar.perfil_cruza_a_si_mesmo(inst))

    def test_medidas_externas_fecham_com_angulos_mistos(self):
        inst = {
            "chapa": "#16",
            "comprimento": 500,
            "segmentos": [
                ["E", 90, 50, "e"],
                ["N", 45, 50, "e"],
                ["E", 90, 50, "e"],
            ],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        _, medidas, _ = desenhar.gerar_corte_medidas_angulos(pol)
        for indice, medida in enumerate(medidas, start=1):
            self.assertAlmostEqual(
                float(medida["externa"]),
                50.0,
                delta=0.05,
                msg=f"segmento {indice}",
            )

    def test_dobra_minima_aviso(self):
        chapa = desenhar.listar_chapas()[0]["codigo"]
        inst = {
            "chapa": chapa,
            "comprimento": 500,
            "segmentos": [["E", 90, 5, "i"], ["N", 90, 50, "i"]],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        avisos = desenhar.verificar_dobras_abaixo_minima(pol, chapa)
        self.assertTrue(len(avisos) >= 1)

    def test_primeira_perna_usa_angulo_real_da_dobra(self):
        inst = {
            "chapa": "#16",
            "comprimento": 500,
            "segmentos": [
                ["E", 90, 50, "e"],
                ["N", 45, 50, "e"],
            ],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        _, medidas, _ = desenhar.gerar_corte_medidas_angulos(pol)
        self.assertAlmostEqual(float(medidas[0]["externa"]), 50.0, delta=0.05)

    def test_medidas_externas_fecham_em_dobras_90(self):
        inst = {
            "chapa": "#16",
            "comprimento": 500,
            "segmentos": [
                ["S", 90, 50, "e"],
                ["E", 90, 50, "e"],
                ["N", 90, 50, "e"],
            ],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        _, medidas, _ = desenhar.gerar_corte_medidas_angulos(pol)
        for indice, medida in enumerate(medidas, start=1):
            self.assertAlmostEqual(
                float(medida["externa"]),
                50.0,
                places=2,
                msg=f"segmento {indice}",
            )


class TestBibliotecaPecas(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        carregar_config()

    def test_salvar_listar_excluir(self):
        registro = biblioteca_pecas.salvar_modelo(
            nome="__teste_unitario__",
            chapa=desenhar.listar_chapas()[0]["codigo"],
            comprimento=100.0,
            segmentos=[["E", 90, 50, "e"]],
        )
        self.assertIn("id", registro)
        encontrados = biblioteca_pecas.listar_modelos("__teste_unitario__")
        self.assertTrue(any(m["id"] == registro["id"] for m in encontrados))
        self.assertTrue(biblioteca_pecas.excluir_modelo(registro["id"]))


class TestConfig(unittest.TestCase):
    def test_diretorio_base_existe(self):
        self.assertTrue(diretorio_base().is_dir())


class TestNumeros(unittest.TestCase):
    def test_parse_e_formatar_com_virgula(self):
        from core.numeros import formatar_compacto, parse_numero

        self.assertEqual(parse_numero("12,5"), 12.5)
        self.assertEqual(parse_numero("1.234,5"), 1234.5)
        self.assertEqual(formatar_compacto(12.5), "12,5")
        self.assertEqual(formatar_compacto(100), "100")


class TestGeradoresPecas(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        carregar_config()

    def test_gerar_perfil_boiadeira_largura_alvo(self):
        from core.geradores_pecas import gerar_perfil_boiadeira

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
        self.assertEqual(len(dados["segmentos"]), 11)
        self.assertEqual(dados["segmentos"][0][1], 90.0)
        self.assertEqual(dados["segmentos"][1][1], 90.0)
        self.assertEqual(dados["segmentos"][-2], ["E", 90.0, 30, "e"])
        self.assertEqual(dados["segmentos"][-1], ["N", 90.0, 20, "e"])
        self.assertEqual(dados["segmentos"][2][1], dados["angulo_gomo"])
        self.assertAlmostEqual(dados["largura_acabada"], 230, delta=0.5)
        self.assertAlmostEqual(dados["altura_acabada"], 20, delta=0.5)
        self.assertGreater(dados["tamanho_diagonal"], 0)
        self.assertGreater(dados["angulo_gomo"], 0)
        self.assertLess(dados["angulo_gomo"], 90)

    def test_gerar_perfil_boiadeira_gomos_superior_inferior(self):
        from core.geradores_pecas import gerar_perfil_boiadeira

        dados = gerar_perfil_boiadeira(
            altura_aba=20,
            largura_total=230,
            chapa="#14",
            primeiro_gomo=30,
            tamanho_gomo_superior=30,
            tamanho_gomo_inferior=25,
            num_gomos=2,
            comprimento=3000,
        )
        segmentos = dados["segmentos"]
        indices_topo = {3, 7}
        for indice, segmento in enumerate(segmentos):
            if segmento[0] == "E" and segmento[1] != 90.0:
                if indice in indices_topo:
                    self.assertEqual(segmento[2], 30)
                else:
                    self.assertEqual(segmento[2], 25)

    def test_gerar_perfil_boiadeira_segmentos_simetricos(self):
        from core.geradores_pecas import gerar_perfil_boiadeira

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
        segmentos = dados["segmentos"]
        self.assertEqual(segmentos[1], segmentos[-2])
        angulos_meio = {segmento[1] for segmento in segmentos[2:-2]}
        self.assertNotIn(90.0, angulos_meio)
        superiores = [
            segmento[2]
            for indice, segmento in enumerate(segmentos)
            if segmento[0] == "E"
            and segmento[1] != 90.0
            and indice in {3, 7}
        ]
        inferiores = [
            segmento[2]
            for indice, segmento in enumerate(segmentos)
            if segmento[0] == "E" and segmento[1] != 90.0 and indice not in {3, 7}
        ]
        self.assertEqual(len(set(superiores)), 1)
        self.assertEqual(len(set(inferiores)), 1)
        self.assertEqual(superiores[0], 30)
        self.assertEqual(inferiores[0], 30)
        self.assertEqual(dados["tamanho_gomo_superior"], 30)
        self.assertEqual(dados["tamanho_gomo_inferior"], 30)


class TestRelatorioDobra(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        carregar_config()

    def test_dados_planificacao_soma_cadeia(self):
        chapa = desenhar.listar_chapas()[0]["codigo"]
        inst = {
            "chapa": chapa,
            "comprimento": 500,
            "segmentos": [
                ["S", 90, 50, "e"],
                ["E", 90, 50, "e"],
                ["N", 90, 50, "e"],
            ],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        dados = desenhar.gerar_dados_planificacao(pol)
        comprimento_retas = sum(
            t["comprimento"] for t in dados["trechos"] if t["tipo"] == "reta"
        )
        comprimento_dobras = sum(
            t["comprimento"] for t in dados["trechos"] if t["tipo"] == "dobra"
        )
        self.assertAlmostEqual(
            comprimento_retas + comprimento_dobras, dados["corte_total"], delta=1.0
        )
        self.assertEqual(len(dados["marcas_dobra"]), 2)
        self.assertEqual(dados["posicoes_ordenadas"][0], 0)
        self.assertEqual(dados["posicoes_ordenadas"][-1], dados["corte_total"])
        for marca in dados["marcas_dobra"]:
            self.assertIsInstance(marca["posicao"], int)

    def test_ami_pontas_simetricas(self):
        inst = {
            "chapa": "#16",
            "comprimento": 2300,
            "segmentos": [
                ["W", 90, 60, "e"],
                ["S", 90, 30, "e"],
                ["E", 90, 22, "e"],
                ["S", 90, 50, "e"],
                ["W", 90, 22, "e"],
                ["S", 90, 30, "e"],
                ["E", 90, 60, "e"],
            ],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        _, medidas, _ = desenhar.gerar_corte_medidas_angulos(pol)
        self.assertEqual(medidas[0]["interna"], medidas[-1]["interna"])
        self.assertEqual(medidas[0]["externa"], medidas[-1]["externa"])
        self.assertEqual(medidas[2]["interna"], medidas[4]["interna"])
        dados = desenhar.gerar_dados_planificacao(pol)
        self.assertEqual(dados["cadeia"][0], dados["cadeia"][-1])

    def test_marcas_dobra_no_centro_da_zona(self):
        chapa = desenhar.listar_chapas()[0]["codigo"]
        inst = {
            "chapa": chapa,
            "comprimento": 500,
            "segmentos": [["E", 90, 100, "e"], ["N", 90, 50, "e"]],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        dados = desenhar.gerar_dados_planificacao(pol)
        acumulado = 0
        for trecho in dados["trechos"]:
            if trecho["tipo"] == "dobra":
                centro_esperado = int(round(acumulado + trecho["comprimento"] / 2))
                self.assertEqual(dados["marcas_dobra"][0]["posicao"], centro_esperado)
                break
            acumulado += trecho["comprimento"]

    def test_gerar_pdf_detalhamento_dobra(self):
        from core.relatorio_dobra import gerar_pdf_detalhamento_dobra

        chapa = desenhar.listar_chapas()[0]["codigo"]
        inst = {
            "chapa": chapa,
            "comprimento": 500,
            "segmentos": [["E", 90, 100, "e"], ["N", 90, 50, "e"]],
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        destino = diretorio_base() / "files" / "__teste_detalhamento_dobra__.pdf"
        if destino.exists():
            destino.unlink()
        caminho = gerar_pdf_detalhamento_dobra(
            pol,
            destino,
            nome_peca="Teste",
            codigo_chapa=chapa,
            comprimento_peca=500,
        )
        self.assertTrue(caminho.exists())
        self.assertGreater(caminho.stat().st_size, 1000)
        destino.unlink()


class TestRelatorioPedido(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        carregar_config()

    def test_gerar_pdf_pedido(self):
        from dataclasses import dataclass

        from core.relatorio_pedido import gerar_pdf_pedido

        @dataclass
        class PecaFake:
            codigo_chapa: str
            indice_chapa: int
            comprimento: float
            quantidade: int = 1
            nome_peca: str = ""
            segmentos: list = None

            def montar_instrucoes(self):
                return {
                    "chapa": self.codigo_chapa,
                    "comprimento": self.comprimento,
                    "segmentos": self.segmentos or [
                        ["E", 90, 100, "i"],
                        ["N", 90, 50, "i"],
                    ],
                }

        chapa = desenhar.listar_chapas()[0]["codigo"]
        peca = PecaFake(
            codigo_chapa=chapa,
            indice_chapa=0,
            comprimento=500,
            quantidade=2,
            nome_peca="Teste",
        )
        destino = diretorio_base() / "files" / "__teste_folha_pedido__.pdf"
        if destino.exists():
            destino.unlink()
        caminho = gerar_pdf_pedido([peca], destino, observacao="Teste unitário")
        self.assertTrue(caminho.exists())
        self.assertGreater(caminho.stat().st_size, 1000)
        caminho.unlink()


class TestCurvasECalandragem(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        carregar_config()

    def test_segmento_curvo_parser(self):
        inst = {
            "chapa": "#16",
            "comprimento": 1000.0,
            "segmentos": [
                ["E", 90.0, 100.0, "e"],
                ["S", 90.0, 157.079, "e", True, {
                    "raio": 100.0,
                    "comprimento_curva": 157.079,
                    "angulo_curva": 90.0,
                    "tipo_raio": "interno"
                }],
                ["W", 90.0, 100.0, "e"]
            ]
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        self.assertEqual(len(pol["coordenadas_polares"]), 3)

        absolutas_discretas = desenhar._gerar_coordenadas_absolutas_discretas(pol)
        self.assertEqual(len(absolutas_discretas), 33)

        dados = desenhar.gerar_dados_planificacao(pol)
        self.assertEqual(len(dados["marcas_calandragem"]), 1)
        self.assertEqual(len(dados["marcas_dobra"]), 0)
        self.assertAlmostEqual(dados["corte_total"], 357.0, delta=1.5)

    def test_tubo_calandrado_gerador(self):
        inst = {
            "chapa": "#16",
            "comprimento": 2000.0,
            "segmentos": [
                ["E", 360.0, 628.3, "e", True, {
                    "raio": 100.0,
                    "comprimento_curva": 628.3,
                    "angulo_curva": 360.0,
                    "tipo_raio": "interno"
                }]
            ]
        }
        pol = desenhar.converter_instrucoes_convencionais_para_coordenadas_polares(inst)
        absolutas_discretas = desenhar._gerar_coordenadas_absolutas_discretas(pol)
        
        start_pt = absolutas_discretas[0]
        end_pt = absolutas_discretas[-1]
        self.assertAlmostEqual(start_pt[0], end_pt[0], delta=0.1)
        self.assertAlmostEqual(start_pt[1], end_pt[1], delta=0.1)

        dados = desenhar.gerar_dados_planificacao(pol)
        self.assertAlmostEqual(dados["corte_total"], 628.0, delta=1.5)


if __name__ == "__main__":
    unittest.main()
