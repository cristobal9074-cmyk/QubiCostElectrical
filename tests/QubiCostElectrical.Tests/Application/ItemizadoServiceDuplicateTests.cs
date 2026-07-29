using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoServiceDuplicateTests
{
    [Fact]
    public void DuplicateAt_inserts_new_child_partida()
    {
        var service = new ItemizadoService();
        var rows = new List<ItemizadoRow>
        {
            new() { Id = "1.0", Item = "1,0", Descripcion = "Cap", EsTitulo = true },
            new() { Id = "1.0.00001", Item = "1.1", Descripcion = "Original", Unidad = "UN", Cantidad = 3m }
        };

        var result = service.DuplicateAt(rows, 1);

        Assert.Equal(3, rows.Count);
        Assert.Equal(2, result.SelectedIndex);
        Assert.NotEqual(rows[1].Id, rows[2].Id);
    }
}
