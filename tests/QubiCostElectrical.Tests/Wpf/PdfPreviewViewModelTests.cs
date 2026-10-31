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
    public void Zoom100Command_ReturnsTo100Percent()
    {
        var vm = new PdfPreviewViewModel();
        vm.ZoomOutCommand.Execute(null);

        vm.Zoom100Command.Execute(null);

        Assert.Equal(1.0, vm.Zoom);
        Assert.Equal("100%", vm.ZoomPercentText);
    }

    [Fact]
    public void FitPageCommand_RaisesRequestFitPageEvent()
    {
        var vm = new PdfPreviewViewModel();
        bool triggered = false;
        vm.RequestFitPage += (_, _) => triggered = true;

        vm.FitPageCommand.Execute(null);

        Assert.True(triggered);
    }

    [Fact]
    public void ToggleTwoPageCommand_SwitchesModesAndLabels()
    {
        var vm = new PdfPreviewViewModel();
        Assert.False(vm.IsTwoPageMode);
        Assert.Equal("2 páginas", vm.TwoPageButtonLabel);

        vm.ToggleTwoPageCommand.Execute(null);
        Assert.True(vm.IsTwoPageMode);
        Assert.Equal("1 página", vm.TwoPageButtonLabel);

        vm.ToggleTwoPageCommand.Execute(null);
        Assert.False(vm.IsTwoPageMode);
        Assert.Equal("2 páginas", vm.TwoPageButtonLabel);
    }

    [Fact]
    public void PageIndicator_ReflectsTwoPageMode()
    {
        var vm = new PdfPreviewViewModel();
        // Reflection to set PageCount for unit testing indicator
        var prop = typeof(PdfPreviewViewModel).GetProperty(nameof(PdfPreviewViewModel.PageCount));
        prop?.SetValue(vm, 8);

        Assert.Equal("1 / 8", vm.PageIndicator);

        vm.IsTwoPageMode = true;
        Assert.Equal("1-2 / 8", vm.PageIndicator);

        vm.NextPageCommand.Execute(null);
        Assert.Equal("3-4 / 8", vm.PageIndicator);

        vm.PreviousPageCommand.Execute(null);
        Assert.Equal("1-2 / 8", vm.PageIndicator);
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
