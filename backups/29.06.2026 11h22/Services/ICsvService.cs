using System.Collections.Generic;
using CapitalAco.DrawingMacro.App.Models;

namespace CapitalAco.DrawingMacro.App.Services
{
    public interface ICsvService
    {
        List<Chapa> CarregarChapas();
    }
}
