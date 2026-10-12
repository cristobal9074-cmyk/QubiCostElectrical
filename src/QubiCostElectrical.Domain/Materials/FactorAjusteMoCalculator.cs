using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Una línea del itemizado para calibrar el FA. <see cref="MoFijaClp"/> es la M.O. unitaria que
/// NO depende del índice (catálogo directo o rendimiento); <see cref="BasePorcentajeClp"/> es la
/// base unitaria que el FA multiplica (Material × % / 100) para los materiales en fórmula
/// "Porcentaje". <see cref="Cantidad"/> escala ambas a pesos del presupuesto.
/// </summary>
public readonly record struct FactorAjusteMoLinea(decimal MoFijaClp, decimal BasePorcentajeClp, decimal Cantidad);

/// <summary>
/// Resultado de calibrar el FA. En el Excel original (COSTOS!E192) el índice era un valor tecleado
/// a mano hasta que la M.O. del itemizado cuadraba con el costo real del plan de mano de obra;
/// aquí se deriva con la misma idea: Remanente ÷ Base.
/// </summary>
public sealed record FactorAjusteMoResultado(
    decimal MoRealClp,
    decimal MoCubiertaClp,
    decimal BasePorcentajeClp,
    int LineasPorcentaje,
    decimal? Fa)
{
    /// <summary>M.O. que el índice debe absorber: real del proyecto menos la ya cubierta por el catálogo.</summary>
    public decimal RemanenteClp => MoRealClp - MoCubiertaClp;

    /// <summary>Hay costo de M.O. cargado (plan de mano de obra o módulo de dotación).</summary>
    public bool TienePlanMo => MoRealClp > 0m;

    /// <summary>Hay materiales con fórmula de porcentaje: sin ellos el FA no se puede derivar.</summary>
    public bool HayBase => BasePorcentajeClp > 0m;

    public bool PuedeCalcular => Fa.HasValue;

    /// <summary>
    /// M.O. que queda en el itemizado con un índice dado: la ya cubierta por el catálogo/rendimiento
    /// más la base de porcentaje escalada por el FA. Es la columna "EN PPTO" del bloque FACTORES del
    /// Excel (COSTOS!F192), que debe cuadrar con el costo real (COSTOS!G192).
    /// </summary>
    public decimal MoItemizadoClp(decimal indiceFa) =>
        MoCubiertaClp + BasePorcentajeClp * Math.Max(0m, indiceFa);

    /// <summary>Desalineación relativa entre la M.O. del itemizado y el costo real, en % (0 = cuadra).</summary>
    public static decimal DesviacionPct(decimal moItemizadoClp, decimal moRealClp) =>
        moRealClp == 0m ? 0m : Math.Round((moItemizadoClp - moRealClp) / moRealClp * 100m, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Deriva el índice global de M.O. "FA" de cada proyecto. El FA no es una constante: en la
/// planilla original nacía de COSTOS!E192 = 62,80 (valor escrito a mano) y su único propósito era
/// que la M.O. calculada en el itemizado cuadrara con el costo real del plan de mano de obra
/// (MANO DE OBRA!T23 = D/H × tarifa). Por eso cambia en cada proyecto.
/// </summary>
public static class FactorAjusteMoCalculator
{
    /// <summary>Piso del índice: evita divisiones degeneradas cuando el remanente es mínimo.</summary>
    public const decimal FaMinimo = 0.01m;

    /// <summary>Techo del índice: corta calibraciones absurdas por base casi nula.</summary>
    public const decimal FaMaximo = 1000m;

    /// <summary>
    /// FA = (M.O. real − M.O. ya cubierta por el catálogo) ÷ Σ (Material × % × cantidad).
    /// Devuelve <see cref="FactorAjusteMoResultado.Fa"/> en null cuando falta plan de M.O. o base.
    /// </summary>
    public static FactorAjusteMoResultado Calcular(decimal moRealClp, IEnumerable<FactorAjusteMoLinea>? lineas)
    {
        decimal moReal = Math.Max(0m, moRealClp);
        decimal cubierta = 0m;
        decimal basePorcentaje = 0m;
        int lineasPorcentaje = 0;

        foreach (var linea in lineas ?? Enumerable.Empty<FactorAjusteMoLinea>())
        {
            decimal cantidad = linea.Cantidad;
            if (cantidad <= 0m)
                continue;

            cubierta += Math.Max(0m, linea.MoFijaClp) * cantidad;

            if (linea.BasePorcentajeClp <= 0m)
                continue;

            basePorcentaje += linea.BasePorcentajeClp * cantidad;
            lineasPorcentaje++;
        }

        decimal remanente = moReal - cubierta;
        decimal? fa = basePorcentaje > 0m && remanente > 0m
            ? Math.Round(Math.Clamp(remanente / basePorcentaje, FaMinimo, FaMaximo), 2, MidpointRounding.AwayFromZero)
            : null;

        return new FactorAjusteMoResultado(moReal, cubierta, basePorcentaje, lineasPorcentaje, fa);
    }
}
