using System.Collections.Generic;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ReportTemplateCoverLayoutTests
{
    [Fact]
    public void Resolve_UsesDefaultSectionsWhenListIsEmpty()
    {
        var layout = ReportTemplateCoverLayout.Resolve(new List<string>());

        Assert.True(layout.ShowLogo);
        Assert.Equal(ReportTemplateCoverLayout.DefaultSections, layout.Sections);
    }

    [Fact]
    public void Resolve_RespectsCustomOrderAndIgnoresUnknownValues()
    {
        var layout = ReportTemplateCoverLayout.Resolve(new[]
        {
            "Condiciones",
            "Logo",
            "Cliente",
            "Desconocido",
            "Resumen UF",
            "Cliente"
        });

        Assert.True(layout.ShowLogo);
        Assert.Equal(
            new[]
            {
                ReportCoverSectionKind.Conditions,
                ReportCoverSectionKind.Client,
                ReportCoverSectionKind.Summary
            },
            layout.Sections);
    }

    [Fact]
    public void Resolve_WithoutLogoKeepsBrandImageDisabled()
    {
        var layout = ReportTemplateCoverLayout.Resolve(new[]
        {
            "Proyecto",
            "Cliente",
            "Firma"
        });

        Assert.False(layout.ShowLogo);
        Assert.Equal(
            new[]
            {
                ReportCoverSectionKind.Project,
                ReportCoverSectionKind.Client,
                ReportCoverSectionKind.Signature
            },
            layout.Sections);
    }
}
