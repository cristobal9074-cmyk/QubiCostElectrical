using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ExportDeliverySupportTests
{
    [Fact]
    public void DocumentSelector_HasSevenDocuments_AllSelectedByDefault()
    {
        var selector = new DeliveryDocumentSelectorVm(WpfAppHost.Current.Dashboard);

        Assert.Equal(7, selector.Documents.Count);
        Assert.All(selector.Documents, d => Assert.True(d.IsSelected));
    }

    [Fact]
    public void DocumentSelector_GetSelectedDocumentIds_ReturnsOnlySelected()
    {
        var selector = new DeliveryDocumentSelectorVm(WpfAppHost.Current.Dashboard);

        selector.Documents[1].IsSelected = false; // OfferLetter
        selector.Documents[3].IsSelected = false; // LetterDocx

        var ids = selector.GetSelectedDocumentIds();

        Assert.Equal(5, ids.Length);
        Assert.Contains("OfferPdf", ids);
        Assert.Contains("ControlExcel", ids);
        Assert.Contains("MemoriaPdf", ids);
        Assert.Contains("ReporteCSEC", ids);
    }

    [Fact]
    public void DocumentSelector_SelectionChanged_FiresEvent()
    {
        var selector = new DeliveryDocumentSelectorVm(WpfAppHost.Current.Dashboard);
        var fired = false;
        selector.SelectionChanged += (_, _) => fired = true;

        selector.Documents[0].IsSelected = false;

        Assert.True(fired);
    }

    [Fact]
    public void ExportResult_ClearResetsState()
    {
        var result = new DeliveryExportResultVm();
        result.SetResults(new[]
        {
            new DeliveryExportResult("OfferPdf", true, "output.pdf")
        });

        Assert.True(result.HasResults);
        Assert.Equal(1, result.SuccessCount);

        result.Clear();

        Assert.False(result.HasResults);
        Assert.Equal(0, result.SuccessCount);
        Assert.Null(result.PreviewFilePath);
    }
}
