using System;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Versions;

/// <summary>
/// Fila de la mini tabla "Detalle de recursos" del comparador de revisiones. El motor entrega
/// <see cref="BudgetVersionResourceDiffRow.UfUnitario"/> y
/// <see cref="BudgetVersionResourceDiffRow.DeltaTotalUf"/> en UF reales; esta VM los presenta
/// en la moneda del proyecto (UF o pesos) y expone las etiquetas de encabezado correspondientes.
/// </summary>
public sealed class VersionsResourceDiffVm
{
    private readonly VersionsCurrencyFormat currency;

    public VersionsResourceDiffVm(BudgetVersionResourceDiffRow source, VersionsCurrencyFormat currency)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        this.currency = currency ?? throw new ArgumentNullException(nameof(currency));
    }

    public BudgetVersionResourceDiffRow Source { get; }

    public string Codigo => Source.Codigo;
    public string Descripcion => Source.Descripcion;
    public string Unidad => Source.Unidad;
    public decimal CantidadBase => Source.CantidadBase;
    public decimal CantidadActual => Source.CantidadActual;
    public decimal DeltaCantidad => Source.DeltaCantidad;

    public string PrecioUnitarioText => currency.FromUf(Source.UfUnitario);
    public string DeltaTotalText => currency.FromUfSigned(Source.DeltaTotalUf);
}
