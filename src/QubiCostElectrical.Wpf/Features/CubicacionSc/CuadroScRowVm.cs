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

public sealed class CuadroScRowVm : ViewModelBase
{
    private readonly CuadroCargaSCRow row;
    private readonly Action? onRenamed;
    private bool isEditing;

    public CuadroScRowVm(CuadroCargaSCRow row, Action? onRenamed = null)
    {
        this.row = row;
        this.onRenamed = onRenamed;
    }

    public string Id => row.Id;

    public string Nombre
    {
        get => row.Nombre;
        set
        {
            var limpio = string.IsNullOrWhiteSpace(value) ? row.Nombre : value.Trim();
            if (limpio == row.Nombre) return;
            row.Nombre = limpio;
            OnPropertyChanged();
            onRenamed?.Invoke();
        }
    }

    // Edición inline del nombre en el sidebar: doble clic activa el TextBox; al confirmar
    // (Enter/perder foco) o cancelar (Escape) vuelve a false. Ver CubicacionScView.xaml.cs.
    public bool IsEditing
    {
        get => isEditing;
        set { isEditing = value; OnPropertyChanged(); }
    }

    public string Descripcion
    {
        get => row.Descripcion;
        set { row.Descripcion = value; OnPropertyChanged(); }
    }

    public int CircuitosProyectados
    {
        get => row.CircuitosProyectados;
        set { row.CircuitosProyectados = value; OnPropertyChanged(); }
    }

    public int CircuitosActivos => row.Circuitos.Count(c => c.EsActivo);

    public void NotifyCircuitosActivosChanged() => OnPropertyChanged(nameof(CircuitosActivos));
}
