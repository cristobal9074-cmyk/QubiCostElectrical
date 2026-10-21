using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>
/// Una fila de la cascada de precio: del costo de ejecución a la venta neta y el total con IVA.
/// La geometría se expresa como proporciones 0..1 sobre el total de la oferta, de modo que la
/// barra se estira con la tarjeta y nunca depende de un ancho fijo en píxeles.
/// </summary>
public sealed class DashboardWaterfallRowVm
{
    public string Label { get; init; } = "";
    public string AmountLabel { get; init; } = "";
    public string Sign { get; init; } = "";

    /// <summary>Importe con su signo, listo para la columna derecha de la fila.</summary>
    public string SignedAmountLabel => Sign + AmountLabel;

    /// <summary>Clave de tono de la barra: exec | gg | fact | neg | iva.</summary>
    public string ToneKey { get; init; } = DashboardWaterfallTones.Execution;

    /// <summary>Fila de total (barra completa desde el origen).</summary>
    public bool IsTotal { get; init; }

    /// <summary>Total general de la oferta: se pinta con el acento corporativo.</summary>
    public bool IsGrandTotal { get; init; }

    /// <summary>Fila atenuada (IVA): queda como referencia, no como parte de la venta neta.</summary>
    public bool IsDimmed { get; init; }

    public double LeftRatio { get; init; }
    public double WidthRatio { get; init; }
    public double RemainderRatio { get; init; }

    /// <summary>Columnas estrelladas de la barra: [desplazamiento][largo][resto].</summary>
    public GridLength BarOffset { get; init; }
    public GridLength BarLength { get; init; }
    public GridLength BarRemainder { get; init; }

    public Brush BarBrush { get; init; } = DashboardKpiBrushes.Blue;

    // --- Geometría del lienzo (1:1 con el lienzo de la vista) -------------------
    // La cascada se dibuja dentro de un Canvas con escala fija y un Viewbox la ajusta al
    // ancho disponible. Por eso cada fila trae sus coordenadas absolutas en vez de
    // proporciones: el resultado es idéntico en cualquier ancho de tarjeta.

    /// <summary>Posición vertical absoluta de la fila dentro del lienzo.</summary>
    public double RowTop { get; init; }

    /// <summary>Posición vertical de la etiqueta dentro de la fila.</summary>
    public double LabelTop { get; init; }

    /// <summary>Posición vertical del importe dentro de la fila.</summary>
    public double ValueTop { get; init; }

    /// <summary>Posición horizontal de la barra dentro del lienzo.</summary>
    public double BarX { get; init; }

    /// <summary>Ancho de la barra en píxeles de lienzo (mínimo legible).</summary>
    public double BarWidth { get; init; }

    /// <summary>Posición vertical de la barra dentro de la fila.</summary>
    public double BarTop { get; init; }

    /// <summary>Alto de la barra.</summary>
    public double BarHeight { get; init; } = DashboardCockpitComposer.BarHeight;

    /// <summary>X del conector hacia la fila siguiente; -1 cuando la fila siguiente es un total.</summary>
    public double ConnectorX { get; init; } = -1;

    /// <summary>La fila tiene conector hacia la etapa siguiente.</summary>
    public bool HasConnector => ConnectorX >= 0;

    /// <summary>Y del conector (borde inferior de la barra).</summary>
    public double ConnectorTop { get; init; }

    /// <summary>Y inferior del conector (borde superior de la barra siguiente).</summary>
    public double ConnectorBottom { get; init; }
}

/// <summary>Una marca del eje de la cascada.</summary>
public sealed class DashboardWaterfallAxisVm
{
    /// <summary>Posición horizontal de la marca.</summary>
    public double X { get; init; }

    /// <summary>Posición vertical de la marca.</summary>
    public double Y { get; init; }

    /// <summary>Rótulo de la marca.</summary>
    public string Text { get; init; } = "";
}

/// <summary>Una línea de grilla vertical de la cascada.</summary>
public sealed class DashboardWaterfallGridVm
{
    /// <summary>Posición horizontal de la línea.</summary>
    public double X { get; init; }

    /// <summary>Y superior de la línea.</summary>
    public double Y1 { get; init; }

    /// <summary>Y inferior de la línea.</summary>
    public double Y2 { get; init; }
}

/// <summary>Tonos de la cascada de precio, compartidos por el modelo y la vista.</summary>
public static class DashboardWaterfallTones
{
    public const string Execution = "exec";
    public const string Overhead = "gg";
    public const string Factor = "fact";
    public const string Discount = "neg";
    public const string Tax = "iva";

    /// <summary>Pincel de la barra según el tono (el rojo del descuento va en el tono, no en el texto).</summary>
    public static Brush ToBrush(string toneKey)
    {
        if (string.Equals(toneKey, Discount, System.StringComparison.OrdinalIgnoreCase))
            return DashboardKpiBrushes.RedSoft;
        if (string.Equals(toneKey, Tax, System.StringComparison.OrdinalIgnoreCase))
            return DashboardKpiBrushes.Muted;
        if (string.Equals(toneKey, Overhead, System.StringComparison.OrdinalIgnoreCase))
            return DashboardKpiBrushes.BlueSoft;
        return DashboardKpiBrushes.Blue;
    }
}

/// <summary>
/// Contrato único de preparación de la oferta. Lo consumen el KPI de partidas listas, su barra de
/// avance, el anillo de preparación, el checklist y el veredicto, de modo que no puedan discrepar.
/// </summary>
public sealed class DashboardReadinessContract
{
    public int TotalPartidas { get; init; }
    public int EmptyPartidas { get; init; }
    public int MaterialsWithoutPrice { get; init; }
    public int BlockingCount { get; init; }
    public int CriticalCount { get; init; }

    /// <summary>Partidas no-título sin vacío crítico y con precio vigente.</summary>
    public int Listas { get; init; }

    public int Pendientes => EmptyPartidas + MaterialsWithoutPrice;

    public double ListasPercent => TotalPartidas <= 0 ? 0 : System.Math.Min(100, Listas * 100d / TotalPartidas);

    /// <summary>El checklist de preparación se resuelve con el mismo contrato: 4 compuertas medidas.</summary>
    public bool CubIcacionOk => TotalPartidas > 0 && EmptyPartidas == 0;
    public bool PreciosOk => TotalPartidas > 0 && MaterialsWithoutPrice == 0;
    public bool RevisioNOk => BlockingCount == 0 && CriticalCount == 0;

    public bool ListoParaOfertar => TotalPartidas > 0 && Listas == TotalPartidas && RevisioNOk;

    public static DashboardReadinessContract Empty { get; } = new();

    public static DashboardReadinessContract Build(
        int totalPartidas,
        int emptyPartidas,
        int materialsWithoutPrice,
        int blockingCount,
        int criticalCount)
    {
        totalPartidas = System.Math.Max(0, totalPartidas);
        emptyPartidas = System.Math.Clamp(emptyPartidas, 0, totalPartidas);
        materialsWithoutPrice = System.Math.Max(0, materialsWithoutPrice);

        // Una partida sin cubicación no puede estar lista, aunque tenga precio: por eso se
        // descuentan ambos frentes y no sólo el que esté peor.
        int pendientes = emptyPartidas + materialsWithoutPrice;
        return new DashboardReadinessContract
        {
            TotalPartidas = totalPartidas,
            EmptyPartidas = emptyPartidas,
            MaterialsWithoutPrice = materialsWithoutPrice,
            BlockingCount = blockingCount,
            CriticalCount = criticalCount,
            Listas = System.Math.Max(0, totalPartidas - pendientes)
        };
    }

    /// <summary>
    /// Escala del anillo de preparación. A diferencia del promedio plano anterior (0/25/50/75/100),
    /// promedia compuertas medidas: cobertura de cubicación, vigencia de precios y checklist limpio.
    /// </summary>
    public double RingPercent
    {
        get
        {
            if (TotalPartidas <= 0) return 0;
            double cubicacion = (TotalPartidas - EmptyPartidas) * 100d / TotalPartidas;
            double precios = (TotalPartidas - MaterialsWithoutPrice) * 100d / TotalPartidas;
            double checklist = RevisioNOk ? 100d : (BlockingCount > 0 ? 0d : 50d);
            return System.Math.Clamp((cubicacion + precios + checklist) / 3d, 0, 100);
        }
    }
}

/// <summary>Formato corto de importes para la cascada ("$ 497,0 M"), compartido por modelo y pruebas.</summary>
public static class DashboardMoneyShort
{
    private static readonly CultureInfo Cl = CultureInfo.GetCultureInfo("es-CL");

    public static string Format(decimal value)
    {
        bool negative = value < 0m;
        decimal abs = System.Math.Abs(value);
        string body = abs switch
        {
            >= 1_000_000_000m => (abs / 1_000_000_000m).ToString("N1", Cl) + " MM",
            >= 1_000_000m => (abs / 1_000_000m).ToString("N1", Cl) + " M",
            >= 1_000m => (abs / 1_000m).ToString("N0", Cl) + " K",
            _ => abs.ToString("N0", Cl)
        };
        return (negative ? "−$ " : "$ ") + body;
    }

    /// <summary>Formato corto en UF ("29,1M UF"): cabe en el centro de la dona y en el eje.</summary>
    public static string FormatUf(decimal value)
    {
        decimal abs = System.Math.Abs(value);
        string body = abs switch
        {
            >= 1_000_000m => (abs / 1_000_000m).ToString("N1", Cl) + "M",
            >= 1_000m => (abs / 1_000m).ToString("N1", Cl) + "K",
            _ => abs.ToString("N1", Cl)
        };
        return body + " UF";
    }
}
