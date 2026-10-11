using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PdfPreviewViewModelTests
{
    [Fact]
    public void Zoom_DefaultsTo100Percent()
    {
        var vm = new PdfPreviewViewModel();

        Assert.Equal(1.0, vm.Zoom);
        Assert.Equal("100%", vm.ZoomPercentText);
    }

    [Fact]
    public void ZoomIn_StepsThroughPresets_AndClampsAtMax()
    {
        var vm = new PdfPreviewViewModel();

        vm.ZoomInCommand.Execute(null);
        Assert.Equal(1.25, vm.Zoom);
        Assert.Equal("125%", vm.ZoomPercentText);

        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);

        Assert.Equal(3.0, vm.Zoom);
        Assert.Equal("300%", vm.ZoomPercentText);
    }

    [Fact]
    public void ZoomOut_StepsThroughPresets_AndClampsAtMin()
    {
        var vm = new PdfPreviewViewModel();

        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(0.75, vm.Zoom);

        vm.ZoomOutCommand.Execute(null);
        vm.ZoomOutCommand.Execute(null);
        vm.ZoomOutCommand.Execute(null);

        Assert.Equal(0.5, vm.Zoom);
        Assert.Equal("50%", vm.ZoomPercentText);
    }

    [Fact]
    public void ZoomReset_FromAnyLevel_ReturnsTo100Percent()
    {
        var vm = new PdfPreviewViewModel();
        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);

        vm.ZoomResetCommand.Execute(null);

        Assert.Equal(1.0, vm.Zoom);
        Assert.Equal("100%", vm.ZoomPercentText);
    }

    [Fact]
    public void Zoom_RaisesPropertyChanged_ForZoomAndPercentText()
    {
        var vm = new PdfPreviewViewModel();
        var raised = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        vm.ZoomInCommand.Execute(null);

        Assert.Contains(nameof(PdfPreviewViewModel.Zoom), raised);
        Assert.Contains(nameof(PdfPreviewViewModel.ZoomPercentText), raised);
    }
}
