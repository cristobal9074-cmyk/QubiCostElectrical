namespace QubiCostElectrical.Wpf.Infrastructure;

public interface IGridCellValidationSource
{
    string? GetCellError(string columnKey);
}
