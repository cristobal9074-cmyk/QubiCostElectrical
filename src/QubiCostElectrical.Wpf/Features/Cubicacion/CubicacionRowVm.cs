using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionRowVm : ViewModelBase, IGridCellValidationSource
{
    private string tipo;
    private string codigo;
    private string descripcion;
    private string unidad;
    private decimal cantidad;
    private decimal matUf;
    private decimal moUf;
    private string area;
    private string notaInterna;
    private bool hasError;
    private string errorMessage = "";
    private bool isSelected;
    private bool isLoadingSectorWindow;
    private bool isSessionBacked = true;
    private int rowNumber;
    private bool isTemplateSlave;

    public bool IsTemplateSlave
    {
        get => isTemplateSlave;
        set => SetProperty(ref isTemplateSlave, value);
    }
}
