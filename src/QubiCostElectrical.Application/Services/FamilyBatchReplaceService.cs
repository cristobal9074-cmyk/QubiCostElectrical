using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum FamilyMappingStatus
{
    NoChange,
    InCubicacion,
    InBank,
    Manual,
    NotFound
}

public sealed class FamilyItemMapping
{
    public bool IsSelected { get; set; } = true;
    public string SourceCodigo { get; set; } = "";
    public string SourceDescripcion { get; set; } = "";
    public string SourceUnidad { get; set; } = "";
    public string SourceCategoria { get; set; } = "";
    public decimal CantidadTotal { get; set; }
    public decimal PrecioActualClp { get; set; }
    public decimal SubtotalActualClp => CantidadTotal * PrecioActualClp;

    public string TargetCodigo { get; set; } = "";
    public string TargetDescripcion { get; set; } = "";
    public string TargetUnidad { get; set; } = "";
    public string TargetCategoria { get; set; } = "";
    public decimal NuevoPrecioClp { get; set; }
    public decimal NuevoSubtotalClp => CantidadTotal * NuevoPrecioClp;
    public decimal DeltaClp => NuevoSubtotalClp - SubtotalActualClp;
    public decimal DeltaUnitarioClp => NuevoPrecioClp - PrecioActualClp;

    public string TargetProveedor { get; set; } = "";
    public decimal TargetMatUf { get; set; }
    public decimal TargetMoClp { get; set; }
    public decimal TargetMoUf { get; set; }

    public FamilyMappingStatus Status { get; set; } = FamilyMappingStatus.NoChange;
    public string StatusDisplay => Status switch
    {
        FamilyMappingStatus.InCubicacion => "En Cubicación",
        FamilyMappingStatus.InBank => "En Banco",
        FamilyMappingStatus.Manual => "Personalizado",
        FamilyMappingStatus.NotFound => "No Encontrado",
        _ => "Sin cambio"
    };

    public int OccurrencesCount { get; set; }
}

public sealed record FamilyBatchReplacePreview(
    IReadOnlyList<FamilyItemMapping> Mappings,
    int PartidasAfectadasCount,
    int TotalOcurrenciasCount,
    decimal CostoTotalAnteriorClp,
    decimal CostoTotalNuevoClp,
    decimal DeltaTotalClp,
    decimal VariacionPorcentual);

public sealed record FamilyBatchReplaceResult(
    int PartidasActualizadas,
    int FilasCubicacionModificadas,
    int FilasApuModificadas,
    decimal CostoTotalAnteriorClp,
    decimal CostoTotalNuevoClp,
    decimal DeltaTotalClp,
    int ItemsReemplazados);

public interface IFamilyBatchReplaceService
{
    IReadOnlyList<FamilyItemMapping> ScanMatchingMaterials(
        BudgetProjectSession session,
        string? patternOrPrefix = null,
        string? category = null,
        bool scanCubicacionOnly = false);

    void AutoMapWithPriceBank(
        IEnumerable<FamilyItemMapping> mappings,
        string sourcePrefix,
        string targetPrefix,
        IPriceBankStore priceBankStore,
        decimal valorUfClp = PriceConversion.DefaultValorUfClp,
        IReadOnlyDictionary<string, string>? explicitOverrides = null,
        BudgetProjectSession? session = null);

    FamilyBatchReplacePreview CalculatePreview(
        BudgetProjectSession session,
        IEnumerable<FamilyItemMapping> mappings,
        bool replaceCubicacion = true,
        bool replaceApu = true);

    FamilyBatchReplaceResult ExecuteReplacement(
        BudgetProjectSession session,
        Action<BudgetProjectSession, string>? onPushUndo,
        IEnumerable<FamilyItemMapping> mappings,
        bool replaceCubicacion = true,
        bool replaceApu = true);
}

public sealed class FamilyBatchReplaceService : IFamilyBatchReplaceService
{
    public IReadOnlyList<FamilyItemMapping> ScanMatchingMaterials(
        BudgetProjectSession session,
        string? patternOrPrefix = null,
        string? category = null,
        bool scanCubicacionOnly = false)
    {
        if (session == null) return Array.Empty<FamilyItemMapping>();

        string cleanPattern = patternOrPrefix?.Trim() ?? string.Empty;
        string cleanCategory = category?.Trim() ?? string.Empty;

        var itemsMap = new Dictionary<string, FamilyItemMapping>(StringComparer.OrdinalIgnoreCase);

        // 1. Escanear Cubicación
        foreach (var r in session.Cubicacion)
        {
            if (string.IsNullOrWhiteSpace(r.Codigo) && string.IsNullOrWhiteSpace(r.Descripcion))
                continue;

            if (!string.IsNullOrWhiteSpace(cleanCategory) &&
                !string.Equals(r.Categoria?.Trim(), cleanCategory, StringComparison.OrdinalIgnoreCase))
                continue;

            string key = !string.IsNullOrWhiteSpace(r.Codigo) ? r.Codigo.Trim() : (r.Descripcion?.Trim() ?? string.Empty);

            if (!MatchesFilter(r.Codigo, r.Descripcion, cleanPattern))
                continue;

            if (!itemsMap.TryGetValue(key, out var mapping))
            {
                mapping = new FamilyItemMapping
                {
                    SourceCodigo = r.Codigo ?? "",
                    SourceDescripcion = r.Descripcion ?? "",
                    SourceUnidad = r.Unidad ?? "",
                    SourceCategoria = r.Categoria ?? "",
                    PrecioActualClp = r.PrecioUnitClp,
                    CantidadTotal = 0m,
                    OccurrencesCount = 0
                };
                itemsMap[key] = mapping;
            }

            mapping.CantidadTotal += r.Cantidad;
            mapping.OccurrencesCount++;
        }

        // 2. Escanear APUs (solo si no es scanCubicacionOnly)
        if (!scanCubicacionOnly)
        {
            decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
            if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;

            var itemizadoDict = session.Itemizado.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var apu in session.Apus)
            {
                itemizadoDict.TryGetValue(apu.ItemId, out var partida);
                decimal partidaCantidad = partida?.Cantidad ?? 1m;
                if (partidaCantidad <= 0m) partidaCantidad = 1m;

                foreach (var rec in apu.Recursos)
                {
                    if (string.IsNullOrWhiteSpace(rec.CodigoMaterial) && string.IsNullOrWhiteSpace(rec.Descripcion))
                        continue;

                    string key = !string.IsNullOrWhiteSpace(rec.CodigoMaterial) ? rec.CodigoMaterial.Trim() : (rec.Descripcion?.Trim() ?? string.Empty);

                    if (!MatchesFilter(rec.CodigoMaterial, rec.Descripcion, cleanPattern))
                        continue;

                    decimal recPrecioClp = rec.PrecioUf > 0m ? rec.PrecioUf * valorUf : 0m;
                    decimal recCantTotal = rec.Rendimiento * partidaCantidad;

                    if (!itemsMap.TryGetValue(key, out var mapping))
                    {
                        mapping = new FamilyItemMapping
                        {
                            SourceCodigo = rec.CodigoMaterial ?? "",
                            SourceDescripcion = rec.Descripcion ?? "",
                            SourceUnidad = rec.Unidad ?? "",
                            SourceCategoria = "",
                            PrecioActualClp = recPrecioClp,
                            CantidadTotal = 0m,
                            OccurrencesCount = 0
                        };
                        itemsMap[key] = mapping;
                    }

                    mapping.CantidadTotal += recCantTotal;
                    mapping.OccurrencesCount++;
                }
            }
        }

        return itemsMap.Values
            .OrderBy(m => m.SourceCodigo)
            .ThenBy(m => m.SourceDescripcion)
            .ToList();
    }

    public void AutoMapWithPriceBank(
        IEnumerable<FamilyItemMapping> mappings,
        string sourcePrefix,
        string targetPrefix,
        IPriceBankStore priceBankStore,
        decimal valorUfClp = PriceConversion.DefaultValorUfClp,
        IReadOnlyDictionary<string, string>? explicitOverrides = null,
        BudgetProjectSession? session = null)
    {
        if (mappings == null) return;
        if (valorUfClp <= 0m) valorUfClp = PriceConversion.DefaultValorUfClp;

        string srcPrefix = sourcePrefix?.Trim() ?? string.Empty;
        string tgtPrefix = targetPrefix?.Trim() ?? string.Empty;

        // Diccionario de materiales de la Cubicación del proyecto si hay sesión
        Dictionary<string, CubicacionRow>? cubicacionByCode = null;
        if (session != null)
        {
            cubicacionByCode = session.Cubicacion
                .Where(c => !string.IsNullOrWhiteSpace(c.Codigo))
                .GroupBy(c => c.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        foreach (var mapping in mappings)
        {
            string targetCode = "";

            // 1. Verificar si hay un override explícito
            if (explicitOverrides != null && explicitOverrides.TryGetValue(mapping.SourceCodigo, out var explicitCode))
            {
                targetCode = explicitCode;
            }
            // 2. Reemplazo por prefijo
            else if (!string.IsNullOrEmpty(srcPrefix) && mapping.SourceCodigo.StartsWith(srcPrefix, StringComparison.OrdinalIgnoreCase))
            {
                targetCode = tgtPrefix + mapping.SourceCodigo.Substring(srcPrefix.Length);
            }
            else if (!string.IsNullOrWhiteSpace(mapping.TargetCodigo))
            {
                targetCode = mapping.TargetCodigo;
            }

            if (string.IsNullOrWhiteSpace(targetCode))
                continue;

            mapping.TargetCodigo = targetCode;

            // 3. Buscar primero en la Cubicación del proyecto activo
            if (cubicacionByCode != null && cubicacionByCode.TryGetValue(targetCode, out var cubRow))
            {
                mapping.TargetDescripcion = cubRow.Descripcion ?? "";
                mapping.TargetUnidad = !string.IsNullOrWhiteSpace(cubRow.Unidad) ? cubRow.Unidad : mapping.SourceUnidad;
                mapping.TargetCategoria = cubRow.Categoria ?? "";
                mapping.NuevoPrecioClp = cubRow.PrecioUnitClp;
                mapping.TargetMatUf = cubRow.MatUf;
                mapping.TargetMoClp = cubRow.MoClp;
                mapping.TargetMoUf = cubRow.MoUf;
                mapping.TargetProveedor = "";
                mapping.Status = FamilyMappingStatus.InCubicacion;
                continue;
            }

            // 4. Si no está en cubicación, buscar en el Banco de Precios
            PriceBankEntry? bankEntry = null;
            try
            {
                bankEntry = priceBankStore.GetByCodigo(targetCode);
                if (bankEntry == null)
                {
                    // Intento de búsqueda insensible a mayúsculas/espacios
                    bankEntry = priceBankStore.Search(targetCode)
                        .FirstOrDefault(e => string.Equals(e.Codigo?.Trim(), targetCode.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }
            catch
            {
                // Silently handle if price store has issues
            }

            if (bankEntry != null)
            {
                mapping.TargetDescripcion = bankEntry.Descripcion;
                mapping.TargetUnidad = !string.IsNullOrWhiteSpace(bankEntry.Unidad) ? bankEntry.Unidad : mapping.SourceUnidad;
                mapping.TargetCategoria = bankEntry.Categoria;
                mapping.NuevoPrecioClp = bankEntry.MatClp > 0m ? bankEntry.MatClp : bankEntry.MatUf * valorUfClp;
                mapping.TargetMatUf = bankEntry.MatUf;
                mapping.TargetMoClp = bankEntry.MoClp;
                mapping.TargetMoUf = bankEntry.MoUf;
                mapping.TargetProveedor = bankEntry.Proveedor ?? "";
                mapping.Status = FamilyMappingStatus.InBank;
            }
            else
            {
                mapping.Status = FamilyMappingStatus.NotFound;
            }
        }
    }

    public FamilyBatchReplacePreview CalculatePreview(
        BudgetProjectSession session,
        IEnumerable<FamilyItemMapping> mappings,
        bool replaceCubicacion = true,
        bool replaceApu = true)
    {
        if (session == null || mappings == null)
            return new FamilyBatchReplacePreview(Array.Empty<FamilyItemMapping>(), 0, 0, 0m, 0m, 0m, 0m);

        var list = mappings.Where(m => m.IsSelected).ToList();
        var affectedPartidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totalOcurrencias = 0;
        decimal totalAnterior = 0m;
        decimal totalNuevo = 0m;

        var selectedDict = list
            .Where(m => !string.IsNullOrWhiteSpace(m.TargetCodigo))
            .ToDictionary(m => !string.IsNullOrWhiteSpace(m.SourceCodigo) ? m.SourceCodigo.Trim() : m.SourceDescripcion.Trim(),
                          StringComparer.OrdinalIgnoreCase);

        // Ocurrencias en Cubicación
        if (replaceCubicacion)
        {
            foreach (var r in session.Cubicacion)
            {
                string key = !string.IsNullOrWhiteSpace(r.Codigo) ? r.Codigo.Trim() : (r.Descripcion?.Trim() ?? "");
                if (selectedDict.TryGetValue(key, out var mapping))
                {
                    affectedPartidas.Add(r.ItemId);
                    totalOcurrencias++;
                    decimal subActual = r.Cantidad * r.PrecioUnitClp;
                    decimal subNuevo = r.Cantidad * mapping.NuevoPrecioClp;
                    totalAnterior += subActual;
                    totalNuevo += subNuevo;
                }
            }
        }

        // Ocurrencias en APU
        if (replaceApu)
        {
            decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
            if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;
            var itemizadoDict = session.Itemizado.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var apu in session.Apus)
            {
                itemizadoDict.TryGetValue(apu.ItemId, out var partida);
                decimal partidaCantidad = partida?.Cantidad ?? 1m;
                if (partidaCantidad <= 0m) partidaCantidad = 1m;

                foreach (var rec in apu.Recursos)
                {
                    string key = !string.IsNullOrWhiteSpace(rec.CodigoMaterial) ? rec.CodigoMaterial.Trim() : (rec.Descripcion?.Trim() ?? "");
                    if (selectedDict.TryGetValue(key, out var mapping))
                    {
                        affectedPartidas.Add(apu.ItemId);
                        totalOcurrencias++;
                        decimal cantTotal = rec.Rendimiento * partidaCantidad;
                        decimal recPrecioActual = rec.PrecioUf > 0m ? rec.PrecioUf * valorUf : 0m;
                        decimal subActual = cantTotal * recPrecioActual;
                        decimal subNuevo = cantTotal * mapping.NuevoPrecioClp;
                        totalAnterior += subActual;
                        totalNuevo += subNuevo;
                    }
                }
            }
        }

        decimal delta = totalNuevo - totalAnterior;
        decimal pct = totalAnterior > 0m ? (delta / totalAnterior) * 100m : 0m;

        return new FamilyBatchReplacePreview(
            Mappings: list,
            PartidasAfectadasCount: affectedPartidas.Count,
            TotalOcurrenciasCount: totalOcurrencias,
            CostoTotalAnteriorClp: totalAnterior,
            CostoTotalNuevoClp: totalNuevo,
            DeltaTotalClp: delta,
            VariacionPorcentual: pct);
    }

    public FamilyBatchReplaceResult ExecuteReplacement(
        BudgetProjectSession session,
        Action<BudgetProjectSession, string>? onPushUndo,
        IEnumerable<FamilyItemMapping> mappings,
        bool replaceCubicacion = true,
        bool replaceApu = true)
    {
        if (session == null || mappings == null)
            return new FamilyBatchReplaceResult(0, 0, 0, 0m, 0m, 0m, 0);

        var activeMappings = mappings
            .Where(m => m.IsSelected &&
                        !string.IsNullOrWhiteSpace(m.TargetCodigo) &&
                        (m.Status == FamilyMappingStatus.InBank || m.Status == FamilyMappingStatus.Manual))
            .ToList();

        if (activeMappings.Count == 0)
            return new FamilyBatchReplaceResult(0, 0, 0, 0m, 0m, 0m, 0);

        onPushUndo?.Invoke(session, $"Reemplazo masivo de familia ({activeMappings.Count} materiales)");

        var mappingMap = activeMappings.ToDictionary(
            m => !string.IsNullOrWhiteSpace(m.SourceCodigo) ? m.SourceCodigo.Trim() : m.SourceDescripcion.Trim(),
            StringComparer.OrdinalIgnoreCase);

        var affectedPartidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int rowsCubMod = 0;
        int rowsApuMod = 0;
        decimal costoAnterior = 0m;
        decimal costoNuevo = 0m;

        decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
        if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;

        // 1. Reemplazo en Cubicación
        if (replaceCubicacion)
        {
            foreach (var r in session.Cubicacion)
            {
                string key = !string.IsNullOrWhiteSpace(r.Codigo) ? r.Codigo.Trim() : (r.Descripcion?.Trim() ?? "");
                if (mappingMap.TryGetValue(key, out var mapping))
                {
                    decimal subActual = r.Cantidad * r.PrecioUnitClp;
                    decimal subNuevo = r.Cantidad * mapping.NuevoPrecioClp;
                    costoAnterior += subActual;
                    costoNuevo += subNuevo;

                    r.Codigo = mapping.TargetCodigo;
                    r.Descripcion = mapping.TargetDescripcion;
                    if (!string.IsNullOrWhiteSpace(mapping.TargetUnidad))
                        r.Unidad = mapping.TargetUnidad;
                    if (!string.IsNullOrWhiteSpace(mapping.TargetCategoria))
                        r.Categoria = mapping.TargetCategoria;

                    r.MatClp = mapping.NuevoPrecioClp;
                    r.MoClp = mapping.TargetMoClp;
                    r.MatUf = mapping.TargetMatUf > 0m ? mapping.TargetMatUf : (valorUf > 0m ? mapping.NuevoPrecioClp / valorUf : 0m);
                    r.MoUf = mapping.TargetMoUf;

                    rowsCubMod++;
                    affectedPartidas.Add(r.ItemId);
                }
            }
        }

        // 2. Reemplazo en APUs
        if (replaceApu)
        {
            var itemizadoDict = session.Itemizado.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var apu in session.Apus)
            {
                itemizadoDict.TryGetValue(apu.ItemId, out var partida);
                decimal partidaCantidad = partida?.Cantidad ?? 1m;
                if (partidaCantidad <= 0m) partidaCantidad = 1m;

                foreach (var rec in apu.Recursos)
                {
                    string key = !string.IsNullOrWhiteSpace(rec.CodigoMaterial) ? rec.CodigoMaterial.Trim() : (rec.Descripcion?.Trim() ?? "");
                    if (mappingMap.TryGetValue(key, out var mapping))
                    {
                        decimal recPrecioActual = rec.PrecioUf > 0m ? rec.PrecioUf * valorUf : 0m;
                        decimal cantTotal = rec.Rendimiento * partidaCantidad;
                        costoAnterior += cantTotal * recPrecioActual;
                        costoNuevo += cantTotal * mapping.NuevoPrecioClp;

                        rec.CodigoMaterial = mapping.TargetCodigo;
                        rec.Descripcion = mapping.TargetDescripcion;
                        if (!string.IsNullOrWhiteSpace(mapping.TargetUnidad))
                            rec.Unidad = mapping.TargetUnidad;

                        rec.PrecioUf = mapping.TargetMatUf > 0m ? mapping.TargetMatUf : (valorUf > 0m ? mapping.NuevoPrecioClp / valorUf : 0m);
                        if (!string.IsNullOrWhiteSpace(mapping.TargetProveedor))
                            rec.Proveedor = mapping.TargetProveedor;

                        rowsApuMod++;
                        affectedPartidas.Add(apu.ItemId);
                    }
                }
            }
        }

        // 3. Sincronizar catálogo de materiales del proyecto
        foreach (var mapping in activeMappings)
        {
            string key = !string.IsNullOrWhiteSpace(mapping.SourceCodigo) ? mapping.SourceCodigo.Trim() : mapping.SourceDescripcion.Trim();
            var existingCat = session.Materiales.FirstOrDefault(m =>
                string.Equals(m.Codigo?.Trim(), key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Descripcion?.Trim(), key, StringComparison.OrdinalIgnoreCase));

            if (existingCat != null)
            {
                existingCat.Codigo = mapping.TargetCodigo;
                existingCat.Descripcion = mapping.TargetDescripcion;
                if (!string.IsNullOrWhiteSpace(mapping.TargetUnidad))
                    existingCat.Unidad = mapping.TargetUnidad;
                if (!string.IsNullOrWhiteSpace(mapping.TargetCategoria))
                    existingCat.Categoria = mapping.TargetCategoria;

                existingCat.MatClp = mapping.NuevoPrecioClp;
                existingCat.MoClp = mapping.TargetMoClp;
                existingCat.MatUf = mapping.TargetMatUf > 0m ? mapping.TargetMatUf : (valorUf > 0m ? mapping.NuevoPrecioClp / valorUf : 0m);
                existingCat.MoUf = mapping.TargetMoUf;
                if (!string.IsNullOrWhiteSpace(mapping.TargetProveedor))
                    existingCat.Proveedor = mapping.TargetProveedor;
            }
        }

        session.MarkDirty("family_batch_replace");

        return new FamilyBatchReplaceResult(
            PartidasActualizadas: affectedPartidas.Count,
            FilasCubicacionModificadas: rowsCubMod,
            FilasApuModificadas: rowsApuMod,
            CostoTotalAnteriorClp: costoAnterior,
            CostoTotalNuevoClp: costoNuevo,
            DeltaTotalClp: costoNuevo - costoAnterior,
            ItemsReemplazados: activeMappings.Count
        );
    }

    private static bool MatchesFilter(string? codigo, string? descripcion, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        if (!string.IsNullOrWhiteSpace(codigo) && codigo.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(descripcion) && descripcion.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
