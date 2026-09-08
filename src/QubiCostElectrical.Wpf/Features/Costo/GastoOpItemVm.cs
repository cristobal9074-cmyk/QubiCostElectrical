using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class GastoOpItemVm : ViewModelBase
{
    private readonly Action? onChanged;
    private readonly Func<bool>? canEdit;
    private string concepto;
    private string unidad;
    private decimal cantidad;
    private decimal precioUnitarioClp;

    public GastoOpItemVm(GastoOperacionalItemRow row, Action? onChanged, Func<bool>? canEdit = null)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        concepto = row.Concepto ?? "";
        unidad = row.Unidad ?? "";
        cantidad = row.Cantidad;
        precioUnitarioClp = row.PrecioUnitarioClp;
        this.onChanged = onChanged;
        this.canEdit = canEdit;
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public string Id { get; }

    public string Concepto
    {
        get => concepto;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref concepto, value ?? ""))
                onChanged?.Invoke();
        }
    }

    public string Unidad
    {
        get => unidad;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref unidad, value ?? ""))
                onChanged?.Invoke();
        }
    }

    public decimal Cantidad
    {
        get => cantidad;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref cantidad, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalLabel));
                OnPropertyChanged(nameof(IsZeroTotal));
                onChanged?.Invoke();
            }
        }
    }

    public decimal PrecioUnitarioClp
    {
        get => precioUnitarioClp;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref precioUnitarioClp, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalLabel));
                OnPropertyChanged(nameof(IsZeroTotal));
                onChanged?.Invoke();
            }
        }
    }

    public decimal Total => Math.Round(Cantidad * PrecioUnitarioClp);
    public string TotalLabel => CurrencyFormat.Clp(Total);
    public bool IsZeroTotal => Total <= 0;

    public GastoOperacionalItemRow ToRow() => new()
    {
        Id = Id,
        Concepto = Concepto,
        Unidad = Unidad,
        Cantidad = Cantidad,
        PrecioUnitarioClp = PrecioUnitarioClp,
    };
}
