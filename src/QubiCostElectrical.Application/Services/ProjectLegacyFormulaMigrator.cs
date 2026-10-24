using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Sincroniza materiales legados de un proyecto (.qce) cuyos códigos provienen del banco
/// de precios oficial y que fueron guardados antes de la incorporación de la fórmula Factor.
/// </summary>
public static class ProjectLegacyFormulaMigrator
{
    public static bool Sync(IEnumerable<MaterialRow>? materiales, IPriceBankStore? priceBank)
    {
        if (materiales == null || priceBank == null) return false;
        bool changed = false;
        foreach (var mat in materiales)
        {
            if (mat == null) continue;
            if (string.IsNullOrWhiteSpace(mat.MoFormulaTipo) && mat.MoFactorBase <= 0m && !string.IsNullOrWhiteSpace(mat.Codigo))
            {
                var entry = priceBank.GetByCodigo(mat.Codigo.Trim());
                if (entry != null && string.Equals(entry.MoFormulaTipo, MaterialMoResolver.MoFormulaFactor, StringComparison.OrdinalIgnoreCase))
                {
                    mat.MoFormulaTipo = MaterialMoResolver.MoFormulaFactor;
                    mat.MoFactorBase = entry.MoFactorBase > 0m ? entry.MoFactorBase : (entry.MoClp > 0m ? entry.MoClp : mat.MoClp);
                    changed = true;
                }
            }
        }
        return changed;
    }

    /// <summary>
    /// Sincroniza fórmulas legacy de materiales y recalibra automáticamente el FA si el proyecto
    /// tiene dotación de mano de obra y su FA previo había quedado descalibrado por las fórmulas faltantes.
    /// </summary>
    public static bool MigrateAndRecalibrate(BudgetProjectSession session, IPriceBankStore? priceBank)
    {
        if (session == null) return false;
        bool changed = Sync(session.Materiales, priceBank);

        if (session.Materiales != null)
        {
            foreach (var m in session.Materiales)
            {
                if (m != null && string.IsNullOrWhiteSpace(m.MoFormulaTipo) && (m.MoFactorBase > 0m || m.MoClp > 0m))
                {
                    m.MoFormulaTipo = MaterialMoResolver.MoFormulaFactor;
                    if (m.MoFactorBase <= 0m) m.MoFactorBase = m.MoClp;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            session.MarkDirty(ProjectDirtyScope.Materiales);
            var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
            if (resultado.PuedeCalcular && resultado.Fa.HasValue)
            {
                session.AplicarIndiceMoFa(resultado.Fa.Value, priceBank);
            }
        }
        return changed;
    }
}
