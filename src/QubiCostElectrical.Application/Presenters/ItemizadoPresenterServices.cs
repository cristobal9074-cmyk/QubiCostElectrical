namespace QubiCostElectrical;

public sealed record ItemizadoPresenterServices(
    IItemizadoService Itemizado,
    IPartidaCompletionService PartidaCompletion,
    IItemizadoSpreadsheetReader ItemizadoSpreadsheet,
    ImportarItemizadoUseCase ImportItemizado,
    PegarItemizadoUseCase PasteItemizado,
    IBudgetCalculationService BudgetCalculation);
