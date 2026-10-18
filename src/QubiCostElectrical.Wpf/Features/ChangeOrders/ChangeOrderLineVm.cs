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

    [ObservableProperty]
    private string _descripcionPartida = "";

    [ObservableProperty]
    private string _unidad = "UN";

    [ObservableProperty]
    private bool _esContractual = true;

    [ObservableProperty]
    private decimal _cantidadBase = 0m;

    [ObservableProperty]
    private decimal _cantidadAdicional = 0m;

    [ObservableProperty]
    private decimal _precioUnitarioClp = 0m;

    [ObservableProperty]
    private decimal _precioUnitarioUf = 0m;

    [ObservableProperty]
    private string _nota = "";

    public bool MuestraUf { get; set; } = true;
    public decimal UfRate { get; set; } = PriceConversion.DefaultValorUfClp;

    public bool IsPriceReadOnly => EsContractual;
    public string TipoEtiqueta => EsContractual ? "🔒 Contractual" : "✨ Extraordinaria";

    public List<CubicacionRow> Materiales { get; set; } = new();
    public int MaterialesCount => Materiales?.Count ?? 0;
    public string MaterialesBadgeText => MaterialesCount > 0 ? $"📐 {MaterialesCount} mat" : "📐 Cubicar";
    public bool HasMateriales => MaterialesCount > 0;

    public void SetMateriales(IEnumerable<CubicacionRow> materiales)
    {
        Materiales = materiales != null ? new List<CubicacionRow>(materiales) : new List<CubicacionRow>();
        OnPropertyChanged(nameof(MaterialesCount));
        OnPropertyChanged(nameof(MaterialesBadgeText));
        OnPropertyChanged(nameof(HasMateriales));
        _onChanged();
    }

    public decimal CantidadNueva => CantidadBase + CantidadAdicional;

    public decimal SubtotalClp => Math.Round(CantidadAdicional * PrecioUnitarioClp, 0, MidpointRounding.AwayFromZero);
    public decimal SubtotalUf => Math.Round(CantidadAdicional * PrecioUnitarioUf, 4);

    public string CantidadNuevaDisplay => $"{(CantidadNueva % 1 == 0 ? Math.Round(CantidadNueva, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : CantidadNueva.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture))} {Unidad}";

    public string PrecioUnitarioText
    {
        get => MuestraUf
            ? PrecioUnitarioUf.ToString("N3", CultureInfo.InvariantCulture)
            : Math.Round(PrecioUnitarioClp, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
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
        get => CantidadAdicional % 1 == 0 ? Math.Round(CantidadAdicional, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture) : CantidadAdicional.ToString("#,##0.##", CultureInfo.InvariantCulture);
        set
        {
            if (ChangeOrderEditorViewModel.ParseDecimal(value, out var val))
            {
                CantidadAdicional = val;
                Recalculate();
            }
        }
    }

    public string SubtotalDisplay => MuestraUf
        ? $"{SubtotalUf:N3} UF"
        : $"$ {SubtotalClp:N0}";

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
            CantidadBase = model.CantidadBase,
            CantidadAdicional = model.CantidadAdicional,
            PrecioUnitarioClp = model.PrecioUnitarioContratoClp,
            PrecioUnitarioUf = model.PrecioUnitarioContratoUf,
            Nota = model.Nota,
            Materiales = model.Materiales != null ? new List<CubicacionRow>(model.Materiales) : new List<CubicacionRow>()
        };
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

    partial void OnCantidadAdicionalChanged(decimal value) => Recalculate();
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
        _onChanged();
    }
}
