using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class GastoOperacionalRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Concepto { get; set; } = "";
    public decimal MontoClp { get; set; }
    public decimal Uf { get; set; }
    public string Detalle { get; set; } = "";
    public List<GastoOperacionalItemRow> Items { get; set; } = new();
}
