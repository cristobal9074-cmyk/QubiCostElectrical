using System;
using System.Collections.Generic;
using System.Globalization;

namespace QubiCostElectrical;

public static class DashboardKpiFactory
{
    public static List<DashboardKpi> FromEvm(
        EarnedValueResult? evm,
        EarnedValueHistoryEntry? previous,
        DashboardTrafficLightThresholds thresholds,
        DashboardDataQuality quality,
        DateTime? asOf)
    {
        var list = new List<DashboardKpi>();
        if (evm is null)
        {
            list.Add(NoData("SPI", "SPI", "EVM", asOf));
            list.Add(NoData("CPI", "CPI", "EVM", asOf));
            return list;
        }

        var i = evm.Indicators;
        list.Add(Index("PV", "PV", i.Pv, previous?.Pv, "CLP", DashboardKpiStatus.Neutral, "Valor planificado", quality, asOf, "EVM"));
        list.Add(Index("EV", "EV", i.Ev, previous?.Ev, "CLP", DashboardKpiStatus.Neutral, "Valor ganado", quality, asOf, "EVM"));
        list.Add(Index("AC", "AC", i.Ac, previous?.Ac, "CLP", DashboardKpiStatus.Neutral, "Costo real", quality, asOf, "EVM"));
        list.Add(Index("BAC", "BAC", i.Bac, previous?.Bac, "CLP", DashboardKpiStatus.Neutral, "Presupuesto al término", quality, asOf, "EVM"));
        list.Add(Index("EAC", "EAC", i.Eac, previous?.Eac, "CLP",
            i.Eac > i.Bac ? DashboardKpiStatus.Warning : DashboardKpiStatus.Good,
            "Estimación al término", quality, asOf, "EVM"));
        list.Add(Index("ETC", "ETC", i.Etc, null, "CLP", DashboardKpiStatus.Neutral, "Estimación por completar", quality, asOf, "EVM"));
        list.Add(Index("VAC", "VAC", i.Vac, null, "CLP", DashboardTrafficLightPolicy.EvaluateVac(i.Vac),
            EarnedValueInterpreter.InterpretVac(i.Vac).Message, quality, asOf, "EVM"));
        list.Add(Index("SV", "SV", i.Sv, null, "CLP", i.Sv < 0 ? DashboardKpiStatus.Warning : DashboardKpiStatus.Good,
            EarnedValueInterpreter.InterpretSv(i.Sv).Message, quality, asOf, "EVM"));
        list.Add(Index("CV", "CV", i.Cv, null, "CLP", i.Cv < 0 ? DashboardKpiStatus.Warning : DashboardKpiStatus.Good,
            EarnedValueInterpreter.InterpretCv(i.Cv).Message, quality, asOf, "EVM"));

        list.Add(Ratio("SPI", "SPI", i.Spi, previous?.Spi,
            DashboardTrafficLightPolicy.EvaluateSpi(i.Spi, thresholds),
            EarnedValueInterpreter.InterpretSpi(i.Spi).Message, quality, asOf, "EVM"));
        list.Add(Ratio("CPI", "CPI", i.Cpi, previous?.Cpi,
            DashboardTrafficLightPolicy.EvaluateCpi(i.Cpi, thresholds),
            EarnedValueInterpreter.InterpretCpi(i.Cpi).Message, quality, asOf, "EVM"));
        list.Add(Ratio("TCPI", "TCPI", i.Tcpi, previous?.Tcpi,
            i.Tcpi.HasValue ? DashboardKpiStatus.Neutral : DashboardKpiStatus.NoData,
            EarnedValueInterpreter.InterpretTcpi(i.Tcpi, i.Cpi).Message, quality, asOf, "EVM"));

        list.Add(Index("PCT_PLAN", "% Planificado", i.PercentPlanned, null, "%", DashboardKpiStatus.Neutral,
            "Avance planificado", quality, asOf, "EVM"));
        list.Add(Index("PCT_EARNED", "% Ganado", i.PercentEarned, null, "%", DashboardKpiStatus.Neutral,
            "Avance ganado", quality, asOf, "EVM"));
        list.Add(Index("PCT_ACTUAL", "% Actual (AC/BAC)", i.PercentActual, null, "%", DashboardKpiStatus.Neutral,
            "Avance financiero", quality, asOf, "EVM"));
        list.Add(Index("FCST_COST", "Forecast costo", evm.Forecast.ForecastCost, null, "CLP", DashboardKpiStatus.Neutral,
            "Costo forecast", quality, asOf, "EVM"));
        list.Add(Ratio("FCST_VAR", "Var. fin forecast", evm.Forecast.ForecastFinishVarianceDays, null,
            DashboardTrafficLightPolicy.EvaluateFinishVarianceDays(evm.Forecast.ForecastFinishVarianceDays),
            "Variación de término", quality, asOf, "Forecast"));

        return list;
    }

    public static DashboardKpi ScheduleKpi(string code, string title, DateTime? date, DashboardDataQuality q, DateTime? asOf) =>
        new()
        {
            Code = code,
            Title = title,
            Category = "Plazo",
            Value = null,
            DisplayValue = date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin datos",
            Unit = "fecha",
            Status = date.HasValue ? DashboardKpiStatus.Neutral : DashboardKpiStatus.NoData,
            Interpretation = date.HasValue ? title : "Sin datos",
            Trend = DashboardTrendDirection.Unknown,
            AsOfDate = asOf,
            Source = "Schedule",
            DataQuality = q
        };

    public static DashboardKpi CountKpi(string code, string title, int count, DashboardKpiStatus status, DashboardDataQuality q, DateTime? asOf) =>
        new()
        {
            Code = code,
            Title = title,
            Category = "Plazo",
            Value = count,
            DisplayValue = count.ToString(CultureInfo.InvariantCulture),
            Unit = "u",
            Status = status,
            Interpretation = title,
            AsOfDate = asOf,
            Source = "CPM/Progress",
            DataQuality = q
        };

    private static DashboardKpi Index(
        string code, string title, decimal value, decimal? prev, string unit,
        DashboardKpiStatus status, string interpretation, DashboardDataQuality q, DateTime? asOf, string source) =>
        new()
        {
            Code = code,
            Title = title,
            Category = source == "EVM" || source == "Forecast" ? "EVM" : "Costos",
            Value = value,
            DisplayValue = value.ToString("N2", CultureInfo.InvariantCulture),
            Unit = unit,
            Status = status,
            Interpretation = interpretation,
            PreviousValue = prev,
            Delta = prev.HasValue ? value - prev.Value : null,
            Trend = TrendOf(value, prev),
            AsOfDate = asOf,
            Source = source,
            DataQuality = q
        };

    private static DashboardKpi Ratio(
        string code, string title, decimal? value, decimal? prev,
        DashboardKpiStatus status, string interpretation, DashboardDataQuality q, DateTime? asOf, string source) =>
        new()
        {
            Code = code,
            Title = title,
            Category = "EVM",
            Value = value,
            DisplayValue = value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "Sin datos",
            Unit = "índice",
            Status = status,
            Interpretation = interpretation,
            PreviousValue = prev,
            Delta = value.HasValue && prev.HasValue ? value.Value - prev.Value : null,
            Trend = TrendOf(value, prev),
            AsOfDate = asOf,
            Source = source,
            DataQuality = value.HasValue ? q : DashboardDataQuality.Partial
        };

    private static DashboardKpi NoData(string code, string title, string category, DateTime? asOf) =>
        new()
        {
            Code = code,
            Title = title,
            Category = category,
            DisplayValue = "Sin datos",
            Status = DashboardKpiStatus.NoData,
            Interpretation = "Sin datos",
            AsOfDate = asOf,
            Source = category,
            DataQuality = DashboardDataQuality.NoProgress
        };

    private static DashboardTrendDirection TrendOf(decimal? current, decimal? previous)
    {
        if (!current.HasValue || !previous.HasValue) return DashboardTrendDirection.Unknown;
        var d = current.Value - previous.Value;
        if (d > 0m) return DashboardTrendDirection.Up;
        if (d < 0m) return DashboardTrendDirection.Down;
        return DashboardTrendDirection.Flat;
    }
}
