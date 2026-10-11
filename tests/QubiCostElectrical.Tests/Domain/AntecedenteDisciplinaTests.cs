using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class AntecedenteDisciplinaTests
{
    [Theory]
    [InlineData(AntecedenteDisciplina.Electricidad, "ELE-")]
    [InlineData(AntecedenteDisciplina.CorrientesDebiles, "CD-")]
    [InlineData(AntecedenteDisciplina.Telecomunicaciones, "TEL-")]
    [InlineData(AntecedenteDisciplina.EspecificacionesTecnicas, "ET-")]
    [InlineData(AntecedenteDisciplina.Arquitectura, "ARQ-")]
    public void PrefixOf_ReturnsExpected(string disciplina, string prefix)
        => Assert.Equal(prefix, AntecedenteDisciplina.PrefixOf(disciplina));

    [Fact]
    public void ApplyCodigoPrefix_AddsSpecialtyPrefix()
        => Assert.Equal("ELE-PE-01", AntecedenteDisciplina.ApplyCodigoPrefix("PE-01", AntecedenteDisciplina.Electricidad));

    [Fact]
    public void ApplyCodigoPrefix_DoesNotDuplicateSamePrefix()
        => Assert.Equal("CD-AL-02", AntecedenteDisciplina.ApplyCodigoPrefix("CD-AL-02", AntecedenteDisciplina.CorrientesDebiles));

    [Fact]
    public void ApplyCodigoPrefix_ReplacesOtherKnownPrefix()
        => Assert.Equal("TEL-PE-01", AntecedenteDisciplina.ApplyCodigoPrefix("ELE-PE-01", AntecedenteDisciplina.Telecomunicaciones));
}
