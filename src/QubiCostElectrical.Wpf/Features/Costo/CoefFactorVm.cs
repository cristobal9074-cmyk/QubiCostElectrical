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

    private string familiasResumen = "Sin familias asignadas";
    public string FamiliasResumen
    {
        get => familiasResumen;
        private set => SetProperty(ref familiasResumen, value);
    }

    public void RefrescarResumenFamilias()
    {
        if (FamiliasPrecio.Count == 0)
        {
            FamiliasResumen = "Sin familias asignadas";
            return;
        }

        var nombres = string.Join(", ", FamiliasPrecio.Select(c => c.Nombre));
        var sinConfigurar = FamiliasPrecio.Where(c => c.TieneAviso).Select(c => c.Nombre).ToList();
        FamiliasResumen = sinConfigurar.Count > 0
            ? $"{nombres} · {sinConfigurar.Count} sin configurar ({string.Join(", ", sinConfigurar)})"
            : nombres;
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

    public void SetPresentation(decimal costoUf, decimal ventaUf, double shareOfCost, string? costoLabel = null, string? ventaLabel = null)
    {
        CostoUfLabel = costoLabel ?? (costoUf > 0m ? costoUf.ToString("N3", CultureInfo.CurrentCulture) : "—");
        VentaUfLabel = ventaLabel ?? (costoUf > 0m ? ventaUf.ToString("N3", CultureInfo.CurrentCulture) : "—");
        Share = shareOfCost;
    }

    public void SetEffectiveSilent(decimal markup, decimal discountPct)
    {
        var eff = ExcelCostosBridge.PresentationFactor(markup, discountPct);
        EffectiveLabel = eff.ToString("0.00", CultureInfo.CurrentCulture) + "×";
    }

    private string origenTexto = "";
    private string origenToolTip = "";
    private bool tieneAviso;
    private string factorAplicadoTexto = "";

    /// <summary>Etiqueta de origen del factor de la fila (vacía cuando está todo en orden).</summary>
    public string OrigenTexto
    {
        get => origenTexto;
        private set => SetProperty(ref origenTexto, value);
    }

    /// <summary>Explicación del origen del factor, con el valor que el motor está aplicando.</summary>
    public string OrigenToolTip
    {
        get => origenToolTip;
        private set => SetProperty(ref origenToolTip, value);
    }

    /// <summary>True cuando la fila necesita atención (factor sin configurar).</summary>
    public bool TieneAviso
    {
        get => tieneAviso;
        private set => SetProperty(ref tieneAviso, value);
    }

    /// <summary>Factor que el motor aplica hoy a las familias de la fila.</summary>
    public string FactorAplicadoTexto
    {
        get => factorAplicadoTexto;
        private set => SetProperty(ref factorAplicadoTexto, value);
    }

    /// <summary>Marca el origen del factor de la fila y el valor que el motor aplica.</summary>
    public void SetEstado(OrigenFactorPresentacion origen, decimal factorAplicado, int familiasSinConfigurar, int familiasTotales = -1)
    {
        var factor = factorAplicado.ToString("0.00", CultureInfo.CurrentCulture) + "×";
        FactorAplicadoTexto = factor;
        if (familiasTotales == 0)
        {
            OrigenTexto = "";
            TieneAviso = false;
            OrigenToolTip = $"{Map}: sin familias asignadas en esta fila.";
            return;
        }

        switch (origen)
        {
            case OrigenFactorPresentacion.SinConfigurar:
                OrigenTexto = "SIN CONFIGURAR";
                TieneAviso = true;
                OrigenToolTip = familiasSinConfigurar > 0
                    ? $"Nadie configuró el factor de {Map}. Hoy el motor aplica {factor} y {familiasSinConfigurar} familia(s) de esta fila quedan sin definir."
                    : $"Nadie configuró el factor de {Map}. Hoy el motor aplica {factor}.";
                break;
            default:
                OrigenTexto = "";
                TieneAviso = false;
                OrigenToolTip = $"Factor propio de {Map}. El motor aplica {factor}.";
                break;
        }
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
            if (!TryParseDecimal(incoming, out var v) || incoming.TrimEnd().EndsWith(',') || incoming.TrimEnd().EndsWith('.'))
            {
                valueText = incoming;
                if (!SuspendPush)
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

    /// <summary>Factor que el motor aplica a esta familia.</summary>
    public string FactorTexto { get; private set; } = "";

    /// <summary>Origen visible de la familia dentro de la fila.</summary>
    public string AvisoTexto { get; private set; } = "";

    /// <summary>True cuando la familia usada no tiene factor configurado.</summary>
    public bool TieneAviso { get; private set; }

    /// <summary>Detalle del origen del factor.</summary>
    public string ToolTip { get; private set; } = "";

    /// <summary>Publica el factor y el origen de la familia en el chip.</summary>
    public void SetEstado(FamiliaPresentacionInfo? info, bool usadaEnCubicacion)
    {
        if (info == null)
        {
            FactorTexto = "";
            AvisoTexto = "";
            TieneAviso = false;
            ToolTip = "";
            return;
        }

        var factor = info.FactorEfectivo.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture) + "×";
        FactorTexto = factor;
        switch (info.Origen)
        {
            case OrigenFactorPresentacion.Propio:
                AvisoTexto = "";
                TieneAviso = false;
                ToolTip = $"{Nombre}: factor propio, el motor aplica {factor}.";
                break;
            case OrigenFactorPresentacion.Asignada:
                AvisoTexto = $"= {info.NombreObjetivo}";
                TieneAviso = false;
                ToolTip = $"{Nombre}: usa el factor de {info.NombreObjetivo} ({factor}).";
                break;
            case OrigenFactorPresentacion.Natural:
                AvisoTexto = $"hereda {info.NombreObjetivo}";
                TieneAviso = false;
                ToolTip = $"{Nombre}: hereda por sinonimia el factor de {info.NombreObjetivo} ({factor}).";
                break;
            default:
                AvisoTexto = usadaEnCubicacion ? "sin configurar" : "";
                TieneAviso = usadaEnCubicacion;
                ToolTip = $"{Nombre}: nadie configuró su factor, el motor aplica {factor}.";
                break;
        }
    }
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
