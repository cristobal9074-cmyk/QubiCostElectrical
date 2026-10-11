using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Fila de partida del estado de pago, lista para exportar.</summary>
public sealed record EstadoPagoExportFila(
    string Item,
    string Descripcion,
    decimal Monto,
    decimal AvanceAnterior,
    decimal AvanceMes,
    decimal MontoMes,
    decimal AcumPct);

/// <summary>Capítulo del Itemizado con sus partidas (fila agrupadora de la tabla).</summary>
public sealed record EstadoPagoExportSeccion(
    string Capitulo,
    IReadOnlyList<EstadoPagoExportFila> Filas);

/// <summary>Estado del mes seleccionado: encabezado, detalle por capítulo y totales.</summary>
public sealed record EstadoPagoExportMes(
    int Anio,
    int Mes,
    string MesNombre,
    string Folio,
    DateTime FechaEmision,
    string Estado,
    string EstadoLabel,
    string AprobadoPor,
    IReadOnlyList<EstadoPagoExportSeccion> Secciones,
    EstadoPagoMesCalc Totales);

/// <summary>Fila del cronograma anual de estados de pago (12 meses).</summary>
public sealed record EstadoPagoExportCronogramaMes(
    int Mes,
    string MesNombre,
    string Folio,
    string Estado,
    string EstadoLabel,
    decimal? Total);

/// <summary>Modelo de exportación: estado del mes + cronograma anual.</summary>
public sealed record EstadoPagoExportRequest(
    string ProjectTitle,
    EstadoPagoConfig Config,
    EstadoPagoExportMes Mes,
    IReadOnlyList<EstadoPagoExportCronogramaMes> Cronograma,
    string ExportadoPor,
    DateTime ExportadoAt);

/// <summary>Entrada del builder: capítulo del Itemizado con sus partidas base.</summary>
public sealed record EstadoPagoExportSeccionInput(
    string Capitulo,
    IReadOnlyList<EstadoPagoPartidaInput> Partidas);

/// <summary>Textos compartidos de la exportación de estados de pago.</summary>
public static class EstadoPagoExportText
{
    public static readonly string[] MesesNombres =
        { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

    public static string MesNombre(int mes) => mes >= 1 && mes <= 12 ? MesesNombres[mes - 1] : "";

    public static string EstadoLabel(string? estado) => estado switch
    {
        EstadoPagoEst.Borrador => "Borrador",
        EstadoPagoEst.Revision => "Revisión",
        EstadoPagoEst.Pago => "Pago",
        _ => "Pendiente"
    };
}

/// <summary>Exportador Excel del estado de pago (ClosedXML).</summary>
public interface IEstadoPagoExcelExporter
{
    void Export(EstadoPagoExportRequest request, string path);
}

/// <summary>Exportador PDF del estado de pago (QuestPDF).</summary>
public interface IEstadoPagoPdfExporter
{
    void Export(EstadoPagoExportRequest request, string path);
}
