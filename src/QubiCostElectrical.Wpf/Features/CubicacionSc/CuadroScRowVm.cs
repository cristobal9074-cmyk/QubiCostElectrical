using System;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class CuadroScRowVm : ViewModelBase
{
    private readonly CuadroCargaSCRow row;
    private readonly Action? onChanged;
    private readonly Func<bool>? canEdit;
    private bool isEditing;

    public CuadroScRowVm(CuadroCargaSCRow row, Action? onChanged = null, Func<bool>? canEdit = null)
    {
        this.row = row;
        this.onChanged = onChanged;
        this.canEdit = canEdit;
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;
    public bool CanEdit => AllowEdit;

    public string Id => row.Id;

    public string Nombre
    {
        get => row.Nombre;
        set
        {
            if (!AllowEdit) return;
            var limpio = string.IsNullOrWhiteSpace(value) ? row.Nombre : value.Trim();
            if (limpio == row.Nombre) return;
            row.Nombre = limpio;
            OnPropertyChanged();
            onChanged?.Invoke();
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
        set
        {
            if (!AllowEdit) return;
            if (row.Descripcion == value) return;
            row.Descripcion = value;
            OnPropertyChanged();
            onChanged?.Invoke();
        }
    }

    public int CircuitosProyectados
    {
        get => row.CircuitosProyectados;
        set
        {
            if (!AllowEdit) return;
            if (row.CircuitosProyectados == value) return;
            row.CircuitosProyectados = value;
            OnPropertyChanged();
            onChanged?.Invoke();
        }
    }

    public int CircuitosActivos => row.Circuitos.Count(c => c.EsActivo);

    public void NotifyCircuitosActivosChanged() => OnPropertyChanged(nameof(CircuitosActivos));
}
