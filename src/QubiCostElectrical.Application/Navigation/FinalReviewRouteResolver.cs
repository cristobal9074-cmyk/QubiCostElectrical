namespace QubiCostElectrical;

public enum WorkspaceReviewTarget
{
    Review,
    Project,
    Documents,
    Itemized,
    Takeoff,
    Apu,
    Prices
}

public static class FinalReviewRouteResolver
{
    public static WorkspaceReviewTarget Resolve(FinalReviewCategory category) => category switch
    {
        FinalReviewCategory.FormulaSyntaxError or FinalReviewCategory.EmptyPartida or FinalReviewCategory.ZeroQuantity or FinalReviewCategory.AreaNotAssigned or FinalReviewCategory.PartidaSinValorizacionGlobal => WorkspaceReviewTarget.Takeoff,
        FinalReviewCategory.MaterialWithoutPrice or FinalReviewCategory.PriceTraceability => WorkspaceReviewTarget.Prices,
        FinalReviewCategory.MissingApu or FinalReviewCategory.ApuIncomplete => WorkspaceReviewTarget.Apu,
        FinalReviewCategory.MissingCurrencyUf or FinalReviewCategory.IvaInconsistent or FinalReviewCategory.MarkupInvalid or FinalReviewCategory.NegativeMargin or FinalReviewCategory.ZeroTotalSale => WorkspaceReviewTarget.Project,
        FinalReviewCategory.AntecedentePendienteMandante or FinalReviewCategory.MedicionSinRegistroPlano or FinalReviewCategory.FechaCorteAntecedentesNoDefinida => WorkspaceReviewTarget.Documents,
        _ => WorkspaceReviewTarget.Review
    };
}
