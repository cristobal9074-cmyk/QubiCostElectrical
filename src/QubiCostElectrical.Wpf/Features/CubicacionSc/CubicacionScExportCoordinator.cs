using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

/// <summary>
/// Coordina la exportación de documentos técnicos de Cubicación SC:
/// Planilla Excel con Cuadro de Cargas SEC y expediente PDF.
/// </summary>
public sealed class CubicacionScExportCoordinator
{
    private readonly ICubicacionSCReportExcelExporter cubScExcel;

    public CubicacionScExportCoordinator(ICubicacionSCReportExcelExporter cubScExcel)
    {
        this.cubScExcel = cubScExcel;
    }

    public void ExportarCuadroSec(BudgetProjectSession session)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Planilla Excel (*.xlsx)|*.xlsx",
                FileName = $"Cuadro_Cargas_SEC_{session.ProjectInfo.Nombre ?? "Proyecto"}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                if (OperationFailureMessages.IsFileLocked(sfd.FileName))
                {
                    WpfDialogService.Error("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                    return;
                }

                var exportContext = new ExportProjectContext
                {
                    Data = session.Data,
                    CurrentProjectPath = session.CurrentProjectPath,
                    ExportedBy = session.CurrentUserName,
                    ExportedAt = AppClock.Now
                };

                cubScExcel.ExportCuadroCargasSec(exportContext, sfd.FileName);
                ShellNotificationService.Instance.ShowSuccess("Cuadro SEC Exportado", $"Cuadro de cargas guardado en {Path.GetFileName(sfd.FileName)}.");
            }
        }
        catch (Exception ex)
        {
            ShellNotificationService.Instance.ShowError("Error al exportar Cuadro SEC", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public void ExportPdf(BudgetProjectSession session, bool isDepto, CubicacionScConfig config)
    {
        try
        {
            var subTitle = isDepto ? "Deptos" : "Generales";
            var sfd = new SaveFileDialog
            {
                Filter = "Archivo PDF (*.pdf)|*.pdf",
                FileName = $"Cubicacion_{subTitle}_{session.ProjectInfo.Nombre ?? "Proyecto"}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                if (OperationFailureMessages.IsFileLocked(sfd.FileName))
                {
                    WpfDialogService.Error("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                    return;
                }

                var cuadrosFiltrados = session.CubicacionSC.Where(c => isDepto ? c.EsDepto : !c.EsDepto).ToList();
                CubicacionScPdfExporter.Export(session.ProjectInfo, cuadrosFiltrados, session.ZonasProyecto, config, sfd.FileName);
                ShellNotificationService.Instance.ShowSuccess("PDF exportado", $"Registro C.SC ({subTitle}) guardado en {Path.GetFileName(sfd.FileName)}.");
            }
        }
        catch (Exception ex)
        {
            ShellNotificationService.Instance.ShowError("Error al exportar PDF", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
