using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class EstadoSolicitud
{
    public const string Borrador = "Borrador";
    public const string Pendiente = "Pendiente";
    public const string Aprobado = "Aprobado";
    public const string Despachado = "Despachado";
}

public sealed class SolicitudPacket
{
    public string Correlativo { get; set; } = "SM 1";
    public string Obra { get; set; } = "";
    public string SolicitadoPor { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Estado { get; set; } = EstadoSolicitud.Borrador;
    public List<SolicitudItemRow> Items { get; set; } = new();
    public string SignatureHash { get; set; } = "";
}
