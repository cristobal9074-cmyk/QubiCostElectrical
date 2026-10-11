using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Estados conocidos de un estado de pago mensual.</summary>
public static class EstadoPagoEst
{
    public const string Borrador = "borrador";
    public const string Revision = "revision";
    public const string Pago = "pago";
}

/// <summary>
/// Estado de pago de un mes del año. La ausencia de fila = mes <c>pendiente</c>
/// (aún no generado). El avance se guarda por partida como % del mes; el
/// acumulado se deriva sumando los avances de los meses generados anteriores.
/// </summary>
public sealed class EstadoPagoMesRow
{
    public string Id { get; set; } = "";
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Estado { get; set; } = EstadoPagoEst.Borrador;
    public string Folio { get; set; } = "";
    public DateTime FechaEmision { get; set; } = AppClock.Now;
    public string AprobadoPor { get; set; } = "";
    public DateTime? FechaAprobacion { get; set; }

    /// <summary>PartidaId/Item → % de avance del mes (0–100).</summary>
    public Dictionary<string, decimal> AvancesPorItem { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string BuildId(int anio, int mes) => $"{anio:0000}-{mes:00}";

    public static string BuildFolio(int anio, int mes) => $"EP-{anio:0000}-{mes:00}";
}
