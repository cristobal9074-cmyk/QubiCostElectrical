using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoImportPreviewPolicyTests
{
    [Fact]
    public void ShouldShowImportPreview_when_small_clean_import_is_false()
    {
        var plan = new ItemizadoImportPlan(
            new[] { new ItemizadoRow { Item = "1,1", Descripcion = "Partida" } },
            1,
            0,
            false,
            0,
            "Importar");

        Assert.False(ItemizadoImportPreviewPolicy.ShouldShowImportPreview(plan));
    }

    [Fact]
    public void ShouldShowImportPreview_when_duplicates_detected()
    {
        var plan = new ItemizadoImportPlan(
            new[]
            {
                new ItemizadoRow { Item = "1,1", Descripcion = "A" },
                new ItemizadoRow { Item = "1,1", Descripcion = "B" }
            },
            2,
            0,
            false,
            0,
            "Importar");

        Assert.True(ItemizadoImportPreviewPolicy.ShouldShowImportPreview(plan, hasDuplicateWbs: true));
    }
}
