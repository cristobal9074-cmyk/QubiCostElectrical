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

public sealed record MaterialPisoDesglose(
    string PisoNombre,
    decimal Cantidad,
    double Porcentaje,
    string Unidad);

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
    IReadOnlyList<MaterialSolicitudUso> Solicitudes,
    IReadOnlyList<MaterialPisoDesglose>? DesglosePisos = null)
{
    public IReadOnlyList<MaterialPisoDesglose> DesglosePisosList => DesglosePisos ?? Array.Empty<MaterialPisoDesglose>();

    public string PisosResumen => DesglosePisosList.Count switch
    {
        0 => "General",
        1 => DesglosePisosList[0].PisoNombre,
        2 => $"{DesglosePisosList[0].PisoNombre}, {DesglosePisosList[1].PisoNombre}",
        _ => $"{DesglosePisosList.Count} pisos / áreas"
    };

    public bool EstaCompleto => CantidadSolicitada >= CantidadCubicada && !TieneSobrepedido && CantidadCubicada > 0m;
    public bool EstaEnProceso => CantidadSolicitada > 0m && CantidadSolicitada < CantidadCubicada;
    public bool SinPedir => CantidadSolicitada == 0m;

    /// <summary>
    /// Alerta visual de sobreconsumo en faena cuando el consumo supera la cubicación presupuestada.
    /// </summary>
    public bool TieneSobreconsumo => TieneSobrepedido || (CantidadSolicitada > CantidadCubicada);
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
