using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Navigation;

public sealed class FinalReviewRouteResolverTests
{
    [Theory]
    [InlineData(FinalReviewCategory.FormulaSyntaxError, WorkspaceReviewTarget.Takeoff)]
    [InlineData(FinalReviewCategory.MissingApu, WorkspaceReviewTarget.Apu)]
    [InlineData(FinalReviewCategory.EmptyPartida, WorkspaceReviewTarget.Takeoff)]
    [InlineData(FinalReviewCategory.FechaCorteAntecedentesNoDefinida, WorkspaceReviewTarget.Documents)]
    [InlineData(FinalReviewCategory.MissingCurrencyUf, WorkspaceReviewTarget.Project)]
    public void Resolve_MapsKnownCategories(FinalReviewCategory category, WorkspaceReviewTarget expected)
    {
        Assert.Equal(expected, FinalReviewRouteResolver.Resolve(category));
    }
}
