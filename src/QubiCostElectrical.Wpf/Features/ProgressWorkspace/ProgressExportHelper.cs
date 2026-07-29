using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QubiCostElectrical.Wpf.Features.ProgressWorkspace;

/// <summary>Exportación CSV sin recálculo — solo serializa datos ya calculados.</summary>
public static class ProgressExportHelper
{
    public static string ExportSummaryCsv(IReadOnlyList<ProgressRowVm> rows, string summary, DateTime statusDate)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QubiCostElectrical", "Exports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"avance-{statusDate:yyyyMMdd-HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine($"# FechaEstado;{statusDate:O}");
        sb.AppendLine($"# Resumen;{summary.Replace(';', ',')}");
        sb.AppendLine("Codigo;Nombre;Estado;Pct;Fisico;Financiero;PlanInicio;PlanFin;ActualInicio;ActualFin;ForecastInicio;ForecastFin;Critica;OOS");
        foreach (var r in rows)
        {
            sb.Append(r.Code).Append(';')
              .Append(r.Name.Replace(';', ',')).Append(';')
              .Append(r.Status).Append(';')
              .Append(r.PercentComplete).Append(';')
              .Append(r.PhysicalProgress).Append(';')
              .Append(r.FinancialProgress).Append(';')
              .Append(r.PlannedStart?.ToString("yyyy-MM-dd")).Append(';')
              .Append(r.PlannedFinish?.ToString("yyyy-MM-dd")).Append(';')
              .Append(r.ActualStart?.ToString("yyyy-MM-dd")).Append(';')
              .Append(r.ActualFinish?.ToString("yyyy-MM-dd")).Append(';')
              .Append(r.ForecastStart?.ToString("yyyy-MM-dd")).Append(';')
              .Append(r.ForecastFinish?.ToString("yyyy-MM-dd")).Append(';')
              .Append(r.CriticalLabel).Append(';')
              .Append(r.HasOutOfSequence ? "Si" : "No")
              .AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}
