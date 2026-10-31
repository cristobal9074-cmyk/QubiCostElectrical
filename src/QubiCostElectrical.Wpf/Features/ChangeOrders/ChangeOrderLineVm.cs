using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public sealed partial class ChangeOrderLineVm : ObservableObject
{
    private readonly Action _onChanged;

    public string Id { get; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _itemId = "";

    [ObservableProperty]
    private string _item = "";

    partial void OnItemChanged(string value)
    {
        _onChanged?.Invoke();
    }

    [ObservableProperty]
    private string _descripcionPartida = "";

    [ObservableProperty]
    private string _unidad = "UN";

    [ObservableProperty]
    private bool _esContractual = true;

    [ObservableProperty]
    private bool _esTitulo = false;

    partial void OnEsTituloChanged(bool value)
    {
        OnPropertyChanged(nameof(TipoEtiqueta));
        Recalculate();
    }

    [ObservableProperty]
    private decimal _cantidadBase = 0m;

    [ObservableProperty]
    private decimal _cantidadAdicional = 0m;

    [ObservableProperty]
    private decimal _precioUnitarioClp = 0m;

    [ObservableProperty]
    private decimal _precioUnitarioUf = 0m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Observaciones))]
    private string _nota = "";

    public string Observaciones
    {
        get => Nota;
        set => Nota = value;
    }

    public bool MuestraUf { get; set; } = true;
    public decimal UfRate { get; set; } = PriceConversion.DefaultValorUfClp;

    public bool IsPriceReadOnly => EsContractual || EsTitulo;
    public string TipoEtiqueta => EsTitulo ? "📑 Título" : (EsContractual ? "🔒 Contractual" : "★ Extraordinaria");

    public List<CubicacionRow> Materiales { get; set; } = new();
    public int MaterialesCount => Materiales?.Count ?? 0;
    public string MaterialesBadgeText => EsTitulo ? "" : (MaterialesCount > 0 ? $"📐 {MaterialesCount} mat" : "📐 Cubicar");
    public bool HasMateriales => MaterialesCount > 0 && !EsTitulo;

    public void SetMateriales(IEnumerable<CubicacionRow> materiales)
    {
        Materiales = materiales != null ? new List<CubicacionRow>(materiales) : new List<CubicacionRow>();
        OnPropertyChanged(nameof(MaterialesCount));
        OnPropertyChanged(nameof(MaterialesBadgeText));
        OnPropertyChanged(nameof(HasMateriales));
        UpdateExtraordinariaPriceFromMateriales();
        Recalculate();
        _onChanged();
    }

    public void UpdateExtraordinariaPriceFromMateriales()
    {
        if (EsContractual || EsTitulo || Materiales == null || Materiales.Count == 0) return;
        decimal totalMatClp = Materiales.Sum(m => m.SubtotalClp);
        decimal totalMatUf = Materiales.Sum(m => m.UfTotal);

        if (totalMatClp > 0m || totalMatUf > 0m)
        {
            if (CantidadAdicional > 0m)
            {
                PrecioUnitarioClp = Math.Round(totalMatClp / CantidadAdicional, 0, MidpointRounding.AwayFromZero);
                PrecioUnitarioUf = UfRate > 0m
                    ? PriceConversion.ClpToUf(PrecioUnitarioClp, UfRate)
                    : Math.Round(totalMatUf / CantidadAdicional, 4);
            }
            else
            {
                PrecioUnitarioClp = totalMatClp;
                PrecioUnitarioUf = totalMatUf;
            }
        }
    }

    public decimal CantidadNueva => CantidadBase + CantidadAdicional;

    public decimal SubtotalClp
    {
        get
        {
            if (EsTitulo || CantidadAdicional == 0m) return 0m;
            if (!EsContractual && Materiales != null && Materiales.Count > 0)
                return Math.Round(Materiales.Sum(m => m.SubtotalClp), 0, MidpointRounding.AwayFromZero);
            return Math.Round(CantidadAdicional * PrecioUnitarioClp, 0, MidpointRounding.AwayFromZero);
        }
    }

    public decimal SubtotalUf
    {
        get
        {
            if (EsTitulo || CantidadAdicional == 0m) return 0m;
            if (!EsContractual && Materiales != null && Materiales.Count > 0)
            {
                return UfRate > 0m
                    ? PriceConversion.ClpToUf(SubtotalClp, UfRate)
                    : Math.Round(Materiales.Sum(m => m.UfTotal), 4);
            }
            return Math.Round(CantidadAdicional * PrecioUnitarioUf, 4);
        }
    }

    public string CantidadNuevaDisplay => EsTitulo ? "—" : $"{(CantidadNueva % 1 == 0 ? Math.Round(CantidadNueva, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : CantidadNueva.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture))} {Unidad}";

    public string PrecioUnitarioText
    {
        get
        {
            if (EsTitulo) return "—";
            return MuestraUf
                ? PrecioUnitarioUf.ToString("N3", CultureInfo.InvariantCulture)
                : Math.Round(PrecioUnitarioClp, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
        }
        set
        {
            if (IsPriceReadOnly) return;
            if (ChangeOrderEditorViewModel.ParseDecimal(value, out var val))
            {
                if (MuestraUf)
                {
                    PrecioUnitarioUf = val;
                    PrecioUnitarioClp = Math.Round(val * UfRate, 0, MidpointRounding.AwayFromZero);
                }
                else
                {
                    PrecioUnitarioClp = val;
                    PrecioUnitarioUf = UfRate > 0 ? PriceConversion.ClpToUf(val, UfRate) : 0m;
                }
                Recalculate();
            }
        }
    }

    public string CantidadAdicionalText
    {
        get
        {
            if (EsTitulo) return "—";
            return CantidadAdicional % 1 == 0 ? Math.Round(CantidadAdicional, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture) : CantidadAdicional.ToString("#,##0.##", CultureInfo.InvariantCulture);
        }
        set
        {
            if (EsTitulo) return;
            string clean = value?.Trim() ?? "";
            if (clean.StartsWith("=", StringComparison.Ordinal))
                clean = clean[1..].TrimStart();

            decimal val = 0m;
            bool ok = false;
            try
            {
                var res = QuantityFormulaService.Evaluate(clean);
                if (res.Success)
                {
                    val = res.Value;
                    ok = true;
                }
            }
            catch { }

            if (!ok && ChangeOrderEditorViewModel.ParseDecimal(clean, out var directVal))
            {
                val = directVal;
                ok = true;
            }

            if (ok)
            {
                CantidadAdicional = val;
                UpdateExtraordinariaPriceFromMateriales();
                Recalculate();
            }
        }
    }

    public string SubtotalDisplay => EsTitulo
        ? "—"
        : (MuestraUf ? $"{SubtotalUf:N3} UF" : $"$ {SubtotalClp:N0}");

    public ChangeOrderLineVm(Action onChanged)
    {
        _onChanged = onChanged ?? (() => { });
    }

    public static ChangeOrderLineVm FromModel(ChangeOrderLineRow model, bool muestraUf, decimal ufRate, Action onChanged)
    {
        var vm = new ChangeOrderLineVm(onChanged)
        {
            MuestraUf = muestraUf,
            UfRate = ufRate > 0 ? ufRate : PriceConversion.DefaultValorUfClp,
            ItemId = model.ItemId,
            Item = model.Item,
            DescripcionPartida = model.DescripcionPartida,
            Unidad = model.Unidad,
            EsContractual = model.EsContractual,
            EsTitulo = model.EsTitulo,
            CantidadBase = model.CantidadBase,
            CantidadAdicional = model.CantidadAdicional,
            PrecioUnitarioClp = model.PrecioUnitarioContratoClp,
            PrecioUnitarioUf = model.PrecioUnitarioContratoUf,
            Nota = model.Nota,
            Materiales = model.Materiales != null ? new List<CubicacionRow>(model.Materiales) : new List<CubicacionRow>()
        };
        if (!vm.EsContractual && !vm.EsTitulo && vm.Materiales.Count > 0)
        {
            vm.UpdateExtraordinariaPriceFromMateriales();
        }
        return vm;
    }

    public ChangeOrderLineRow ToModel()
    {
        return new ChangeOrderLineRow
        {
            Id = Id,
            ItemId = ItemId,
            Item = Item,
            DescripcionPartida = DescripcionPartida,
            Unidad = Unidad,
            EsContractual = EsContractual,
            EsTitulo = EsTitulo,
            CantidadBase = CantidadBase,
            CantidadAdicional = CantidadAdicional,
            CantidadNueva = CantidadNueva,
            PrecioUnitarioContratoClp = PrecioUnitarioClp,
            PrecioUnitarioContratoUf = PrecioUnitarioUf,
            SubtotalClp = SubtotalClp,
            SubtotalUf = SubtotalUf,
            DeltaCostoUf = SubtotalUf,
            Nota = Nota,
            Materiales = new List<CubicacionRow>(Materiales)
        };
    }

    partial void OnCantidadAdicionalChanged(decimal value)
    {
        UpdateExtraordinariaPriceFromMateriales();
        Recalculate();
    }
    partial void OnPrecioUnitarioClpChanged(decimal value) => Recalculate();
    partial void OnPrecioUnitarioUfChanged(decimal value) => Recalculate();
    partial void OnEsContractualChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPriceReadOnly));
        OnPropertyChanged(nameof(TipoEtiqueta));
    }

    public void Recalculate()
    {
        OnPropertyChanged(nameof(PrecioUnitarioText));
        OnPropertyChanged(nameof(CantidadNueva));
        OnPropertyChanged(nameof(CantidadNuevaDisplay));
        OnPropertyChanged(nameof(CantidadAdicionalText));
        OnPropertyChanged(nameof(SubtotalClp));
        OnPropertyChanged(nameof(SubtotalUf));
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(MaterialesBadgeText));
        _onChanged();
    }
}