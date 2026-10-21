using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>Cascada de precio ya compuesta: filas + pie explicativo.</summary>
public sealed class DashboardWaterfallVm
{
    public IReadOnlyList<DashboardWaterfallRowVm> Rows { get; init; } = Array.Empty<DashboardWaterfallRowVm>();

    /// <summary>Describe si el total contractual incluye IVA, para no rotular de más.</summary>
    public string Subtitle { get; init; } = "";

    /// <summary>Ancho del lienzo de la cascada (escala fija, ajustada por Viewbox).</summary>
    public double CanvasWidth { get; init; } = DashboardCockpitComposer.CanvasWidth;

    /// <summary>Alto del lienzo de la cascada.</summary>
    public double CanvasHeight { get; init; } = 360;

    /// <summary>Marcas del eje inferior.</summary>
    public IReadOnlyList<DashboardWaterfallAxisVm> AxisLabels { get; init; } = Array.Empty<DashboardWaterfallAxisVm>();

    /// <summary>Líneas de grilla verticales del eje.</summary>
    public IReadOnlyList<DashboardWaterfallGridVm> GridLines { get; init; } = Array.Empty<DashboardWaterfallGridVm>();

    /// <summary>Y inferior del área de dibujo.</summary>
    public double PlotBottom { get; init; } = 340;

    /// <summary>Y donde se dibujan las marcas del eje.</summary>
    public double AxisTop { get; init; } = 346;

    public static DashboardWaterfallVm Empty { get; } = new();
}

/// <summary>Política comercial vigente, sólo para rotular los porcentajes de cada escalón.</summary>
public readonly record struct DashboardWaterfallPolicy(
    decimal RiesgoPct,
    decimal GastosGeneralesPct,
    decimal UtilidadPct,
    decimal ValorAgregadoPct,
    decimal DescuentoPct,
    decimal IvaPct,
    bool IncluirIvaEnTotal);

/// <summary>
/// Compone la cascada de precio del cockpit. Cada escalón sale de un total ya calculado por el
/// motor (nunca se recalcula la fórmula) y el salto «diferencia por factores» se deriva como el
/// cierre exacto entre la estructura de costo y el neto del motor, de modo que la cascada siempre
/// termina en el mismo importe que el KPI de venta.
/// </summary>
public static class DashboardCockpitComposer
{
    private static readonly CultureInfo Cl = CultureInfo.GetCultureInfo("es-CL");

    // --- Escala del lienzo de la cascada -----------------------------------------
    // Lienzo de coordenadas fijas (700 de ancho) que la vista envuelve en un Viewbox.
    // El margen izquierdo aloja las etiquetas y el derecho los importes.
    public const double CanvasWidth = 900;
    public const double PlotLeft = 250;
    public const double PlotWidth = 470;
    public const double RowHeight = 30;
    public const double BarHeight = 16;
    public const double TopPad = 8;
    public const double AxisHeight = 28;
    public const double LabelTop = 7;
    public const double ValueTop = 7;

    /// <summary>Una etapa de la cascada antes de proyectarla al lienzo.</summary>
    private readonly record struct Step(
        string Label,
        decimal From,
        decimal To,
        string ToneKey,
        string Sign,
        bool IsTotal,
        bool IsGrandTotal,
        bool IsDimmed);

    public static DashboardWaterfallVm BuildWaterfall(
        CostBreakdown cost,
        DashboardWaterfallPolicy policy,
        Func<decimal, string> money,
        Func<decimal, string>? axisMoney = null)
    {
        cost ??= new CostBreakdown();
        money ??= v => v.ToString("N0", Cl);
        axisMoney ??= money;

        decimal ejecucion = Math.Max(0m, cost.CostoDirectoUf);
        decimal riesgo = Math.Max(0m, cost.RiesgoUf);
        decimal gg = Math.Max(0m, cost.GastosGeneralesUf);
        decimal utilidad = Math.Max(0m, cost.UtilidadUf);
        decimal va = Math.Max(0m, cost.ValorAgregadoEquiposUf);

        // Suma de la estructura: en ambos caminos del motor (factores y legado) equivale al bruto
        // comercial, por eso se usa como escalón y no CostoProyectoUf (que en la ruta por factores
        // es un piso comercial y no la suma de los escalones).
        decimal costoProyecto = ejecucion + riesgo + gg + utilidad + va;

        decimal descuento = Math.Max(0m, cost.DescuentoUf);
        decimal neto = Math.Max(0m, cost.TotalVentaNetoUf);
        decimal iva = Math.Max(0m, cost.IvaUf);
        decimal conIva = Math.Max(0m, cost.TotalVentaConIvaUf);
        decimal ventaOficial = Math.Max(0m, cost.TotalVentaUf);

        // Neto pre-descuento reconstruido desde los totales del motor: hace que el descuento
        // comercial se dibuje sobre la misma recta que la venta neta.
        decimal netoBase = neto + descuento;

        // Cierre exacto: lo que separa la estructura de costo del precio de venta.
        decimal diferencia = netoBase - costoProyecto;

        decimal escala = Math.Max(1m, Math.Max(conIva, Math.Max(ventaOficial, Math.Max(costoProyecto, netoBase))));

        var steps = new List<Step>(11);

        // Los escalones base siempre existen: son los hitos que un director busca primero.
        steps.Add(new Step("Costo de ejecución", 0m, ejecucion, DashboardWaterfallTones.Execution, "", true, false, false));

        // Los recargos opcionales solo se dibujan si aportan monto: un factor en 0 (por ejemplo
        // "VA sobre equipos" en un proyecto sin equipos) no debe ocupar una fila en blanco, porque
        // rompe la lectura de la cascada y hace creer que falta un dato.
        AddIf(riesgo, () => steps.Add(new Step($"Riesgo ({pct(policy.RiesgoPct)})", ejecucion, ejecucion + riesgo, DashboardWaterfallTones.Overhead, "+", false, false, false)));
        AddIf(gg, () => steps.Add(new Step($"Gastos generales ({pct(policy.GastosGeneralesPct)})", ejecucion + riesgo, ejecucion + riesgo + gg, DashboardWaterfallTones.Overhead, "+", false, false, false)));
        AddIf(utilidad, () => steps.Add(new Step($"Utilidad ({pct(policy.UtilidadPct)})", ejecucion + riesgo + gg, ejecucion + riesgo + gg + utilidad, DashboardWaterfallTones.Overhead, "+", false, false, false)));
        AddIf(va, () => steps.Add(new Step($"VA sobre equipos ({pct(policy.ValorAgregadoPct)})", ejecucion + riesgo + gg + utilidad, costoProyecto, DashboardWaterfallTones.Overhead, "+", false, false, false)));

        steps.Add(new Step("Costo total proyecto", 0m, costoProyecto, DashboardWaterfallTones.Execution, "", true, false, false));

        AddIf(Math.Abs(diferencia), () => steps.Add(new Step("Diferencia por factores", costoProyecto, netoBase, DashboardWaterfallTones.Factor, "+", false, false, false)));
        AddIf(descuento, () => steps.Add(new Step($"Descuento comercial ({pct(policy.DescuentoPct)})", netoBase, neto, DashboardWaterfallTones.Discount, "−", false, false, false)));

        steps.Add(new Step("Venta neta", 0m, neto, DashboardWaterfallTones.Factor, "", true, false, false));

        AddIf(iva, () => steps.Add(new Step($"IVA ({pct(policy.IvaPct)})", neto, conIva, DashboardWaterfallTones.Tax, "+", false, false, true)));

        string totalLabel = policy.IncluirIvaEnTotal ? "TOTAL VENTA (con IVA)" : "TOTAL VENTA (neto)";
        steps.Add(new Step(totalLabel, 0m, ventaOficial > 0m ? ventaOficial : neto, DashboardWaterfallTones.Execution, "", true, true, false));

        void AddIf(decimal amount, Action add)
        {
            if (amount != 0m)
                add();
        }

        var rows = new List<DashboardWaterfallRowVm>(steps.Count);
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            decimal left = step.IsTotal ? 0m : Math.Min(step.From, step.To);
            decimal width = step.IsTotal ? step.To : Math.Abs(step.To - step.From);
            decimal safeScale = escala <= 0m ? 1m : escala;

            double leftRatio = Clamp01((double)(left / safeScale));
            double widthRatio = Math.Max(0.0035, Clamp01((double)(width / safeScale)));
            // La barra nunca puede pasarse del borde: el resto se recorta antes de dimensionar columnas.
            if (leftRatio + widthRatio > 1d)
                widthRatio = Math.Max(0.0035, 1d - leftRatio);
            double remainder = Math.Max(0d, 1d - leftRatio - widthRatio);

            double rowTop = TopPad + i * RowHeight;
            double barTop = rowTop + (RowHeight - BarHeight) / 2d;

            // El conector baja desde el cierre de esta etapa hasta la barra siguiente. Cuando la
            // etapa siguiente es un total (barra desde el origen) no hay conector: el total
            // reinicia la lectura en cero y un hilo desde el extremo derecho sería engañoso.
            bool nextIsTotal = i + 1 < steps.Count && steps[i + 1].IsTotal;
            bool hasConnector = i + 1 < steps.Count && !nextIsTotal;
            double chainEndRatio = Clamp01((double)(step.To / safeScale));

            rows.Add(new DashboardWaterfallRowVm
            {
                Label = step.Label,
                AmountLabel = money(step.IsTotal ? step.To : Math.Abs(step.To - step.From)),
                Sign = step.Sign,
                ToneKey = step.ToneKey,
                IsTotal = step.IsTotal,
                IsGrandTotal = step.IsGrandTotal,
                IsDimmed = step.IsDimmed,
                LeftRatio = leftRatio,
                WidthRatio = widthRatio,
                RemainderRatio = remainder,
                BarOffset = new GridLength(leftRatio, GridUnitType.Star),
                BarLength = new GridLength(widthRatio, GridUnitType.Star),
                BarRemainder = new GridLength(remainder, GridUnitType.Star),
                BarBrush = DashboardWaterfallTones.ToBrush(step.ToneKey),
                RowTop = rowTop,
                LabelTop = LabelTop,
                ValueTop = ValueTop,
                BarX = PlotLeft + leftRatio * PlotWidth,
                BarWidth = Math.Max(4d, widthRatio * PlotWidth),
                BarTop = (RowHeight - BarHeight) / 2d,
                BarHeight = BarHeight,
                ConnectorX = hasConnector ? PlotLeft + chainEndRatio * PlotWidth : -1,
                ConnectorTop = barTop + BarHeight - rowTop,
                ConnectorBottom = RowHeight + (RowHeight - BarHeight) / 2d
            });
        }

        // Eje: cinco divisiones del total de la oferta.
        double plotBottom = TopPad + steps.Count * RowHeight;
        double axisTop = plotBottom + 5;

        var axis = new List<DashboardWaterfallAxisVm>(6);
        var grid = new List<DashboardWaterfallGridVm>(4);
        for (int k = 0; k <= 5; k++)
        {
            double ratio = k / 5d;
            double x = PlotLeft + ratio * PlotWidth;
            axis.Add(new DashboardWaterfallAxisVm
            {
                X = x,
                Y = axisTop,
                Text = k == 0 ? "0" : axisMoney(escala * (decimal)ratio)
            });
            if (k > 0 && k < 5)
                grid.Add(new DashboardWaterfallGridVm { X = x, Y1 = TopPad, Y2 = plotBottom });
        }

        string subtitle = policy.IncluirIvaEnTotal
            ? "El total contractual ya incluye IVA; el neto es la base de la comisión y del margen."
            : "El total contractual es neto; el IVA se informa como referencia y no forma parte de la venta.";

        return new DashboardWaterfallVm
        {
            Rows = rows,
            Subtitle = subtitle,
            CanvasWidth = CanvasWidth,
            CanvasHeight = plotBottom + AxisHeight,
            PlotBottom = plotBottom,
            AxisTop = axisTop,
            AxisLabels = axis,
            GridLines = grid
        };
    }

    /// <summary>
    /// Veredicto de una línea. Usa el mismo contrato de preparación que el KPI, la barra de avance
    /// y el anillo, así que no puede contradecirlos.
    /// </summary>
    public static (string Title, string Reason, string Kind) BuildVerdict(
        DashboardReadinessContract readiness,
        string scenarioLabel)
    {
        readiness ??= DashboardReadinessContract.Empty;

        if (readiness.TotalPartidas <= 0)
            return ("Sin itemizado", "El proyecto todavía no tiene partidas cargadas.", "mute");

        if (readiness.ListoParaOfertar)
            return ("Apta para ofertar", $"{n(readiness.Listas)} de {n(readiness.TotalPartidas)} partidas listas · sin alertas bloqueantes.", "ok");

        if (readiness.BlockingCount > 0)
        {
            return (
                "No apta para ofertar",
                $"{n(readiness.Listas)} de {n(readiness.TotalPartidas)} partidas listas · " +
                $"{BlockingAlertPhrase(readiness.BlockingCount)} {(readiness.BlockingCount == 1 ? "impide" : "impiden")} cerrar la oferta.",
                "bad");
        }

        string reason = readiness.Pendientes > 0
            ? $"{n(readiness.Listas)} de {n(readiness.TotalPartidas)} partidas listas · " +
              $"{Plural(readiness.Pendientes, "pendiente", "pendientes")} " +
              $"({n(readiness.EmptyPartidas)} sin cubicación, {n(readiness.MaterialsWithoutPrice)} sin precio vigente)"
            : $"{n(readiness.Listas)} de {n(readiness.TotalPartidas)} partidas listas";

        if (!string.IsNullOrWhiteSpace(scenarioLabel))
            reason += $" · {scenarioLabel}";

        return ("Apta para ofertar con reservas", reason, "warn");
    }

    /// <summary>
    /// Preparación medida: cada compuerta informa su avance real, no un booleano suelto. Es la
    /// misma fuente que alimenta el anillo.
    /// </summary>
    public static IReadOnlyList<(string Label, string Detail, bool Ok, bool Warn)> BuildReadinessChecks(
        DashboardReadinessContract readiness)
    {
        readiness ??= DashboardReadinessContract.Empty;
        int total = readiness.TotalPartidas;
        int cubiertas = Math.Max(0, total - readiness.EmptyPartidas);
        int vigentes = Math.Max(0, total - readiness.MaterialsWithoutPrice);

        return new List<(string, string, bool, bool)>
        {
            ("Datos del proyecto", readiness.TotalPartidas > 0 ? "Completos" : "Faltan datos", readiness.TotalPartidas > 0, false),
            ("Itemizado importado", $"{n(total)} partidas", total > 0, false),
            ("Cubicación", total == 0 ? "Sin partidas" : $"{n(cubiertas)} de {n(total)}", readiness.CubIcacionOk, !readiness.CubIcacionOk && total > 0),
            ("Precios vigentes", total == 0 ? "Sin partidas" : $"{n(vigentes)} de {n(total)}", readiness.PreciosOk, !readiness.PreciosOk && total > 0),
        };
    }

    /// <summary>Etiqueta del escenario de sensibilidad ("sensibilidad +10% materiales = +4,6% sobre la venta neta").</summary>
    public static string BuildScenarioLabel(decimal scenarioPct, decimal impactPct)
        => $"sensibilidad {scenarioPct.ToString("+0.#;-0.#;0", Cl)}% materiales = {impactPct.ToString("+0.0;-0.0;0.0", Cl)}% sobre la venta neta";

    /// <summary>
    /// Alertas bloqueantes con concordancia singular/plural. Fuente única del texto: el veredicto,
    /// su chip, el checklist y el "siguiente paso" deben decir exactamente lo mismo.
    /// </summary>
    public static string BlockingLabel(int count) => count == 1 ? "1 bloqueante" : $"{n(count)} bloqueantes";

    /// <summary>Variante con la palabra "alerta" ("1 alerta bloqueante" / "3 alertas bloqueantes").</summary>
    public static string BlockingAlertPhrase(int count) => count == 1
        ? "1 alerta bloqueante"
        : $"{n(count)} alertas bloqueantes";

    /// <summary>Cantidad con sustantivo concordado ("1 pendiente" / "2 pendientes").</summary>
    private static string Plural(int count, string one, string many)
        => $"{n(count)} {(count == 1 ? one : many)}";

    private static string pct(decimal value) => (value <= 0m ? 0m : value).ToString("N1", Cl) + "%";

    private static string n(int value) => value.ToString("N0", Cl);

    private static double Clamp01(double value) => value < 0d ? 0d : (value > 1d ? 1d : value);
}
