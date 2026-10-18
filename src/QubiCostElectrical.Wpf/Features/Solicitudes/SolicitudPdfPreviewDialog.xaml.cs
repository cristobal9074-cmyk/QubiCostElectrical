using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public partial class SolicitudPdfPreviewDialog : Window
{
    private readonly string pdfPath;

    public PdfPreviewViewModel PreviewVm { get; } = new();
    public string StatusText => PreviewVm.StatusText;

    public SolicitudPdfPreviewDialog(string pdfPath)
    {
        this.pdfPath = pdfPath;
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await PreviewVm.LoadAsync(pdfPath);
    }

    private void OnPrintClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo(pdfPath)
            {
                Verb = "print",
                CreateNoWindow = true,
                UseShellExecute = true
            };
            proc.Start();
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error de Impresión", "No se pudo enviar a la impresora: " + ex.Message);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar Solicitud de Pedido",
            Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = Path.GetFileName(pdfPath),
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(pdfPath, dialog.FileName, overwrite: true);
                ShellNotificationService.Instance.ShowSuccess("Guardado exitoso", "El archivo se ha guardado exitosamente.");
            }
            catch (Exception ex)
            {
                WpfDialogService.ErrorModal("Error", "Error al guardar el archivo: " + ex.Message);
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
