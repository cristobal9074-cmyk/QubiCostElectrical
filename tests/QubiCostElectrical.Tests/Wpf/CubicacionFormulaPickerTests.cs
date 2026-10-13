using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public class CubicacionFormulaPickerTests
{
    public CubicacionFormulaPickerTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void DownAndUp_NavigatesRowsCorrectly_WithU1_U2_Tokens()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var tb = new TextBox { Text = "UN" };
            var rows = new List<CubicacionRowVm>
            {
                new CubicacionRowVm(new CubicacionRow { Unidad = "ML" }, false, () => true),
                new CubicacionRowVm(new CubicacionRow { Unidad = "UN" }, false, () => true),
                new CubicacionRowVm(new CubicacionRow { Unidad = "PZA" }, false, () => true),
            };

            CubicacionFormulaPicker.SetActiveUnidadBox(tb);
            Assert.Equal(-1, CubicacionFormulaPicker.GetCurrentUnidadIndex(tb));

            // Down from initial -> should be row 0 (+U1)
            int maxRow = rows.Count - 1;
            int cur = CubicacionFormulaPicker.GetCurrentUnidadIndex(tb);
            int next = cur < 0 ? 0 : System.Math.Min(cur + 1, maxRow);
            CubicacionFormulaPicker.ApplyUnidadPick(tb, next, rows, null);

            Assert.Equal("+U1", tb.Text);
            Assert.True(rows[0].IsFormulaPickTarget);
            Assert.Equal(0, CubicacionFormulaPicker.GetCurrentUnidadIndex(tb));

            // Down again -> row 1 (+U2)
            cur = CubicacionFormulaPicker.GetCurrentUnidadIndex(tb);
            next = cur < 0 ? 0 : System.Math.Min(cur + 1, maxRow);
            CubicacionFormulaPicker.ApplyUnidadPick(tb, next, rows, null);

            Assert.Equal("+U2", tb.Text);
            Assert.False(rows[0].IsFormulaPickTarget);
            Assert.True(rows[1].IsFormulaPickTarget);
            Assert.Equal(1, CubicacionFormulaPicker.GetCurrentUnidadIndex(tb));

            // Up -> should be row 0 (+U1)
            cur = CubicacionFormulaPicker.GetCurrentUnidadIndex(tb);
            Assert.True(cur > 0);
            CubicacionFormulaPicker.ApplyUnidadPick(tb, cur - 1, rows, null);

            Assert.Equal("+U1", tb.Text);
            Assert.True(rows[0].IsFormulaPickTarget);
            Assert.False(rows[1].IsFormulaPickTarget);

            // Escape -> should cancel and restore "UN"
            CubicacionFormulaPicker.CancelUnidadPick(tb, rows, null);
            Assert.Equal("UN", tb.Text);
            Assert.False(rows[0].IsFormulaPickTarget);
        });
    }

    [Fact]
    public void CancelUnidadPick_DoesNotRestoreFormulaToken()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var tb = new TextBox { Text = "GL" };
            var rows = new List<CubicacionRowVm>
            {
                new CubicacionRowVm(new CubicacionRow { Unidad = "M" }, false, () => true),
                new CubicacionRowVm(new CubicacionRow { Unidad = "KG" }, false, () => true),
            };

            CubicacionFormulaPicker.SetActiveUnidadBox(tb);
            CubicacionFormulaPicker.ApplyUnidadPick(tb, 1, rows, null);
            Assert.Equal("+U2", tb.Text);

            // Re-calling SetActiveUnidadBox when box contains "+U2" must NOT overwrite original "GL"
            CubicacionFormulaPicker.SetActiveUnidadBox(tb);

            CubicacionFormulaPicker.CancelUnidadPick(tb, rows, null);
            Assert.Equal("GL", tb.Text);
            Assert.False(rows[1].IsFormulaPickTarget);
            Assert.Equal(-1, CubicacionFormulaPicker.UnidadPickIndex);
        });
    }

    [Fact]
    public void ResolveUnidadReference_ResolvesU1_And_PlusU2_Correctly()
    {
        var rows = new List<CubicacionRowVm>
        {
            new CubicacionRowVm(new CubicacionRow { Unidad = "ML" }, false, () => true),
            new CubicacionRowVm(new CubicacionRow { Unidad = "PZA" }, false, () => true),
            new CubicacionRowVm(new CubicacionRow { Unidad = "GLOBAL" }, false, () => true),
        };

        Assert.Equal("ML", CubicacionPartidaHelper.ResolveUnidadReference("+U1", rows));
        Assert.Equal("PZA", CubicacionPartidaHelper.ResolveUnidadReference("U2", rows));
        Assert.Equal("GLOBAL", CubicacionPartidaHelper.ResolveUnidadReference("+u3", rows));
        Assert.Equal("PZA", CubicacionPartidaHelper.ResolveUnidadReference("+F2", rows)); // retrocompatible
    }

    [Fact]
    public void DownAndUp_NavigatesCantRows_AndChainsMultipleTokensCorrectly()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var tb = new TextBox { Text = "+" };
            var rows = new List<CubicacionRowVm>
            {
                new CubicacionRowVm(new CubicacionRow { Cantidad = 10m }, false, () => true),
                new CubicacionRowVm(new CubicacionRow { Cantidad = 20m }, false, () => true),
                new CubicacionRowVm(new CubicacionRow { Cantidad = 30m }, false, () => true),
            };

            CubicacionFormulaPicker.SetActiveCantBox(tb);

            // 1. Initial Down from "+" -> "+C1"
            CubicacionFormulaPicker.ApplyCantPick(tb, 0, rows, null);
            Assert.Equal("+C1", tb.Text);
            Assert.True(rows[0].IsCantPickTarget);

            // 2. Down again -> slides to "+C2"
            CubicacionFormulaPicker.ApplyCantPick(tb, 1, rows, null);
            Assert.Equal("+C2", tb.Text);
            Assert.False(rows[0].IsCantPickTarget);
            Assert.True(rows[1].IsCantPickTarget);

            // 3. User types "+" to add another term -> "+C2+"
            tb.Text = "+C2+";
            tb.CaretIndex = tb.Text.Length;

            // 4. Down picks next term starting at row 0 -> "+C2+C1"
            int caret = tb.CaretIndex;
            bool afterOp = (caret > 0 && CubicacionFormulaPicker.IsOperatorChar(tb.Text[caret - 1])) || CubicacionFormulaPicker.EndsWithOperator(tb.Text);
            Assert.True(afterOp);
            CubicacionFormulaPicker.ApplyCantPick(tb, 0, rows, null);
            Assert.Equal("+C2+C1", tb.Text);
            Assert.True(rows[0].IsCantPickTarget);

            // 5. Down again slides the second term -> "+C2+C2" then "+C2+C3"
            CubicacionFormulaPicker.ApplyCantPick(tb, 2, rows, null);
            Assert.Equal("+C2+C3", tb.Text);
            Assert.True(rows[2].IsCantPickTarget);

            // 6. User types "-" -> "+C2+C3-"
            tb.Text = "+C2+C3-";
            tb.CaretIndex = tb.Text.Length;

            // 7. PickCantCell clicking row 0 -> appends C1 -> "+C2+C3-C1"
            CubicacionFormulaPicker.PickCantCell(tb, 0, rows, null, null);
            Assert.Equal("+C2+C3-C1", tb.Text);
            Assert.True(rows[0].IsCantPickTarget);

            // 8. Escape cancels and restores original
            CubicacionFormulaPicker.CancelCantPick(tb, rows, null);
            Assert.False(rows[0].IsCantPickTarget);
            Assert.Equal("+", tb.Text);
        });
    }

    [Fact]
    public void PartidaListItemVm_StoresFormula_AndProducesToolTip()
    {
        var row = new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida Prueba", Cantidad = 30m };
        var vm = new PartidaListItemVm(row);

        Assert.Equal("", vm.CantidadFormula);
        Assert.Contains("Cantidad oficial", vm.CantidadToolTip);

        vm.CantidadFormula = "+C1+C2";
        Assert.Equal("+C1+C2", vm.CantidadFormula);
        Assert.Contains("+C1+C2", vm.CantidadToolTip);
        Assert.Contains("Resultado: 30", vm.CantidadToolTip);
    }
}
