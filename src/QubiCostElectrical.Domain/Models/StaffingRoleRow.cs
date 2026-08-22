using System;

namespace QubiCostElectrical;

/// <summary>
/// Dotación indirecta de un cargo (Administrador, Supervisor, Capataz, etc.) con tarifa mensual
/// y % de dedicación, sostenida durante una cantidad de meses — igual a la hoja "MANO DE OBRA"
/// del Excel de la empresa (fila 4: cargo × dedicación, fila 5: tarifa mensual).
/// </summary>
public sealed class StaffingRoleRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Cargo { get; set; } = "";
    public decimal TarifaMensualClp { get; set; }
    public decimal DedicacionPct { get; set; } = 100m;
    public int Meses { get; set; } = 1;

    /// <summary>Costo total = tarifa mensual × dedicación% × meses.</summary>
    public decimal TotalClp =>
        Math.Max(0m, TarifaMensualClp) * (Math.Clamp(DedicacionPct, 0m, 100m) / 100m) * Math.Max(0, Meses);
}
