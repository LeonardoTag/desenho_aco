using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapitalAco.DrawingMacro.App.Models;
using CapitalAco.DrawingMacro.App.Services;

namespace CapitalAco.DrawingMacro.App.ViewModels
{
    public partial class BibliotecaViewModel : ObservableObject
    {
        private readonly IBibliotecaPecasService _bibliotecaService;

        [ObservableProperty]
        private string _filtro = string.Empty;

        public ObservableCollection<ModeloPeca> Modelos { get; } = new();

        public event Action<ModeloPeca>? PecaSelecionadaParaEdicao;

        public BibliotecaViewModel(IBibliotecaPecasService bibliotecaService)
        {
            _bibliotecaService = bibliotecaService;
            CarregarModelos();
        }

        [RelayCommand]
        public void CarregarModelos()
        {
            Modelos.Clear();
            var lista = _bibliotecaService.ListarModelos(Filtro);
            foreach (var item in lista)
            {
                Modelos.Add(item);
            }
        }

        [RelayCommand]
        private void Pesquisar()
        {
            CarregarModelos();
        }

        [RelayCommand]
        private void CarregarNoEditor(ModeloPeca? peca)
        {
            if (peca != null)
            {
                PecaSelecionadaParaEdicao?.Invoke(peca);
            }
        }

        [RelayCommand]
        private void ExcluirPeca(ModeloPeca? peca)
        {
            if (peca != null)
            {
                var sucesso = _bibliotecaService.ExcluirModelo(peca.Id);
                if (sucesso)
                {
                    CarregarModelos();
                }
            }
        }
    }
}
