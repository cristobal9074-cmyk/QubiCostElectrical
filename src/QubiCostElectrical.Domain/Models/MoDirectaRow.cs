using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class MoDirectaRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Cargo { get; set; } = "";
    public int Dotacion { get; set; }
    public decimal SueldoMensualClp { get; set; }
    /// <summary>Dotación por mes (índice 0 = M1). Vacío = usar curva / pico.</summary>
    public List<int> DotacionMensual { get; set; } = new();
}
