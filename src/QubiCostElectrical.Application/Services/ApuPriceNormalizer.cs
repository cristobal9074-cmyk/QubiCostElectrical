using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Reparación idempotente de precios de recursos APU guardados en UF en vez de CLP.
///
/// Contexto: <see cref="ApuRow"/> hereda el sufijo "Uf" en sus montos, pero tras la migración de
/// moneda del motor esos valores son la moneda base (CLP). Antes, el selector de materiales y el
/// autocompletado del APU sembraban <c>PrecioUf</c> con <c>MaterialRow.MatUf</c> (UF real), de modo
/// que el costo del APU quedaba ~1/valorUF del correcto.
///
/// La normalización solo convierte cuando puede probar que el valor es UF:
/// <list type="bullet">
/// <item>el material existe en el catálogo del proyecto y el precio coincide con <c>MatUf</c> (y no
/// con <c>MatClp</c>) ⇒ se reemplaza por el CLP autoritativo del catálogo; o</item>
/// <item>el material ya no está en el catálogo y el precio es imposible como unitario en pesos
/// (&lt; 1) ⇒ se multiplica por la UF vigente.</item>
/// </list>
/// En cualquier otro caso el precio se deja intacto. Tras convertir, el valor queda en CLP, por lo
/// que una segunda ejecución no vuelve a convertirlo (idempotente, sin marca persistida).
/// </summary>
public static class ApuPriceNormalizer
{
    public static int Normalize(ProjectFileData? data)
    {
        if (data?.Apus == null || data.Apus.Count == 0)
            return 0;

        decimal valorUfClp = data.Proyecto?.ValorUfClp > 0m
            ? data.Proyecto.ValorUfClp
            : PriceConversion.DefaultValorUfClp;

        var materialesPorCodigo = (data.Materiales ?? new List<MaterialRow>())
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo))
            .GroupBy(m => CodeHelper.Normalize(m.Codigo))
            .ToDictionary(g => g.Key, g => g.First());

        int reparados = 0;

        foreach (var apu in data.Apus)
        {
            if (apu?.Recursos == null)
                continue;

            foreach (var recurso in apu.Recursos)
            {
                MaterialRow? material = null;
                if (!string.IsNullOrWhiteSpace(recurso.CodigoMaterial))
                    materialesPorCodigo.TryGetValue(CodeHelper.Normalize(recurso.CodigoMaterial), out material);

                if (TryFix(recurso.PrecioUf, material, valorUfClp, out decimal precioCorregido))
                {
                    recurso.PrecioUf = precioCorregido;
                    reparados++;
                }

                if (TryFix(recurso.ValorHHUf, material, valorUfClp, out decimal hhCorregido))
                    recurso.ValorHHUf = hhCorregido;
            }
        }

        return reparados;
    }

    private static bool TryFix(decimal precio, MaterialRow? material, decimal valorUfClp, out decimal corregido)
    {
        corregido = precio;
        if (precio <= 0m)
            return false;

        if (material != null)
        {
            // Coincide con el precio en pesos del catálogo ⇒ ya está correcto.
            if (material.MatClp > 0m && NearlyEqual(precio, material.MatClp))
                return false;

            // Coincide con la equivalencia UF del catálogo ⇒ es un precio UF heredado.
            if (material.MatUf > 0m && NearlyEqual(precio, material.MatUf))
            {
                corregido = material.MatClp > 0m
                    ? material.MatClp
                    : (material.MatClpConFallback > 0m
                        ? material.MatClpConFallback
                        : Math.Round(precio * valorUfClp, MidpointRounding.AwayFromZero));
                return !NearlyEqual(corregido, precio);
            }

            return false;
        }

        // Sin material en el catálogo: solo un valor imposible como unitario en pesos es un UF.
        if (precio < 1m)
        {
            corregido = Math.Round(precio * valorUfClp, MidpointRounding.AwayFromZero);
            return corregido != precio;
        }

        return false;
    }

    private static bool NearlyEqual(decimal a, decimal b)
    {
        decimal diff = Math.Abs(a - b);
        if (diff <= 0.000001m)
            return true;

        decimal scale = Math.Max(Math.Abs(a), Math.Abs(b));
        return diff <= scale * 0.0005m;
    }
}
