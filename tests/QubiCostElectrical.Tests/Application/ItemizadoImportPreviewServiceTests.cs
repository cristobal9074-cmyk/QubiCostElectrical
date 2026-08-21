using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoImportPreviewServiceTests
{
    [Fact]
    public void CreateValidatedPreviewRows_flags_duplicate_wbs()
    {
        var source = new[]
        {
            new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "A", Cantidad = 1m },
            new ItemizadoRow { Id = "1.0.00002", Item = "1,1", Descripcion = "B", Cantidad = 1m }
        };

        var rows = ItemizadoImportPreviewService.CreateValidatedPreviewRows(source);

        Assert.All(rows, r => Assert.True(r.IsRejected));
    }

    [Fact]
    public void AcceptRows_skips_rejected()
    {
        var previews = ItemizadoImportPreviewService.CreateValidatedPreviewRows(new[]
        {
            new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "OK", Unidad = "UN", Cantidad = 1m },
            new ItemizadoRow { Id = "1.0.00002", Item = "1,2", Descripcion = "Dup A", Unidad = "UN", Cantidad = 1m },
            new ItemizadoRow { Id = "1.0.00003", Item = "1,2", Descripcion = "Dup B", Unidad = "UN", Cantidad = 1m }
        });

        var accepted = ItemizadoImportPreviewService.AcceptRows(previews);

        Assert.Single(accepted);
        Assert.Equal("OK", accepted[0].Descripcion);
    }
}
