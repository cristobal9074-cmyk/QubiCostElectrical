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
    public void DownAndUp_NavigatesRowsCorrectly()
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

            // Down from initial -> should be row 0 (+F1)
            int maxRow = rows.Count - 1;
            int cur = CubicacionFormulaPicker.GetCurrentUnidadIndex(tb);
            int next = cur < 0 ? 0 : System.Math.Min(cur + 1, maxRow);
            CubicacionFormulaPicker.ApplyUnidadPick(tb, next, rows, null);

            Assert.Equal("+F1", tb.Text);
            Assert.True(rows[0].IsFormulaPickTarget);
            Assert.Equal(0, CubicacionFormulaPicker.GetCurrentUnidadIndex(tb));

            // Down again -> row 1 (+F2)
            cur = CubicacionFormulaPicker.GetCurrentUnidadIndex(tb);
            next = cur < 0 ? 0 : System.Math.Min(cur + 1, maxRow);
            CubicacionFormulaPicker.ApplyUnidadPick(tb, next, rows, null);

            Assert.Equal("+F2", tb.Text);
            Assert.False(rows[0].IsFormulaPickTarget);
            Assert.True(rows[1].IsFormulaPickTarget);
            Assert.Equal(1, CubicacionFormulaPicker.GetCurrentUnidadIndex(tb));

            // Up -> should be row 0 (+F1)
            cur = CubicacionFormulaPicker.GetCurrentUnidadIndex(tb);
            Assert.True(cur > 0);
            CubicacionFormulaPicker.ApplyUnidadPick(tb, cur - 1, rows, null);

            Assert.Equal("+F1", tb.Text);
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
            Assert.Equal("+F2", tb.Text);

            // Re-calling SetActiveUnidadBox when box contains "+F2" must NOT overwrite original "GL"
            CubicacionFormulaPicker.SetActiveUnidadBox(tb);

            CubicacionFormulaPicker.CancelUnidadPick(tb, rows, null);
            Assert.Equal("GL", tb.Text);
            Assert.False(rows[1].IsFormulaPickTarget);
            Assert.Equal(-1, CubicacionFormulaPicker.UnidadPickIndex);
        });
    }
}
