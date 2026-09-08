namespace QubiCostElectrical;

public sealed record CostoGoComposeSegment(
    int PaletteIndex,
    double WidthUnits,
    double FillOpacity);

public sealed record CostoGoComposeLegendItem(
    int PaletteIndex,
    string Codigo,
    string PctLabel);

public sealed record CostoGoComposeProjection(
    IReadOnlyList<CostoGoComposeSegment> Segments,
    IReadOnlyList<CostoGoComposeLegendItem> Legend);

public sealed record CostoGoCategoryShare(string Codigo, decimal MontoClp, decimal Pct);

/// <summary>
/// Proyecta la barra de composición GO (unidades/porcentajes) sin brushes WPF.
/// </summary>
public static class CostoGoComposeProjectionBuilder
{
    public static CostoGoComposeProjection Build(IReadOnlyList<CostoGoCategoryShare> categories, decimal totalGoClp)
    {
        if (categories.Count == 0)
            return new CostoGoComposeProjection([], []);

        if (totalGoClp <= 0)
        {
            var emptySegments = new List<CostoGoComposeSegment>
            {
                new(0, 1000, 0.22)
            };
            var emptyLegend = categories
                .Select((c, i) => new CostoGoComposeLegendItem(i, c.Codigo, "—"))
                .ToList();
            return new CostoGoComposeProjection(emptySegments, emptyLegend);
        }

        var segments = new List<CostoGoComposeSegment>(categories.Count);
        var legend = new List<CostoGoComposeLegendItem>(categories.Count);
        for (var i = 0; i < categories.Count; i++)
        {
            var cat = categories[i];
            var share = (double)(cat.MontoClp / totalGoClp);
            var units = Math.Max(share * 1000, cat.MontoClp > 0 ? 18 : 0.01);
            segments.Add(new CostoGoComposeSegment(i, units, 1));
            legend.Add(new CostoGoComposeLegendItem(i, cat.Codigo, $"{cat.Pct:N0}%"));
        }

        return new CostoGoComposeProjection(segments, legend);
    }
}
