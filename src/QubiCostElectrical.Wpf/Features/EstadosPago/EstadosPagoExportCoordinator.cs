using System;
using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadosPago;

/// <summary>
/// Cableado de exportación de la pantalla Estados de pago: reúne el estado del mes
/// seleccionado (Itemizado + sesión), arma el modelo con <see cref="EstadoPagoExportBuilder"/>
/// y pide la ruta con el diálogo de guardado estándar antes de invocar el exportador.
/// Fuera del ViewModel para no consumir presupuesto de líneas del tipo.
/// </summary>
public sealed class EstadosPagoExportCoordinator
{
    private readonly BudgetProjectSession session;
    private readonly ItemizadoPresenter presenter;
    private readonly IItemizadoWorkspaceService itemizadoWorkspace;
    private readonly IEstadoPagoExcelExporter excelExporter;
    private readonly IEstadoPagoPdfExporter pdfExporter;
    private readonly IFileDialogService fileDialog;
    private readonly IFileOpenService fileOpener;

    public EstadosPagoExportCoordinator(
        BudgetProjectSession session,
        ItemizadoPresenter presenter,
        IItemizadoWorkspaceService itemizadoWorkspace,
        IEstadoPagoExcelExporter excelExporter,
        IEstadoPagoPdfExporter pdfExporter,
        IFileDialogService fileDialog,
        IFileOpenService fileOpener)
    {
        this.session = session;
        this.presenter = presenter;
        this.itemizadoWorkspace = itemizadoWorkspace;
        this.excelExporter = excelExporter;
        this.pdfExporter = pdfExporter;
        this.fileDialog = fileDialog;
        this.fileOpener = fileOpener;
    }

    public void ExportarExcel(int mes) =>
        Exportar(mes, "Exportar estado de pago (Excel)", "Libro de Excel (*.xlsx)|*.xlsx", "xlsx", excelExporter.Export);

    public void ExportarPdf(int mes) =>
        Exportar(mes, "Exportar estado de pago (PDF)", "Documento PDF (*.pdf)|*.pdf", "pdf", pdfExporter.Export);

    private void Exportar(
        int mes,
        string titulo,
        string filtro,
        string extension,
        Action<EstadoPagoExportRequest, string> exportar)
    {
        if (mes < 1 || mes > 12)
            return;

        var request = BuildRequest(mes);
        if (request is null)
        {
            WpfDialogService.Warning(
                "Estados de pago",
                $"No hay estado generado para {EstadoPagoExportText.MesNombre(mes)}. Genera el mes antes de exportarlo.");
            return;
        }

        string defaultName = $"Estado-{request.Mes.Folio}.{extension}";
        string? path = fileDialog.PromptSaveFile(titulo, defaultName, filtro);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            exportar(request, path);
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error al exportar", $"No se pudo generar el archivo:\n{ex.Message}");
            return;
        }

        if (WpfDialogService.Confirm(
                "Exportación generada",
                $"El archivo fue generado exitosamente:\n{path}\n\n¿Desea abrirlo ahora?"))
        {
            fileOpener.OpenFile(path);
        }
    }

    private EstadoPagoExportRequest? BuildRequest(int mes)
    {
        var cfg = session.EstadoPagoConfig;
        EstadoPagoMesRow? row = session.EstadoPagoMeses.Find(r =>
            r is not null && r.Anio == cfg.Anio && r.Mes == mes);
        if (row is null)
            return null;

        return EstadoPagoExportBuilder.Build(
            session.ProjectInfo.Nombre,
            cfg,
            row,
            LoadSecciones(),
            session.EstadoPagoMeses,
            session.ProjectInfo.UsuarioNombre,
            AppClock.Now,
            session.ProjectInfo.MonedaPresupuesto,
            session.ValorUfClp);
    }

    /// <summary>Repite el agrupado por capítulos de la pantalla (título → partidas).</summary>
    private List<EstadoPagoExportSeccionInput> LoadSecciones()
    {
        var state = itemizadoWorkspace.BuildPageState(presenter, session);
        var secciones = new List<EstadoPagoExportSeccionInput>();
        var partidas = new List<EstadoPagoPartidaInput>();
        string capitulo = "";

        foreach (var row in state.Rows)
        {
            if (row.EsTitulo)
            {
                if (partidas.Count > 0)
                    secciones.Add(new EstadoPagoExportSeccionInput(capitulo, partidas));
                capitulo = string.IsNullOrWhiteSpace(row.Item) ? row.Descripcion : $"{row.Item} · {row.Descripcion}";
                partidas = new List<EstadoPagoPartidaInput>();
            }
            else
            {
                partidas.Add(new EstadoPagoPartidaInput(row.Id, row.Item, row.Descripcion, row.TotalUf));
            }
        }

        if (partidas.Count > 0)
            secciones.Add(new EstadoPagoExportSeccionInput(capitulo, partidas));

        return secciones;
    }
}
