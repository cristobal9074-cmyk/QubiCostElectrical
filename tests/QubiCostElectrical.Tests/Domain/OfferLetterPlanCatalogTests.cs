using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class OfferLetterPlanCatalogTests
{
    [Fact]
    public void Group_SplitsUploadedPlansBySpecialty_AndSkipsEmptyOnes()
    {
        var uploaded = new[]
        {
            new AntecedenteTecnicoRow { Tipo = AntecedenteTecnicoTipo.Plano, Disciplina = AntecedenteDisciplina.Electricidad, Codigo = "3566-IE-001", Revision = "B", Descripcion = "Planta Nivel 1 - Iluminación" },
            new AntecedenteTecnicoRow { Tipo = AntecedenteTecnicoTipo.Plano, Disciplina = AntecedenteDisciplina.CorrientesDebiles, Codigo = "01-PY", Revision = "L2", Descripcion = "Carátula" },
            new AntecedenteTecnicoRow { Tipo = AntecedenteTecnicoTipo.Plano, Disciplina = AntecedenteDisciplina.Telecomunicaciones, Codigo = "TEL-01", Revision = "A", Descripcion = "Rack de telecomunicaciones" },
            new AntecedenteTecnicoRow { Tipo = AntecedenteTecnicoTipo.Consulta, Disciplina = AntecedenteDisciplina.Electricidad, Descripcion = "No es plano" }
        };

        var groups = OfferLetterPlanCatalog.Group(OfferLetterPlanCatalog.Resolve(new OfferPdfSettings(), uploaded));

        Assert.Equal(3, groups.Count);
        Assert.Equal("ANTECEDENTES ELECTRICIDAD", groups[0].Title);
        Assert.Equal("ANTECEDENTES CORRIENTES DEBILES", groups[1].Title);
        Assert.Equal("ANTECEDENTES TELECOMUNICACIONES", groups[2].Title);
        Assert.Contains("3566-IE-001", groups[0].Rows[0].Descripcion);
        Assert.DoesNotContain(groups.SelectMany(g => g.Rows), r => r.Descripcion.Contains("No es plano"));
    }

    [Fact]
    public void Resolve_PrefersTheOfferList_WhenTheUserEditedIt()
    {
        var offer = new OfferPdfSettings
        {
            Planos = { new OfferPlanReferenceRow { Disciplina = "Electricidad", Descripcion = "Solo este" } }
        };
        var uploaded = new[]
        {
            new AntecedenteTecnicoRow { Tipo = AntecedenteTecnicoTipo.Plano, Disciplina = AntecedenteDisciplina.Telecomunicaciones, Descripcion = "Subido" }
        };

        var rows = OfferLetterPlanCatalog.Resolve(offer, uploaded);

        var only = Assert.Single(rows);
        Assert.Equal("Solo este", only.Descripcion);
    }
}
