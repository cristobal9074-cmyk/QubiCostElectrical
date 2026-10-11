namespace QubiCostElectrical;

public sealed record CompraMaterialViewModel(
    string Codigo,
    string Descripcion,
    string Unidad,
    string Categoria,
    string Proveedor,
    decimal CantidadNeta,
    decimal ToleranciaPct,
    decimal CantidadCompra,
    decimal MatUfUnitario,
    decimal TotalNetoUf,
    decimal TotalCompraUf,
    int Lineas,
    decimal MatClpUnitario = 0m,
    decimal TotalNetoClp = 0m,
    decimal TotalCompraClp = 0m)
{
    /// <summary>
    /// Clave de agrupación: proveedor si existe; si no, categoría (fallback UI).
    /// </summary>
    public string GrupoProveedor =>
        !string.IsNullOrWhiteSpace(Proveedor)
            ? Proveedor.Trim()
            : string.IsNullOrWhiteSpace(Categoria) ? "Sin categoría" : Categoria.Trim();
}

/// <summary>
/// Desglose de trazabilidad viva: en qué partida y área participa un material comprado.
/// </summary>
public sealed record MaterialUsageBreakdown(
    string ItemId,
    string ItemNumber,
    string PartidaDescripcion,
    string Area,
    decimal Cantidad,
    decimal PorcentajeDelTotal,
    string Codigo,
    string Descripcion);
