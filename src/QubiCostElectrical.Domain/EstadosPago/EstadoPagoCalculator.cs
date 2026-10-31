using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Partida base (hoja del Itemizado) para el cálculo de estados de pago.</summary>
public sealed record EstadoPagoPartidaInput(
    string Id,
    string Item,
    string Descripcion,
    decimal Monto,
    string Unidad = Unidades.GL,
    decimal Cantidad = 1m,
    decimal PrecioUnitario = 0m);

/// <summary>Fila calculada de la tabla de detalle del mes.</summary>
public sealed record EstadoPagoPartidaCalc(
    string ItemId,
    string Item,
    string Descripcion,
    decimal Monto,
    decimal AvanceAnterior,
    decimal AvanceMes,
    decimal MontoMes,
    decimal AcumPct,
    string Unidad = Unidades.GL,
    decimal Cantidad = 1m,
    decimal PrecioUnitario = 0m,
    decimal CantidadMes = 0m,
    decimal CantidadAcum = 0m,
    decimal SaldoCantidad = 0m,
    decimal SaldoMonto = 0m);

/// <summary>Totales calculados de un estado de pago mensual.</summary>
public sealed record EstadoPagoMesCalc(
    decimal TrabajoMes,
    decimal AcumAntes,
    decimal Acum,
    decimal PctAcum,
    decimal RecuperoMes,
    decimal AnticipoTotal,
    decimal AnticipoSaldo,
    decimal RetencionMes,
    decimal RetencionAcum,
    decimal Neto,
    decimal Iva,
    decimal Total);

/// <summary>Detalle completo de un mes: filas + totales.</summary>
public sealed record EstadoPagoMesDetalle(IReadOnlyList<EstadoPagoPartidaCalc> Filas, EstadoPagoMesCalc Totales);

/// <summary>Indicadores de cabecera.</summary>
public sealed record EstadoPagoKpis(
    decimal TotalContratado,
    int Pagados,
    int CantMeses,
    decimal Acumulado,
    decimal Saldo,
    decimal RetencionAcum,
    decimal AvanceFisicoPct);

/// <summary>
/// Cálculo puro de estados de pago (fórmulas de docs/propuesta-estados-pago.html,
/// función <c>calculoMes</c>): trabajo del mes → recupero de anticipo →
/// retención garantía → neto → IVA → total. Sin acceso a UI ni persistencia.
/// </summary>
public static class EstadoPagoCalculator
{
    /// <summary>Calcula el mes anterior (retrocediendo de año si mes == 1).</summary>
    public static (int Anio, int Mes) PreviousMonth(int anio, int mes) =>
        mes <= 1 ? (anio - 1, 12) : (anio, mes - 1);

    /// <summary>Calcula el mes siguiente (avanzando de año si mes == 12).</summary>
    public static (int Anio, int Mes) NextMonth(int anio, int mes) =>
        mes >= 12 ? (anio + 1, 1) : (anio, mes + 1);

    /// <summary>Calcula el año y mes calendario para un período dado según MesInicio y Anio.</summary>
    public static (int Anio, int Mes) GetMonthForPeriod(EstadoPagoConfig cfg, int periodoIndex)
    {
        cfg ??= EstadoPagoConfig.Default();
        int mesInicioClamped = Math.Clamp(cfg.MesInicio, 1, 12);
        int totalMeses = (mesInicioClamped - 1) + Math.Max(0, periodoIndex);
        int anio = cfg.Anio + (totalMeses / 12);
        int mes = (totalMeses % 12) + 1;
        return (anio, mes);
    }

    /// <summary>
    /// Acumulado % por partida considerando los meses generados hasta <paramref name="mesInclusive"/> de <paramref name="anio"/>.
    /// Orden cronológico multi-año para obras iniciadas en cualquier mes.
    /// </summary>
    public static Dictionary<string, decimal> BuildAcumulado(
        IEnumerable<EstadoPagoMesRow> meses,
        int anio,
        int mesInclusive,
        int? anioInicio = null)
    {
        int targetKey = anio * 12 + mesInclusive;
        int minKey = anioInicio.HasValue ? anioInicio.Value * 12 + 1 : anio * 12 + 1;
        var acum = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in (meses ?? Enumerable.Empty<EstadoPagoMesRow>())
                     .Where(r => r is not null)
                     .OrderBy(r => r.Anio * 12 + r.Mes))
        {
            if (anioInicio.HasValue)
            {
                int rowKey = row.Anio * 12 + row.Mes;
                if (rowKey < minKey || rowKey > targetKey)
                    continue;
            }
            else
            {
                if (row.Anio != anio || row.Mes > mesInclusive)
                    continue;
            }

            foreach (var kv in row.AvancesPorItem)
            {
                decimal v = kv.Value;
                if (v < 0m) v = 0m;
                acum.TryGetValue(kv.Key, out var prev);
                decimal total = prev + v;
                acum[kv.Key] = total > 100m ? 100m : total;
            }
        }
        return acum;
    }

    /// <summary>
    /// Calcula el detalle de un mes: filas con avance anterior/mes/acumulado y
    /// totales (anticipo, retención, IVA) según la configuración vigente.
    /// </summary>
    public static EstadoPagoMesDetalle CalcularMes(
        IReadOnlyList<EstadoPagoPartidaInput> partidas,
        IReadOnlyDictionary<string, decimal> acumAntes,
        IReadOnlyDictionary<string, decimal> avancesMes,
        EstadoPagoConfig config)
    {
        partidas ??= Array.Empty<EstadoPagoPartidaInput>();
        config ??= EstadoPagoConfig.Default();

        var filas = new List<EstadoPagoPartidaCalc>(partidas.Count);
        decimal trabajoMes = 0m, acumAntesTotal = 0m, acumTotal = 0m, total = 0m;

        foreach (var p in partidas)
        {
            decimal anterior = Clamped(acumAntes, p.Id, 0m, 100m);
            decimal av = Clamped(avancesMes, p.Id, 0m, 100m - anterior);
            decimal acum = anterior + av;

            decimal montoMes = p.Monto * av / 100m;
            trabajoMes += montoMes;
            acumAntesTotal += p.Monto * anterior / 100m;
            acumTotal += p.Monto * acum / 100m;
            total += p.Monto;

            decimal cantMes = p.Cantidad * av / 100m;
            decimal cantAcum = p.Cantidad * acum / 100m;
            decimal saldoCant = Math.Max(0m, p.Cantidad - cantAcum);
            decimal saldoMonto = Math.Max(0m, p.Monto - (p.Monto * acum / 100m));
            decimal pu = p.PrecioUnitario > 0m ? p.PrecioUnitario : (p.Cantidad > 0m ? p.Monto / p.Cantidad : 0m);

            filas.Add(new EstadoPagoPartidaCalc(
                p.Id, p.Item, p.Descripcion, p.Monto, anterior, av, montoMes, acum, p.Unidad, p.Cantidad, pu, cantMes, cantAcum, saldoCant, saldoMonto));
        }

        return new EstadoPagoMesDetalle(filas, CalcularTotales(total, trabajoMes, acumAntesTotal, acumTotal, config));
    }

    /// <summary>Totales del mes a partir de los agregados ya sumados.</summary>
    public static EstadoPagoMesCalc CalcularTotales(
        decimal total,
        decimal trabajoMes,
        decimal acumAntes,
        decimal acum,
        EstadoPagoConfig config)
    {
        config ??= EstadoPagoConfig.Default();

        decimal anticipoTotal = total * config.AnticipoPct / 100m;
        decimal anticipoAcum = acum * config.AnticipoPct / 100m;
        decimal anticipoAntes = acumAntes * config.AnticipoPct / 100m;
        decimal recuperoMes = anticipoAcum - anticipoAntes;

        decimal retencionMes = trabajoMes * config.RetencionPct / 100m;
        decimal retencionAcum = acum * config.RetencionPct / 100m;

        decimal neto = trabajoMes - recuperoMes - retencionMes;
        if (neto < 0m) neto = 0m;

        decimal iva = neto * config.IvaPct / 100m;

        return new EstadoPagoMesCalc(
            TrabajoMes: trabajoMes,
            AcumAntes: acumAntes,
            Acum: acum,
            PctAcum: total > 0m ? acum / total * 100m : 0m,
            RecuperoMes: recuperoMes,
            AnticipoTotal: anticipoTotal,
            AnticipoSaldo: anticipoTotal - anticipoAcum,
            RetencionMes: retencionMes,
            RetencionAcum: retencionAcum,
            Neto: neto,
            Iva: iva,
            Total: neto + iva);
    }

    /// <summary>Indicadores de cabecera según el acumulado de la base seleccionada (mesBase).</summary>
    public static EstadoPagoKpis BuildKpis(
        IReadOnlyList<EstadoPagoPartidaInput> partidas,
        IReadOnlyDictionary<string, decimal> acumBase,
        IReadOnlyCollection<EstadoPagoMesRow> meses,
        EstadoPagoConfig config)
    {
        partidas ??= Array.Empty<EstadoPagoPartidaInput>();
        config ??= EstadoPagoConfig.Default();
        acumBase ??= new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        decimal total = 0m, acum = 0m;
        foreach (var p in partidas)
        {
            decimal a = Clamped(acumBase, p.Id, 0m, 100m);
            total += p.Monto;
            acum += p.Monto * a / 100m;
        }

        int pagados = meses?.Count(m => m is not null && m.Anio == config.Anio && m.Estado == EstadoPagoEst.Pago) ?? 0;

        return new EstadoPagoKpis(
            TotalContratado: total,
            Pagados: pagados,
            CantMeses: config.CantMeses,
            Acumulado: acum,
            Saldo: total - acum,
            RetencionAcum: acum * config.RetencionPct / 100m,
            AvanceFisicoPct: total > 0m ? acum / total * 100m : 0m);
    }

    /// <summary>
    /// Copia los avances de un mes origen limitando cada partida al saldo restante
    /// del acumulado anterior (monotonía: el acumulado nunca supera 100 %).
    /// </summary>
    public static Dictionary<string, decimal> ClampedCopy(
        EstadoPagoMesRow origen,
        IReadOnlyDictionary<string, decimal> acumAntes)
    {
        var copia = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (origen is null)
            return copia;

        foreach (var kv in origen.AvancesPorItem)
        {
            decimal av = kv.Value;
            if (av < 0m) av = 0m;
            acumAntes.TryGetValue(kv.Key, out decimal anterior);
            decimal max = 100m - anterior;
            if (max < 0m) max = 0m;
            if (av > max) av = max;
            copia[kv.Key] = av;
        }
        return copia;
    }

    private static decimal Clamped(IReadOnlyDictionary<string, decimal> map, string key, decimal min, decimal max)
    {
        if (max < min) max = min;
        if (map is null || string.IsNullOrEmpty(key) || !map.TryGetValue(key, out var v))
            return min;
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}
