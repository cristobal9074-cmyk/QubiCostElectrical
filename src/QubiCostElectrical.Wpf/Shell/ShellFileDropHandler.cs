using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

/// <summary>
/// Gestiona el arrastrar y soltar de archivos (.qce y planillas Excel) sobre la ventana principal.
/// </summary>
public sealed class ShellFileDropHandler
{
    private readonly ShellViewModel viewModel;

    public ShellFileDropHandler(ShellViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public static bool HasValidDroppedFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return false;

        return files.Any(f =>
            f.EndsWith(".qce", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));
    }

    public async Task HandleDropAsync(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        var qceFile = files.FirstOrDefault(f => f.EndsWith(".qce", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(qceFile) && File.Exists(qceFile))
        {
            await viewModel.OpenRecentProjectAsync(qceFile);
            return;
        }

        var excelFile = files.FirstOrDefault(f =>
            f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(excelFile) && File.Exists(excelFile))
        {
            await HandleExcelDropAsync(excelFile);
        }
    }

    private async Task HandleExcelDropAsync(string excelFile)
    {
        var fileName = Path.GetFileName(excelFile);

        // Si la vista actual es Itemizado y el ViewModel está activo
        if (viewModel.CurrentRoute == ShellRoute.Itemized && viewModel.CurrentView?.DataContext is ItemizadoViewModel itemizadoVm)
        {
            await itemizadoVm.ImportExcelAsync(excelFile);
            return;
        }

        // Si la vista actual es Banco de Precios
        if (viewModel.CurrentRoute == ShellRoute.Prices && viewModel.CurrentView?.DataContext is PriceBankViewModel priceBankVm)
        {
            priceBankVm.ImportFromFile(excelFile);
            return;
        }

        // Si hay un proyecto abierto pero estamos en otra vista
        if (viewModel.HasOpenProject)
        {
            bool importToItemizado = WpfDialogService.Confirm(
                "Importar archivo Excel",
                $"Ha soltado la planilla: «{fileName}»\n\n¿Desea importarla como partidas en el Itemizado del proyecto actual?\n\n(Haga clic en «Cancelar» si prefiere importarla en el Banco de Precios).",
                isWarning: false);

            if (importToItemizado)
            {
                viewModel.Navigate(ShellRoute.Itemized);
                if (viewModel.CurrentView?.DataContext is ItemizadoViewModel targetItemizadoVm)
                    await targetItemizadoVm.ImportExcelAsync(excelFile);
            }
            else
            {
                viewModel.Navigate(ShellRoute.Prices);
                if (viewModel.CurrentView?.DataContext is PriceBankViewModel targetPricesVm)
                    targetPricesVm.ImportFromFile(excelFile);
            }
            return;
        }

        // Si no hay ningún proyecto abierto
        bool importToGlobalPrices = WpfDialogService.Confirm(
            "Importar banco de precios",
            $"No hay ningún proyecto abierto.\n\n¿Desea importar «{fileName}» en el Banco de Precios global?",
            isWarning: false);

        if (importToGlobalPrices)
        {
            viewModel.Navigate(ShellRoute.Prices);
            if (viewModel.CurrentView?.DataContext is PriceBankViewModel targetPricesVm)
                targetPricesVm.ImportFromFile(excelFile);
        }
    }
}
