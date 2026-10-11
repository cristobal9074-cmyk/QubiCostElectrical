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

    // ── M.O. derivada: precedencia rendimiento > porcentaje > directa ────────

    [Fact]
    public void MoDerivadaClp_SinFormula_RetornaMoDirecto()
    {
        var vm = new PriceBankEntryVm(new PriceBankEntry { Codigo = "A", MoClp = 350m });
        Assert.Equal(350m, vm.MoDerivadaClp);
    }

    [Fact]
    public void MoDerivadaClp_ModoRendimiento_UsaTarifaSobreRendimiento()
    {
        var entry = new PriceBankEntry { Codigo = "A", MoClp = 100m, RendimientoDh = 464m };
        var vm = new PriceBankEntryVm(entry, modoRendimiento: true, tarifaDhClp: 42955m);
        Assert.Equal(92.58m, vm.MoDerivadaClp);
    }

    [Fact]
    public void MoDerivadaClp_FormulaPorcentaje_MultiplicaPrecioPctFaDefault()
    {
        var entry = new PriceBankEntry
        {
            Codigo = "A",
            MatClp = 1000m,
            MoClp = 123m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        };
        var vm = new PriceBankEntryVm(entry); // FA default 62,80
        Assert.Equal(1570m, vm.MoDerivadaClp);
    }

    [Fact]
    public void MoDerivadaClp_FormulaPorcentaje_FaPersonalizado()
    {
        var entry = new PriceBankEntry
        {
            Codigo = "A",
            MatClp = 1000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        };
        var vm = new PriceBankEntryVm(entry, indiceMoFa: 50m);
        Assert.Equal(1250m, vm.MoDerivadaClp);
    }

    [Fact]
    public void MoDerivadaClp_ModoRendimientoYPorcentaje_PriorizaRendimiento()
    {
        var entry = new PriceBankEntry
        {
            Codigo = "A",
            MatClp = 1000m,
            MoClp = 123m,
            RendimientoDh = 464m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        };
        var vm = new PriceBankEntryVm(entry, modoRendimiento: true, tarifaDhClp: 42955m);
        Assert.Equal(92.58m, vm.MoDerivadaClp);
    }

    [Fact]
    public void MoEsPorcentaje_AlAlternar_MarcaCambioYActualizaFormula()
    {
        var vm = new PriceBankEntryVm(new PriceBankEntry { Codigo = "A" });

        vm.MoEsPorcentaje = true;

        Assert.Equal(MaterialMoResolver.MoFormulaPorcentaje, vm.Source.MoFormulaTipo);
        Assert.True(vm.HasChanges);

        vm.MoEsPorcentaje = false;
        Assert.Equal("", vm.Source.MoFormulaTipo);
    }

    [Fact]
    public void MoEsPorcentaje_SetterBloqueado_WhenReadOnly()
    {
        var entry = new PriceBankEntry { Codigo = "A" };
        var vm = new PriceBankEntryVm(entry, valorUfClp: 0m, canEdit: () => false);

        vm.MoEsPorcentaje = true;
        vm.MoPorcentajeMaterial = 9m;

        Assert.Equal("", entry.MoFormulaTipo);
        Assert.Equal(2.5m, entry.MoPorcentajeMaterial);
        Assert.False(vm.HasChanges);
    }
}
