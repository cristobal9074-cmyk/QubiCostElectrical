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

    public static void SyncCalendarioFromProject(ApuCondicionesLaboralesRow condiciones, LaborDaySettingsRow? settings)
    {
        if (condiciones == null || settings == null) return;
        if (condiciones.HorasPorJornada <= 0m) condiciones.HorasPorJornada = settings.HorasPorJornada;
        if (condiciones.DiasPorSemana <= 0m) condiciones.DiasPorSemana = settings.DiasPorSemana;
        if (string.IsNullOrWhiteSpace(condiciones.CuadrillaReferencia))
            condiciones.CuadrillaReferencia = settings.CuadrillaDefault;
    }

    public static void EnsureRendimientoEstimado(ApuRow apu)
    {
        if (apu?.CondicionesLaborales == null) return;
        if (apu.CondicionesLaborales.RendimientoEstimadoHH > 0m) return;

        decimal hh = apu.HHUnitarias;
        if (hh <= 0m)
            hh = apu.Recursos.Where(r => r.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH).Sum(r => r.HHUnitarias);

        apu.CondicionesLaborales.RendimientoEstimadoHH = hh;
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
