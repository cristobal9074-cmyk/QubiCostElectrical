using System.Collections.ObjectModel;
using System.Linq;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class MaterialConZonasVm : FlatRowVm
{
    internal ResumenScTomarViewModel? _parent;

    public override bool IsGroupHeader => false;

    public string Nombre { get; init; } = "";
    public string Codigo => Nombre;
    public string Descripcion => Nombre;
    public string Unidad { get; init; } = "";
    public string Categoria { get; init; } = "";
    public decimal Total { get; set; }
    public string TotalDisplay => Total.ToString("#,##0.##");
    public string CodigoDisplay => Nombre;

    public ObservableCollection<ZonaQtyWrapper> ZonaQties { get; } = new();

    public void RecalcTotal()
    {
        Total = ZonaQties.Sum(z => decimal.TryParse(z.Text, out var result) ? result : 0m);
        foreach (var zonaQty in ZonaQties)
            zonaQty.NotifyValueChanged();
    }
}
