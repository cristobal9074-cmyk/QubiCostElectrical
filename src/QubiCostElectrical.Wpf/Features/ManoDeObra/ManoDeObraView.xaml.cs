using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public partial class ManoDeObraView : UserControl
{
    private static readonly FontFamily MonoFont = new("Consolas");
    private static readonly FontFamily UiFont = new("Segoe UI");

    private static readonly Color Green = Color.FromRgb(0x15, 0x80, 0x3D);
    private static readonly Color Green2 = Color.FromRgb(0x16, 0xA3, 0x4A);
    private static readonly Color Amber = Color.FromRgb(0x9A, 0x6A, 0x10);
    private static readonly Color Blue = Color.FromRgb(0x25, 0x63, 0xB0);
    private static readonly Color Blue2 = Color.FromRgb(0x3B, 0x82, 0xF6);
    private static readonly Color Purple = Color.FromRgb(0x6B, 0x4F, 0xA8);
    private static readonly Color Ink = Color.FromRgb(0x10, 0x1A, 0x26);
    private static readonly Color Muted = Color.FromRgb(0x7B, 0x87, 0x98);
    private static readonly Color BlueFaint = Color.FromRgb(0xED, 0xF3, 0xFA);
    private static readonly Color PurpleSoft = Color.FromRgb(0xEF, 0xEA, 0xFA);
    private static readonly Color Surface2 = Color.FromRgb(0xF7, 0xF9, 0xFB);
    private static readonly Color Line = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color Line2 = Color.FromRgb(0xED, 0xF0, 0xF5);
    private static readonly Color Navy2 = Color.FromRgb(0x14, 0x26, 0x3C);
    private static readonly Color PeakBg = Color.FromArgb(0x12, 0x15, 0x80, 0x3D);

    private const int ColCargo = 168;
    private const int ColDotPico = 45;
    private const int ColBase = 88;
    private const int ColLeyes = 78;
    private const int ColTotalMes = 96;
    private const int ColMonth = 40;
    private const int ColFinal = 92;
    private const int RowH = 30;
    private const int CellSz = 10;

    private void OnOpenProjection(object sender, RoutedEventArgs e)
    {
        if (DataContext is ManoDeObraViewModel vm)
        {
            BuildProjectionTable(vm, ProjectionTableGrid);
            ProjectionPopup.IsOpen = true;
        }
    }

    private void OnCloseProjection(object sender, RoutedEventArgs e)
    {
        ProjectionPopup.IsOpen = false;
    }

    private void OnAddProjectionTask(object sender, RoutedEventArgs e)
    {
        if (DataContext is ManoDeObraViewModel vm)
        {
            vm.AddProjectionTask();
            BuildProjectionTable(vm, ProjectionTableGrid);
        }
    }

    public void BuildProjectionTable(ManoDeObraViewModel vm, Grid grid)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        var projectionTasks = vm.ProjectionTasks;
        AddCol(grid, 76);
        AddCol(grid, 84);
        foreach (var _ in projectionTasks)
            AddCol(grid, 120);
        AddCol(grid, 90);

        AddRowDef(grid, 34);
        AddCell(grid, 0, 0, "Mes", bold: true, TextAlignment.Left, mono: false, Muted, Surface2);
        AddCell(grid, 0, 1, "Unidad", bold: true, TextAlignment.Left, mono: false, Muted, Surface2);
        for (var j = 0; j < projectionTasks.Length; j++)
            AddCell(grid, 0, 2 + j, projectionTasks[j].Nombre, bold: true, TextAlignment.Center, mono: false, Blue, BlueFaint);
        AddCell(grid, 0, 2 + projectionTasks.Length, "Total mes", bold: true, TextAlignment.Right, mono: false, Muted, Surface2);

        for (var k = 0; k < vm.TotalMonths; k++)
        {
            var row = k + 1;
            AddRowDef(grid, 34);
            AddCell(grid, row, 0, $"M{k + 1}", bold: true, TextAlignment.Left, mono: false, Blue, Surface2);
            AddCell(grid, row, 1, "Personas", bold: false, TextAlignment.Left, mono: false, Muted, Surface2);
            var total = 0;
            for (var l = 0; l < projectionTasks.Length; l++)
            {
                var cantidad = projectionTasks[l].Cantidades.ElementAtOrDefault(k);
                total += cantidad;
                AddCell(grid, row, 2 + l, cantidad.ToString(), bold: true, TextAlignment.Center, mono: true);
            }
            AddCell(grid, row, 2 + projectionTasks.Length, total.ToString(), bold: true, TextAlignment.Right, mono: true, Blue, BlueFaint);
        }

        AddRowDef(grid, 36);
        var totalRow = vm.TotalMonths + 1;
        AddCell(grid, totalRow, 0, "Total", bold: true, TextAlignment.Left, mono: false, Colors.White, Navy2);
        AddCell(grid, totalRow, 1, "", bold: false, TextAlignment.Left, mono: false, null, Navy2);
        for (var m = 0; m < projectionTasks.Length; m++)
            AddCell(grid, totalRow, 2 + m, projectionTasks[m].Cantidades.Sum().ToString(), bold: true, TextAlignment.Center, mono: true, Colors.White, Navy2);
        AddCell(grid, totalRow, 2 + projectionTasks.Length, projectionTasks.Sum(t => t.Cantidades.Sum()).ToString(), bold: true, TextAlignment.Right, mono: true, Colors.White, Navy2);
    }
}
