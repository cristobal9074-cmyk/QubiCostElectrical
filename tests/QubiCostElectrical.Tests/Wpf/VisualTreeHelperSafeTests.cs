using System;
using System.Threading;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Documents;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class VisualTreeHelperSafeTests
{
    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null)
            ExceptionDispatchInfo.Capture(ex).Throw();
    }

    [Fact]
    public void GetParentSafe_WhenRunPassed_DoesNotThrowAndFindsTextBlock()
    {
        RunOnSta(() =>
        {
            var textBlock = new TextBlock();
            var run = new Run("Texto de prueba");
            textBlock.Inlines.Add(run);

            // En WPF clásico, VisualTreeHelper.GetParent(run) lanza InvalidOperationException.
            // GetParentSafe no debe lanzar ninguna excepción.
            var parent = VisualTreeHelperSafe.GetParentSafe(run);

            Assert.NotNull(parent);
            Assert.Same(textBlock, parent);
        });
    }

    [Fact]
    public void FindAncestor_WhenRunInsideCell_FindsDataGridCellSafely()
    {
        RunOnSta(() =>
        {
            var cell = new DataGridCell();
            var textBlock = new TextBlock();
            var run = new Run("123");
            textBlock.Inlines.Add(run);
            cell.Content = textBlock;

            // Debe resolver ancestro tipo TextBlock sin lanzar
            var foundTextBlock = VisualTreeHelperSafe.FindAncestor<TextBlock>(run);
            Assert.Same(textBlock, foundTextBlock);
        });
    }

    [Fact]
    public void GetParentSafe_WhenNull_ReturnsNull()
    {
        Assert.Null(VisualTreeHelperSafe.GetParentSafe(null));
        Assert.Null(VisualTreeHelperSafe.FindAncestor<DataGridCell>(null));
    }
}
