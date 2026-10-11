using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class CoefFactorVm : ViewModelBase
{
    private readonly Action<string, decimal> onCommit;
    private readonly Action<string, decimal> onDiscountCommit;
    private string valueText;
    private string discountText;

    public CoefFactorVm(
        string familyKey,
        string name,
        string map,
        decimal value,
        Action<string, decimal> onCommit,
        Action<string, decimal> onDiscountCommit)
    {
        FamilyKey = familyKey;
        Name = name;
        Map = map;
        this.onCommit = onCommit;
        this.onDiscountCommit = onDiscountCommit;
        valueText = value.ToString("0.00", CultureInfo.CurrentCulture);
        discountText = "0";
    }

    /// <summary>Mientras la celda tiene el foco, un recálculo no puede reescribir el texto.</summary>
    public bool SuspendPush { get; set; }

    public string FamilyKey { get; }
    public string Name { get; }
    public string Map { get; }
    public ObservableCollection<PrecioFamiliaChipVm> FamiliasPrecio { get; } = new();
    public ObservableCollection<PrecioFamiliaPickVm> Catalogo { get; } = new();

    private bool catalogoAbierto;
    public bool CatalogoAbierto
    {
        get => catalogoAbierto;
        set => SetProperty(ref catalogoAbierto, value);
    }

    private string familiasResumen = "";
    public string FamiliasResumen
    {
        get => familiasResumen;
        private set => SetProperty(ref familiasResumen, value);
    }

    public void RefrescarResumenFamilias()
    {
        FamiliasResumen = FamiliasPrecio.Count == 0
            ? ""
            : string.Join(", ", FamiliasPrecio.Select(c => c.Nombre));
    }

    public string EffectiveLabel
    {
        get => effectiveLabel;
        private set => SetProperty(ref effectiveLabel, value);
    }

    private string effectiveLabel = "";
    private string costoUfLabel = "—";
    private string ventaUfLabel = "—";
    private double share;

    public string CostoUfLabel
    {
        get => costoUfLabel;
        private set => SetProperty(ref costoUfLabel, value);
    }

    public string VentaUfLabel
    {
        get => ventaUfLabel;
        private set => SetProperty(ref ventaUfLabel, value);
    }

    public double Share
    {
        get => share;
        private set => SetProperty(ref share, value);
    }

    public void SetPresentation(decimal costoUf, decimal ventaUf, double shareOfCost)
    {
        CostoUfLabel = costoUf > 0m ? costoUf.ToString("N2", CultureInfo.CurrentCulture) : "—";
        VentaUfLabel = costoUf > 0m ? ventaUf.ToString("N2", CultureInfo.CurrentCulture) : "—";
        Share = shareOfCost;
    }

    public void SetEffectiveSilent(decimal markup, decimal discountPct)
    {
        var eff = ExcelCostosBridge.PresentationFactor(markup, discountPct);
        EffectiveLabel = eff.ToString("0.00", CultureInfo.CurrentCulture) + "×";
    }

    public string DiscountText
    {
        get => discountText;
        set
        {
            var incoming = value ?? "";
            if (string.IsNullOrWhiteSpace(incoming))
            {
                SetProperty(ref discountText, "0");
                onDiscountCommit(FamilyKey, 0m);
                return;
            }

            if (!TryParseDiscount(incoming, out var v))
            {
                OnPropertyChanged(nameof(DiscountText));
                return;
            }

            v = Math.Clamp(v, 0m, 50m);
            discountText = decimal.Round(v, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
            if (!SuspendPush)
                OnPropertyChanged(nameof(DiscountText));
            onDiscountCommit(FamilyKey, v);
        }
    }

    public void SetDiscountSilent(decimal pct)
    {
        discountText = FormatDiscount(pct);
        if (!SuspendPush)
            OnPropertyChanged(nameof(DiscountText));
    }

    public string ValueText
    {
        get => valueText;
        set
        {
            var incoming = value ?? "";
            if (!TryParseDecimal(incoming, out var v))
            {
                OnPropertyChanged(nameof(ValueText));
                return;
            }

            v = Math.Clamp(decimal.Round(v, 2, MidpointRounding.AwayFromZero), 1m, 5m);
            valueText = v.ToString("0.00", CultureInfo.CurrentCulture);
            if (!SuspendPush)
                OnPropertyChanged(nameof(ValueText));
            onCommit(FamilyKey, v);
        }
    }

    public void SetValueSilent(decimal value)
    {
        valueText = value.ToString("0.00", CultureInfo.CurrentCulture);
        if (!SuspendPush)
            OnPropertyChanged(nameof(ValueText));
    }

    public void NudgeValue(int direction)
    {
        if (!TryParseDecimal(valueText, out var v))
            v = 1m;
        v = Math.Clamp(decimal.Round(v + (direction * 0.01m), 2, MidpointRounding.AwayFromZero), 1m, 5m);
        ValueText = v.ToString("0.00", CultureInfo.CurrentCulture);
    }

    public void NudgeDiscount(int direction)
    {
        if (!TryParseDiscount(discountText, out var v))
            v = 0m;
        v = Math.Clamp(v + direction, 0m, 50m);
        DiscountText = FormatDiscount(v);
    }

    private static string FormatDiscount(decimal pct)
        => decimal.Round(pct, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Un descuento escrito "20" es veinte. No se interpreta como 2,0 ni se le quita el cero.</summary>
    private static bool TryParseDiscount(string? text, out decimal value)
    {
        value = 0m;
        var s = (text ?? "").Trim().TrimEnd('%').Trim();
        if (s.Length == 0)
            return false;
        if (s.All(char.IsDigit))
            return decimal.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

}

public sealed class PrecioFamiliaChipVm
{
    public PrecioFamiliaChipVm(string nombre, string clave)
    {
        Nombre = nombre;
        Clave = clave;
    }

    public string Nombre { get; }
    public string Clave { get; }
}

public sealed class PrecioFamiliaPickVm
{
    public PrecioFamiliaPickVm(string nombre, string clave, bool enEstaFila, bool bloqueada, string filaDuena)
    {
        Nombre = nombre;
        Clave = clave;
        EnEstaFila = enEstaFila;
        Bloqueada = bloqueada;
        Nota = bloqueada ? "En " + filaDuena : "";
    }

    public string Nombre { get; }
    public string Clave { get; }
    public bool EnEstaFila { get; }
    public bool Bloqueada { get; }
    public string Nota { get; }
    public bool PuedeElegir => !Bloqueada;
    public string Marca => EnEstaFila ? "\u2713" : "";
}
