using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Construye series temporales de forma determinista a partir de puntos ya calculados.
/// No inventa valores: periodos sin dato quedan con HasData=false.
/// Si Mode=Cumulative, acumula solo puntos con HasData (huecos no interpolan).
/// </summary>
public static class DashboardSeriesBuilder
{
    public static DashboardSeries BuildFromHistory(
        DashboardSeriesKind kind,
        string name,
        IEnumerable<(DateTime Date, decimal Value)> rawPoints,
        DashboardSeriesGranularity granularity,
        DashboardSeriesMode mode,
        DashboardSeriesUnit unit,
        string currencyCode,
        DateTime? rangeFrom,
        DateTime? rangeTo)
    {
        ArgumentNullException.ThrowIfNull(rawPoints);
        var ordered = rawPoints
            .Where(p => (!rangeFrom.HasValue || p.Date.Date >= rangeFrom.Value.Date)
                        && (!rangeTo.HasValue || p.Date.Date <= rangeTo.Value.Date))
            .OrderBy(p => p.Date)
            .ThenBy(p => p.Value)
            .ToList();

        var buckets = new SortedDictionary<DateTime, decimal>();
        foreach (var p in ordered)
        {
            var key = BucketStart(p.Date, granularity);
            if (buckets.ContainsKey(key))
                buckets[key] += p.Value;
            else
                buckets[key] = p.Value;
        }

        var points = new List<DashboardDataPoint>();
        decimal running = 0m;
        foreach (var kv in buckets)
        {
            var end = BucketEnd(kv.Key, granularity);
            var periodValue = RoundMoney(kv.Value);
            if (mode == DashboardSeriesMode.Cumulative)
                running = RoundMoney(running + periodValue);

            points.Add(new DashboardDataPoint
            {
                PeriodStart = kv.Key,
                PeriodEnd = end,
                Value = mode == DashboardSeriesMode.Cumulative ? running : periodValue,
                HasData = true
            });
        }

        return new DashboardSeries
        {
            Kind = kind,
            Name = name,
            Granularity = granularity,
            Mode = mode,
            Unit = unit,
            CurrencyCode = currencyCode,
            Points = points
        };
    }

    public static DashboardSeries BuildEmpty(
        DashboardSeriesKind kind, string name,
        DashboardSeriesGranularity granularity, DashboardSeriesMode mode,
        DashboardSeriesUnit unit, string currencyCode) =>
        new()
        {
            Kind = kind,
            Name = name,
            Granularity = granularity,
            Mode = mode,
            Unit = unit,
            CurrencyCode = currencyCode,
            Points = Array.Empty<DashboardDataPoint>()
        };

    public static DateTime BucketStart(DateTime date, DashboardSeriesGranularity g) =>
        g switch
        {
            DashboardSeriesGranularity.Daily => date.Date,
            DashboardSeriesGranularity.Weekly => StartOfWeek(date.Date),
            DashboardSeriesGranularity.Monthly => new DateTime(date.Year, date.Month, 1),
            _ => date.Date
        };

    public static DateTime BucketEnd(DateTime start, DashboardSeriesGranularity g) =>
        g switch
        {
            DashboardSeriesGranularity.Daily => start,
            DashboardSeriesGranularity.Weekly => start.AddDays(6),
            DashboardSeriesGranularity.Monthly => start.AddMonths(1).AddDays(-1),
            _ => start
        };

    private static DateTime StartOfWeek(DateTime d)
    {
        // Lunes como inicio — determinista independiente de cultura.
        var diff = ((int)d.DayOfWeek + 6) % 7;
        return d.AddDays(-diff);
    }

    private static decimal RoundMoney(decimal v) =>
        Math.Round(v, 2, MidpointRounding.ToEven);
}
