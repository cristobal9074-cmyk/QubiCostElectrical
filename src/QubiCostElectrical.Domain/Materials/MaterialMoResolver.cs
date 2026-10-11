using System;

namespace QubiCostElectrical;

/// <summary>
/// Precio de M.O. de instalación por material. Dos capas:
/// 1) Modo del PROYECTO (<see cref="ProjectInfoRow.ModoMoInstalacion"/>):
///    "Directo" (o vacío) → M.O. del catálogo; "Rendimiento" → TarifaDhClp ÷ RendimientoDh
///    con fallback automático al directo cuando falta tarifa o rendimiento.
/// 2) Fórmula del MATERIAL (<see cref="MaterialRow.MoFormulaTipo"/>): "Porcentaje" →
///    Material × MoPorcentajeMaterial × FA (índice global del proyecto, <see cref="ProjectInfoRow.IndiceMoFa"/>),
///    para materiales que no tienen M.O. propia. Precedencia: Rendimiento &gt; Porcentaje &gt; directo.
/// </summary>
public static class MaterialMoResolver
{
    public const string ModoDirecto = "Directo";
    public const string ModoRendimiento = "Rendimiento";

    /// <summary>Valor de MaterialRow.MoFormulaTipo que activa la fórmula Material × % × FA. Vacío = M.O. fija.</summary>
    public const string MoFormulaPorcentaje = "Porcentaje";

    /// <summary>Índice global de M.O. (FA = COSTOS!E192 del Excel, 62,80). Usado cuando el proyecto no trae valor.</summary>
    public const decimal IndiceMoFaDefault = 62.80m;

    /// <summary>Días laborales por mes usados para derivar la tarifa D/H desde el sueldo mensual.</summary>
    public const decimal DiasPorJornal = 22m;

    /// <summary>True si el material debe cotizar M.O. por rendimiento con los datos del proyecto.</summary>
    public static bool DebeUsarRendimiento(MaterialRow? material, ProjectInfoRow? project)
    {
        if (material == null || project == null) return false;
        if (!string.Equals(project.ModoMoInstalacion, ModoRendimiento, StringComparison.OrdinalIgnoreCase))
            return false;
        return project.TarifaDhClp > 0m && material.RendimientoDh > 0m;
    }

    /// <summary>True si el material está en fórmula "Porcentaje" (M.O. = Material × % × FA).</summary>
    public static bool DebeUsarPorcentaje(MaterialRow? material)
    {
        if (material == null) return false;
        if (!string.Equals(material.MoFormulaTipo, MoFormulaPorcentaje, StringComparison.OrdinalIgnoreCase))
            return false;
        return material.MoPorcentajeMaterial > 0m;
    }

    /// <summary>
    /// M.O. efectiva en CLP por unidad: por rendimiento si el modo lo permite; si no, por
    /// fórmula de porcentaje (Material × % × FA) si el material la tiene activa; si no,
    /// el M.O. directo del catálogo.
    /// </summary>
    public static decimal MoClpEfectiva(MaterialRow? material, ProjectInfoRow? project)
    {
        if (material == null) return 0m;
        if (DebeUsarRendimiento(material, project))
            return Math.Round(project!.TarifaDhClp / material.RendimientoDh, 2, MidpointRounding.AwayFromZero);
        if (DebeUsarPorcentaje(material))
        {
            decimal fa = project != null && project.IndiceMoFa > 0m ? project.IndiceMoFa : IndiceMoFaDefault;
            return Math.Round(material.MatClp * (material.MoPorcentajeMaterial / 100m) * fa, 2, MidpointRounding.AwayFromZero);
        }
        return material.MoClp;
    }

    /// <summary>
    /// Deriva la tarifa D/H desde el sueldo mensual del rol maestro: sueldo ÷ 22 jornadas,
    /// opcionalmente multiplicado por (1 + leyes sociales%). 0 si el sueldo no es válido.
    /// </summary>
    public static decimal DerivarTarifaDh(decimal sueldoMensualClp, decimal leyesSocialesPct, bool conLeyes)
    {
        if (sueldoMensualClp <= 0m) return 0m;
        decimal tarifa = sueldoMensualClp / DiasPorJornal;
        if (conLeyes && leyesSocialesPct > 0m)
            tarifa *= 1m + leyesSocialesPct / 100m;
        return Math.Round(tarifa, 0, MidpointRounding.AwayFromZero);
    }
}
