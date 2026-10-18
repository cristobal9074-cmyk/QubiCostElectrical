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

    /// <summary>Valor de MaterialRow.MoFormulaTipo que activa la fórmula BaseMo × FA. Paridad con columna F de LISTA-PR.</summary>
    public const string MoFormulaFactor = "Factor";

    /// <summary>
    /// FA sin calibrar. El 62,80 del Excel no era un valor de la app: era el número que el estimador
    /// escribía para SU proyecto hasta que la M.O. del itemizado cuadraba con el plan de obra. Por eso
    /// la app arranca en 0 y el índice se deriva (modo Automático o Calcular FA) desde el costo real.
    /// </summary>
    public const decimal IndiceMoFaDefault = 0m;

    /// <summary>
    /// Valor de referencia de la planilla original (COSTOS!E192 = 62,80).
    /// </summary>
    public const decimal IndiceMoFaHeredado = 62.80m;

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

    /// <summary>True si el material está en fórmula "Factor" (M.O. = BaseMo × FA, paridad LISTA-PR).</summary>
    public static bool DebeUsarFactor(MaterialRow? material)
    {
        if (material == null) return false;
        return string.Equals(material.MoFormulaTipo, MoFormulaFactor, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Base de M.O. para la fórmula Factor: MoFactorBase o respaldo de MoClp.</summary>
    public static decimal BaseMoFactor(MaterialRow? material)
    {
        if (material == null) return 0m;
        return material.MoFactorBase > 0m ? material.MoFactorBase : material.MoClp;
    }

    /// <summary>
    /// FA vigente del proyecto. 0 = sin calibrar: la M.O. por % se calcula con factor neutro
    /// (Material × %) hasta que el índice se derive del plan de obra (ver
    /// <see cref="MoClpPorPorcentaje"/>).
    /// </summary>
    public static decimal IndiceFa(ProjectInfoRow? project) =>
        project != null && project.IndiceMoFa > 0m ? project.IndiceMoFa : IndiceMoFaDefault;

    /// <summary>
    /// Normaliza el índice leído de un .qce: valores negativos se aplanan a 0.
    /// Preserva valores calibrados como 62,80.
    /// </summary>
    public static decimal NormalizarFaGuardado(decimal indiceGuardado) => Math.Max(0m, indiceGuardado);

    /// <summary>
    /// M.O. por fórmula de porcentaje: Material × % × FA. Expuesto aparte para que las pantallas
    /// que crean o editan materiales (p. ej. el alta rápida de la lista de precios) muestren en
    /// vivo exactamente la misma cifra que luego aplica el motor.
    ///
    /// FA sin calibrar cuenta como 1 (factor neutro): la fórmula queda en Material × %, una M.O.
    /// provisional visible en vez de $0, que se leía como un dato que faltó ingresar. Al mover el FA
    /// desde Costos, <see cref="MaterialMoRecalibrationService"/> recalcula los materiales con el
    /// índice real y la cifra definitiva reemplaza a la provisional.
    /// </summary>
    public static decimal MoClpPorPorcentaje(decimal matClp, decimal porcentajeMaterial, decimal indiceMoFa)
    {
        if (matClp <= 0m || porcentajeMaterial <= 0m)
            return 0m;
        decimal fa = indiceMoFa > 0m ? indiceMoFa : 1m;
        return Math.Round(matClp * (porcentajeMaterial / 100m) * fa, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// M.O. por fórmula de factor base: BaseMo × FA.
    /// Paridad con `=ROUND(VLOOKUP(...) * FA, 0)` de la hoja DETALLE.
    /// </summary>
    public static decimal MoClpPorFactor(decimal baseMo, decimal indiceMoFa)
    {
        if (baseMo <= 0m)
            return 0m;
        decimal fa = indiceMoFa > 0m ? indiceMoFa : 1m;
        return Math.Round(baseMo * fa, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// M.O. efectiva en CLP por unidad: por rendimiento si el modo lo permite; si no, por
    /// fórmula de porcentaje (Material × % × FA) si el material la tiene activa; si no, por
    /// fórmula de factor (BaseMo × FA); si no, el M.O. directo del catálogo.
    /// </summary>
    public static decimal MoClpEfectiva(MaterialRow? material, ProjectInfoRow? project)
    {
        if (material == null) return 0m;
        if (DebeUsarRendimiento(material, project))
            return Math.Round(project!.TarifaDhClp / material.RendimientoDh, 2, MidpointRounding.AwayFromZero);
        if (DebeUsarPorcentaje(material))
            return MoClpPorPorcentaje(material.MatClp, material.MoPorcentajeMaterial, IndiceFa(project));
        if (DebeUsarFactor(material))
            return MoClpPorFactor(BaseMoFactor(material), IndiceFa(project));
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
