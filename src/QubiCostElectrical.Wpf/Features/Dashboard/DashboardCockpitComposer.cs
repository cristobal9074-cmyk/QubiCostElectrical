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

    public static DashboardWaterfallVm BuildWaterfall(
        CostBreakdown cost,
        DashboardWaterfallPolicy policy,
        Func<decimal, string> money)
    {
        cost ??= new CostBreakdown();
        money ??= v => v.ToString("N0", Cl);

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

        var rows = new List<DashboardWaterfallRowVm>(11);

        // Los escalones base siempre existen: son los hitos que un director busca primero.
        rows.Add(Row("Costo de ejecución", 0m, ejecucion, escala, money, DashboardWaterfallTones.Execution, total: true));

        // Los recargos opcionales solo se dibujan si aportan monto: un factor en 0 (por ejemplo
        // "VA sobre equipos" en un proyecto sin equipos) no debe ocupar una fila en blanco, porque
        // rompe la lectura de la cascada y hace creer que falta un dato.
        AddIf(riesgo, () => Row($"Riesgo ({pct(policy.RiesgoPct)})", ejecucion, ejecucion + riesgo, escala, money, DashboardWaterfallTones.Overhead, sign: "+"));
        AddIf(gg, () => Row($"Gastos generales ({pct(policy.GastosGeneralesPct)})", ejecucion + riesgo, ejecucion + riesgo + gg, escala, money, DashboardWaterfallTones.Overhead, sign: "+"));
        AddIf(utilidad, () => Row($"Utilidad ({pct(policy.UtilidadPct)})", ejecucion + riesgo + gg, ejecucion + riesgo + gg + utilidad, escala, money, DashboardWaterfallTones.Overhead, sign: "+"));
        AddIf(va, () => Row($"VA sobre equipos ({pct(policy.ValorAgregadoPct)})", ejecucion + riesgo + gg + utilidad, costoProyecto, escala, money, DashboardWaterfallTones.Overhead, sign: "+"));

        rows.Add(Row("Costo total proyecto", 0m, costoProyecto, escala, money, DashboardWaterfallTones.Execution, total: true));

        AddIf(Math.Abs(diferencia), () => Row("Diferencia por factores", costoProyecto, netoBase, escala, money, DashboardWaterfallTones.Factor, sign: "+"));
        AddIf(descuento, () => Row($"Descuento comercial ({pct(policy.DescuentoPct)})", netoBase, neto, escala, money, DashboardWaterfallTones.Discount, sign: "−"));

        rows.Add(Row("Venta neta", 0m, neto, escala, money, DashboardWaterfallTones.Factor, total: true));

        AddIf(iva, () => Row($"IVA ({pct(policy.IvaPct)})", neto, conIva, escala, money, DashboardWaterfallTones.Tax, sign: "+", dim: true));

        string totalLabel = policy.IncluirIvaEnTotal ? "TOTAL VENTA (con IVA)" : "TOTAL VENTA (neto)";
        rows.Add(Row(totalLabel, 0m, ventaOficial > 0m ? ventaOficial : neto, escala, money, DashboardWaterfallTones.Execution, total: true, grand: true));

        void AddIf(decimal amount, Func<DashboardWaterfallRowVm> factory)
        {
            if (amount != 0m)
                rows.Add(factory());
        }

        string subtitle = policy.IncluirIvaEnTotal
            ? "El total contractual ya incluye IVA; el neto es la base de la comisión y del margen."
            : "El total contractual es neto; el IVA se informa como referencia y no forma parte de la venta.";

        return new DashboardWaterfallVm { Rows = rows, Subtitle = subtitle };
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

    private static DashboardWaterfallRowVm Row(
        string label,
        decimal from,
        decimal to,
        decimal scale,
        Func<decimal, string> money,
        string toneKey,
        string sign = "",
        bool total = false,
        bool grand = false,
        bool dim = false)
    {
        decimal left = total ? 0m : Math.Min(from, to);
        decimal width = total ? to : Math.Abs(to - from);
        decimal safeScale = scale <= 0m ? 1m : scale;

        double leftRatio = Clamp01((double)(left / safeScale));
        double widthRatio = Math.Max(0.0035, Clamp01((double)(width / safeScale)));
        // La barra nunca puede pasarse del borde: el resto se recorta antes de dimensionar columnas.
        if (leftRatio + widthRatio > 1d)
            widthRatio = Math.Max(0.0035, 1d - leftRatio);

        double remainder = Math.Max(0d, 1d - leftRatio - widthRatio);
        decimal amount = total ? to : Math.Abs(to - from);

        return new DashboardWaterfallRowVm
        {
            Label = label,
            AmountLabel = money(amount),
            Sign = sign,
            ToneKey = toneKey,
            IsTotal = total,
            IsGrandTotal = grand,
            IsDimmed = dim,
            LeftRatio = leftRatio,
            WidthRatio = widthRatio,
            RemainderRatio = remainder,
            BarOffset = new GridLength(leftRatio, GridUnitType.Star),
            BarLength = new GridLength(widthRatio, GridUnitType.Star),
            BarRemainder = new GridLength(remainder, GridUnitType.Star),
            BarBrush = DashboardWaterfallTones.ToBrush(toneKey)
        };
    }

    private static double Clamp01(double value) => value < 0d ? 0d : (value > 1d ? 1d : value);
}
