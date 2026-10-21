using System;

namespace QubiCostElectrical;

public static class ApuLaborEngine
{
    public static decimal FactorCompuesto(ApuCondicionesLaboralesRow? condiciones)
    {
        if (condiciones == null) return 1m;

        decimal pct = 0m;
        pct += condiciones.FactorAlturaPct;
        if (condiciones.TrabajoNocturno) pct += condiciones.RecargoNocturnoPct;
        if (condiciones.EdificioEnOperacion) pct += condiciones.RecargoOperacionPct;
        pct += FactorDificultadPct(condiciones.NivelDificultad);
        pct += condiciones.FactorRepetitividadPct;
        pct += FactorUbicacionPct(condiciones.Ubicacion, condiciones.FactorUbicacionPct);
        pct += condiciones.FactorCondicionesEspecialesPct;

        if (condiciones.HorasExtraordinariasPct > 0m && condiciones.RecargoHoraExtraPct > 0m)
            pct += condiciones.HorasExtraordinariasPct * condiciones.RecargoHoraExtraPct / 100m;

        return Math.Max(0.25m, 1m + pct / 100m);
    }

    public static decimal CostoUnitLabor(ApuRecursoRow recurso, ApuCondicionesLaboralesRow? condiciones)
    {
        if (recurso == null) return 0m;
        if (recurso.Tipo is not (ApuTipoRecurso.MO or ApuTipoRecurso.HH or ApuTipoRecurso.SC))
            return recurso.CostoUnitUf;

        return recurso.CostoUnitUf * FactorCompuesto(condiciones);
    }

    public static decimal? DesviacionRendimientoPct(ApuCondicionesLaboralesRow? condiciones)
    {
        if (condiciones == null || condiciones.RendimientoEstimadoHH <= 0m) return null;
        return (condiciones.RendimientoRealHH - condiciones.RendimientoEstimadoHH) / condiciones.RendimientoEstimadoHH * 100m;
    }

    public static bool RequiereRevisionRendimiento(ApuCondicionesLaboralesRow? condiciones, decimal umbralPct = 25m)
    {
        var desviacion = DesviacionRendimientoPct(condiciones);
        return desviacion.HasValue && Math.Abs(desviacion.Value) > umbralPct;
    }

    private static decimal FactorDificultadPct(string? nivel) => (nivel ?? "").Trim().ToUpperInvariant() switch
    {
        "BAJA" => -5m,
        "ALTA" => 15m,
        "EXTREMA" => 30m,
        _ => 0m
    };

    private static decimal FactorUbicacionPct(string? ubicacion, decimal manualPct)
    {
        if (manualPct != 0m) return manualPct;
        return (ubicacion ?? "").Trim().ToUpperInvariant() switch
        {
            "ALTURA" => 12m,
            "SUBTERRÁNEO" or "SUBTERRANEO" => 8m,
            "FACHADA" => 10m,
            "ESPACIO CONFINADO" => 18m,
            _ => 0m
        };
    }
}
