using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class MoIndirectaRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Cargo { get; set; } = "";
    public string Tag { get; set; } = "";
    public int Dotacion { get; set; }
    public decimal SueldoMensualClp { get; set; }
    /// <summary>Fracción del sueldo cobrada en esta obra (0..1; 1 = 100%). Retrocompatible: JSON sin el campo = 1.</summary>
    public decimal FraccionObra { get; set; } = 1m;

    /// <summary>Fracción lista para cálculo: recorta valores fuera de rango (0 queda en 0).</summary>
    public decimal FraccionEfectiva => Math.Clamp(FraccionObra, 0m, 1m);
    /// <summary>Dotación por mes (índice 0 = M1). Vacío = usar Dotacion fija todos los meses.</summary>
    public List<int> DotacionMensual { get; set; } = new();
}
