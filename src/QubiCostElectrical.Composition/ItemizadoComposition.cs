namespace QubiCostElectrical;

public sealed record ItemizadoPageServices(
    IItemizadoService Itemizado,
    IPartidaCompletionService PartidaCompletion,
    IItemizadoSpreadsheetReader ItemizadoSpreadsheet,
    IMaterialSearchService MaterialSearch,
    ImportarItemizadoUseCase ImportItemizado,
    PegarItemizadoUseCase PasteItemizado)
{
    public static ItemizadoPageServices From(AppServices services) => new(
        services.Itemizado,
        services.PartidaCompletion,
        services.ItemizadoSpreadsheet,
        services.MaterialSearch,
        services.ImportItemizado,
        services.PasteItemizado);
}

public static class ItemizadoPresenterFactory
{
    public static ItemizadoPresenter CreatePresenter(AppServices services, BudgetProjectSession session)
        => new(new ItemizadoPresenterServices(
            services.Itemizado,
            services.PartidaCompletion,
            services.ItemizadoSpreadsheet,
            services.ImportItemizado,
            services.PasteItemizado,
            services.BudgetCalculation), session);
}
