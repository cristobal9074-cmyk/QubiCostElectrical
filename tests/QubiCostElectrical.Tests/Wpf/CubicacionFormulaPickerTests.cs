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
}
