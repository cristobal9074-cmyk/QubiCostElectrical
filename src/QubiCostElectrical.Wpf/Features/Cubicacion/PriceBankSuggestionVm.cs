#nullable enable
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PriceBankSuggestionVm
{
    public PriceBankEntry? Source { get; }

    public string Codigo { get; }

    public string Descripcion { get; }

    public string Unidad { get; }

    public decimal MatClp { get; }

    public decimal MoClp { get; }

    public string MatClpDisplay
    {
        get
        {
            if (!(MatClp <= 0m))
            {
                return MatClp.ToString("N0");
            }
            return "—";
        }
    }

    public string MoClpDisplay
    {
        get
        {
            if (!(MoClp <= 0m))
            {
                return MoClp.ToString("N0");
            }
            return "—";
        }
    }

    public PriceBankSuggestionVm(PriceBankEntry entry)
    {
        Source = entry;
        Codigo = entry.Codigo ?? "";
        Descripcion = entry.Descripcion ?? "";
        Unidad = entry.Unidad ?? "";
        MatClp = entry.MatClp;
        MoClp = entry.MoClp;
    }

    /// <summary>
    /// Sugerencia armada desde la lista en pantalla. Necesaria para los materiales que solo existen en
    /// el proyecto: no tienen entrada en el banco de precios, así que no hay <see cref="Source"/>.
    /// </summary>
    public PriceBankSuggestionVm(string codigo, string descripcion, string unidad, decimal matClp, decimal moClp)
    {
        Codigo = codigo ?? "";
        Descripcion = descripcion ?? "";
        Unidad = unidad ?? "";
        MatClp = matClp;
        MoClp = moClp;
    }
}
