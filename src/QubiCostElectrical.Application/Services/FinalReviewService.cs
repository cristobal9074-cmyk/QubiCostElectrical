using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum FinalReviewSeverity
{
    Blocker,
    Critical,
    Warning,
    Info
}

public enum FinalReviewCategory
{
    EmptyPartida,
    MaterialWithoutPrice,
    AreaNotAssigned,
    MissingApu,
    ZeroQuantity,
    MissingCurrencyUf,
    NegativeMargin,
    ZeroTotalSale,
    IvaInconsistent,
    ApuIncomplete,
    PriceTraceability,
    MarkupInvalid,
    AntecedentePendienteMandante,
    MedicionSinRegistroPlano,
    FechaCorteAntecedentesNoDefinida
    ,CommercialModelIncomplete
    ,EngineInconsistency
}

public sealed record FinalReviewIssue(
    string Id,
    FinalReviewSeverity Severity,
    FinalReviewCategory Category,
    string ItemId,
    string Item,
    string Partida,
    string Codigo,
    string Descripcion,
    int Count,
    string Problem,
    string Recommendation)
{
    public string SeverityText => Severity switch
    {
        FinalReviewSeverity.Blocker => "Bloqueante",
        FinalReviewSeverity.Critical => "Crítico",
        FinalReviewSeverity.Warning => "Advertencia",
        _ => "Info"
    };

    public string CategoryText => Category switch
    {
        FinalReviewCategory.EmptyPartida => "Partida vacía",
        FinalReviewCategory.MaterialWithoutPrice => "Material sin precio",
        FinalReviewCategory.AreaNotAssigned => "Área no asignada",
        FinalReviewCategory.MissingApu => "Partida sin APU",
        FinalReviewCategory.ZeroQuantity => "Cantidad cero",
        FinalReviewCategory.MissingCurrencyUf => "UF / moneda no configurada",
        FinalReviewCategory.NegativeMargin => "Margen negativo",
        FinalReviewCategory.ZeroTotalSale => "Total venta cero",
        FinalReviewCategory.IvaInconsistent => "IVA inconsistente",
        FinalReviewCategory.ApuIncomplete => "APU incompleto",
        FinalReviewCategory.PriceTraceability => "Trazabilidad de precio",
        FinalReviewCategory.MarkupInvalid => "Markup inválido",
        FinalReviewCategory.AntecedentePendienteMandante => "Pendiente del mandante",
        FinalReviewCategory.MedicionSinRegistroPlano => "Medición sin registro de plano",
        FinalReviewCategory.FechaCorteAntecedentesNoDefinida => "Fecha de corte no definida",
        FinalReviewCategory.CommercialModelIncomplete => "Modelo comercial incompleto",
        FinalReviewCategory.EngineInconsistency => "Inconsistencia de cálculo",
        _ => "Revisión"
    };
}

public sealed class FinalReviewSummary
{
    public IReadOnlyList<FinalReviewIssue> Issues { get; init; } = Array.Empty<FinalReviewIssue>();
    public int TotalIssues => Issues.Count;
    public int BlockingCount => Issues.Count(x => x.Severity == FinalReviewSeverity.Blocker);
    public int CriticalCount => Issues.Count(x => x.Severity == FinalReviewSeverity.Critical);
    public int WarningCount => Issues.Count(x => x.Severity == FinalReviewSeverity.Warning);
    public int InfoCount => Issues.Count(x => x.Severity == FinalReviewSeverity.Info);
    public int EmptyPartidas => Issues.Count(x => x.Category == FinalReviewCategory.EmptyPartida);
    public int MaterialsWithoutPrice => Issues.Count(x => x.Category == FinalReviewCategory.MaterialWithoutPrice);
    public int AreasNotAssigned => Issues.Count(x => x.Category == FinalReviewCategory.AreaNotAssigned);
    public int MissingApus => Issues.Count(x => x.Category == FinalReviewCategory.MissingApu || x.Category == FinalReviewCategory.ApuIncomplete);
    public bool ReadyToExport => BlockingCount == 0;

    public decimal ConfidencePct
    {
        get
        {
            int penalizableInfo = Issues.Count(x => x.Severity == FinalReviewSeverity.Info && x.Category != FinalReviewCategory.MissingApu);
            decimal score = 100m - BlockingCount * 20m - CriticalCount * 10m - WarningCount * 4m - penalizableInfo * 1m;
            return Math.Max(0m, Math.Min(100m, score));
        }
    }
}

public interface IFinalReviewService
{
    FinalReviewSummary Build(
        IEnumerable<ItemizadoRow> itemizado,
        IEnumerable<MaterialRow> materiales,
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<ApuRow> apus,
        ProjectInfoRow? proyecto = null,
        BudgetSettings? settings = null,
        IEnumerable<ChangeOrderRow>? changeOrders = null,
        IEnumerable<AntecedenteTecnicoRow>? antecedentes = null,
        IEnumerable<MeasurementLineRow>? mediciones = null);

    bool IsAreaNotAssigned(string? area);
}

public sealed partial class FinalReviewService : IFinalReviewService
{
    private static string S(string? value) => value ?? string.Empty;
    private readonly AntecedentesTecnicosService _antecedentes = new();
    private readonly System.Func<BudgetEngine> _engineFactory;

    public FinalReviewService() : this(null) { }

    public FinalReviewService(System.Func<BudgetEngine>? engineFactory)
        => _engineFactory = engineFactory ?? (static () => new BudgetEngine());

    public FinalReviewSummary Build(
        IEnumerable<ItemizadoRow>? itemizado,
        IEnumerable<MaterialRow>? materiales,
        IEnumerable<CubicacionRow>? cubicacion,
        IEnumerable<ApuRow>? apus,
        ProjectInfoRow? proyecto = null,
        BudgetSettings? settings = null,
        IEnumerable<ChangeOrderRow>? changeOrders = null,
        IEnumerable<AntecedenteTecnicoRow>? antecedentes = null,
        IEnumerable<MeasurementLineRow>? mediciones = null)
    {
        var itemList = (itemizado ?? Enumerable.Empty<ItemizadoRow>()).ToList();
        var materialList = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();
        var cubList = (cubicacion ?? Enumerable.Empty<CubicacionRow>()).ToList();
        var apuList = (apus ?? Enumerable.Empty<ApuRow>()).ToList();
        var issues = new List<FinalReviewIssue>();

        var itemById = itemList
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => S(x.Id), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var commercialRules = proyecto?.CommercialPartidaRules ?? new List<CommercialPartidaRule>();
        var overriddenItemIds = itemList.Where(i => commercialRules.Any(r => !r.UseProjectModel && r.PartidaId == i.PartidaId && r.Model != CommercialCostingModel.ApuDirectCostWithCommercialFactors)).Select(i => S(i.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        AddCommercialModelIssues(issues, itemList, commercialRules);
        AddEmptyOrIncompletePartidas(issues, itemList, cubList, overriddenItemIds);
        AddZeroQuantities(issues, cubList, itemById);
        AddMaterialsWithoutPrice(issues, cubList, materialList, itemById);
        AddAreasNotAssigned(issues, cubList, itemById);
        AddMissingOrIncompleteApus(issues, itemList, cubList, apuList, settings, overriddenItemIds);
        AddFinancialGuards(issues, proyecto, settings, cubList, apuList, itemList, changeOrders, overriddenItemIds);
        AddPriceTraceabilityWarnings(issues, apuList);
        AddAntecedentesTecnicosWarnings(issues, proyecto, antecedentes ?? proyecto?.AntecedentesTecnicos, mediciones);

        return new FinalReviewSummary
        {
            Issues = issues
                .OrderBy(x => SeveritySort(x.Severity))
                .ThenBy(x => CategorySort(x.Category))
                .ThenBy(x => NaturalKey(x.Item))
                .ThenBy(x => x.Codigo)
                .ToList()
        };
    }
}
