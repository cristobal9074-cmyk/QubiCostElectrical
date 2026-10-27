using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Cambio de M.O. guardada de un material al recalibrar el FA del proyecto.</summary>
public sealed record MaterialMoRecalibrado(string Codigo, decimal AnteriorClp, decimal NuevoClp);

/// <summary>Efecto de aplicar un índice FA: qué se recalculó en materiales y en el itemizado.</summary>
public sealed record IndiceMoFaAplicado(
    decimal IndiceAnterior,
    decimal IndiceMoFa,
    IReadOnlyList<MaterialMoRecalibrado> Materiales,
    int CubicacionSincronizada)
{
    public int MaterialesRecalibrados => Materiales.Count;
}

/// <summary>
/// Recalibra la M.O. guardada del catálogo (MaterialRow.MoClp) al FA vigente del proyecto.
///
/// En la planilla original (PEL039A2026, COSTOS!E192 = 62,80) el factor era un número tecleado a
/// mano que multiplicaba la M.O. de cada línea del DETALLE; al moverlo, todas las líneas se
/// recalculaban solas. En la app el motor ya resuelve la M.O. en vivo
/// (<see cref="MaterialMoResolver.MoClpEfectiva"/>), pero los catálogos guardan además un snapshot
/// de la cifra para listas, compras y exportaciones: este servicio mantiene ese snapshot al día
/// cuando el FA del proyecto cambia, para que la pantalla y el motor nunca muestren cifras distintas.
/// </summary>
public static class MaterialMoRecalibrationService
{
    /// <summary>Recalcula en memoria la M.O. de los materiales en fórmula "Porcentaje" o "Factor" y devuelve los cambios.</summary>
    public static IReadOnlyList<MaterialMoRecalibrado> Recalibrar(
        IEnumerable<MaterialRow>? materiales,
        ProjectInfoRow? proyecto)
    {
        var cambios = new List<MaterialMoRecalibrado>();
        foreach (var material in materiales ?? Array.Empty<MaterialRow>())
        {
            if (material == null) continue;
            bool esPct = MaterialMoResolver.DebeUsarPorcentaje(material);
            bool esFactor = MaterialMoResolver.DebeUsarFactor(material);
            if (!esPct && !esFactor)
            {
                if (material.MoFactorBase > 0m)
                {
                    material.MoFormulaTipo = MaterialMoResolver.MoFormulaFactor;
                    esFactor = true;
                }
                else
                {
                    continue;
                }
            }

            // Si es fórmula Factor y aún no guardaba su base original, preservarla antes de actualizar MoClp
            if (esFactor && material.MoFactorBase <= 0m && material.MoClp > 0m)
                material.MoFactorBase = material.MoClp;

            decimal nuevo = MaterialMoResolver.MoClpEfectiva(material, proyecto);
            if (nuevo == material.MoClp)
                continue;

            cambios.Add(new MaterialMoRecalibrado(material.Codigo ?? "", material.MoClp, nuevo));
            material.MoClp = nuevo;
            material.RecalcularUf(proyecto?.ValorUfClp ?? 0m);
        }

        return cambios;
    }

    /// <summary>Lleva al banco de precios el snapshot de M.O. de los materiales recalibrados.</summary>
    public static int SincronizarBanco(
        IReadOnlyList<MaterialMoRecalibrado>? cambios,
        IPriceBankStore? banco,
        decimal valorUfClp)
    {
        if (cambios == null || cambios.Count == 0 || banco == null)
            return 0;

        var nuevos = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var cambio in cambios)
        {
            if (!string.IsNullOrWhiteSpace(cambio.Codigo))
                nuevos[cambio.Codigo.Trim()] = cambio.NuevoClp;
        }

        int actualizados = 0;
        foreach (var entry in banco.GetAll())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Codigo))
                continue;
            if (!nuevos.TryGetValue(entry.Codigo.Trim(), out decimal nuevo) || nuevo == entry.MoClp)
                continue;

            entry.MoClp = nuevo;
            entry.RecalcularUf(valorUfClp);
            banco.Upsert(entry);
            actualizados++;
        }

        return actualizados;
    }
}
