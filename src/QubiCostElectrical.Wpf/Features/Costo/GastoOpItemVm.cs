using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class GastoOpItemVm : ViewModelBase
{
    private readonly Action? onChanged;
    private string concepto;
    private string unidad;
    private decimal cantidad;
    private decimal precioUnitarioClp;

    public GastoOpItemVm(GastoOperacionalItemRow row, Action? onChanged)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        concepto = row.Concepto ?? "";
        unidad = row.Unidad ?? "";
        cantidad = row.Cantidad;
        precioUnitarioClp = row.PrecioUnitarioClp;
        this.onChanged = onChanged;
    }

    public string Id { get; }

    public string Concepto
    {
        get => concepto;
        set
        {
            if (SetProperty(ref concepto, value ?? ""))
                onChanged?.Invoke();
        }
    }

    public string Unidad
    {
        get => unidad;
        set
        {
            if (SetProperty(ref unidad, value ?? ""))
                onChanged?.Invoke();
        }
    }

    public decimal Cantidad
    {
        get => cantidad;
        set
        {
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
    public string TotalLabel => $"$ {Total:N0}";
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
