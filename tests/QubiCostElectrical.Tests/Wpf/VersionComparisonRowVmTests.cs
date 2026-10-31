using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Versions;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class VersionComparisonRowVmTests
{
    [Fact]
    public void AdicionalRow_FormatsDiffVisualsProperly()
    {
        var raw = new BudgetVersionComparisonRow(
            ItemId: "item-1",
            Item: "1.1",
            Descripcion: "Luminaria LED",
            BaselineUf: 0m,
            CurrentUf: 10.5m,
            DeltaUf: 10.5m,
            DeltaPct: 100m,
            Estado: "Adicional")
        {
            Unidad = "UN",
            CantidadBase = 0m,
            CantidadActual = 15m,
            PuUfBase = 0m,
            PuUfActual = 0.7m,
            DiffKind = RevisionDiffKind.Adicional
        };

        var vm = new VersionComparisonRowVm(raw);

        Assert.Equal("+ AGREGADA", vm.DiffStatusBadgeText);
        Assert.True(vm.IsAdicional);
        Assert.False(vm.IsDeductivo);
        Assert.False(vm.IsModificado);

        // Side A (baseline) shows dashes because item did not exist in A
        Assert.Equal("—", vm.ItemA);
        Assert.Equal("— (Partida nueva en Versión B)", vm.DescripcionA);
        Assert.Equal("—", vm.UnidadA);
        Assert.Equal("—", vm.CantidadBaseNumDisplay);
        Assert.Equal("—", vm.TotalBaseDisplay);

        // Side B (current) shows active values
        Assert.Equal("1.1", vm.ItemB);
        Assert.Equal("Luminaria LED", vm.DescripcionB);
        Assert.Equal("UN", vm.UnidadB);
        Assert.Equal("15", vm.CantidadActualNumDisplay);
        Assert.Equal(10.5m.ToString("N3"), vm.TotalActualDisplay);
    }

    [Fact]
    public void DeductivoRow_FormatsDiffVisualsProperly()
    {
        var raw = new BudgetVersionComparisonRow(
            ItemId: "item-2",
            Item: "1.2",
            Descripcion: "Canaleta PVC",
            BaselineUf: 25m,
            CurrentUf: 0m,
            DeltaUf: -25m,
            DeltaPct: -100m,
            Estado: "Deductivo")
        {
            Unidad = "M",
            CantidadBase = 50m,
            CantidadActual = 0m,
            PuUfBase = 0.5m,
            PuUfActual = 0m,
            DiffKind = RevisionDiffKind.Deductivo
        };

        var vm = new VersionComparisonRowVm(raw);

        Assert.Equal("- ELIMINADA", vm.DiffStatusBadgeText);
        Assert.False(vm.IsAdicional);
        Assert.True(vm.IsDeductivo);
        Assert.False(vm.IsModificado);

        // Side A shows baseline values
        Assert.Equal("1.2", vm.ItemA);
        Assert.Equal("Canaleta PVC", vm.DescripcionA);
        Assert.Equal("M", vm.UnidadA);
        Assert.Equal("50", vm.CantidadBaseNumDisplay);
        Assert.Equal(25m.ToString("N3"), vm.TotalBaseDisplay);

        // Side B shows dashes because item was removed in B
        Assert.Equal("—", vm.ItemB);
        Assert.Equal("— (Partida eliminada en Versión B)", vm.DescripcionB);
        Assert.Equal("—", vm.UnidadB);
        Assert.Equal("—", vm.CantidadActualNumDisplay);
        Assert.Equal("—", vm.TotalActualDisplay);
    }

    [Fact]
    public void ModificadoRow_WithPriceDelta_ShowsPriceBadgeAndDeltaPu()
    {
        var raw = new BudgetVersionComparisonRow(
            ItemId: "item-3",
            Item: "2.1",
            Descripcion: "Cable THHN 2.5mm2",
            BaselineUf: 50m,
            CurrentUf: 60m,
            DeltaUf: 10m,
            DeltaPct: 20m,
            Estado: "Modificado")
        {
            Unidad = "M",
            CantidadBase = 100m,
            CantidadActual = 100m,
            PuUfBase = 0.500m,
            PuUfActual = 0.600m,
            DiffKind = RevisionDiffKind.Modificado
        };

        var vm = new VersionComparisonRowVm(raw);

        Assert.Equal("Δ PRECIO", vm.DiffStatusBadgeText);
        Assert.True(vm.IsModificado);
        Assert.True(vm.HasDeltaPu);
        Assert.Contains(0.100m.ToString("N3"), vm.DeltaPuDisplay);
        Assert.EndsWith("UF/u", vm.DeltaPuDisplay);

        // Both sides show values
        Assert.Equal("2.1", vm.ItemA);
        Assert.Equal("2.1", vm.ItemB);
        Assert.Equal("100", vm.CantidadBaseNumDisplay);
        Assert.Equal("100", vm.CantidadActualNumDisplay);
    }

    [Fact]
    public void ModificadoRow_WithQuantityDeltaOnly_ShowsQuantityBadge()
    {
        var raw = new BudgetVersionComparisonRow(
            ItemId: "item-4",
            Item: "2.2",
            Descripcion: "Enchufe doble",
            BaselineUf: 15m,
            CurrentUf: 30m,
            DeltaUf: 15m,
            DeltaPct: 100m,
            Estado: "Modificado")
        {
            Unidad = "UN",
            CantidadBase = 10m,
            CantidadActual = 20m,
            PuUfBase = 1.500m,
            PuUfActual = 1.500m,
            DiffKind = RevisionDiffKind.Modificado
        };

        var vm = new VersionComparisonRowVm(raw);

        Assert.Equal("Δ CANTIDAD", vm.DiffStatusBadgeText);
        Assert.True(vm.IsModificado);
        Assert.False(vm.HasDeltaPu);
        Assert.Empty(vm.DeltaPuDisplay);
    }
}
