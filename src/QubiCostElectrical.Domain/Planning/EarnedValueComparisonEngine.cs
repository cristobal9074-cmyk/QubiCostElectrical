using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

public static class EarnedValueSnapshotHasher
{
    public static string ComputeHash(EarnedValueSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Result);
        var r = snapshot.Result;
        var i = r.Indicators;
        var sb = new StringBuilder();
        sb.Append($"EVM:{snapshot.ScheduleId.Value}|{snapshot.Version}|{snapshot.StatusDate:yyyyMMdd}|");
        sb.Append($"{i.Pv}|{i.Ev}|{i.Ac}|{i.Bac}|{i.Eac}|{i.Etc}|{i.Vac}|{i.Sv}|{i.Cv}|");
        sb.Append($"{i.Spi}|{i.Cpi}|{i.Tcpi}|{i.PercentPlanned}|{i.PercentEarned}|{i.PercentActual}|");
        sb.Append($"{r.Forecast.ForecastCost}|{r.Forecast.ForecastFinishVarianceDays}|{r.CurrencyCode}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) hex.Append(b.ToString("x2"));
        return hex.ToString();
    }

    public static bool Verify(EarnedValueSnapshot snapshot) =>
        string.Equals(snapshot.SHA256, ComputeHash(snapshot), StringComparison.OrdinalIgnoreCase);
}

public static class EarnedValueComparisonEngine
{
    public static EarnedValueComparison Compare(string leftLabel, string rightLabel, EarnedValueResult left, EarnedValueResult right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var li = left.Indicators;
        var ri = right.Indicators;
        var findings = new List<string>();

        var costDelta = ri.Cv - li.Cv;
        var schedDelta = ri.Sv - li.Sv;
        if (costDelta < 0m) findings.Add("La variación de costo empeoró.");
        else if (costDelta > 0m) findings.Add("La variación de costo mejoró.");
        if (schedDelta < 0m) findings.Add("La variación de plazo empeoró.");
        else if (schedDelta > 0m) findings.Add("La variación de plazo mejoró.");

        if (li.Cpi.HasValue && ri.Cpi.HasValue && ri.Cpi.Value < li.Cpi.Value)
            findings.Add("El rendimiento de costo (CPI) disminuyó.");
        if (li.Spi.HasValue && ri.Spi.HasValue && ri.Spi.Value < li.Spi.Value)
            findings.Add("El rendimiento de plazo (SPI) disminuyó.");

        return new EarnedValueComparison
        {
            LeftLabel = leftLabel,
            RightLabel = rightLabel,
            LeftStatusDate = left.StatusDate,
            RightStatusDate = right.StatusDate,
            PvDelta = ri.Pv - li.Pv,
            EvDelta = ri.Ev - li.Ev,
            AcDelta = ri.Ac - li.Ac,
            BacDelta = ri.Bac - li.Bac,
            SpiDelta = (ri.Spi ?? 0m) - (li.Spi ?? 0m),
            CpiDelta = (ri.Cpi ?? 0m) - (li.Cpi ?? 0m),
            TcpiDelta = (ri.Tcpi ?? 0m) - (li.Tcpi ?? 0m),
            EacDelta = ri.Eac - li.Eac,
            CostVarianceDelta = costDelta,
            ScheduleVarianceDelta = schedDelta,
            Findings = findings
        };
    }
}
