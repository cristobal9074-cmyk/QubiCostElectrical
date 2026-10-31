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

    public string NombreCompletoTooltip =>
        $"{Nombre} · {PotenciaBadgeText} · {CircuitosActivos}/{CircuitosProyectados} circ ({CapacidadPct:N0}%)";

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
            OnPropertyChanged(nameof(CapacidadPct));
            OnPropertyChanged(nameof(CapacidadBarValue));
            OnPropertyChanged(nameof(CapacidadText));
            OnPropertyChanged(nameof(CapacidadEstado));
            OnPropertyChanged(nameof(CapacidadTooltip));
            OnPropertyChanged(nameof(NombreCompletoTooltip));
            onChanged?.Invoke();
        }
    }

    public int CircuitosActivos => row.Circuitos.Count(c => c.EsActivo);

    public decimal TotalPotenciaW
    {
        get
        {
            if (row.Circuitos == null || row.Circuitos.Count == 0) return 0m;
            return row.Circuitos
                .Where(c => c.EsActivo || c.EsReservaEquipada)
                .Sum(CuadroCargasSecEngine.EstimateCircuitPowerW);
        }
    }

    public decimal TotalPotenciaKw => Math.Round(TotalPotenciaW / 1000m, 1);

    public decimal TotalPotenciaKva => TotalPotenciaKw > 0m
        ? Math.Round(TotalPotenciaKw / 0.93m, 1)
        : 0m;

    public string PotenciaBadgeText => $"{TotalPotenciaKw:N1} kW / {TotalPotenciaKva:N1} kVA";

    public double CapacidadPct
    {
        get
        {
            if (CircuitosProyectados <= 0) return 0.0;
            return Math.Round((double)CircuitosActivos / CircuitosProyectados * 100.0, 1);
        }
    }

    public double CapacidadBarValue => Math.Clamp(CapacidadPct, 0.0, 100.0);

    public string CapacidadText => $"{CircuitosActivos}/{CircuitosProyectados} circ ({CapacidadPct:N0}%)";

    public string CapacidadEstado => CapacidadPct switch
    {
        > 100.0 => "Overload",
        > 75.0 => "Warning",
        _ => "Normal"
    };

    public string CapacidadTooltip =>
        $"{Nombre}\n" +
        $"Potencia calculada: {TotalPotenciaKw:N1} kW / {TotalPotenciaKva:N1} kVA\n" +
        $"Capacidad ocupada: {CircuitosActivos} de {CircuitosProyectados} circuitos ({CapacidadPct:N1}%)\n" +
        (CapacidadPct > 100.0
            ? "⚠ Sobrecapacidad: supera el 100% de la capacidad proyectada"
            : CapacidadPct > 75.0
                ? "⚠ Alerta SEC: reserva libre inferior al 25% (RIC N°02)"
                : "✓ Capacidad y reserva conforme normativa SEC");

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

    public void NotifyCircuitosActivosChanged()
    {
        OnPropertyChanged(nameof(CircuitosActivos));
        OnPropertyChanged(nameof(TotalPotenciaW));
        OnPropertyChanged(nameof(TotalPotenciaKw));
        OnPropertyChanged(nameof(TotalPotenciaKva));
        OnPropertyChanged(nameof(PotenciaBadgeText));
        OnPropertyChanged(nameof(CapacidadPct));
        OnPropertyChanged(nameof(CapacidadBarValue));
        OnPropertyChanged(nameof(CapacidadText));
        OnPropertyChanged(nameof(CapacidadEstado));
        OnPropertyChanged(nameof(CapacidadTooltip));
        OnPropertyChanged(nameof(NombreCompletoTooltip));
    }
}
