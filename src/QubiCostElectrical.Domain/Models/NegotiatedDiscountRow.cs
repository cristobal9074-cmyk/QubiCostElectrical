using System;

namespace QubiCostElectrical;

/// <summary>
/// Descuento negociado sobre un equipo/material específico (por código), independiente del
/// factor de presentación por categoría (ver BudgetProjectSession.GetFamilyPresentationFactor).
/// Ej.: "DESCTO TRAFO 5%" sobre el transformador de este proyecto puntual.
/// </summary>
public sealed class NegotiatedDiscountRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal DescuentoPct { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Codigo))
            errors.Add("El descuento negociado necesita un código de material/equipo.");
        if (DescuentoPct < 0m || DescuentoPct > 100m)
            errors.Add($"Descuento {DescuentoPct:0.##}% fuera de rango [0-100].");
        return errors;
    }
}
