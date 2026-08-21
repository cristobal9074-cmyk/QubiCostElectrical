using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Prices;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class PriceBankEntryVmTests
{
    [Fact]
    public void Constructor_MigraEntradaLegadaDeUfAClp()
    {
        var entry = new PriceBankEntry { Codigo = "A", MatUf = 0.1282m, MoUf = 0.0513m };

        var vm = new PriceBankEntryVm(entry, valorUfClp: 39000m);

        Assert.Equal(5000m, vm.PrecioClp);
        Assert.Equal(2001m, vm.MoClp); // 0.0513 * 39000 = 2000.7 -> redondeo AwayFromZero
        Assert.False(vm.HasChanges); // la migración no marca la fila como editada
    }

    [Fact]
    public void PrecioClp_AlEditar_RecalculaUfDeInmediato()
    {
        var vm = new PriceBankEntryVm(new PriceBankEntry { Codigo = "A" }, valorUfClp: 39000m);

        vm.PrecioClp = 7800m;

        Assert.Equal(0.2m, vm.PrecioUfCalculado);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void ValorUfClp_AlCambiar_RecalculaTodasLasFilasSinMarcarCambio()
    {
        var vm = new PriceBankEntryVm(new PriceBankEntry { Codigo = "A", MatClp = 5000m }, valorUfClp: 39000m);
        vm.AcceptChanges();

        vm.ValorUfClp = 40000m;

        Assert.Equal(0.125m, vm.PrecioUfCalculado);
        Assert.False(vm.HasChanges); // solo cambió el valor de UF, no el precio en pesos
    }

    [Fact]
    public void VigenciaKind_MenosDe6Meses_EsVigente()
    {
        var vm = new PriceBankEntryVm(new PriceBankEntry { FechaPrecio = DateTime.Today.AddDays(-30) });
        Assert.Equal("Vigente", vm.VigenciaKind);
        Assert.Equal("Vigente", vm.VigenciaLabel);
    }

    [Fact]
    public void VigenciaKind_6MesesOMas_RequiereRevisar()
    {
        var vm = new PriceBankEntryVm(new PriceBankEntry { FechaPrecio = DateTime.Today.AddDays(-180) });
        Assert.Equal("Revisar", vm.VigenciaKind);
        Assert.Equal("REVISAR", vm.VigenciaLabel);
    }
}
