using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace QubiCostElectrical;

public sealed class MaterialCatalogFilter
{
    public string Categoria { get; set; } = "";
    public string Subcategoria { get; set; } = "";
    public string Subcategoria2 { get; set; } = "";
    public string TipoInstalacion { get; set; } = "";
    public string EspecificacionTecnica { get; set; } = "";

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Categoria)
        && string.IsNullOrWhiteSpace(Subcategoria)
        && string.IsNullOrWhiteSpace(Subcategoria2)
        && string.IsNullOrWhiteSpace(TipoInstalacion)
        && string.IsNullOrWhiteSpace(EspecificacionTecnica);
}


public sealed class MaterialCatalogQuickFilter
{
    public string Label { get; set; } = "";
    public string Hint { get; set; } = "";
    public MaterialCatalogFilter Filter { get; set; } = new();
    public int Count { get; set; }
}

public sealed partial class MaterialSearchService : IMaterialSearchService
{
    private readonly object invertedIndexLock = new();
    private int invertedIndexFingerprint;
    private Dictionary<string, List<int>> invertedTokenIndex = new(StringComparer.OrdinalIgnoreCase);
}
