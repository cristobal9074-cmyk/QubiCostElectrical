using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class MaterialControlService : IMaterialControlService
{
    public IReadOnlyList<MaterialControlRow> Build(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        IEnumerable<SolicitudItemRow> solicitudes,
        decimal toleranciaPct = 5m)
    {
        decimal safeTolerance = Math.Clamp(toleranciaPct, 0m, 50m);
        decimal factor = 1m + safeTolerance / 100m;

        var materialLookup = (materiales ?? Enumerable.Empty<MaterialRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => NormalizeCode(x.Codigo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Agrupar cubicación comprable
        var cubicacionGroups = (cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Where(IsPurchasableMaterial)
            .GroupBy(x => ResolvePurchaseGroupKey(x, materialLookup), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Agrupar solicitudes válidas de materiales
        var validSolicitudes = (solicitudes ?? Enumerable.Empty<SolicitudItemRow>())
            .Where(s => string.Equals(s.Tipo, "Material", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(s.Tipo))
            .Where(s => !string.IsNullOrWhiteSpace(s.Codigo) || !string.IsNullOrWhiteSpace(s.Descripcion))
            .ToList();

        var solicitudesByCode = validSolicitudes
            .GroupBy(s => ResolveSolicitudKey(s, materialLookup), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var allKeys = new HashSet<string>(cubicacionGroups.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in solicitudesByCode.Keys)
            allKeys.Add(key);

        var result = new List<MaterialControlRow>();

        foreach (var codeKey in allKeys)
        {
            cubicacionGroups.TryGetValue(codeKey, out var cubRows);
            solicitudesByCode.TryGetValue(codeKey, out var reqRows);

            materialLookup.TryGetValue(codeKey, out var mat);
            if (mat == null && cubRows != null)
            {
                mat = cubRows
                    .Select(r => materialLookup.TryGetValue(NormalizeCode(r.Codigo), out var m) ? m : null)
                    .FirstOrDefault(m => m != null);
            }

            string descripcion = mat?.Descripcion
                ?? cubRows?.Select(r => r.Descripcion).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))
                ?? reqRows?.Select(r => r.Descripcion).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))
                ?? "Material sin descripción";

            string unidad = mat?.Unidad
                ?? cubRows?.Select(r => r.Unidad).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
                ?? reqRows?.Select(r => r.Unidad).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
                ?? "UN";

            string categoria = mat?.Categoria
                ?? cubRows?.Select(r => r.Area).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))
                ?? "General";

            decimal cantidadNeta = cubRows != null ? cubRows.Sum(r => r.Cantidad) : 0m;
            decimal cantidadCubicada = Math.Round(cantidadNeta * factor, 2);

            decimal cantidadSolicitada = reqRows != null ? reqRows.Sum(r => r.Cantidad) : 0m;
            decimal cantidadPendiente = Math.Max(0m, cantidadCubicada - cantidadSolicitada);
            decimal cantidadExceso = Math.Max(0m, cantidadSolicitada - cantidadCubicada);
            bool tieneSobrepedido = cantidadSolicitada > cantidadCubicada;

            double porcentajeAvance = cantidadCubicada > 0m
                ? (double)Math.Round((cantidadSolicitada / cantidadCubicada) * 100m, 1)
                : (cantidadSolicitada > 0m ? 100.0 : 0.0);

            string estadoControl;
            if (cantidadSolicitada == 0m)
                estadoControl = "Sin pedir";
            else if (tieneSobrepedido)
                estadoControl = cantidadCubicada == 0m
                    ? $"⚠️ No presupuestado (+{cantidadExceso:N0})"
                    : $"⚠️ Sobrepedido (+{cantidadExceso:N0})";
            else if (cantidadSolicitada == cantidadCubicada)
                estadoControl = "✓ Completado";
            else
                estadoControl = "En proceso";

            // Historial de solicitudes calculando acumulado para marcar cuál provocó el exceso
            var usosList = new List<MaterialSolicitudUso>();
            if (reqRows != null)
            {
                decimal acumulado = 0m;
                foreach (var req in reqRows)
                {
                    bool causaExceso = (acumulado < cantidadCubicada) && (acumulado + req.Cantidad > cantidadCubicada);
                    if (cantidadCubicada == 0m && req.Cantidad > 0m)
                        causaExceso = true;

                    acumulado += req.Cantidad;

                    usosList.Add(new MaterialSolicitudUso(
                        string.IsNullOrWhiteSpace(req.SolicitudCodigo) ? "SM" : req.SolicitudCodigo,
                        string.IsNullOrWhiteSpace(req.Fecha) ? "—" : req.Fecha,
                        string.IsNullOrWhiteSpace(req.SolicitadoPor) ? "En terreno" : req.SolicitadoPor,
                        req.Cantidad,
                        unidad,
                        causaExceso,
                        req.Observaciones ?? ""));
                }
            }

            var desglosePisos = (cubRows ?? Enumerable.Empty<CubicacionRow>())
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Area) ? "General" : r.Area.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    decimal cantArea = Math.Round(g.Sum(r => r.Cantidad) * factor, 2);
                    double pctArea = cantidadCubicada > 0m ? (double)Math.Round((cantArea / cantidadCubicada) * 100m, 1) : 0.0;
                    return new MaterialPisoDesglose(g.Key, cantArea, pctArea, unidad);
                })
                .OrderByDescending(d => d.Cantidad)
                .ToList();

            result.Add(new MaterialControlRow(
                codeKey,
                descripcion,
                unidad,
                categoria,
                cantidadCubicada,
                cantidadSolicitada,
                cantidadPendiente,
                cantidadExceso,
                porcentajeAvance,
                estadoControl,
                tieneSobrepedido,
                usosList,
                desglosePisos));
        }

        return result
            .OrderByDescending(r => r.TieneSobrepedido)
            .ThenByDescending(r => r.PorcentajeAvance)
            .ThenBy(r => r.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public MaterialControlSummary GetSummary(IReadOnlyList<MaterialControlRow> rows)
    {
        if (rows == null || rows.Count == 0)
        {
            return new MaterialControlSummary(0m, 0m, 0m, 0m, 0, 0, 0, 0, 0.0);
        }

        decimal totalCubicado = rows.Sum(r => r.CantidadCubicada);
        decimal totalSolicitado = rows.Sum(r => r.CantidadSolicitada);
        decimal totalPendiente = rows.Sum(r => r.CantidadPendiente);
        decimal totalExceso = rows.Sum(r => r.CantidadExceso);
        int sobrepedidos = rows.Count(r => r.TieneSobrepedido);
        int pendientes = rows.Count(r => r.EstaEnProceso || r.SinPedir);
        int completos = rows.Count(r => r.EstaCompleto);

        double pctGlobal = totalCubicado > 0m
            ? (double)Math.Round((totalSolicitado / totalCubicado) * 100m, 1)
            : 0.0;

        return new MaterialControlSummary(
            totalCubicado,
            totalSolicitado,
            totalPendiente,
            totalExceso,
            rows.Count,
            sobrepedidos,
            pendientes,
            completos,
            pctGlobal);
    }

    private static string ResolvePurchaseGroupKey(CubicacionRow row, IReadOnlyDictionary<string, MaterialRow> materialLookup)
    {
        string code = NormalizeCode(row.Codigo);
        if (materialLookup.TryGetValue(code, out var material) && !string.IsNullOrWhiteSpace(material.EquivalenciaKey))
            return NormalizeCode(material.EquivalenciaKey);
        return code;
    }

    private static string ResolveSolicitudKey(SolicitudItemRow item, IReadOnlyDictionary<string, MaterialRow> materialLookup)
    {
        string code = NormalizeCode(item.Codigo);
        if (!string.IsNullOrWhiteSpace(code))
        {
            if (materialLookup.TryGetValue(code, out var material) && !string.IsNullOrWhiteSpace(material.EquivalenciaKey))
                return NormalizeCode(material.EquivalenciaKey);
            return code;
        }

        // Si viene sin código, usar la descripción normalizada
        return NormalizeCode(item.Descripcion);
    }

    private static bool IsPurchasableMaterial(CubicacionRow row)
    {
        if (row == null) return false;
        if (string.IsNullOrWhiteSpace(row.Codigo)) return false;
        if (row.Cantidad == 0m) return false;

        string tipo = (row.Tipo ?? "MAT").Trim().ToUpperInvariant();
        if (tipo is "MO" or "MANO_OBRA" or "MANO DE OBRA" or "EQUIPO" or "HERRAMIENTA")
            return false;

        return true;
    }

    private static string NormalizeCode(string? value) => (value ?? "").Trim().ToUpperInvariant();
}
