using QubiCostElectrical.Wpf.Features.Export;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PreviewDocumentSwitchTests
{
    [Fact]
    public void PreviewSwitch_DefaultsToOfferLetter()
    {
        var switcher = new PreviewDocumentSwitch(() => { });

        Assert.Equal(PreviewDocumentSwitch.OfferLetterId, switcher.SelectedId);
    }

    [Fact]
    public void SelectCommand_ChangesSelection_AndFiresCallback()
    {
        var fired = 0;
        var switcher = new PreviewDocumentSwitch(() => fired++);

        switcher.SelectCommand.Execute(PreviewDocumentSwitch.OfferPdfId);

        Assert.Equal(PreviewDocumentSwitch.OfferPdfId, switcher.SelectedId);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SelectCommand_RepeatedSelection_DoesNotFireCallback()
    {
        var fired = 0;
        var switcher = new PreviewDocumentSwitch(() => fired++);

        switcher.SelectCommand.Execute(PreviewDocumentSwitch.OfferPdfId);
        switcher.SelectCommand.Execute(PreviewDocumentSwitch.OfferPdfId);

        Assert.Equal(PreviewDocumentSwitch.OfferPdfId, switcher.SelectedId);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SelectCommand_NullOrBlankId_IsIgnored()
    {
        var fired = 0;
        var switcher = new PreviewDocumentSwitch(() => fired++);

        switcher.SelectCommand.Execute(null);
        switcher.SelectCommand.Execute(string.Empty);
        switcher.SelectCommand.Execute("   ");

        Assert.Equal(PreviewDocumentSwitch.OfferLetterId, switcher.SelectedId);
        Assert.Equal(0, fired);
    }
}
