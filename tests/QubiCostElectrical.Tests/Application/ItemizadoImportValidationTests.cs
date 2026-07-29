using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoImportValidationTests
{
    [Fact]
    public void ValidateRowCount_rejects_above_limit()
    {
        string? error = ItemizadoImportValidation.ValidateRowCount(ItemizadoImportValidation.MaxImportRows + 1);
        Assert.NotNull(error);
        Assert.Contains("10.000", error);
    }

    [Fact]
    public void FindDuplicatePartidaCodes_detects_repeated_wbs()
    {
        var rows = new[]
        {
            new ItemizadoRow { Item = "1,1", Descripcion = "A" },
            new ItemizadoRow { Item = "1,1", Descripcion = "B" },
            new ItemizadoRow { Item = "1,2", Descripcion = "C" }
        };

        var dups = ItemizadoImportValidation.FindDuplicatePartidaCodes(rows);
        Assert.Single(dups);
        Assert.Contains("1,1", dups);
    }
}
