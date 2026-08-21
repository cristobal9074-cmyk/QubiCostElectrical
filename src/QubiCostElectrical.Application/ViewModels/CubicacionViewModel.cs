using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record CubicacionLineViewModel(
    string Id,
    string ItemId,
    string Tipo,
    string Codigo,
    string Descripcion,
    string Unidad,
    decimal Cantidad,
    decimal MaterialUf,
    decimal ManoObraUf,
    decimal UnitarioUf,
    decimal TotalUf,
    string Area);

public sealed record CubicacionViewModel(
    string ItemId,
    IReadOnlyList<CubicacionLineViewModel> Rows,
    int LineasValorizadas,
    decimal CantidadTotal,
    CostSummaryViewModel Summary);
