using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Data;
using QubiCostElectrical.Wpf.Controls;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class DataGridSearchOverlayTests
{
    private sealed class TestRowItem
    {
        public string Item { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Unidad { get; set; } = "";
        public decimal Cantidad { get; set; }
    }

    private static void RunOnSta(System.Action action)
    {
        WpfTestHost.EnsureInitialized();
        System.Exception? ex = null;
        var thread = new System.Threading.Thread(() =>
        {
            try { action(); }
            catch (System.Exception e) { ex = e; }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
    }

    [Fact]
    public void Overlay_OpenAndClose_TogglesVisibility()
    {
        RunOnSta(() =>
        {
            var overlay = new DataGridSearchOverlay();
            Assert.False(overlay.IsOpen);

            overlay.Open();
            Assert.True(overlay.IsOpen);

            overlay.Close();
            Assert.False(overlay.IsOpen);
        });
    }

    [Fact]
    public void Overlay_SearchFindsMatchesAcrossColumns_CyclesWithNextAndPrevious()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var col1 = new DataGridTextColumn { Header = "ÍTEM", Binding = new Binding("Item") };
            var col2 = new DataGridTextColumn { Header = "DESCRIPCIÓN", Binding = new Binding("Descripcion") };
            var col3 = new DataGridTextColumn { Header = "UD", Binding = new Binding("Unidad") };
            grid.Columns.Add(col1);
            grid.Columns.Add(col2);
            grid.Columns.Add(col3);

            var items = new List<TestRowItem>
            {
                new() { Item = "1.1", Descripcion = "Alimentador EVA 4mm2", Unidad = "m" },
                new() { Item = "1.2", Descripcion = "Interruptor termomagnético 16A", Unidad = "un" },
                new() { Item = "1.3", Descripcion = "Conductor libre halógeno eva 2.5mm", Unidad = "m" },
                new() { Item = "1.4", Descripcion = "Ducto EMT 25mm", Unidad = "tira" }
            };
            grid.ItemsSource = items;

            var overlay = new DataGridSearchOverlay
            {
                TargetGrid = grid
            };

            // Búsqueda case-insensitive de "eva"
            overlay.Open();
            overlay.SearchBox.Text = "EVA";

            // Debe haber encontrado 2 coincidencias (fila 0 col 2 "Alimentador EVA 4mm2", y fila 2 col 2 "... eva 2.5mm")
            Assert.Contains("1 / 2", overlay.MatchCountText.Text);

            // Avanzar a la siguiente coincidencia
            overlay.FindNext();
            Assert.Equal("2 / 2", overlay.MatchCountText.Text);

            // Wrap around al presionar siguiente
            overlay.FindNext();
            Assert.Equal("1 / 2", overlay.MatchCountText.Text);

            // Retroceder con FindPrevious
            overlay.FindPrevious();
            Assert.Equal("2 / 2", overlay.MatchCountText.Text);
        });
    }

    [Fact]
    public void Overlay_NoResultsQuery_DisplaysSinResultados()
    {
        RunOnSta(() =>
        {
            var grid = new DataGrid();
            var col = new DataGridTextColumn { Header = "DESCRIPCIÓN", Binding = new Binding("Descripcion") };
            grid.Columns.Add(col);

            var items = new List<TestRowItem>
            {
                new() { Descripcion = "Tablero General" }
            };
            grid.ItemsSource = items;

            var overlay = new DataGridSearchOverlay
            {
                TargetGrid = grid
            };

            overlay.Open();
            overlay.SearchBox.Text = "InexistenteXYZ123";

            Assert.Equal("Sin resultados", overlay.MatchCountText.Text);
        });
    }
}
