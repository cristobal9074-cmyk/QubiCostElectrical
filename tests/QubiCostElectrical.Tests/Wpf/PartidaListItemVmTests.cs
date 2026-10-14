using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class PartidaListItemVmTests
{
    [Fact]
    public void PartidaSinValorizacionGlobal_DetectaFaltaDeCantidadOUnidad()
    {
        var row = new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Alimentador Trifásico",
            Unidad = "M",
            Cantidad = 0m,
            EsTitulo = false
        };

        var vm = new PartidaListItemVm(row, recursoCount: 3);

        Assert.True(vm.SinValorizacionGlobal);
        Assert.True(vm.HasMissingGlobalCantidad);
        Assert.False(vm.HasMissingGlobalUnidad);
        Assert.Equal("Sin cantidad global", vm.StatusBadgeText);

        // Al asignar cantidad mayor a cero, la alerta desaparece
        vm.Cantidad = 55m;
        Assert.False(vm.SinValorizacionGlobal);
        Assert.Equal("Cubicada", vm.StatusBadgeText);

        // Si la unidad queda vacía, la alerta reaparece
        vm.Unidad = "";
        Assert.True(vm.SinValorizacionGlobal);
        Assert.True(vm.HasMissingGlobalUnidad);
        Assert.Equal("Sin unidad global", vm.StatusBadgeText);

        // Si faltan ambas
        vm.Cantidad = 0m;
        Assert.True(vm.SinValorizacionGlobal);
        Assert.True(vm.HasMissingGlobalUnidad);
        Assert.True(vm.HasMissingGlobalCantidad);
        Assert.Equal("Sin unidad ni cant. global", vm.StatusBadgeText);
    }

    [Fact]
    public void PartidaSinCubicacion_NoActivaAlertaDeValorizacionGlobal()
    {
        var row = new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Alimentador",
            Unidad = "",
            Cantidad = 0m,
            EsTitulo = false
        };

        var vm = new PartidaListItemVm(row, recursoCount: 0);

        Assert.False(vm.SinValorizacionGlobal);
        Assert.Equal("Sin cubicación", vm.StatusBadgeText);
    }

    [Fact]
    public void PartidaExcluida_NoActivaAlertaDeValorizacionGlobal()
    {
        var row = new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "No considerada",
            Unidad = "",
            Cantidad = 0m,
            ExcluidoCubicacion = true,
            EsTitulo = false
        };

        var vm = new PartidaListItemVm(row, recursoCount: 5);

        Assert.False(vm.SinValorizacionGlobal);
    }
}
