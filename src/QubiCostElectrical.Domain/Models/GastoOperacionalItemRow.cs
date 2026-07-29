using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class GastoOperacionalItemRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Concepto { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitarioClp { get; set; }
}
