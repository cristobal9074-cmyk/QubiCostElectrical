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
    public CuadroCargaSCRow Source => row;

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
            OnPropertyChanged(nameof(NombreCorto));
            OnPropertyChanged(nameof(NombreCompletoTooltip));
            onChanged?.Invoke();
        }
    }

    public string NombreCorto
    {
        get
        {
            var n = Nombre?.Trim();
            if (string.IsNullOrEmpty(n)) return "T";
            if (n.Length <= 4) return n.ToUpperInvariant();

            var parts = n.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                if (char.IsDigit(parts[^1][0]))
                    return (parts[0][..1] + parts[^1]).ToUpperInvariant();

                var initials = string.Concat(parts.Select(p => p[..1])).ToUpperInvariant();
                if (initials.Length <= 4) return initials;
            }

            return n.Length > 3 ? n[..3].ToUpperInvariant() : n.ToUpperInvariant();
        }
    }

    public string NombreCompletoTooltip => $"{Nombre} ({CircuitosActivos}/{CircuitosProyectados} circ)";

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
            OnPropertyChanged(nameof(NombreCompletoTooltip));
            onChanged?.Invoke();
        }
    }

    public int CircuitosActivos => row.Circuitos.Count(c => c.EsActivo);

    public string Categoria
    {
        get => row.Categoria ?? "Generales";
        set
        {
            if (!AllowEdit) return;
            var val = string.IsNullOrWhiteSpace(value) ? "Generales" : value.Trim();
            if (row.Categoria == val) return;
            row.Categoria = val;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDepto));
            onChanged?.Invoke();
        }
    }

    public bool IsDepto => row.EsDepto;

    public static bool IsCuadroDepto(CuadroCargaSCRow? row) => row?.EsDepto ?? false;

    public void NotifyCircuitosActivosChanged()
    {
        OnPropertyChanged(nameof(CircuitosActivos));
        OnPropertyChanged(nameof(NombreCompletoTooltip));
    }
}
