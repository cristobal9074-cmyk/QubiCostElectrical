using System.ComponentModel;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// CubicacionRowVm no tenía tests propios pese a ser la fila de trabajo de la pantalla más
/// grande y más usada del programa (Cubicación). UfUnit/UfTotal antes recalculaban la misma
/// fórmula que ya vive en CubicacionRow (Domain) con campos privados propios del ViewModel —
/// duplicación que podía divergir en silencio. Ahora delegan directamente en Source.
/// </summary>
public sealed class CubicacionRowVmTests
{
    private static CubicacionRowVm Make(CubicacionRow? source = null) => new(source ?? new CubicacionRow());

    [Fact]
    public void UfUnit_And_UfTotal_MatchSourceDomainCalculation()
    {
        var source = new CubicacionRow { Cantidad = 4m, MatUf = 2.5m, MoUf = 1.5m };
        var vm = new CubicacionRowVm(source);

        Assert.Equal(source.UfUnit, vm.UfUnit);
        Assert.Equal(source.UfTotal, vm.UfTotal);
        Assert.Equal(4m, vm.UfUnit);
        Assert.Equal(16m, vm.UfTotal);
    }

    [Fact]
    public void UfTotal_UpdatesWhenCantidadChanges()
    {
        var source = new CubicacionRow { Cantidad = 1m, MatUf = 2m, MoUf = 0m };
        var vm = new CubicacionRowVm(source);

        vm.Cantidad = 10m;

        Assert.Equal(20m, vm.UfTotal);
        Assert.Equal(20m, source.UfTotal);
    }

    [Fact]
    public void Constructor_CopiesFieldsFromSource()
    {
        var source = new CubicacionRow
        {
            Tipo = "MO",
            Codigo = "COD-1",
            Descripcion = "Descripción",
            Unidad = "ml",
            Cantidad = 3m,
            MatUf = 1m,
            MoUf = 2m,
            Area = "Piso 1",
            NotaInterna = "nota"
        };

        var vm = new CubicacionRowVm(source);

        Assert.Equal("MO", vm.Tipo);
        Assert.Equal("COD-1", vm.Codigo);
        Assert.Equal("Descripción", vm.Descripcion);
        Assert.Equal("ml", vm.Unidad);
        Assert.Equal(3m, vm.Cantidad);
        Assert.Equal("Piso 1", vm.Area);
        Assert.Equal("nota", vm.NotaInterna);
        Assert.Equal(source.Id, vm.Id);
        Assert.Equal(source.ItemId, vm.ItemId);
    }

    [Fact]
    public void Constructor_DefaultsAreaToGeneral_WhenSourceAreaIsBlank()
    {
        var vm = Make(new CubicacionRow { Area = "  " });

        Assert.Equal("General", vm.Area);
    }

    [Fact]
    public void Area_Setter_TrimsAndDefaultsToGeneral_WhenBlank()
    {
        var vm = Make();

        vm.Area = "  Piso 2  ";
        Assert.Equal("Piso 2", vm.Area);
        Assert.Equal("Piso 2", vm.Source.Area);

        vm.Area = "   ";
        Assert.Equal("General", vm.Area);
    }

    [Fact]
    public void Codigo_Setter_NormalizesAndWritesThroughToSource()
    {
        var vm = Make();

        vm.Codigo = "  cod  01  ";

        Assert.Equal("COD 01", vm.Codigo);
        Assert.Equal("COD 01", vm.Source.Codigo);
    }

    [Fact]
    public void Cantidad_MatUf_MoUf_Setters_WriteThroughToSource()
    {
        var vm = Make();

        vm.Cantidad = 5m;
        vm.MatUf = 1.5m;
        vm.MoUf = 0.5m;

        Assert.Equal(5m, vm.Source.Cantidad);
        Assert.Equal(1.5m, vm.Source.MatUf);
        Assert.Equal(0.5m, vm.Source.MoUf);
    }

    [Theory]
    [InlineData("", 0, false)]
    [InlineData("COD-1", 0, true)]
    [InlineData("", 5, true)]
    public void HasContent_ReflectsCodigoOrNonZeroCantidad(string codigo, decimal cantidad, bool expected)
    {
        var vm = Make();
        vm.Codigo = codigo;
        vm.Cantidad = cantidad;

        Assert.Equal(expected, vm.HasContent);
    }

    [Fact]
    public void HasFormula_IsFalse_WhenSourceHasNoExpressions()
    {
        var vm = Make(new CubicacionRow { QtyExpressions = new[] { "", " " }, TotalExpression = "" });

        Assert.False(vm.HasFormula);
    }

    [Fact]
    public void HasFormula_IsTrue_WhenAnyQtyExpressionIsPresent()
    {
        var vm = Make(new CubicacionRow { QtyExpressions = new[] { "", "2+3" } });

        Assert.True(vm.HasFormula);
    }

    [Fact]
    public void HasFormula_IsTrue_WhenTotalExpressionIsPresent()
    {
        var vm = Make(new CubicacionRow { TotalExpression = "10/2" });

        Assert.True(vm.HasFormula);
    }

    [Fact]
    public void ShowPrecioVencidoBadge_IsTrue_OnlyWhenVencidoAndNoError()
    {
        var vm = Make();

        Assert.False(vm.ShowPrecioVencidoBadge);

        vm.PrecioVencido = true;
        Assert.True(vm.ShowPrecioVencidoBadge);

        vm.HasError = true;
        Assert.False(vm.ShowPrecioVencidoBadge);
    }

    [Fact]
    public void ApplyCellValidation_SetsErrorsAndGetCellError_ResolvesByColumnKey()
    {
        var vm = Make();

        vm.ApplyCellValidation("err-codigo", "err-desc", "err-cant", "err-unidad", "err-area");

        Assert.Equal("err-codigo", vm.GetCellError("Codigo"));
        Assert.Equal("err-codigo", vm.GetCellError("CÓDIGO"));
        Assert.Equal("err-desc", vm.GetCellError("Descripcion"));
        Assert.Equal("err-cant", vm.GetCellError("Cantidad"));
        Assert.Equal("err-unidad", vm.GetCellError("Unidad"));
        Assert.Equal("err-area", vm.GetCellError("Area"));
        Assert.Null(vm.GetCellError("columna-inexistente"));
    }

    [Fact]
    public void CantidadDisplay_RoundsToNearestInteger_NoDecimals()
    {
        var vm = Make();

        vm.Cantidad = 6.6667m;

        Assert.Equal("7", vm.CantidadDisplay);
    }

    [Fact]
    public void UfTotalDisplay_FormatsWithThreeDecimals()
    {
        var vm = Make(new CubicacionRow { Cantidad = 2m, MatUf = 1.23456m, MoUf = 0m });

        // Política monetaria: la UF se muestra con 3 decimales.
        Assert.Equal("2,469", vm.UfTotalDisplay);
    }

    [Fact]
    public void FormulaPickTargetOffsets_AddAndClear_RaisePropertyChanged()
    {
        var vm = Make();
        var raised = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CubicacionRowVm.FormulaPickTargetOffsets)) raised++;
        };

        vm.AddFormulaPickTargetOffset(5);
        vm.AddFormulaPickTargetOffset(5); // duplicado: no debe volver a notificar
        vm.AddFormulaPickTargetOffset(7);

        Assert.Equal(new[] { 5, 7 }, vm.FormulaPickTargetOffsets.OrderBy(x => x));
        Assert.Equal(2, raised);

        vm.ClearFormulaPickTargetOffsets();
        Assert.Empty(vm.FormulaPickTargetOffsets);
        Assert.Equal(3, raised);
    }

    [Fact]
    public void SectorValues_Initialized_WithSectorWindowSizeEmptySlots()
    {
        var vm = Make();

        Assert.Equal(CubicacionPageVm.SectorWindowSize, vm.SectorValues.Count);
        Assert.All(vm.SectorValues, v => Assert.Equal("", v));
    }

    [Fact]
    public void SectorExpressionChanged_Fires_WhenASectorValueIsReplaced()
    {
        var vm = Make();
        (CubicacionRowVm Vm, int Slot, string Text)? captured = null;
        vm.SectorExpressionChanged = (rowVm, slot, text) => captured = (rowVm, slot, text);

        vm.SectorValues[2] = "2+3";

        Assert.NotNull(captured);
        Assert.Same(vm, captured!.Value.Vm);
        Assert.Equal(2, captured.Value.Slot);
        Assert.Equal("2+3", captured.Value.Text);
    }

    [Fact]
    public void LoadSectorWindow_DoesNotFireSectorExpressionChanged()
    {
        var source = new CubicacionRow { QtyExpressions = new[] { "1", "2", "3" } };
        var vm = new CubicacionRowVm(source);
        var fired = false;
        vm.SectorExpressionChanged = (_, _, _) => fired = true;

        vm.LoadSectorWindow(0);

        Assert.False(fired);
        Assert.Equal("1", vm.SectorValues[0]);
        Assert.Equal("2", vm.SectorValues[1]);
        Assert.Equal("3", vm.SectorValues[2]);
    }

    [Fact]
    public void MutableSetters_DoNotMutateSource_WhenCanEditIsFalse()
    {
        var source = new CubicacionRow
        {
            Tipo = "MAT",
            Codigo = "COD-1",
            Descripcion = "Original",
            Unidad = "ML",
            Cantidad = 4m,
            MatUf = 1m,
            MoUf = 2m,
            Area = "Piso 1",
            NotaInterna = "nota"
        };
        var vm = new CubicacionRowVm(source, canEdit: () => false);

        vm.Codigo = "OTRO";
        vm.Cantidad = 99m;
        vm.Descripcion = "Cambiado";
        vm.Tipo = "MO";
        vm.Unidad = "GL";
        vm.MatUf = 10m;
        vm.MoUf = 20m;
        vm.MatClp = 1000m;
        vm.MoClp = 2000m;
        vm.Area = "Piso 9";
        vm.NotaInterna = "otra";

        Assert.Equal("COD-1", source.Codigo);
        Assert.Equal(4m, source.Cantidad);
        Assert.Equal("Original", source.Descripcion);
        Assert.Equal("MAT", source.Tipo);
        Assert.Equal("ML", source.Unidad);
        Assert.Equal(1m, source.MatUf);
        Assert.Equal(2m, source.MoUf);
        Assert.Equal(0m, source.MatClp);
        Assert.Equal(0m, source.MoClp);
        Assert.Equal("Piso 1", source.Area);
        Assert.Equal("nota", source.NotaInterna);
    }

    [Fact]
    public void MutableSetters_WriteThroughToSource_WhenCanEditIsTrue()
    {
        var source = new CubicacionRow
        {
            Codigo = "COD-1",
            Descripcion = "Original",
            Cantidad = 4m
        };
        var vm = new CubicacionRowVm(source, canEdit: () => true);

        vm.Codigo = "OTRO";
        vm.Cantidad = 8m;
        vm.Descripcion = "Nueva";

        Assert.Equal("OTRO", source.Codigo);
        Assert.Equal(8m, source.Cantidad);
        Assert.Equal("Nueva", source.Descripcion);
    }

    [Fact]
    public void SectorExpressionChanged_DoesNotFire_WhenCanEditIsFalse()
    {
        var vm = new CubicacionRowVm(new CubicacionRow(), canEdit: () => false);
        var fired = false;
        vm.SectorExpressionChanged = (_, _, _) => fired = true;

        vm.SectorValues[0] = "2+3";

        Assert.False(fired);
    }

    [Fact]
    public void Codigo_Clearing_ResetsDescriptionUnitAndPrices()
    {
        var vm = Make();
        vm.Codigo = "M001";
        vm.Descripcion = "Cable";
        vm.Unidad = "ML";
        vm.MatClp = 1000m;
        vm.MoClp = 500m;

        // Act
        vm.Codigo = "";

        // Assert
        Assert.Equal("", vm.Codigo);
        Assert.Equal("", vm.Descripcion);
        Assert.Equal("", vm.Unidad);
        Assert.Equal(0m, vm.MatClp);
        Assert.Equal(0m, vm.MoClp);
        Assert.Equal(0m, vm.MatUf);
        Assert.Equal(0m, vm.MoUf);
    }
}
