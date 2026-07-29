using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ApuResourceViewModel(
    string Tipo,
    string Codigo,
    string Descripcion,
    string Unidad,
    decimal CantidadUnitaria,
    decimal PrecioMaterialUf,
    decimal PrecioManoObraUf,
    decimal TotalMaterialUf,
    decimal TotalManoObraUf,
    decimal TotalUf,
    string Area);

public sealed record ApuViewModel(
    string ItemId,
    string CodigoItem,
    string DescripcionItem,
    string UnidadItem,
    decimal CantidadItem,
    IReadOnlyList<ApuResourceViewModel> Recursos,
    CostSummaryViewModel Summary,
    decimal PrecioUnitarioVentaUf,
    bool TieneRecursos,
    IReadOnlyList<string> Alertas);
