using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ValidationServiceTests
{
    [Fact]
    public void ValidateBudget_EmptyProject_ReturnsWarning()
    {
        var service = new ValidationService();

        var result = service.ValidateBudget(new ProjectInfoRow(), new List<ItemizadoRow>(), new List<MaterialRow>(), new List<CubicacionRow>(), new List<TemplateRow>());

        Assert.True(result.HasWarnings);
        Assert.Contains(result.Issues, x => x.Code == "VAL-PRJ-EMPTY");
    }
}
