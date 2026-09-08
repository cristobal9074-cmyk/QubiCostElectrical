using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record MaterialOccurrencePreview(
    string ItemId,
    string PartidaItem,
    string PartidaDescripcion,
    string Modulo, // "Cubicación" o "APU"
    string SourceCodigo,
    string SourceDescripcion,
    string SourceUnidad,
    decimal Cantidad,
    decimal PrecioUnitarioActualClp,
    decimal SubtotalActualClp,
    string TargetCodigo,
    string TargetDescripcion,
    string TargetUnidad,
    decimal NuevoPrecioUnitarioClp,
    decimal NuevoSubtotalClp,
    decimal DeltaClp);

public sealed record MaterialBatchReplaceResult(
    int PartidasActualizadas,
    int FilasCubicacionModificadas,
    int FilasApuModificadas,
    decimal CostoTotalAnteriorClp,
    decimal CostoTotalNuevoClp,
    decimal DeltaTotalClp,
    IReadOnlyList<MaterialOccurrencePreview> Ocurrencias);

public interface IMaterialBatchReplaceService
{
    IReadOnlyList<MaterialRow> GetUsedMaterials(BudgetProjectSession session);
    IReadOnlyList<MaterialOccurrencePreview> PreviewReplacements(
        BudgetProjectSession session,
        string sourceCodeOrDescription,
        MaterialRow targetMaterial);
    MaterialBatchReplaceResult ExecuteReplacement(
        BudgetProjectSession session,
        Action<BudgetProjectSession, string>? onPushUndo,
        string sourceCodeOrDescription,
        MaterialRow targetMaterial,
        bool replaceInCubicacion = true,
        bool replaceInApu = true);
}

public sealed class MaterialBatchReplaceService : IMaterialBatchReplaceService
{
    public IReadOnlyList<MaterialRow> GetUsedMaterials(BudgetProjectSession session)
    {
        if (session == null) return Array.Empty<MaterialRow>();

        var usedDict = new Dictionary<string, MaterialRow>(StringComparer.OrdinalIgnoreCase);

        // De cubicación
        foreach (var r in session.Cubicacion)
        {
            if (string.IsNullOrWhiteSpace(r.Codigo) && string.IsNullOrWhiteSpace(r.Descripcion))
                continue;

            string key = !string.IsNullOrWhiteSpace(r.Codigo) ? r.Codigo.Trim() : r.Descripcion.Trim();
            if (!usedDict.ContainsKey(key))
            {
                usedDict[key] = new MaterialRow
                {
                    Codigo = r.Codigo,
                    Descripcion = r.Descripcion,
                    Unidad = r.Unidad,
                    Categoria = r.Categoria,
                    MatClp = r.MatClp,
                    MoClp = r.MoClp,
                    MatUf = r.MatUf,
                    MoUf = r.MoUf
                };
            }
        }

        // De APUs
        foreach (var apu in session.Apus)
        {
            foreach (var r in apu.Recursos)
            {
                if (string.IsNullOrWhiteSpace(r.CodigoMaterial) && string.IsNullOrWhiteSpace(r.Descripcion))
                    continue;

                string key = !string.IsNullOrWhiteSpace(r.CodigoMaterial) ? r.CodigoMaterial.Trim() : r.Descripcion.Trim();
                if (!usedDict.ContainsKey(key))
                {
                    usedDict[key] = new MaterialRow
                    {
                        Codigo = r.CodigoMaterial,
                        Descripcion = r.Descripcion,
                        Unidad = r.Unidad,
                        MatUf = r.PrecioUf,
                        Proveedor = r.Proveedor
                    };
                }
            }
        }

        return usedDict.Values
            .OrderBy(m => m.Codigo)
            .ThenBy(m => m.Descripcion)
            .ToList();
    }

    public IReadOnlyList<MaterialOccurrencePreview> PreviewReplacements(
        BudgetProjectSession session,
        string sourceCodeOrDescription,
        MaterialRow targetMaterial)
    {
        if (session == null || string.IsNullOrWhiteSpace(sourceCodeOrDescription) || targetMaterial == null)
            return Array.Empty<MaterialOccurrencePreview>();

        string cleanSource = sourceCodeOrDescription.Trim();
        var partidasDict = session.Itemizado.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var previews = new List<MaterialOccurrencePreview>();

        // 1. Analizar Cubicación
        foreach (var row in session.Cubicacion)
        {
            if (!Matches(row.Codigo, row.Descripcion, cleanSource))
                continue;

            partidasDict.TryGetValue(row.ItemId, out var partida);
            string partidaItem = partida?.Item ?? "";
            string partidaDesc = partida?.Descripcion ?? "Sin partida";

            decimal cant = row.Cantidad;
            decimal precioActual = row.PrecioUnitClp;
            decimal subtotalActual = cant * precioActual;

            decimal nuevoPrecio = targetMaterial.MatClp + targetMaterial.MoClp;
            decimal nuevoSubtotal = cant * nuevoPrecio;
            decimal delta = nuevoSubtotal - subtotalActual;

            previews.Add(new MaterialOccurrencePreview(
                ItemId: row.ItemId,
                PartidaItem: partidaItem,
                PartidaDescripcion: partidaDesc,
                Modulo: "Cubicación",
                SourceCodigo: row.Codigo,
                SourceDescripcion: row.Descripcion,
                SourceUnidad: row.Unidad,
                Cantidad: cant,
                PrecioUnitarioActualClp: precioActual,
                SubtotalActualClp: subtotalActual,
                TargetCodigo: targetMaterial.Codigo,
                TargetDescripcion: targetMaterial.Descripcion,
                TargetUnidad: !string.IsNullOrWhiteSpace(targetMaterial.Unidad) ? targetMaterial.Unidad : row.Unidad,
                NuevoPrecioUnitarioClp: nuevoPrecio,
                NuevoSubtotalClp: nuevoSubtotal,
                DeltaClp: delta
            ));
        }

        // 2. Analizar APUs
        foreach (var apu in session.Apus)
        {
            partidasDict.TryGetValue(apu.ItemId, out var partida);
            string partidaItem = partida?.Item ?? "";
            string partidaDesc = partida?.Descripcion ?? apu.Descripcion ?? "Sin partida";

            foreach (var recurso in apu.Recursos)
            {
                if (!Matches(recurso.CodigoMaterial, recurso.Descripcion, cleanSource))
                    continue;

                decimal rend = recurso.Rendimiento;
                decimal precioActualUf = recurso.PrecioUf;
                decimal subtotalActualUf = rend * precioActualUf;

                decimal nuevoPrecioUf = targetMaterial.MatUf;
                decimal nuevoSubtotalUf = rend * nuevoPrecioUf;
                decimal deltaUf = nuevoSubtotalUf - subtotalActualUf;

                decimal ufValor = session.ProjectInfo?.ValorUfClp ?? 38000m;
                if (ufValor <= 0m) ufValor = 38000m;

                previews.Add(new MaterialOccurrencePreview(
                    ItemId: apu.ItemId,
                    PartidaItem: partidaItem,
                    PartidaDescripcion: partidaDesc,
                    Modulo: "APU",
                    SourceCodigo: recurso.CodigoMaterial,
                    SourceDescripcion: recurso.Descripcion,
                    SourceUnidad: recurso.Unidad,
                    Cantidad: rend,
                    PrecioUnitarioActualClp: precioActualUf * ufValor,
                    SubtotalActualClp: subtotalActualUf * ufValor,
                    TargetCodigo: targetMaterial.Codigo,
                    TargetDescripcion: targetMaterial.Descripcion,
                    TargetUnidad: !string.IsNullOrWhiteSpace(targetMaterial.Unidad) ? targetMaterial.Unidad : recurso.Unidad,
                    NuevoPrecioUnitarioClp: nuevoPrecioUf * ufValor,
                    NuevoSubtotalClp: nuevoSubtotalUf * ufValor,
                    DeltaClp: deltaUf * ufValor
                ));
            }
        }

        return previews;
    }

    public MaterialBatchReplaceResult ExecuteReplacement(
        BudgetProjectSession session,
        Action<BudgetProjectSession, string>? onPushUndo,
        string sourceCodeOrDescription,
        MaterialRow targetMaterial,
        bool replaceInCubicacion = true,
        bool replaceInApu = true)
    {
        if (session == null || string.IsNullOrWhiteSpace(sourceCodeOrDescription) || targetMaterial == null)
            return new MaterialBatchReplaceResult(0, 0, 0, 0m, 0m, 0m, Array.Empty<MaterialOccurrencePreview>());

        var previews = PreviewReplacements(session, sourceCodeOrDescription, targetMaterial);
        if (previews.Count == 0)
            return new MaterialBatchReplaceResult(0, 0, 0, 0m, 0m, 0m, Array.Empty<MaterialOccurrencePreview>());

        string cleanSource = sourceCodeOrDescription.Trim();
        onPushUndo?.Invoke(session, $"Reemplazar material {cleanSource} por {targetMaterial.Codigo}");

        int rowsCubicacionMod = 0;
        int rowsApuMod = 0;
        var affectedPartidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        decimal costoAnterior = previews.Sum(p => p.SubtotalActualClp);
        decimal costoNuevo = previews.Sum(p => p.NuevoSubtotalClp);

        // 1. Reemplazar en Cubicación
        if (replaceInCubicacion)
        {
            foreach (var row in session.Cubicacion)
            {
                if (!Matches(row.Codigo, row.Descripcion, cleanSource))
                    continue;

                row.Codigo = targetMaterial.Codigo;
                row.Descripcion = targetMaterial.Descripcion;
                if (!string.IsNullOrWhiteSpace(targetMaterial.Unidad))
                    row.Unidad = targetMaterial.Unidad;
                if (!string.IsNullOrWhiteSpace(targetMaterial.Categoria))
                    row.Categoria = targetMaterial.Categoria;

                row.MatClp = targetMaterial.MatClp;
                row.MoClp = targetMaterial.MoClp;
                row.MatUf = targetMaterial.MatUf;
                row.MoUf = targetMaterial.MoUf;

                rowsCubicacionMod++;
                affectedPartidas.Add(row.ItemId);
            }
        }

        // 2. Reemplazar en APUs
        if (replaceInApu)
        {
            foreach (var apu in session.Apus)
            {
                foreach (var recurso in apu.Recursos)
                {
                    if (!Matches(recurso.CodigoMaterial, recurso.Descripcion, cleanSource))
                        continue;

                    recurso.CodigoMaterial = targetMaterial.Codigo;
                    recurso.Descripcion = targetMaterial.Descripcion;
                    if (!string.IsNullOrWhiteSpace(targetMaterial.Unidad))
                        recurso.Unidad = targetMaterial.Unidad;

                    recurso.PrecioUf = targetMaterial.MatUf;
                    recurso.Proveedor = targetMaterial.Proveedor;

                    rowsApuMod++;
                    affectedPartidas.Add(apu.ItemId);
                }
            }
        }

        // 3. Sincronizar en el catálogo de materiales del proyecto si existe
        var existingCat = session.Materiales.FirstOrDefault(m =>
            Matches(m.Codigo, m.Descripcion, cleanSource));
        if (existingCat != null)
        {
            existingCat.Codigo = targetMaterial.Codigo;
            existingCat.Descripcion = targetMaterial.Descripcion;
            existingCat.Unidad = targetMaterial.Unidad;
            existingCat.MatClp = targetMaterial.MatClp;
            existingCat.MoClp = targetMaterial.MoClp;
            existingCat.MatUf = targetMaterial.MatUf;
            existingCat.MoUf = targetMaterial.MoUf;
            existingCat.Proveedor = targetMaterial.Proveedor;
            existingCat.Marca = targetMaterial.Marca;
        }

        session.MarkDirty("material_batch_replace");

        return new MaterialBatchReplaceResult(
            PartidasActualizadas: affectedPartidas.Count,
            FilasCubicacionModificadas: rowsCubicacionMod,
            FilasApuModificadas: rowsApuMod,
            CostoTotalAnteriorClp: costoAnterior,
            CostoTotalNuevoClp: costoNuevo,
            DeltaTotalClp: costoNuevo - costoAnterior,
            Ocurrencias: previews
        );
    }

    private static bool Matches(string? codigo, string? descripcion, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        if (!string.IsNullOrWhiteSpace(codigo) && string.Equals(codigo.Trim(), query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(descripcion) && string.Equals(descripcion.Trim(), query, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
