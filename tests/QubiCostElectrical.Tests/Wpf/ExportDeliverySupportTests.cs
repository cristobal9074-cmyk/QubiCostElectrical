using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ExportDeliverySupportTests
{
    [Fact]
    public void DocumentSelector_HasTenDocuments_OnlyOfferLetterAndResumenExcelSelectedByDefault()
    {
        var selector = new DeliveryDocumentSelectorVm(WpfAppHost.Current.Dashboard);

        Assert.Equal(10, selector.Documents.Count);
        Assert.Equal(4, selector.PrimaryDocuments.Count);
        Assert.Equal(6, selector.OptionalDocuments.Count);

        var selectedIds = selector.GetSelectedDocumentIds();
        Assert.Equal(2, selectedIds.Length);
        Assert.Contains("OfferLetter", selectedIds);
        Assert.Contains("ResumenExcel", selectedIds);
    }

    [Fact]
    public void DocumentSelector_GetSelectedDocumentIds_ReturnsOnlySelected()
    {
        var selector = new DeliveryDocumentSelectorVm(WpfAppHost.Current.Dashboard);

        // Turn on PartidasAbiertasExcel and LetterDocx
        var partidasDoc = selector.Documents.First(d => d.Id == "PartidasAbiertasExcel");
        partidasDoc.IsSelected = true;

        var ids = selector.GetSelectedDocumentIds();

        Assert.Equal(3, ids.Length);
        Assert.Contains("OfferLetter", ids);
        Assert.Contains("ResumenExcel", ids);
        Assert.Contains("PartidasAbiertasExcel", ids);
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
