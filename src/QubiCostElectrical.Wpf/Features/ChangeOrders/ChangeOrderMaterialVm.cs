using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public sealed partial class ChangeOrderMaterialVm : ObservableObject
{
    private readonly Action _onChanged;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _tipo = TipoRecurso.MAT;

    [ObservableProperty]
    private string _codigo = "";

    [ObservableProperty]
    private string _descripcion = "";

    [ObservableProperty]
    private string _unidad = "UN";

    [ObservableProperty]
    private string _cantidadFormula = "1";

    [ObservableProperty]
    private decimal _cantidad = 1m;

    public ObservableCollection<string> SectorValues { get; } = new();

    public string GetSector(int slot)
    {
        while (SectorValues.Count <= slot)
            SectorValues.Add(string.Empty);
        return SectorValues[slot];
    }

    public void SetSector(int slot, string? value)
    {
        while (SectorValues.Count <= slot)
            SectorValues.Add(string.Empty);

        string val = value ?? string.Empty;
        if (SectorValues[slot] != val)
        {
            _isUpdatingSectorValues = true;
            try
            {
                SectorValues[slot] = val;
            }
            finally
            {
                _isUpdatingSectorValues = false;
            }
            RecalculateFromZones();
            if (slot == 0) OnPropertyChanged(nameof(Zona1));
            else if (slot == 1) OnPropertyChanged(nameof(Zona2));
            else if (slot == 2) OnPropertyChanged(nameof(Zona3));
            else if (slot == 3) OnPropertyChanged(nameof(Zona4));
        }
    }

    public string Zona1 { get => GetSector(0); set => SetSector(0, value); }
    public string Zona2 { get => GetSector(1); set => SetSector(1, value); }
    public string Zona3 { get => GetSector(2); set => SetSector(2, value); }
    public string Zona4 { get => GetSector(3); set => SetSector(3, value); }

    [ObservableProperty]
    private decimal _matClp = 0m;

    [ObservableProperty]
    private decimal _moClp = 0m;

    [ObservableProperty]
    private decimal _matUf = 0m;

    [ObservableProperty]
    private decimal _moUf = 0m;

    public bool MuestraUf { get; set; } = true;
    public decimal UfRate { get; set; } = PriceConversion.DefaultValorUfClp;

    public decimal UnitarioClp => MatClp + MoClp;
    public decimal UnitarioUf => MatUf + MoUf;

    public decimal SubtotalClp => Math.Round(Cantidad * UnitarioClp, 0, MidpointRounding.AwayFromZero);
    public decimal SubtotalUf => Math.Round(Cantidad * UnitarioUf, 4);

    public string UnitarioDisplay => MuestraUf
        ? $"{UnitarioUf:N3} UF"
        : $"$ {UnitarioClp:N0}";

    public string SubtotalDisplay => MuestraUf
        ? $"{SubtotalUf:N3} UF"
        : $"$ {SubtotalClp:N0}";

    public string CantidadDisplay => Cantidad % 1 == 0 ? Math.Round(Cantidad, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentCulture) : Cantidad.ToString("#,##0.##", CultureInfo.CurrentCulture);

    public string MatClpText
    {
        get => Math.Round(MatClp, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
        set
        {
            if (ChangeOrderEditorViewModel.ParseDecimal(value, out var val))
            {
                MatClp = val;
                Recalculate();
            }
        }
    }

    public string MoClpText
    {
        get => Math.Round(MoClp, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
        set
        {
            if (ChangeOrderEditorViewModel.ParseDecimal(value, out var val))
            {
                MoClp = val;
                Recalculate();
            }
        }
    }

    private bool _isUpdatingSectorValues;

    public ChangeOrderMaterialVm(Action onChanged)
    {
        _onChanged = onChanged ?? (() => { });
        for (int i = 0; i < 24; i++)
            SectorValues.Add(string.Empty);

        SectorValues.CollectionChanged += (s, e) =>
        {
            if (_isUpdatingSectorValues) return;
            RecalculateFromZones();
        };
    }

    partial void OnCodigoChanged(string value) => _onChanged();
    partial void OnDescripcionChanged(string value) => _onChanged();
    partial void OnUnidadChanged(string value) => _onChanged();
    partial void OnTipoChanged(string value) => _onChanged();

    partial void OnCantidadFormulaChanged(string value) => EvaluateFormula();

    public void RecalculateFromZones()
    {
        // Si hay al menos una zona escrita, la cantidad se calcula a partir de las zonas
        bool hasZones = SectorValues.Any(s => !string.IsNullOrWhiteSpace(s));

        if (hasZones)
        {
            decimal sum = 0m;
            foreach (var s in SectorValues)
            {
                sum += EvalZone(s);
            }
            Cantidad = Math.Max(0m, sum);
            CantidadFormula = Cantidad.ToString("G29", CultureInfo.InvariantCulture);
        }
        else
        {
            EvaluateFormula();
        }

        Recalculate();
    }

    private static decimal EvalZone(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        try
        {
            var res = QuantityFormulaService.Evaluate(text);
            if (res.Success) return res.Value;
            if (ChangeOrderEditorViewModel.ParseDecimal(text, out var direct)) return direct;
        }
        catch
        {
            if (ChangeOrderEditorViewModel.ParseDecimal(text, out var direct)) return direct;
        }
        return 0m;
    }

    public void EvaluateFormula()
    {
        if (string.IsNullOrWhiteSpace(CantidadFormula))
        {
            Cantidad = 0m;
        }
        else
        {
            try
            {
                var eval = QuantityFormulaService.Evaluate(CantidadFormula);
                if (eval.Success)
                {
                    Cantidad = Math.Max(0m, eval.Value);
                }
                else if (ChangeOrderEditorViewModel.ParseDecimal(CantidadFormula, out var directVal))
                {
                    Cantidad = Math.Max(0m, directVal);
                }
            }
            catch
            {
                if (ChangeOrderEditorViewModel.ParseDecimal(CantidadFormula, out var directVal))
                {
                    Cantidad = Math.Max(0m, directVal);
                }
            }
        }

        Recalculate();
    }

    public void ResolveFromPriceBank(IPriceBankStore? store)
    {
        if (store == null || string.IsNullOrWhiteSpace(Codigo))
            return;

        var clean = Codigo.Trim();
        var entry = store.GetByCodigo(clean);
        if (entry != null)
        {
            Descripcion = entry.Descripcion;
            if (!string.IsNullOrWhiteSpace(entry.Unidad))
                Unidad = entry.Unidad;

            MatClp = entry.MatClpConFallback;
            MoClp = entry.MoClpConFallback;
            Recalculate();
        }
    }

    public void Recalculate()
    {
        var rate = UfRate > 0m ? UfRate : PriceConversion.DefaultValorUfClp;
        MatUf = PriceConversion.ClpToUf(MatClp, rate);
        MoUf = PriceConversion.ClpToUf(MoClp, rate);

        OnPropertyChanged(nameof(CantidadDisplay));
        OnPropertyChanged(nameof(UnitarioClp));
        OnPropertyChanged(nameof(UnitarioUf));
        OnPropertyChanged(nameof(UnitarioDisplay));
        OnPropertyChanged(nameof(SubtotalClp));
        OnPropertyChanged(nameof(SubtotalUf));
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(MatClpText));
        OnPropertyChanged(nameof(MoClpText));

        _onChanged();
    }

    public static ChangeOrderMaterialVm FromModel(CubicacionRow row, bool muestraUf, decimal ufRate, Action onChanged)
    {
        var vm = new ChangeOrderMaterialVm(onChanged)
        {
            Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id,
            Tipo = string.IsNullOrWhiteSpace(row.Tipo) ? TipoRecurso.MAT : row.Tipo,
            Codigo = row.Codigo ?? "",
            Descripcion = row.Descripcion ?? "",
            Unidad = string.IsNullOrWhiteSpace(row.Unidad) ? "UN" : row.Unidad,
            Cantidad = row.Cantidad,
            CantidadFormula = !string.IsNullOrWhiteSpace(row.TotalExpression) ? row.TotalExpression : row.Cantidad.ToString("G29", CultureInfo.InvariantCulture),
            MatClp = row.MatClpConFallback,
            MoClp = row.MoClpConFallback,
            MuestraUf = muestraUf,
            UfRate = ufRate > 0m ? ufRate : PriceConversion.DefaultValorUfClp
        };

        if (row.QtyExpressions != null && row.QtyExpressions.Length > 0)
        {
            for (int i = 0; i < row.QtyExpressions.Length; i++)
            {
                vm.SetSector(i, row.QtyExpressions[i] ?? "");
            }
        }
        else if (row.Cantidad > 0m)
        {
            vm.SetSector(0, row.Cantidad.ToString("G29", CultureInfo.InvariantCulture));
        }

        vm.Recalculate();
        return vm;
    }

    public CubicacionRow ToModel(string itemId)
    {
        var rate = UfRate > 0m ? UfRate : PriceConversion.DefaultValorUfClp;
        var row = new CubicacionRow
        {
            Id = Id,
            ItemId = itemId,
            Tipo = Tipo,
            Codigo = Codigo.Trim(),
            Descripcion = Descripcion.Trim(),
            Unidad = Unidad.Trim(),
            Cantidad = Cantidad,
            TotalExpression = CantidadFormula.Trim(),
            QtyExpressions = SectorValues.Select(s => s?.Trim() ?? "").ToArray(),
            MatClp = MatClp,
            MoClp = MoClp
        };
        row.RecalcularUf(rate);
        return row;
    }
}
