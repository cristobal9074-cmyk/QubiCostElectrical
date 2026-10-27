using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

public enum FamilyMappingStatus
{
    NoChange,
    InCubicacion,
    InBank,
    Manual,
    NotFound
}

public sealed class FamilyItemMapping : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool isSelected = true;
    public bool IsSelected
    {
        get => isSelected;
        set { if (isSelected != value) { isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
    }

    private string sourceCodigo = "";
    public string SourceCodigo
    {
        get => sourceCodigo;
        set { if (sourceCodigo != value) { sourceCodigo = value; OnPropertyChanged(nameof(SourceCodigo)); } }
    }

    private string sourceDescripcion = "";
    public string SourceDescripcion
    {
        get => sourceDescripcion;
        set { if (sourceDescripcion != value) { sourceDescripcion = value; OnPropertyChanged(nameof(SourceDescripcion)); } }
    }

    private string sourceUnidad = "";
    public string SourceUnidad
    {
        get => sourceUnidad;
        set { if (sourceUnidad != value) { sourceUnidad = value; OnPropertyChanged(nameof(SourceUnidad)); } }
    }

    private string sourceCategoria = "";
    public string SourceCategoria
    {
        get => sourceCategoria;
        set { if (sourceCategoria != value) { sourceCategoria = value; OnPropertyChanged(nameof(SourceCategoria)); } }
    }

    private decimal cantidadTotal;
    public decimal CantidadTotal
    {
        get => cantidadTotal;
        set
        {
            if (cantidadTotal != value)
            {
                cantidadTotal = value;
                OnPropertyChanged(nameof(CantidadTotal));
                OnPropertyChanged(nameof(SubtotalActualClp));
                OnPropertyChanged(nameof(NuevoSubtotalClp));
                OnPropertyChanged(nameof(DeltaClp));
            }
        }
    }

    private decimal precioActualClp;
    public decimal PrecioActualClp
    {
        get => precioActualClp;
        set
        {
            if (precioActualClp != value)
            {
                precioActualClp = value;
                OnPropertyChanged(nameof(PrecioActualClp));
                OnPropertyChanged(nameof(SubtotalActualClp));
                OnPropertyChanged(nameof(DeltaClp));
                OnPropertyChanged(nameof(DeltaUnitarioClp));
            }
        }
    }

    public decimal SubtotalActualClp => CantidadTotal * PrecioActualClp;

    private string targetCodigo = "";
    public string TargetCodigo
    {
        get => targetCodigo;
        set { if (targetCodigo != value) { targetCodigo = value; OnPropertyChanged(nameof(TargetCodigo)); } }
    }

    private string targetDescripcion = "";
    public string TargetDescripcion
    {
        get => targetDescripcion;
        set { if (targetDescripcion != value) { targetDescripcion = value; OnPropertyChanged(nameof(TargetDescripcion)); } }
    }

    private string targetUnidad = "";
    public string TargetUnidad
    {
        get => targetUnidad;
        set { if (targetUnidad != value) { targetUnidad = value; OnPropertyChanged(nameof(TargetUnidad)); } }
    }

    private string targetCategoria = "";
    public string TargetCategoria
    {
        get => targetCategoria;
        set { if (targetCategoria != value) { targetCategoria = value; OnPropertyChanged(nameof(TargetCategoria)); } }
    }

    private decimal nuevoPrecioClp;
    public decimal NuevoPrecioClp
    {
        get => nuevoPrecioClp;
        set
        {
            if (nuevoPrecioClp != value)
            {
                nuevoPrecioClp = value;
                OnPropertyChanged(nameof(NuevoPrecioClp));
                OnPropertyChanged(nameof(NuevoSubtotalClp));
                OnPropertyChanged(nameof(DeltaClp));
                OnPropertyChanged(nameof(DeltaUnitarioClp));
            }
        }
    }

    public decimal NuevoSubtotalClp => CantidadTotal * NuevoPrecioClp;
    public decimal DeltaClp => NuevoSubtotalClp - SubtotalActualClp;
    public decimal DeltaUnitarioClp => NuevoPrecioClp - PrecioActualClp;

    public string TargetProveedor { get; set; } = "";
    public decimal TargetMatUf { get; set; }
    public decimal TargetMoClp { get; set; }
    public decimal TargetMoUf { get; set; }

    private FamilyMappingStatus status = FamilyMappingStatus.NoChange;
    public FamilyMappingStatus Status
    {
        get => status;
        set
        {
            if (status != value)
            {
                status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }
    }

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

        // 2. Escanear APUs (si no es scanCubicacionOnly)
        if (!scanCubicacionOnly)
        {
            decimal valorUf = session.ProjectInfo?.ValorUfClp ?? PriceConversion.DefaultValorUfClp;
            if (valorUf <= 0m) valorUf = PriceConversion.DefaultValorUfClp;

            var itemizadoDict = session.Itemizado.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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

            // 3. Escanear Cubicación SC (Circuitos, tuberías y artefactos)
            if (session.CubicacionSC != null)
            {
                foreach (var cuadro in session.CubicacionSC)
                {
                    foreach (var c in cuadro.Circuitos)
                    {
                        if (!c.EsActivo) continue;

                        // Tubería 1
                        if (c.LargoTuberia > 0m && !string.IsNullOrWhiteSpace(c.TipoTuberia))
                        {
                            string code = $"TUBERIA {c.TipoTuberia} {c.SeccionTuberia}".Trim();
                            if (MatchesFilter(c.TipoTuberia, code, cleanPattern))
                            {
                                AddOrUpdateScMapping(itemsMap, session, code, code, "ML", c.LargoTuberia);
                            }
                        }

                        // Tubería 2
                        if (c.LargoTuberia2 > 0m && !string.IsNullOrWhiteSpace(c.TipoTuberia2))
                        {
                            string code2 = $"TUBERIA {c.TipoTuberia2} {c.SeccionTuberia2}".Trim();
                            if (MatchesFilter(c.TipoTuberia2, code2, cleanPattern))
                            {
                                AddOrUpdateScMapping(itemsMap, session, code2, code2, "ML", c.LargoTuberia2);
                            }
                        }

                        // Artefactos
                        foreach (var a in c.Artefactos)
                        {
                            if (a.Cantidad > 0m && !string.IsNullOrWhiteSpace(a.Nombre))
                            {
                                if (MatchesFilter(null, a.Nombre, cleanPattern))
                                {
                                    AddOrUpdateScMapping(itemsMap, session, a.Nombre.Trim(), a.Nombre.Trim(), "UN", a.Cantidad);
                                }
                            }
                        }
                    }
                }
            }
        }

        return itemsMap.Values
            .OrderBy(m => m.SourceCodigo)
            .ThenBy(m => m.SourceDescripcion)
            .ToList();
    }

    private static void AddOrUpdateScMapping(
        Dictionary<string, FamilyItemMapping> map,
        BudgetProjectSession session,
        string key,
        string descripcion,
        string unidad,
        decimal cantidad)
    {
        if (!map.TryGetValue(key, out var mapping))
        {
            decimal precio = 0m;
            // Buscar precio de referencia en catálogo de materiales o cubicación
            var existingMat = session.Materiales.FirstOrDefault(m =>
                string.Equals(m.Codigo?.Trim(), key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Descripcion?.Trim(), descripcion, StringComparison.OrdinalIgnoreCase));
            if (existingMat != null)
            {
                precio = existingMat.MatClp > 0m ? existingMat.MatClp : existingMat.MatClpConFallback;
            }
            else
            {
                var existingCub = session.Cubicacion.FirstOrDefault(c =>
                    string.Equals(c.Codigo?.Trim(), key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Descripcion?.Trim(), descripcion, StringComparison.OrdinalIgnoreCase));
                if (existingCub != null)
                {
                    precio = existingCub.PrecioUnitClp;
                }
            }

            mapping = new FamilyItemMapping
            {
                SourceCodigo = key,
                SourceDescripcion = descripcion,
                SourceUnidad = unidad,
                SourceCategoria = "Cubicación SC",
                PrecioActualClp = precio,
                CantidadTotal = 0m,
                OccurrencesCount = 0
            };
            map[key] = mapping;
        }

        mapping.CantidadTotal += cantidad;
        mapping.OccurrencesCount++;
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

        Dictionary<string, CubicacionRow>? cubicacionByCode = null;
        Dictionary<string, MaterialRow>? materialesByCode = null;
        if (session != null)
        {
            cubicacionByCode = session.Cubicacion
                .Where(c => !string.IsNullOrWhiteSpace(c.Codigo))
                .GroupBy(c => c.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            materialesByCode = session.Materiales
                .Where(m => !string.IsNullOrWhiteSpace(m.Codigo))
                .GroupBy(m => m.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        foreach (var mapping in mappings)
        {
            string targetCode = "";

            // 1. Override explícito por código o descripción exactos
            if (explicitOverrides != null)
            {
                if (explicitOverrides.TryGetValue(mapping.SourceCodigo, out var explicitCode) ||
                    explicitOverrides.TryGetValue(mapping.SourceDescripcion, out explicitCode))
                {
                    targetCode = explicitCode;
                }
                else
                {
                    // Buscar coincidencia parcial en claves de override
                    foreach (var kvp in explicitOverrides)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Key) &&
                            (mapping.SourceCodigo.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                             mapping.SourceDescripcion.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                        {
                            targetCode = !string.IsNullOrWhiteSpace(mapping.SourceCodigo)
                                ? Regex.Replace(mapping.SourceCodigo, Regex.Escape(kvp.Key), kvp.Value, RegexOptions.IgnoreCase)
                                : kvp.Value;
                            break;
                        }
                    }
                }
            }

            // 2. Reemplazo por prefijo o subcadena
            if (string.IsNullOrWhiteSpace(targetCode) && !string.IsNullOrEmpty(srcPrefix))
            {
                if (mapping.SourceCodigo.StartsWith(srcPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    targetCode = tgtPrefix + mapping.SourceCodigo.Substring(srcPrefix.Length);
                }
                else if (mapping.SourceCodigo.Contains(srcPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    targetCode = Regex.Replace(mapping.SourceCodigo, Regex.Escape(srcPrefix), tgtPrefix, RegexOptions.IgnoreCase);
                }
                else if (mapping.SourceDescripcion.Contains(srcPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string baseCode = !string.IsNullOrWhiteSpace(mapping.SourceCodigo) ? mapping.SourceCodigo : mapping.SourceDescripcion;
                    targetCode = Regex.Replace(baseCode, Regex.Escape(srcPrefix), tgtPrefix, RegexOptions.IgnoreCase);
                }
            }

            // 3. Respaldo a TargetCodigo existente
            if (string.IsNullOrWhiteSpace(targetCode) && !string.IsNullOrWhiteSpace(mapping.TargetCodigo))
            {
                targetCode = mapping.TargetCodigo;
            }

            if (string.IsNullOrWhiteSpace(targetCode))
                continue;

            mapping.TargetCodigo = targetCode;

            // 4. Buscar en la Cubicación del proyecto
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

            // 5. Buscar en catálogo de Materiales del proyecto
            if (materialesByCode != null && materialesByCode.TryGetValue(targetCode, out var matRow))
            {
                mapping.TargetDescripcion = matRow.Descripcion ?? "";
                mapping.TargetUnidad = !string.IsNullOrWhiteSpace(matRow.Unidad) ? matRow.Unidad : mapping.SourceUnidad;
                mapping.TargetCategoria = matRow.Categoria ?? "";
                mapping.NuevoPrecioClp = matRow.MatClp > 0m ? matRow.MatClp : matRow.MatClpConFallback;
                mapping.TargetMatUf = matRow.MatUf;
                mapping.TargetMoClp = matRow.MoClp;
                mapping.TargetMoUf = matRow.MoUf;
                mapping.TargetProveedor = matRow.Proveedor ?? "";
                mapping.Status = FamilyMappingStatus.InCubicacion;
                continue;
            }

            // 6. Buscar en Banco de Precios
            PriceBankEntry? bankEntry = null;
            try
            {
                bankEntry = priceBankStore.GetByCodigo(targetCode);
                if (bankEntry == null)
                {
                    bankEntry = priceBankStore.Search(targetCode)
                        .FirstOrDefault(e => string.Equals(e.Codigo?.Trim(), targetCode.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }
            catch
            {
                // Fallback safe
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
                // 7. No encontrado en banco ni catálogo: generar descripción automática y conservar precio base para edición
                if (!string.IsNullOrEmpty(srcPrefix) && mapping.SourceDescripcion.Contains(srcPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    mapping.TargetDescripcion = Regex.Replace(mapping.SourceDescripcion, Regex.Escape(srcPrefix), tgtPrefix, RegexOptions.IgnoreCase);
                }
                else
                {
                    mapping.TargetDescripcion = targetCode;
                }

                mapping.TargetUnidad = mapping.SourceUnidad;
                mapping.NuevoPrecioClp = mapping.PrecioActualClp; // Línea base: conserva el precio actual para que no quede en $0
                mapping.Status = FamilyMappingStatus.Manual;
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
            .Where(m => !string.IsNullOrWhiteSpace(m.TargetCodigo) || !string.IsNullOrWhiteSpace(m.TargetDescripcion))
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
            var itemizadoDict = session.Itemizado.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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
                        (!string.IsNullOrWhiteSpace(m.TargetCodigo) || !string.IsNullOrWhiteSpace(m.TargetDescripcion)))
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

                    r.Codigo = !string.IsNullOrWhiteSpace(mapping.TargetCodigo) ? mapping.TargetCodigo : r.Codigo;
                    r.Descripcion = (!string.IsNullOrWhiteSpace(mapping.TargetDescripcion) ? mapping.TargetDescripcion : r.Descripcion) ?? "";
                    if (!string.IsNullOrWhiteSpace(mapping.TargetUnidad))
                        r.Unidad = mapping.TargetUnidad;
                    if (!string.IsNullOrWhiteSpace(mapping.TargetCategoria))
                        r.Categoria = mapping.TargetCategoria;

                    r.MatClp = mapping.NuevoPrecioClp;
                    r.MoClp = mapping.TargetMoClp > 0m ? mapping.TargetMoClp : r.MoClp;
                    r.MatUf = mapping.TargetMatUf > 0m ? mapping.TargetMatUf : (valorUf > 0m ? mapping.NuevoPrecioClp / valorUf : 0m);
                    r.MoUf = mapping.TargetMoUf > 0m ? mapping.TargetMoUf : r.MoUf;

                    rowsCubMod++;
                    affectedPartidas.Add(r.ItemId);
                }
            }
        }

        // 2. Reemplazo en APUs
        if (replaceApu)
        {
            var itemizadoDict = session.Itemizado.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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

                        rec.CodigoMaterial = !string.IsNullOrWhiteSpace(mapping.TargetCodigo) ? mapping.TargetCodigo : rec.CodigoMaterial;
                        rec.Descripcion = (!string.IsNullOrWhiteSpace(mapping.TargetDescripcion) ? mapping.TargetDescripcion : rec.Descripcion) ?? "";
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

        // 3. Reemplazo en Cubicación SC (Tuberías y Artefactos)
        if (session.CubicacionSC != null && session.CubicacionSC.Count > 0)
        {
            foreach (var cuadro in session.CubicacionSC)
            {
                foreach (var c in cuadro.Circuitos)
                {
                    // TipoTuberia
                    if (!string.IsNullOrWhiteSpace(c.TipoTuberia))
                    {
                        if (mappingMap.TryGetValue(c.TipoTuberia, out var mTub) && !string.IsNullOrWhiteSpace(mTub.TargetCodigo))
                        {
                            c.TipoTuberia = mTub.TargetCodigo;
                            affectedPartidas.Add(cuadro.Id);
                        }
                        else
                        {
                            foreach (var m in activeMappings)
                            {
                                if (!string.IsNullOrWhiteSpace(m.SourceCodigo) &&
                                    (string.Equals(c.TipoTuberia, m.SourceCodigo, StringComparison.OrdinalIgnoreCase) ||
                                     c.TipoTuberia.Contains(m.SourceCodigo, StringComparison.OrdinalIgnoreCase)))
                                {
                                    string tgt = !string.IsNullOrWhiteSpace(m.TargetCodigo) ? m.TargetCodigo : m.TargetDescripcion;
                                    c.TipoTuberia = Regex.Replace(c.TipoTuberia, Regex.Escape(m.SourceCodigo), tgt, RegexOptions.IgnoreCase);
                                    affectedPartidas.Add(cuadro.Id);
                                    break;
                                }
                            }
                        }
                    }

                    // TipoTuberia2
                    if (!string.IsNullOrWhiteSpace(c.TipoTuberia2))
                    {
                        if (mappingMap.TryGetValue(c.TipoTuberia2, out var mTub2) && !string.IsNullOrWhiteSpace(mTub2.TargetCodigo))
                        {
                            c.TipoTuberia2 = mTub2.TargetCodigo;
                            affectedPartidas.Add(cuadro.Id);
                        }
                        else
                        {
                            foreach (var m in activeMappings)
                            {
                                if (!string.IsNullOrWhiteSpace(m.SourceCodigo) &&
                                    (string.Equals(c.TipoTuberia2, m.SourceCodigo, StringComparison.OrdinalIgnoreCase) ||
                                     c.TipoTuberia2.Contains(m.SourceCodigo, StringComparison.OrdinalIgnoreCase)))
                                {
                                    string tgt = !string.IsNullOrWhiteSpace(m.TargetCodigo) ? m.TargetCodigo : m.TargetDescripcion;
                                    c.TipoTuberia2 = Regex.Replace(c.TipoTuberia2, Regex.Escape(m.SourceCodigo), tgt, RegexOptions.IgnoreCase);
                                    affectedPartidas.Add(cuadro.Id);
                                    break;
                                }
                            }
                        }
                    }

                    // Artefactos
                    foreach (var a in c.Artefactos)
                    {
                        if (string.IsNullOrWhiteSpace(a.Nombre)) continue;

                        if (mappingMap.TryGetValue(a.Nombre, out var mArt))
                        {
                            a.Nombre = !string.IsNullOrWhiteSpace(mArt.TargetDescripcion) ? mArt.TargetDescripcion : mArt.TargetCodigo;
                            affectedPartidas.Add(cuadro.Id);
                        }
                        else
                        {
                            foreach (var m in activeMappings)
                            {
                                if (!string.IsNullOrWhiteSpace(m.SourceCodigo) &&
                                    a.Nombre.Contains(m.SourceCodigo, StringComparison.OrdinalIgnoreCase))
                                {
                                    string tgt = !string.IsNullOrWhiteSpace(m.TargetDescripcion) ? m.TargetDescripcion : m.TargetCodigo;
                                    a.Nombre = Regex.Replace(a.Nombre, Regex.Escape(m.SourceCodigo), tgt, RegexOptions.IgnoreCase);
                                    affectedPartidas.Add(cuadro.Id);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        // 4. Sincronizar catálogo de materiales del proyecto
        foreach (var mapping in activeMappings)
        {
            string key = !string.IsNullOrWhiteSpace(mapping.SourceCodigo) ? mapping.SourceCodigo.Trim() : mapping.SourceDescripcion.Trim();
            var existingCat = session.Materiales.FirstOrDefault(m =>
                string.Equals(m.Codigo?.Trim(), key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Descripcion?.Trim(), key, StringComparison.OrdinalIgnoreCase));

            if (existingCat != null)
            {
                existingCat.Codigo = !string.IsNullOrWhiteSpace(mapping.TargetCodigo) ? mapping.TargetCodigo : existingCat.Codigo;
                existingCat.Descripcion = (!string.IsNullOrWhiteSpace(mapping.TargetDescripcion) ? mapping.TargetDescripcion : existingCat.Descripcion) ?? "";
                if (!string.IsNullOrWhiteSpace(mapping.TargetUnidad))
                    existingCat.Unidad = mapping.TargetUnidad;
                if (!string.IsNullOrWhiteSpace(mapping.TargetCategoria))
                    existingCat.Categoria = mapping.TargetCategoria;

                existingCat.MatClp = mapping.NuevoPrecioClp;
                existingCat.MoClp = mapping.TargetMoClp > 0m ? mapping.TargetMoClp : existingCat.MoClp;
                existingCat.MatUf = mapping.TargetMatUf > 0m ? mapping.TargetMatUf : (valorUf > 0m ? mapping.NuevoPrecioClp / valorUf : 0m);
                existingCat.MoUf = mapping.TargetMoUf > 0m ? mapping.TargetMoUf : existingCat.MoUf;
                if (!string.IsNullOrWhiteSpace(mapping.TargetProveedor))
                    existingCat.Proveedor = mapping.TargetProveedor;
            }
        }

        session.SyncCubicacionPricesFromMateriales();
        session.SyncCubicacionGgUtFactorsFromVariables();
        session.MarkDirty(
            ProjectDirtyScope.Cubicacion,
            ProjectDirtyScope.Apu,
            ProjectDirtyScope.Itemizado,
            ProjectDirtyScope.CubicacionSc,
            ProjectDirtyScope.Materiales);

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
