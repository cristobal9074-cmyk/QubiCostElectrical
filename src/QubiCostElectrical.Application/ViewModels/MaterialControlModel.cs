using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record MaterialSolicitudUso(
    string SolicitudCodigo,
    string Fecha,
    string SolicitadoPor,
    decimal Cantidad,
    string Unidad,
    bool ProvocoExceso = false,
    string Observaciones = "");

public sealed record MaterialControlRow(
    string Codigo,
    string Descripcion,
    string Unidad,
    string Categoria,
    decimal CantidadCubicada,
    decimal CantidadSolicitada,
    decimal CantidadPendiente,
    decimal CantidadExceso,
    double PorcentajeAvance,
    string EstadoControl,
    bool TieneSobrepedido,
    IReadOnlyList<MaterialSolicitudUso> Solicitudes)
{
    public bool EstaCompleto => CantidadSolicitada >= CantidadCubicada && !TieneSobrepedido && CantidadCubicada > 0m;
    public bool EstaEnProceso => CantidadSolicitada > 0m && CantidadSolicitada < CantidadCubicada;
    public bool SinPedir => CantidadSolicitada == 0m;
}

public sealed record MaterialControlSummary(
    decimal TotalCubicado,
    decimal TotalSolicitado,
    decimal TotalPendiente,
    decimal TotalExceso,
    int TotalMateriales,
    int CantidadSobrepedidos,
    int CantidadPendientes,
    int CantidadCompletos,
    double PorcentajeGlobal);
