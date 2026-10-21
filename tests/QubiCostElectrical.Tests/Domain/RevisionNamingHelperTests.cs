using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class RevisionNamingHelperTests
{
    [Fact]
    public void SuggestNextRevision_PelFormat_IncrementsLetterFromAToB()
    {
        string folder = Path.Combine("C:", "Presupuestos", "Obra");
        string current = Path.Combine(folder, "PEL039A2026 ABIERTO.qce");
        var suggestion = RevisionNamingHelper.SuggestNextRevision(current);

        Assert.Equal("Rev A", suggestion.CurrentRevisionCode);
        Assert.Equal("Rev B", suggestion.NextRevisionCode);
        Assert.Equal("PEL039B2026 ABIERTO.qce", suggestion.NextSuggestedFileName);
        Assert.Equal("PEL039B2026", suggestion.NextSuggestedProjectCode);
        Assert.Equal(Path.Combine(folder, "PEL039B2026 ABIERTO.qce"), suggestion.NextSuggestedFilePath);
    }

    [Fact]
    public void SuggestNextRevision_PelFormat_IncrementsLetterFromBToC()
    {
        string current = "PEL039B2026 ABIERTO.qce";
        var suggestion = RevisionNamingHelper.SuggestNextRevision(current);

        Assert.Equal("Rev B", suggestion.CurrentRevisionCode);
        Assert.Equal("Rev C", suggestion.NextRevisionCode);
        Assert.Equal("PEL039C2026 ABIERTO.qce", suggestion.NextSuggestedFileName);
        Assert.Equal("PEL039C2026", suggestion.NextSuggestedProjectCode);
    }

    [Fact]
    public void SuggestNextRevision_RevPattern_IncrementsLetter()
    {
        string current = "Presupuesto_Rev_A.qce";
        var suggestion = RevisionNamingHelper.SuggestNextRevision(current);

        Assert.Equal("Rev A", suggestion.CurrentRevisionCode);
        Assert.Equal("Rev B", suggestion.NextRevisionCode);
        Assert.Equal("Presupuesto_Rev_B.qce", suggestion.NextSuggestedFileName);
    }

    [Fact]
    public void SuggestNextRevision_VersionNumberPattern_IncrementsNumber()
    {
        string current = "Hospital_V1.qce";
        var suggestion = RevisionNamingHelper.SuggestNextRevision(current);

        Assert.Equal("V1", suggestion.CurrentRevisionCode);
        Assert.Equal("V2", suggestion.NextRevisionCode);
        Assert.Equal("Hospital_V2.qce", suggestion.NextSuggestedFileName);
    }

    [Fact]
    public void SuggestNextRevision_ZeroPaddedVersion_PreservesPadding()
    {
        string current = "Hospital_V01.qce";
        var suggestion = RevisionNamingHelper.SuggestNextRevision(current);

        Assert.Equal("V1", suggestion.CurrentRevisionCode);
        Assert.Equal("V2", suggestion.NextRevisionCode);
        Assert.Equal("Hospital_V02.qce", suggestion.NextSuggestedFileName);
    }

    [Fact]
    public void SuggestNextRevision_NoVersion_AddsRevB()
    {
        string current = "Edificio Central.qce";
        var suggestion = RevisionNamingHelper.SuggestNextRevision(current);

        Assert.Equal("Rev A", suggestion.CurrentRevisionCode);
        Assert.Equal("Rev B", suggestion.NextRevisionCode);
        Assert.Equal("Edificio Central_Rev_B.qce", suggestion.NextSuggestedFileName);
    }

    [Fact]
    public void SuggestNextRevision_NullOrEmpty_ProvidesSafeDefault()
    {
        var suggestion = RevisionNamingHelper.SuggestNextRevision(null);

        Assert.Equal("Rev A", suggestion.CurrentRevisionCode);
        Assert.Equal("Rev B", suggestion.NextRevisionCode);
        Assert.Equal("Proyecto_Rev_B.qce", suggestion.NextSuggestedFileName);
    }
}
