using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Export;

public partial class ExportView : UserControl
{
    public ExportView()
    {
        InitializeComponent();
    }

    private void OnUserControlKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ExportViewModel vm)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key is Key.D1 or Key.NumPad1)
            {
                if (vm.GoToConfigureStepCommand.CanExecute(null))
                {
                    vm.GoToConfigureStepCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key is Key.D2 or Key.NumPad2)
            {
                if (vm.GoToPreviewStepCommand.CanExecute(null))
                {
                    vm.GoToPreviewStepCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key is Key.D3 or Key.NumPad3)
            {
                if (vm.GoToExportStepCommand.CanExecute(null))
                {
                    vm.GoToExportStepCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.E)
            {
                if (vm.DeliverPackageCommand.CanExecute(null))
                {
                    vm.DeliverPackageCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.S)
            {
                if (vm.SaveOfferSettingsCommand.CanExecute(null))
                {
                    vm.SaveOfferSettingsCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
        else if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (vm.GoToPreviewStepCommand.CanExecute(null) && vm.CurrentStep == OfferWizardStep.Configure)
                vm.GoToPreviewStepCommand.Execute(null);
            else if (vm.GoToExportStepCommand.CanExecute(null))
                vm.GoToExportStepCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (vm.CurrentStep == OfferWizardStep.Export && vm.GoToPreviewStepCommand.CanExecute(null))
                vm.GoToPreviewStepCommand.Execute(null);
            else if (vm.GoToConfigureStepCommand.CanExecute(null))
                vm.GoToConfigureStepCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            if (vm.RefreshCommand.CanExecute(null))
            {
                vm.RefreshCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
