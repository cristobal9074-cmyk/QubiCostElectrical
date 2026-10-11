using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ItemizadoRowVmErgonomicsTests
{
    [Theory]
    [InlineData("1", true, 0)]
    [InlineData("1.1", false, 1)]
    [InlineData("1,1", false, 1)]
    [InlineData("1,2,1", false, 2)]
    [InlineData("1.2.1", false, 2)]
    [InlineData("1,2,3,4", false, 3)]
    public void WbsLevel_SupportsBothDotsAndCommas(string item, bool isTitle, int expectedLevel)
    {
        var model = new ItemizadoRow { Item = item, EsTitulo = isTitle };
        var vm = new ItemizadoRowVm(model);

        Assert.Equal(expectedLevel, vm.WbsLevel);
    }

    [Theory]
    [InlineData("12.50", 12.5)]
    [InlineData("12,50", 12.5)]
    [InlineData("100", 100)]
    [InlineData("0,75", 0.75)]
    public void CantidadText_AcceptsBothDotsAndCommas(string input, decimal expected)
    {
        var model = new ItemizadoRow { Cantidad = 1m };
        var vm = new ItemizadoRowVm(model);

        vm.CantidadText = input;

        Assert.Equal(expected, vm.Cantidad);
    }

    [Fact]
    public void ChapterCollapse_HidesChildPartidasCorrectly()
    {
        var cap1 = new ItemizadoRowVm(new ItemizadoRow { Item = "1", EsTitulo = true });
        var part11 = new ItemizadoRowVm(new ItemizadoRow { Item = "1.1", EsTitulo = false });
        var part12 = new ItemizadoRowVm(new ItemizadoRow { Item = "1.2", EsTitulo = false });
        var cap2 = new ItemizadoRowVm(new ItemizadoRow { Item = "2", EsTitulo = true });
        var part21 = new ItemizadoRowVm(new ItemizadoRow { Item = "2.1", EsTitulo = false });

        var rows = new List<ItemizadoRowVm> { cap1, part11, part12, cap2, part21 };

        // Initial state: none collapsed
        ItemizadoCollapseCoordinator.UpdateHierarchy(rows);
        Assert.False(cap1.IsHiddenByParentCollapse);
        Assert.False(part11.IsHiddenByParentCollapse);
        Assert.False(part12.IsHiddenByParentCollapse);
        Assert.False(cap2.IsHiddenByParentCollapse);
        Assert.False(part21.IsHiddenByParentCollapse);

        // Collapse Chapter 1
        ItemizadoCollapseCoordinator.ToggleChapter(cap1, rows);
        Assert.True(cap1.IsCollapsed);
        Assert.Equal("▶", cap1.CollapseIcon);
        Assert.False(cap1.IsHiddenByParentCollapse);
        Assert.True(part11.IsHiddenByParentCollapse);
        Assert.True(part12.IsHiddenByParentCollapse);
        Assert.False(cap2.IsHiddenByParentCollapse);
        Assert.False(part21.IsHiddenByParentCollapse);

        // Re-expand Chapter 1
        ItemizadoCollapseCoordinator.ToggleChapter(cap1, rows);
        Assert.False(cap1.IsCollapsed);
        Assert.Equal("▼", cap1.CollapseIcon);
        Assert.False(part11.IsHiddenByParentCollapse);
        Assert.False(part12.IsHiddenByParentCollapse);
    }

    [Fact]
    public void Observaciones_BindsToModel_AndNotifiesTooltips()
    {
        var model = new ItemizadoRow { Item = "1.1", Observaciones = "" };
        var vm = new ItemizadoRowVm(model);

        Assert.Equal("", vm.Observaciones);
        Assert.False(vm.HasObservaciones);
        Assert.Null(vm.ObservacionesToolTip);

        bool obsChanged = false;
        bool hasObsChanged = false;
        bool tipChanged = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.Observaciones)) obsChanged = true;
            if (e.PropertyName == nameof(vm.HasObservaciones)) hasObsChanged = true;
            if (e.PropertyName == nameof(vm.ObservacionesToolTip)) tipChanged = true;
        };

        vm.Observaciones = "Canalización embutida en muro hormigón";

        Assert.Equal("Canalización embutida en muro hormigón", vm.Observaciones);
        Assert.Equal("Canalización embutida en muro hormigón", model.Observaciones);
        Assert.True(vm.HasObservaciones);
        Assert.Equal("Canalización embutida en muro hormigón", vm.ObservacionesToolTip);
        Assert.True(obsChanged);
        Assert.True(hasObsChanged);
        Assert.True(tipChanged);
    }

    [Fact]
    public void Observaciones_WhenReadOnly_DoesNotMutate()
    {
        var model = new ItemizadoRow { Item = "1.1", Observaciones = "Original" };
        var vm = new ItemizadoRowVm(model, canEdit: () => false);

        vm.Observaciones = "Nuevo texto";

        Assert.Equal("Original", vm.Observaciones);
        Assert.Equal("Original", model.Observaciones);
    }
}
