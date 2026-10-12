using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public sealed partial class ChangeOrderEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _numero = "";

    [ObservableProperty]
    private string _titulo = "";

    [ObservableProperty]
    private string _creadoPor = "";

    [ObservableProperty]
    private string _deltaVentaText = "";

    [ObservableProperty]
    private string _deltaCostoText = "";

    [ObservableProperty]
    private string _costoBaseText = "";

    [ObservableProperty]
    private string _nota = "";

    [ObservableProperty]
    private string? _validationErrorMessage;

    /// <summary>
    /// Política de moneda de presentación. Los montos se guardan en UF (motor); cuando el proyecto
    /// está en CLP se muestran y se capturan en pesos usando el valor UF vigente.
    /// </summary>
    public bool MuestraUf { get; private set; } = true;

    public decimal UfRate { get; private set; } = PriceConversion.DefaultValorUfClp;

    public string EtiquetaMoneda => MuestraUf ? "UF" : "$";

    public string CostoBaseLabel => $"Costo base ({EtiquetaMoneda})";
    public string DeltaCostoLabel => $"Delta costo ({EtiquetaMoneda})";
    public string DeltaVentaLabel => $"Delta venta ({EtiquetaMoneda})";

    /// <summary>Aplica la política de moneda antes de <see cref="LoadFrom"/>.</summary>
    public void ConfigureCurrency(bool muestraUf, decimal ufRate)
    {
        MuestraUf = muestraUf;
        UfRate = ufRate > 0m ? ufRate : PriceConversion.DefaultValorUfClp;
        OnPropertyChanged(nameof(MuestraUf));
        OnPropertyChanged(nameof(EtiquetaMoneda));
        OnPropertyChanged(nameof(CostoBaseLabel));
        OnPropertyChanged(nameof(DeltaCostoLabel));
        OnPropertyChanged(nameof(DeltaVentaLabel));
    }

    partial void OnNumeroChanged(string value) => ClearError();
    partial void OnTituloChanged(string value) => ClearError();
    partial void OnCreadoPorChanged(string value) => ClearError();
    partial void OnDeltaVentaTextChanged(string value) => ClearError();
    partial void OnDeltaCostoTextChanged(string value) => ClearError();
    partial void OnCostoBaseTextChanged(string value) => ClearError();
    partial void OnNotaChanged(string value) => ClearError();

    public void LoadFrom(ChangeOrderRow oc)
    {
        Numero = oc.Numero;
        Titulo = oc.Titulo;
        CreadoPor = oc.CreadoPor;
        DeltaVentaText = ToText(oc.DeltaVentaUf);
        DeltaCostoText = ToText(oc.DeltaCostoUf);
        CostoBaseText = ToText(oc.CostoBaseUf);
        Nota = oc.Nota;
        ValidationErrorMessage = null;
    }

    /// <summary>Monto de motor (UF) al texto de la moneda de presentación.</summary>
    private string ToText(decimal uf)
        => MuestraUf
            ? uf.ToString("N3", CultureInfo.InvariantCulture)
            : Math.Round(uf * UfRate, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Texto de la moneda de presentación al monto de motor (UF).</summary>
    private decimal ToUf(decimal valor)
        => MuestraUf ? valor : PriceConversion.ClpToUf(valor, UfRate);

    public bool TrySave(ChangeOrderRow target)
    {
        var numero = Numero.Trim();
        var titulo = Titulo.Trim();

        if (string.IsNullOrWhiteSpace(numero))
        {
            ValidationErrorMessage = "El número de orden es obligatorio.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(titulo))
        {
            ValidationErrorMessage = "El título es obligatorio.";
            return false;
        }
        if (!ParseDecimal(DeltaVentaText, out var deltaVenta))
        {
            ValidationErrorMessage = "Delta venta: ingrese un número válido (ej: 12.500).";
            return false;
        }
        if (!ParseDecimal(DeltaCostoText, out var deltaCosto))
        {
            ValidationErrorMessage = "Delta costo: ingrese un número válido.";
            return false;
        }
        if (!ParseDecimal(CostoBaseText, out var costoBase) || costoBase < 0)
        {
            ValidationErrorMessage = "Costo base: ingrese un número válido no negativo.";
            return false;
        }

        target.Numero = numero;
        target.Titulo = titulo;
        target.CreadoPor = CreadoPor.Trim();
        target.DeltaVentaUf = ToUf(deltaVenta);
        target.DeltaCostoUf = ToUf(deltaCosto);
        target.CostoBaseUf = ToUf(costoBase);
        target.Nota = Nota.Trim();

        ValidationErrorMessage = null;
        return true;
    }

    internal static bool ParseDecimal(string? text, out decimal value)
    {
        text = (text ?? "").Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private void ClearError() => ValidationErrorMessage = null;
}
