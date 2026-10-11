using QubiCostElectrical.Wpf.Features.Export;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ExportViewModelWizardTests
{
    public ExportViewModelWizardTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void Wizard_DefaultStep_IsConfigure()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);

        Assert.True(vm.IsStep1Active);
        Assert.False(vm.IsStep1Completed);
        Assert.Equal("1. Configurar oferta", vm.WizardStepTitle);
        Assert.True(vm.IsConfigureStep);
    }

    [Fact]
    public void Wizard_GoToPreview_UpdatesStepFlags()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("wizard-step");

        var vm = new ExportViewModel(host);
        vm.GoToPreviewStepCommand.Execute(null);

        Assert.True(vm.IsStep2Active);
        Assert.True(vm.IsStep1Completed);
        Assert.Equal("2. Vista previa", vm.WizardStepTitle);
        Assert.True(vm.IsPreviewStep);
    }

    [Fact]
    public void DeliveryPackageText_UsesProperSpanishEncoding()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("wizard-encoding");

        var vm = new ExportViewModel(host);
        vm.Refresh();

        Assert.Contains("cubicaci\u00f3n", vm.DeliveryPackageText);
        Assert.DoesNotContain("\uFFFD", vm.DeliveryPackageText);
    }
}
