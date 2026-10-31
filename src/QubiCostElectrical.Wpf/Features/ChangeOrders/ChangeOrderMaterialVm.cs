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
    private string _cantidadFormula = "";

    [ObservableProperty]
    private decimal _cantidad = 0m;

    [ObservableProperty]
    private int _formulaPickSlot = -1;

    public ObservableCollection<string> SectorValues { get; } = new();
    public ObservableCollection<string> SectorDisplayValues { get; } = new();

    public Action? OnRecalculateFormulasRequested { get; set; }
    public bool WasSetFromZones { get; set; }

    public string GetSector(int slot)
    {
        while (SectorValues.Count <= slot)
            SectorValues.Add(string.Empty);
        return SectorValues[slot];
    }

    public string GetSectorDisplay(int slot)
    {
        while (SectorDisplayValues.Count <= slot)
            SectorDisplayValues.Add(string.Empty);
        return SectorDisplayValues[slot];
    }

    public void SetSectorDisplay(int slot, string? value)
    {
        while (SectorDisplayValues.Count <= slot)
            SectorDisplayValues.Add(string.Empty);

        string val = value ?? string.Empty;
        if (SectorDisplayValues[slot] != val)
        {
            SectorDisplayValues[slot] = val;
        }
    }

    public void SetSector(int slot, string? value)
    {
        while (SectorValues.Count <= slot)
            SectorValues.Add(string.Empty);

        string val = value ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(val))
            WasSetFromZones = true;

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

            if (OnRecalculateFormulasRequested != null)
            {
                OnRecalculateFormulasRequested();
            }
            else
            {
                RecalculateFromZones();
            }

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

    [ObservableProperty]
    private decimal _ggUtFactor = 1.00m;

    [ObservableProperty]
    private string _notaInterna = "";

    partial void OnGgUtFactorChanged(decimal value) => Recalculate();
    partial void OnNotaInternaChanged(string value) { if (!SuppressNotifications) _onChanged(); }

    public string GgUtFactorText
    {
        get => GgUtFactor > 0m ? GgUtFactor.ToString("0.00", CultureInfo.InvariantCulture) : "1.00";
        set
        {
            if (ChangeOrderEditorViewModel.ParseDecimal(value, out var val) && val >= 0m)
            {
                GgUtFactor = val;
                Recalculate();
            }
        }
    }

    public string GgUtDisplay => (GgUtFactor > 0m ? GgUtFactor : 1.00m).ToString("0.00", CultureInfo.CurrentCulture);

    public bool MuestraUf { get; set; } = true;
    public decimal UfRate { get; set; } = PriceConversion.DefaultValorUfClp;

    public decimal UnitarioClp => MatClp + MoClp;
    public decimal UnitarioUf => MatUf + MoUf;

    public decimal SubtotalClp => Math.Round(Cantidad * UnitarioClp, 0, MidpointRounding.AwayFromZero);
    public decimal SubtotalUf => Math.Round(Cantidad * UnitarioUf, 4);

    public string UnitarioDisplayClp => $"$ {UnitarioClp:N0}";
    public string SubtotalDisplayClp => $"$ {SubtotalClp:N0}";
    public string MatClpDisplay => $"$ {MatClp:N0}";
    public string MoClpDisplay => $"$ {MoClp:N0}";

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
        {
            SectorValues.Add(string.Empty);
            SectorDisplayValues.Add(string.Empty);
        }

        SectorValues.CollectionChanged += (s, e) =>
        {
            if (_isUpdatingSectorValues) return;
            if (OnRecalculateFormulasRequested != null)
                OnRecalculateFormulasRequested();
            else
                RecalculateFromZones();
        };
    }

    public bool SuppressNotifications { get; set; }

    partial void OnCodigoChanged(string value) { if (!SuppressNotifications) _onChanged(); }
    partial void OnDescripcionChanged(string value) { if (!SuppressNotifications) _onChanged(); }
    partial void OnUnidadChanged(string value) { if (!SuppressNotifications) _onChanged(); }
    partial void OnTipoChanged(string value) { if (!SuppressNotifications) _onChanged(); }
    partial void OnMatClpChanged(decimal value) => Recalculate();
    partial void OnMoClpChanged(decimal value) => Recalculate();
    partial void OnCantidadChanged(decimal value) => Recalculate();

    partial void OnCantidadFormulaChanged(string value) => EvaluateFormula();

    public void RecalculateFromZones()
    {
        if (OnRecalculateFormulasRequested != null)
        {
            OnRecalculateFormulasRequested();
            return;
        }

        // Si hay al menos una zona escrita, la cantidad se calcula a partir de las zonas
        bool hasZones = SectorValues.Any(s => !string.IsNullOrWhiteSpace(s));

        if (hasZones)
        {
            WasSetFromZones = true;
            decimal sum = 0m;
            for (int slot = 0; slot < SectorValues.Count; slot++)
            {
                string s = SectorValues[slot];
                decimal val = EvalZone(s);
                SetSectorDisplay(slot, string.IsNullOrWhiteSpace(s) ? "" : FormatDisplayDecimal(val));
                sum += val;
            }
            Cantidad = Math.Max(0m, sum);
            CantidadFormula = Cantidad > 0m ? Cantidad.ToString("G29", CultureInfo.InvariantCulture) : "";
        }
        else
        {
            for (int slot = 0; slot < SectorDisplayValues.Count; slot++)
                SetSectorDisplay(slot, "");

            if (WasSetFromZones)
            {
                Cantidad = 0m;
                CantidadFormula = "";
                WasSetFromZones = false;
            }
            else if (!string.IsNullOrWhiteSpace(CantidadFormula))
            {
                EvaluateFormula();
            }
            else if (Cantidad == 0m)
            {
                CantidadFormula = "";
            }
        }

        Recalculate();
    }

    public static string FormatDisplayDecimal(decimal value)
    {
        if (value % 1 == 0)
            return Math.Round(value, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentCulture);
        return value.ToString("#,##0.##", CultureInfo.CurrentCulture);
    }

    private static decimal EvalZone(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        string clean = text.Trim();
        if (clean.StartsWith("=", StringComparison.Ordinal))
            clean = clean[1..].TrimStart();

        if (string.IsNullOrWhiteSpace(clean)) return 0m;

        try
        {
            var res = QuantityFormulaService.Evaluate(clean);
            if (res.Success) return res.Value;
        }
        catch { }

        if (ChangeOrderEditorViewModel.ParseDecimal(clean, out var direct)) return direct;
        return 0m;
    }

    public void EvaluateFormula()
    {
        if (WasSetFromZones)
        {
            RecalculateFromZones();
            return;
        }

        if (string.IsNullOrWhiteSpace(CantidadFormula))
        {
            Cantidad = 0m;
        }
        else
        {
            string clean = CantidadFormula.Trim();
            if (clean.StartsWith("=", StringComparison.Ordinal))
                clean = clean[1..].TrimStart();

            try
            {
                var eval = QuantityFormulaService.Evaluate(clean);
                if (eval.Success)
                {
                    Cantidad = Math.Max(0m, eval.Value);
                }
                else if (ChangeOrderEditorViewModel.ParseDecimal(clean, out var directVal))
                {
                    Cantidad = Math.Max(0m, directVal);
                }
                else
                {
                    Cantidad = 0m;
                }
            }
            catch
            {
                if (ChangeOrderEditorViewModel.ParseDecimal(clean, out var directVal))
                {
                    Cantidad = Math.Max(0m, directVal);
                }
                else
                {
                    Cantidad = 0m;
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
        OnPropertyChanged(nameof(UnitarioDisplayClp));
        OnPropertyChanged(nameof(SubtotalClp));
        OnPropertyChanged(nameof(SubtotalUf));
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(SubtotalDisplayClp));
        OnPropertyChanged(nameof(MatClpText));
        OnPropertyChanged(nameof(MoClpText));
        OnPropertyChanged(nameof(GgUtFactorText));
        OnPropertyChanged(nameof(GgUtDisplay));
        OnPropertyChanged(nameof(NotaInterna));

        if (!SuppressNotifications)
            _onChanged();
    }

    public static ChangeOrderMaterialVm FromModel(CubicacionRow row, bool muestraUf, decimal ufRate, Action onChanged)
    {
        var vm = new ChangeOrderMaterialVm(onChanged)
        {
            SuppressNotifications = true,
            Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id,
            Tipo = string.IsNullOrWhiteSpace(row.Tipo) ? TipoRecurso.MAT : row.Tipo,
            Codigo = row.Codigo ?? "",
            Descripcion = row.Descripcion ?? "",
            Unidad = string.IsNullOrWhiteSpace(row.Unidad) ? "UN" : row.Unidad,
            Cantidad = row.Cantidad,
            CantidadFormula = !string.IsNullOrWhiteSpace(row.TotalExpression) ? row.TotalExpression : row.Cantidad.ToString("G29", CultureInfo.InvariantCulture),
            MatClp = row.MatClpConFallback,
            MoClp = row.MoClpConFallback,
            GgUtFactor = row.GgUtFactor > 0m ? row.GgUtFactor : 1.00m,
            NotaInterna = row.NotaInterna ?? "",
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
        vm.SuppressNotifications = false;
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
            TotalExpression = Cantidad > 0m ? CantidadFormula.Trim() : "",
            QtyExpressions = SectorValues.Select(s => s?.Trim() ?? "").ToArray(),
            MatClp = MatClp,
            MoClp = MoClp,
            GgUtFactor = GgUtFactor > 0m ? GgUtFactor : 1.00m,
            NotaInterna = (NotaInterna ?? "").Trim()
        };
        row.RecalcularUf(rate);
        return row;
    }
}
