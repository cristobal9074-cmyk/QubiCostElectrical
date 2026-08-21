namespace QubiCostElectrical;

public sealed record CostSummaryViewModel(
    decimal MaterialUf,
    decimal ManoObraUf,
    decimal EquipoUf,
    decimal OtrosUf,
    decimal CostoDirectoUf,
    decimal GastosGeneralesUf,
    decimal SubtotalUf,
    decimal UtilidadUf,
    decimal VentaUf,
    decimal UfM2);
