using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Epp;

public sealed class EppItemVm : ViewModelBase
{
    private readonly Action? onChanged;
    private string concepto;
    private decimal cantidad;
    private decimal precioUnitario;

    public EppItemVm(EppItemRow row, Action? onChanged)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        concepto = row.Concepto ?? "";
        cantidad = row.Cantidad;
        precioUnitario = row.PrecioUnitarioClp;
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
    public decimal Cantidad
    {
        get => cantidad;
        set
        {
            if (SetProperty(ref cantidad, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(CantidadLabel));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalLabel));
                onChanged?.Invoke();
            }
        }
    }
    public decimal PrecioUnitario
    {
        get => precioUnitario;
        set
        {
            if (SetProperty(ref precioUnitario, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(PrecioLabel));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalLabel));
                onChanged?.Invoke();
            }
        }
    }

    public decimal Total => Math.Round(Cantidad * PrecioUnitario);
    public string CantidadLabel => $"{Cantidad:N2}";
    public string PrecioLabel => $"$ {PrecioUnitario:N0}";
    public string TotalLabel => $"$ {Total:N0}";

    public EppItemRow ToRow() => new()
    {
        Id = Id,
        Concepto = Concepto,
        Cantidad = Cantidad,
        PrecioUnitarioClp = PrecioUnitario
    };
}

public sealed class EppSeccionVm : ViewModelBase
{
    private readonly Action? onChanged;

    public EppSeccionVm(EppSeccionRow row, Action? onChanged)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        Titulo = row.Titulo ?? "";
        Badge = row.Badge ?? "";
        this.onChanged = onChanged;
        foreach (var item in row.Items ?? Enumerable.Empty<EppItemRow>())
            Items.Add(new EppItemVm(item, NotifyChanged));
    }

    public string Id { get; }
    public string Titulo { get; }
    public string Badge { get; }
    public ObservableCollection<EppItemVm> Items { get; } = new();
    public decimal Subtotal => Items.Sum(i => i.Total);
    public string SubtotalLabel => $"$ {Subtotal:N0}";
    public bool HasItems => Items.Count > 0;

    public void AddItem(string concepto, decimal cant, decimal precio)
    {
        Items.Add(new EppItemVm(new EppItemRow
        {
            Concepto = concepto,
            Cantidad = cant,
            PrecioUnitarioClp = precio
        }, NotifyChanged));
        NotifyChanged();
    }

    public void RemoveItem(EppItemVm? item)
    {
        if (item == null) return;
        Items.Remove(item);
        NotifyChanged();
    }

    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalLabel));
        OnPropertyChanged(nameof(HasItems));
    }

    public void NotifyChanged()
    {
        RefreshLabels();
        onChanged?.Invoke();
    }

    public EppSeccionRow ToRow() => new()
    {
        Id = Id,
        Titulo = Titulo,
        Badge = Badge,
        Items = Items.Select(i => i.ToRow()).ToList()
    };
}

public sealed class EppCatalogEntryVm
{
    public string Concepto { get; init; } = "";
    public decimal Precio { get; init; }
    public string Codigo { get; init; } = "";
    public string PrecioLabel => $"$ {Precio:N0}";
}

public sealed partial class EppViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private bool showEmptyState = true;
    private bool hasPendingChanges;
    private bool canEdit;
    private bool suppressDirty;
    private bool suppressSessionReload;
    private int mesesDirecta = 3;
    private int mesesIndirecta = 6;
    private int dotacionDirecta;
    private int dotacionIndirecta;
    private bool opcionalesIncluidos = true;
    private decimal totalEpp;
    private decimal totalMoDirecta;
    private decimal totalMoIndirecta;
    private decimal totalAccOpcionales;
    private string statusMessage = "";
    private string moSyncLabel = "Sin MO";
    private EppSeccionVm? catalogTargetSeccion;
    private string catalogTargetSide = "opc";
}
