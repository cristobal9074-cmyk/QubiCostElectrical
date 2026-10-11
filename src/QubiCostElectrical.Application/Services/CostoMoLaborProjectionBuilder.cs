namespace QubiCostElectrical;

public sealed record CostoMoLaborLine(
    string Cargo,
    int Dotacion,
    decimal SueldoMensualClp,
    decimal Pct,
    bool IsDirecta,
    decimal FraccionObra = 1m);

public sealed record CostoMoLaborTotals(
    decimal DirectaClp,
    decimal IndirectaClp,
    decimal TotalClp,
    decimal DirectaPct,
    decimal IndirectaPct,
    int DirectaDotacion,
    int TotalDotacion);

public sealed record CostoMoLaborProjection(
    IReadOnlyList<CostoMoLaborLine> Directa,
    IReadOnlyList<CostoMoLaborLine> Indirecta,
    CostoMoLaborTotals Totals);

/// <summary>
/// Proyecta filas MO directa/indirecta y totales desde el snapshot (sin WPF).
/// </summary>
public static class CostoMoLaborProjectionBuilder
{
    public static IReadOnlyList<(string Cargo, decimal SueldoMensualClp)> DefaultDirectaTemplate { get; } =
    [
        ("Maestro eléctrico (M1/M2)", 750000m),
        ("Ayudante eléctrico (AA/AB)", 650000m),
    ];

    public static IReadOnlyList<(string Cargo, decimal SueldoMensualClp)> DefaultIndirectaTemplate { get; } =
    [
        ("Administrador de obra", 1237500m),
        ("Supervisor de obra", 1466667m),
        ("Capataz de obra", 916667m),
        ("Oficina técnica", 779167m),
        ("Encargado de bodega", 595833m),
        ("Prevencionista de riesgos", 641667m),
    ];

    public static CostoMoLaborProjection Build(IReadOnlyList<MoDirectaRow>? directa, IReadOnlyList<MoIndirectaRow>? indirecta)
    {
        var dirLines = BuildDirecta(directa);
        var indLines = BuildIndirecta(indirecta);

        var totalDir = dirLines.Sum(r => r.Dotacion * r.SueldoMensualClp);
        var totalInd = indLines.Sum(r => r.Dotacion * r.SueldoMensualClp * r.FraccionObra);
        var totalMo = totalDir + totalInd;
        var totals = new CostoMoLaborTotals(
            totalDir,
            totalInd,
            totalMo,
            totalMo > 0 ? totalDir / totalMo * 100m : 0,
            totalMo > 0 ? totalInd / totalMo * 100m : 0,
            dirLines.Sum(r => r.Dotacion),
            dirLines.Sum(r => r.Dotacion) + indLines.Sum(r => r.Dotacion));

        return new CostoMoLaborProjection(dirLines, indLines, totals);
    }

    private static IReadOnlyList<CostoMoLaborLine> BuildDirecta(IReadOnlyList<MoDirectaRow>? rows)
    {
        if (rows is { Count: > 0 })
        {
            var clp = rows.Sum(r => r.Dotacion * r.SueldoMensualClp);
            return rows.Select(r =>
            {
                var itemTotal = r.Dotacion * r.SueldoMensualClp;
                return new CostoMoLaborLine(
                    r.Cargo,
                    r.Dotacion,
                    r.SueldoMensualClp,
                    clp > 0 ? itemTotal / clp * 100m : 0,
                    IsDirecta: true);
            }).ToList();
        }

        return DefaultDirectaTemplate
            .Select(t => new CostoMoLaborLine(t.Cargo, 0, t.SueldoMensualClp, 0, true))
            .ToList();
    }

    private static IReadOnlyList<CostoMoLaborLine> BuildIndirecta(IReadOnlyList<MoIndirectaRow>? rows)
    {
        if (rows is { Count: > 0 })
        {
            var clp = rows.Sum(r => r.Dotacion * r.SueldoMensualClp * r.FraccionEfectiva);
            return rows.Select(r =>
            {
                var itemTotal = r.Dotacion * r.SueldoMensualClp * r.FraccionEfectiva;
                return new CostoMoLaborLine(
                    r.Cargo,
                    r.Dotacion,
                    r.SueldoMensualClp,
                    clp > 0 ? itemTotal / clp * 100m : 0,
                    IsDirecta: false,
                    FraccionObra: r.FraccionEfectiva);
            }).ToList();
        }

        return DefaultIndirectaTemplate
            .Select(t => new CostoMoLaborLine(t.Cargo, 0, t.SueldoMensualClp, 0, false))
            .ToList();
    }
}
