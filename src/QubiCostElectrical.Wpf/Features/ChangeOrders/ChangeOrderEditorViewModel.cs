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
        DeltaVentaText = oc.DeltaVentaUf.ToString("N3", CultureInfo.InvariantCulture);
        DeltaCostoText = oc.DeltaCostoUf.ToString("N3", CultureInfo.InvariantCulture);
        CostoBaseText = oc.CostoBaseUf.ToString("N3", CultureInfo.InvariantCulture);
        Nota = oc.Nota;
        ValidationErrorMessage = null;
    }

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
        target.DeltaVentaUf = deltaVenta;
        target.DeltaCostoUf = deltaCosto;
        target.CostoBaseUf = costoBase;
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
