using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

internal static class CubicacionScKpiCoordinator
{
    public static string GetCanalizacionText(bool showEmptyState, IReadOnlyList<CuadroCargaSCRow> cuadros)
    {
        if (showEmptyState || cuadros == null || cuadros.Count == 0) return "0 m";
        var activeCircs = cuadros.SelectMany(cuadro => cuadro.Circuitos).Where(c => c.EsActivo).ToList();
        decimal total = activeCircs.Sum(c => c.MetrosBandejaEfectivo + c.MetrosTuberiaEfectivo);
        return $"{total:N1} m";
    }

    public static int GetCircuitosCount(bool showEmptyState, IReadOnlyList<CuadroCargaSCRow> cuadros)
    {
        if (showEmptyState || cuadros == null || cuadros.Count == 0) return 0;
        return cuadros.Sum(t => t.Circuitos.Count(c => c.EsActivo || c.EsReservaEquipada));
    }

    public static string GetCajasTerminalesText(bool showEmptyState, IReadOnlyList<CuadroCargaSCRow> cuadros, CubicacionScConfig config)
    {
        if (showEmptyState || cuadros == null || cuadros.Count == 0) return "0 un / 0.0 m";
        var activeCircs = cuadros.SelectMany(cuadro => cuadro.Circuitos).Where(c => c.EsActivo).ToList();
        decimal totalCajas = activeCircs.Sum(c => c.TotalCajasEfectivas);
        decimal totalTerminales = 0m;
        foreach (var c in activeCircs)
        {
            var flex = config?.Flexibles?.FirstOrDefault(f => string.Equals(f.Diametro, c.SeccionTuberia, StringComparison.OrdinalIgnoreCase));
            if (flex != null && c.TotalCajasEfectivas > 0m && flex.MetrosPorCaja > 0m)
                totalTerminales += c.TotalCajasEfectivas * flex.MetrosPorCaja;
        }
        return $"{totalCajas} un / {totalTerminales:N1} m";
    }

    public static string GetPotenciaText(bool showEmptyState, IReadOnlyList<CuadroCargaSCRow> cuadros)
    {
        if (showEmptyState || cuadros == null || cuadros.Count == 0) return "0.0 kW";
        var activeCircs = cuadros.SelectMany(cuadro => cuadro.Circuitos).Where(c => c.EsActivo || c.EsReservaEquipada).ToList();
        if (activeCircs.Count == 0) return "0.0 kW";
        decimal totalW = activeCircs.Sum(CuadroCargasSecEngine.EstimateCircuitPowerW);
        decimal kw = Math.Round(totalW / 1000m, 1);
        return $"{kw:N1} kW";
    }

    public static string GetDesbalanceText(bool showEmptyState, CuadroCargaSCRow? cuadro)
    {
        if (showEmptyState || cuadro == null) return "0.0% SEC";
        var summary = CuadroCargasSecEngine.CalculateTableroSummary(cuadro);
        return $"{summary.PorcentajeDesbalance:N1}% {(summary.ConformeSec ? "✓ SEC" : "⚠ Desbalance")}";
    }

    public static bool GetIsDesbalanceConformeSec(bool showEmptyState, CuadroCargaSCRow? cuadro)
    {
        if (showEmptyState || cuadro == null) return true;
        return CuadroCargasSecEngine.CalculateTableroSummary(cuadro).ConformeSec;
    }
}
