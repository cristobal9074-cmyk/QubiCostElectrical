using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed partial class ConjuntoElectricoService
{
    /// <summary>
    /// Expande un conjunto electrico a filas de cubicacion con calculo PER-ZONA.
    /// Las filas se crean EXACTAMENTE en el orden de la plantilla, SIN mergear.
    /// Cantidad arranca en 0 (el usuario llena por piso en la matriz de sectores).
    /// Cada fila lleva TplExpansionId y TplRecursoJson para recalcular esclavos
    /// al editar el maestro en un piso.
    /// </summary>
    public ConjuntoExpansionResult ExpandToCubicacionPerZona(
        ConjuntoElectricoRow conjunto,
        ItemizadoRow partida,
        IEnumerable<MaterialRow>? materiales)
    {
        var result = new ConjuntoExpansionResult();
        if (conjunto == null || partida == null) return result;

        var mats = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();
        var recursos = conjunto.Recursos ?? new List<ConjuntoRecursoRow>();
        string expansionId = Guid.NewGuid().ToString("N");

        foreach (var recurso in recursos)
        {
            if (recurso.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH or ApuTipoRecurso.HERR)
                continue;

            var material = FindMaterial(mats, recurso.Codigo);
            if (material == null && string.IsNullOrWhiteSpace(recurso.Descripcion))
            {
                string reference = string.IsNullOrWhiteSpace(recurso.Codigo)
                    ? "sin codigo ni descripcion"
                    : $"'{recurso.Codigo}'";
                result.Warnings.Add($"No se encontro el material {reference} en la plantilla '{conjunto.Nombre}'.");
                continue;
            }

            var snapshot = TplRecursoSnapshot.FromRecurso(recurso, conjunto.Parametros);

            result.Rows.Add(new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = partida.Id ?? "",
                Tipo = MapCubicacionTipo(recurso.Tipo),
                Codigo = material?.Codigo ?? recurso.Codigo,
                Descripcion = material?.Descripcion ?? recurso.Descripcion,
                Unidad = material?.Unidad ?? recurso.Unidad,
                Cantidad = 0m,
                MatClp = material?.MatClp ?? 0m,
                MoClp = material?.MoClp ?? 0m,
                MatUf = material?.MatUf ?? recurso.PrecioUf,
                MoUf = material?.MoUf ?? 0m,
                Area = conjunto.TipoConjunto,
                TplExpansionId = expansionId,
                TplRecursoJson = snapshot.ToJson()
            });
        }

        return result;
    }
}
