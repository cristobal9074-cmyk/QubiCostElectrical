using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ItemizadoRowVmReadOnlyTests
{
    [Fact]
    public void MutableSetters_DoNotMutateModel_WhenCanEditIsFalse()
    {
        var model = new ItemizadoRow
        {
            Item = "1.1",
            Descripcion = "Original",
            Unidad = "ML",
            Cantidad = 10m,
            EsTitulo = false,
            ExcluidoCubicacion = false
        };
        var vm = new ItemizadoRowVm(model, () => false);

        vm.Descripcion = "Cambiado";
        vm.Cantidad = 99m;
        vm.CantidadText = "50";
        vm.EsTitulo = true;
        vm.ExcluidoCubicacion = true;
        vm.Item = "9.9";
        vm.Unidad = "GL";

        Assert.Equal("Original", model.Descripcion);
        Assert.Equal(10m, model.Cantidad);
        Assert.False(model.EsTitulo);
        Assert.False(model.ExcluidoCubicacion);
        Assert.Equal("1.1", model.Item);
        Assert.Equal("ML", model.Unidad);
    }

    [Fact]
    public void MutableSetters_WriteThrough_WhenCanEditIsTrue()
    {
        var model = new ItemizadoRow
        {
            Item = "1.1",
            Descripcion = "Original",
            Unidad = "ML",
            Cantidad = 10m
        };
        var vm = new ItemizadoRowVm(model, () => true);

        vm.Descripcion = "Nueva";
        vm.Cantidad = 5m;
        vm.Unidad = "UN";

        Assert.Equal("Nueva", model.Descripcion);
        Assert.Equal(5m, model.Cantidad);
        Assert.Equal("UN", model.Unidad);
    }

    [Fact]
    public void MutableSetters_WriteThrough_WhenCanEditIsNull()
    {
        var model = new ItemizadoRow { Descripcion = "A", Cantidad = 1m };
        var vm = new ItemizadoRowVm(model);

        vm.Descripcion = "B";
        vm.Cantidad = 2m;

        Assert.Equal("B", model.Descripcion);
        Assert.Equal(2m, model.Cantidad);
    }
}
