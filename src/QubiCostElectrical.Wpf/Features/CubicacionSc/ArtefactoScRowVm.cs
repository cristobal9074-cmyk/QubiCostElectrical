using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ArtefactoScRowVm : ViewModelBase
{
    internal ArtefactoSCRow Source { get; }

    public ArtefactoScRowVm(ArtefactoSCRow row) => Source = row;

    public string Nombre
    {
        get => Source.Nombre;
        set { Source.Nombre = value; OnPropertyChanged(); }
    }

    public string Categoria
    {
        get => Source.Categoria;
        set { Source.Categoria = value; OnPropertyChanged(); }
    }

    public decimal Cantidad
    {
        get => Source.Cantidad;
        set { Source.Cantidad = value; OnPropertyChanged(); }
    }
}
