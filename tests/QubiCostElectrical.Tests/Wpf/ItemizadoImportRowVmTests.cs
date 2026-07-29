using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ItemizadoImportRowVmTests
{
    [Fact]
    public void Revalidate_rejects_non_positive_quantity()
    {
        var row = new ItemizadoImportRowVm(new ItemizadoRow
        {
            Id = "1.0.00001",
            Item = "1,1",
            Descripcion = "Partida",
            Cantidad = -1m
        });

        Assert.True(row.IsRejected);
        Assert.Contains("Cantidad", row.RejectionReason);
    }

    [Fact]
    public void ToRow_preserves_partida_id()
    {
        var partidaId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var vm = new ItemizadoImportRowVm(new ItemizadoRow
        {
            PartidaId = partidaId,
            Id = "1.0.00001",
            Item = "1,1",
            Descripcion = "Partida",
            Cantidad = 2m
        });

        var exported = vm.ToRow();
        Assert.Equal(partidaId, exported.PartidaId);
    }

    [Fact]
    public void Revalidate_rejects_duplicate_wbs()
    {
        var row = new ItemizadoImportRowVm(new ItemizadoRow
        {
            Id = "1.0.00001",
            Item = "1,1",
            Descripcion = "Partida",
            Cantidad = 1m
        });
        row.SetDuplicateWbs(true);

        Assert.True(row.IsRejected);
        Assert.Contains("duplicado", row.RejectionReason, StringComparison.OrdinalIgnoreCase);
    }
}
