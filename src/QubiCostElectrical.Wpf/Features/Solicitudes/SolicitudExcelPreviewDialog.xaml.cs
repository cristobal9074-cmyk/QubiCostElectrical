using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public partial class SolicitudExcelPreviewDialog : Window
{
    public string Correlativo { get; }
    public string Obra { get; }
    public string SolicitadoPor { get; }
    public IReadOnlyList<SolicitudItemRowVm> Materiales { get; }
    public IReadOnlyList<SolicitudItemRowVm> Epps { get; }

    public SolicitudExcelPreviewDialog(
        string correlativo,
        string obra,
        string solicitadoPor,
        IReadOnlyList<SolicitudItemRowVm> materiales,
        IReadOnlyList<SolicitudItemRowVm> epps)
    {
        Correlativo = string.IsNullOrWhiteSpace(correlativo) ? "SM 1" : correlativo;
        Obra = string.IsNullOrWhiteSpace(obra) ? "Sin Obra" : obra;
        SolicitadoPor = string.IsNullOrWhiteSpace(solicitadoPor) ? "No Especificado" : solicitadoPor;
        Materiales = materiales;
        Epps = epps;

        InitializeComponent();
        DataContext = this;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar Solicitud a Excel",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            FileName = $"Solicitud_Materiales_{Obra.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() == true)
        {
            if (OperationFailureMessages.IsFileLocked(dialog.FileName))
            {
                WpfDialogService.ErrorModal("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                return;
            }

            try
            {
                SolicitudesExcelExporter.Export(
                    dialog.FileName,
                    Correlativo,
                    Obra,
                    SolicitadoPor,
                    DateTime.Now.ToString("dd-MM-yyyy"),
                    Materiales,
                    Epps);

                ShellNotificationService.Instance.ShowSuccess("Exportación exitosa", "El archivo Excel se ha exportado y guardado con éxito.");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                WpfDialogService.ErrorModal("Error de Exportación", OperationFailureMessages.ToUserMessage(ex));
            }
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
