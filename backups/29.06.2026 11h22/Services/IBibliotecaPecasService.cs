using System;
using System.Collections.Generic;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public interface IBibliotecaPecasService
    {
        List<ModeloPeca> ListarModelos(string filtro = "");
        ModeloPeca? ObterModelo(Guid id);
        ModeloPeca SalvarModelo(string nome, string chapa, double? comprimento, List<Segmento> segmentos, Guid? id = null, string descricao = "");
        bool ExcluirModelo(Guid id);
    }
}
