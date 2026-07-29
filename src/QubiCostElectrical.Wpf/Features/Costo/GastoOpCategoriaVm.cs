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

public sealed class GastoOpCategoriaVm : ViewModelBase
{
    private readonly Action? onChanged;
    private string codigo;
    private string concepto;
    private decimal montoClp;
    private decimal uf;
    private decimal pct;
    private decimal valorUf = 1m;
    private bool isExpanded;

    public GastoOpCategoriaVm(GastoOperacionalRow row, decimal valorUf, Action? onChanged, bool expanded = false)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        codigo = row.Codigo ?? "";
        concepto = row.Concepto ?? "";
        this.valorUf = valorUf > 0 ? valorUf : 1m;
        this.onChanged = onChanged;
        isExpanded = expanded;
        foreach (var item in row.Items ?? Enumerable.Empty<GastoOperacionalItemRow>())
            Items.Add(new GastoOpItemVm(item, NotifyItemChanged));
        RecalcFromItems(notify: false);
    }

    public string Id { get; }
    public ObservableCollection<GastoOpItemVm> Items { get; } = new();
    public bool HasItems => Items.Count > 0;
    public string ItemsCountLabel => $"{Items.Count} ítems";

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (SetProperty(ref isExpanded, value))
                OnPropertyChanged(nameof(Chevron));
        }
    }

    public string Chevron => IsExpanded ? "▾" : "›";

    public string Codigo
    {
        get => codigo;
        set
        {
            if (SetProperty(ref codigo, value ?? ""))
            {
                OnPropertyChanged(nameof(IsViaticosCategory));
                onChanged?.Invoke();
            }
        }
    }

    public string Concepto
    {
        get => concepto;
        set
        {
            if (SetProperty(ref concepto, value ?? ""))
            {
                OnPropertyChanged(nameof(IsViaticosCategory));
                onChanged?.Invoke();
            }
        }
    }

    public bool IsViaticosCategory =>
        string.Equals(Codigo, "3.3", StringComparison.OrdinalIgnoreCase)
        || Concepto.Contains("Movilización", StringComparison.OrdinalIgnoreCase)
        || Concepto.Contains("viático", StringComparison.OrdinalIgnoreCase);

    public decimal MontoClp
    {
        get => montoClp;
        private set
        {
            if (SetProperty(ref montoClp, value))
            {
                OnPropertyChanged(nameof(MontoLabel));
                OnPropertyChanged(nameof(Uf));
                OnPropertyChanged(nameof(UfLabel));
            }
        }
    }

    public decimal Uf
    {
        get => uf;
        private set
        {
            if (SetProperty(ref uf, value))
                OnPropertyChanged(nameof(UfLabel));
        }
    }

    public decimal Pct
    {
        get => pct;
        set
        {
            if (SetProperty(ref pct, value))
                OnPropertyChanged(nameof(PctLabel));
        }
    }

    public string MontoLabel => $"$ {MontoClp:N0}";
    public string UfLabel => $"{Uf:N2} UF";
    public string PctLabel => $"{Pct:N1}%";

    public void SetValorUf(decimal v)
    {
        valorUf = v > 0 ? v : 1m;
        RecalcFromItems(notify: false);
    }

    public void ToggleExpanded() => IsExpanded = !IsExpanded;

    public void AddBlankItem()
    {
        Items.Add(new GastoOpItemVm(new GastoOperacionalItemRow
        {
            Concepto = "Nuevo ítem",
            Unidad = "un",
            Cantidad = 0,
            PrecioUnitarioClp = 0,
        }, NotifyItemChanged));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ItemsCountLabel));
        IsExpanded = true;
        RecalcFromItems();
    }

    public void AddItemSilent(string concepto, string unidad)
    {
        Items.Add(new GastoOpItemVm(new GastoOperacionalItemRow
        {
            Concepto = concepto,
            Unidad = unidad,
            Cantidad = 0,
            PrecioUnitarioClp = 0,
        }, NotifyItemChanged));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ItemsCountLabel));
        RecalcFromItems(notify: false);
    }

    public void RemoveItem(GastoOpItemVm? item)
    {
        if (item == null || !Items.Remove(item))
            return;
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ItemsCountLabel));
        RecalcFromItems();
    }

    public void ReplaceItems(IEnumerable<GastoOperacionalItemRow> rows, GastoOperacionalItemRow? preserveBus = null)
    {
        Items.Clear();
        foreach (var row in rows)
            Items.Add(new GastoOpItemVm(row, NotifyItemChanged));

        if (preserveBus != null
            && !Items.Any(i =>
                i.Concepto.Contains("bus", StringComparison.OrdinalIgnoreCase)
                || i.Concepto.Contains("Arriendo / traslado", StringComparison.OrdinalIgnoreCase)))
        {
            Items.Add(new GastoOpItemVm(preserveBus, NotifyItemChanged));
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ItemsCountLabel));
        IsExpanded = true;
        RecalcFromItems();
    }

    public void RecalcFromItems(bool notify = true)
    {
        MontoClp = Items.Sum(i => i.Total);
        Uf = MontoClp / valorUf;
        if (notify)
            onChanged?.Invoke();
    }

    private void NotifyItemChanged() => RecalcFromItems();

    public GastoOperacionalRow ToRow() => new()
    {
        Id = Id,
        Codigo = Codigo,
        Concepto = Concepto,
        MontoClp = MontoClp,
        Uf = Uf,
        Items = Items.Select(i => i.ToRow()).ToList(),
    };
}
