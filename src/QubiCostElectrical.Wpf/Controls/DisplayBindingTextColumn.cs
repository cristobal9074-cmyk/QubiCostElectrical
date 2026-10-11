using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Controls;

/// <summary>
/// DataGridTextColumn cuyo texto en modo lectura puede venir de un binding distinto al de edición
/// (p.ej. columnas de sectores con formato de despliegue propio, u Observaciones con texto de marcador
/// de posición cuando está vacía). La edición siempre usa el Binding base, sin transformar.
/// </summary>
public sealed class DisplayBindingTextColumn : DataGridTextColumn
{
    public System.Windows.Data.BindingBase? DisplayBinding { get; set; }

    /// <summary>Índice de casilla/sector (0..N). -1 = no es columna de sector.</summary>
    public int SectorSlot { get; set; } = -1;

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var element = base.GenerateElement(cell, dataItem);
        if (DisplayBinding != null && element is TextBlock textBlock)
            textBlock.SetBinding(TextBlock.TextProperty, DisplayBinding);
        return element;
    }
}
