namespace QubiCostElectrical;

/// <summary>Reglas de mantenimiento de categorías GO (3.1–3.6).</summary>
public static class GastoOperacionalCategoryMaintenance
{
    public const string TrasladoBusConcepto = "Arriendo / traslado en bus";
    public const string TrasladoBusUnidad = "viaje";

    public static bool IsMovilizacionCategory(string? codigo, string? concepto)
        => string.Equals(codigo, "3.3", StringComparison.OrdinalIgnoreCase)
           || (!string.IsNullOrWhiteSpace(concepto)
               && concepto.Contains("Movilización", StringComparison.OrdinalIgnoreCase));

    public static bool HasTrasladoBusItem(IEnumerable<string> itemConceptos)
        => itemConceptos.Any(c =>
            c.Contains("bus", StringComparison.OrdinalIgnoreCase)
            || c.Contains("Arriendo / traslado", StringComparison.OrdinalIgnoreCase));

    /// <summary>Asegura el ítem de bus en 3.3. Devuelve true si agregó.</summary>
    public static bool EnsureTrasladoBusItem(IList<GastoOperacionalRow> categories)
    {
        var cat = categories.FirstOrDefault(c => IsMovilizacionCategory(c.Codigo, c.Concepto));
        if (cat == null)
            return false;

        cat.Items ??= new List<GastoOperacionalItemRow>();
        if (HasTrasladoBusItem(cat.Items.Select(i => i.Concepto)))
            return false;

        cat.Items.Add(new GastoOperacionalItemRow
        {
            Concepto = TrasladoBusConcepto,
            Unidad = TrasladoBusUnidad,
            Cantidad = 0,
            PrecioUnitarioClp = 0,
        });
        return true;
    }
}
