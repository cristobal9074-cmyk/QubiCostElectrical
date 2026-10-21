using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Tests.Fixtures;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed partial class CubicacionViewModelTests
{
    [Fact]
    public void PasteClipboardDirect_ValidTabSeparatedRows_AddsRowsAndMarksDirty()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-paste-direct");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t15\nPVC25\tTubo PVC 25mm\tML\t8");

        Assert.Equal(2, host.Session.Cubicacion.Count);
        Assert.Contains(host.Session.Cubicacion, r => r.Codigo == "PVC20" && r.Cantidad == 15m);
        Assert.Contains(host.Session.Cubicacion, r => r.Codigo == "PVC25" && r.Cantidad == 8m);
        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void PasteClipboardDirect_FormulaLikeQuantity_StoresExpressionAndEvaluates()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-paste-formula");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t10+2");

        var row = Assert.Single(host.Session.Cubicacion);
        Assert.Equal("10+2", row.TotalExpression);
        Assert.Equal(12m, row.Cantidad);
    }

    [Fact]
    public void PasteClipboardDirect_AllRowsRejected_AddsNothingAndWarns()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-paste-rejected");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t@@@");

        Assert.Empty(host.Session.Cubicacion);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void PasteClipboardDirect_MixedRows_ImportsOnlyAccepted()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-paste-mixed");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t15\nPVC25\tTubo PVC 25mm\tML\t@@@");

        var row = Assert.Single(host.Session.Cubicacion);
        Assert.Equal("PVC20", row.Codigo);
    }

    [Fact]
    public void PasteClipboardDirect_ReadOnlySession_DoesNotAddRows()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-paste-readonly");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        host.Session.SetRoleReadOnly(true, "Prueba solo lectura");

        try
        {
            vm.PasteClipboardDirect("PVC20\tTubo PVC 20mm\tML\t15");

            Assert.Empty(host.Session.Cubicacion);
        }
        finally
        {
            // Restaurar: la sesión es compartida por toda la colección WpfHost.
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void PasteClipboardDirect_BlankText_DoesNothing()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-paste-blank");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        vm.PasteClipboardDirect("   ");

        Assert.Empty(host.Session.Cubicacion);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public async Task SearchMaterialsAsync_ShortQuery_HidesAutocompleteWithHint()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-autocomplete-short");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        await vm.SearchMaterialsAsync("P");

        Assert.False(vm.AutocompleteVisible);
        Assert.Contains("al menos 2 caracteres", vm.AutocompleteStatusText);
    }

    [Fact]
    public async Task SearchMaterialsAsync_ExactCodeMatch_ReturnsSingleResultWithExactStatus()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-autocomplete-exact");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });
        host.Session.Materiales.Add(new MaterialRow { Codigo = "PVC20", Descripcion = "Tubo PVC 20mm", Unidad = "ML" });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        await vm.SearchMaterialsAsync("PVC20");

        Assert.True(vm.AutocompleteVisible);
        Assert.Single(vm.AutocompleteResults);
        Assert.Equal("PVC20", vm.AutocompleteFirstItem?.Codigo);
        Assert.Contains("Coincidencia exacta", vm.AutocompleteStatusText);
        Assert.Null(vm.QuickAddCodigoDraft);
    }

    [Fact]
    public async Task SearchMaterialsAsync_NoMatches_OffersQuickAddDraft()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-autocomplete-nomatch");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        await vm.SearchMaterialsAsync("NOEXISTE99");

        Assert.True(vm.AutocompleteVisible);
        Assert.Empty(vm.AutocompleteResults);
        Assert.Equal("NOEXISTE99", vm.QuickAddCodigoDraft);
    }

    [Fact]
    public void HideAutocomplete_HidesResultsList()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-autocomplete-hide");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        vm.AutocompleteVisible = true;

        vm.HideAutocomplete();

        Assert.False(vm.AutocompleteVisible);
    }

    [Fact]
    public async Task NotifyCodigoChangedAsync_CodeNotInCatalog_FlagsRowError()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-autocomplete-notify");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "C1", ItemId = "P1", Tipo = "MAT", Codigo = "NOEXISTE", Descripcion = "X", Unidad = "ML", Cantidad = 1m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = Assert.Single(vm.Rows, r => r.Codigo == "NOEXISTE");

        await vm.NotifyCodigoChangedAsync(row, "NOEXISTE");

        Assert.True(row.HasError);
        Assert.Contains("no existe en el catálogo", row.ErrorMessage);
    }

    [Fact]
    public async Task NotifyCodigoChangedAsync_CodeInCatalog_ClearsRowError()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-autocomplete-notify-ok");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });
        host.Session.Materiales.Add(new MaterialRow { Codigo = "PVC20", Descripcion = "Tubo PVC 20mm", Unidad = "ML" });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "C1", ItemId = "P1", Tipo = "MAT", Codigo = "PVC20", Descripcion = "X", Unidad = "ML", Cantidad = 1m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = Assert.Single(vm.Rows, r => r.Codigo == "PVC20");

        await vm.NotifyCodigoChangedAsync(row, "PVC20");

        Assert.False(row.HasError);
        Assert.Equal("", row.ErrorMessage);
    }

    [Fact]
    public void DuplicateRowCommand_OnEmptyPlaceholderRow_DoesNothing()
    {
        // Regresión: duplicar una fila placeholder (casilla vacía, nunca confirmada) hacía
        // que session.Cubicacion.IndexOf(src.Source) devolviera -1, insertando el clon en la
        // posición 0 de TODO el proyecto en vez de junto a la fila origen de esta partida.
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-duplicate-placeholder");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var placeholder = vm.Rows.First(r => !r.IsSessionBacked);
        vm.SelectedRow = placeholder;

        vm.DuplicateRowCommand.Execute(null);

        Assert.Empty(host.Session.Cubicacion);
        Assert.Equal(8, vm.Rows.Count);
    }

    [Fact]
    public void MedicionesAgregar_OnPlaceholderRow_PersistsRowToSession()
    {
        // Regresión: agregar una línea de medición a una fila placeholder creaba un
        // MeasurementLineRow referenciando un CubicacionRow.Id que nunca llegaba a
        // session.Cubicacion — una medición huérfana.
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-mediciones-placeholder");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 10m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var placeholder = vm.Rows.First(r => !r.IsSessionBacked);
        vm.SelectedRow = placeholder;

        vm.RightPanel.Mediciones.AgregarCommand.Execute(null);

        Assert.True(placeholder.IsSessionBacked);
        Assert.Contains(placeholder.Source, host.Session.Cubicacion);
        Assert.Single(host.Session.Mediciones, m => m.CubicacionRowId == placeholder.Source.Id);
    }

    [Fact]
    public void TryPasteAnchored_BatchExecution_ExecutesSuccessfully()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var host = WpfAppHost.Current;
                host.Session.NewProject("try-paste-anchored");
                host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "GL", Cantidad = 1m });

                var vm = new CubicacionPageVm(new FakeShell(), host);
                vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
                var row = vm.Rows.First();

                // Simular pegado de 2 filas
                var text = "COD1\tDesc1\tUN\t15\nCOD2\tDesc2\tUN\t25";

                // Simular el foco de la grilla en la celda del primer elemento (CodigoColumn)
                vm.SelectedRow = row;

                // Creamos un DataGrid de prueba en WPF para pasar el parámetro
                var grid = new System.Windows.Controls.DataGrid();
                grid.ItemsSource = vm.Rows;
                grid.SelectedItem = row;

                var col = new System.Windows.Controls.DataGridTextColumn { Header = "CÓDIGO" };
                grid.Columns.Add(col);
                grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(row, col);

                bool success = vm.TryPasteAnchored(grid, text);

                Assert.True(success);
                Assert.Equal(8, vm.Rows.Count); // El tamaño mínimo de la partida sigue siendo MinVisibleRows (8)
                Assert.Equal("COD1", vm.Rows[0].Codigo);
                Assert.Equal("COD2", vm.Rows[1].Codigo);
                Assert.Equal(15m, vm.Rows[0].Cantidad);
                Assert.Equal(25m, vm.Rows[1].Cantidad);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadEx).Throw();
        }
    }

    [Fact]
    public void TryPasteAnchored_FromCodigo_WritesFollowingColumnsIntoSectors()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var host = WpfAppHost.Current;
                host.Session.NewProject("try-paste-sectors");
                host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "GL", Cantidad = 1m });

                var vm = new CubicacionPageVm(new FakeShell(), host);
                vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
                var row = vm.Rows.First();
                vm.SelectedRow = row;

                var grid = new System.Windows.Controls.DataGrid { ItemsSource = vm.Rows, SelectedItem = row };
                var col = new System.Windows.Controls.DataGridTextColumn { Header = "CÓDIGO" };
                grid.Columns.Add(col);
                grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(row, col);

                // Código + Desc + UD + Cant + 3 casillas
                bool success = vm.TryPasteAnchored(grid, "PVC20\tTubo\tML\t0\t10\t20\t30");

                Assert.True(success);
                Assert.Equal("PVC20", vm.Rows[0].Codigo);
                Assert.Equal("10", vm.Rows[0].SectorValues[0]);
                Assert.Equal("20", vm.Rows[0].SectorValues[1]);
                Assert.Equal("30", vm.Rows[0].SectorValues[2]);
                Assert.Equal(60m, vm.Rows[0].Cantidad);
                Assert.NotNull(vm.Rows[0].Source.QtyExpressions);
                Assert.Equal("10", vm.Rows[0].Source.QtyExpressions![0]);
                Assert.Equal("20", vm.Rows[0].Source.QtyExpressions[1]);
                Assert.Equal("30", vm.Rows[0].Source.QtyExpressions[2]);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadEx).Throw();
    }

    [Fact]
    public void TryPasteAnchored_FromSectorColumn_PastesBlockIntoCasillas()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var host = WpfAppHost.Current;
                host.Session.NewProject("try-paste-sector-anchor");
                host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "GL", Cantidad = 1m });

                var vm = new CubicacionPageVm(new FakeShell(), host);
                vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
                var row = vm.Rows.First();
                vm.SelectedRow = row;

                var grid = new System.Windows.Controls.DataGrid { ItemsSource = vm.Rows, SelectedItem = row };
                var sectorCol = new QubiCostElectrical.Wpf.Controls.DisplayBindingTextColumn { Header = "S1", SectorSlot = 0 };
                grid.Columns.Add(sectorCol);
                grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(row, sectorCol);

                bool success = vm.TryPasteAnchored(grid, "5\t6\t7\n8\t9\t10");

                Assert.True(success);
                Assert.Equal("5", vm.Rows[0].SectorValues[0]);
                Assert.Equal("6", vm.Rows[0].SectorValues[1]);
                Assert.Equal("7", vm.Rows[0].SectorValues[2]);
                Assert.Equal("8", vm.Rows[1].SectorValues[0]);
                Assert.Equal("9", vm.Rows[1].SectorValues[1]);
                Assert.Equal("10", vm.Rows[1].SectorValues[2]);
                Assert.Equal(18m, vm.Rows[0].Cantidad);
                Assert.Equal(27m, vm.Rows[1].Cantidad);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadEx).Throw();
    }

    [Fact]
    public void CubicacionTakeoffQueryService_FastPath_ForEmptyExpressions()
    {
        var session = new BudgetProjectSession();
        session.NewProject("fast-path-test", "Tester");
        var row = new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            TotalExpression = ""
        };

        var svc = new CubicacionTakeoffQueryService();
        var result = svc.EvaluateStoredExpressions(session, row, 300);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CubicacionTakeoffQueryService_FormulaEvaluation_BasicCorrectness()
    {
        var session = new BudgetProjectSession();
        session.NewProject("formula-basic", "Tester");
        session.ProjectVariables["ancho"] = 5m;

        var row = new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            TotalExpression = "ancho * 3"
        };

        var svc = new CubicacionTakeoffQueryService();
        var result = svc.EvaluateStoredExpressions(session, row, 300);

        Assert.Equal(15m, result);
    }

    [Fact]
    public void CubicacionTakeoffQueryService_EvaluateStoredExpressionsCore_ZonasTotal_CorrectValue()
    {
        var session = new BudgetProjectSession();
        session.NewProject("zonas-total-test", "Tester");

        var row = new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            QtyExpressions = new[] { "10", "20", "30" },
            TotalExpression = "zonas_total * 2"
        };

        var svc = new CubicacionTakeoffQueryService();
        var result = svc.EvaluateStoredExpressions(session, row, 300);

        // Sum(10, 20, 30) = 60; 60 * 2 = 120
        Assert.Equal(120m, result);
    }

    [Fact]
    public void AutocompleteVisible_CancellationTest()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cancellation-test");
        var vm = new CubicacionPageVm(new FakeShell(), host);

        vm.AutocompleteVisible = true;
        Assert.True(vm.AutocompleteVisible);

        vm.AutocompleteVisible = false;
        Assert.False(vm.AutocompleteVisible);
    }

    [Fact]
    public void SyncGridRowSelection_UpdatesSelectedState()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("sync-selection-test");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "GL", Cantidad = 1m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        var r1 = vm.Rows[0];
        var r2 = vm.Rows[1];

        vm.SyncGridRowSelection(new[] { r1 });

        Assert.True(r1.IsSelected);
        Assert.False(r2.IsSelected);
    }

    [Fact]
    public void TryAutoResolveFromPriceBank_NoOverwriteOfExistingFields()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("auto-resolve-test");
        // ValorUfClp=1 para que MatClp (fuente del precio) y MatUf (derivado) coincidan
        // numéricamente — ver comentario equivalente en RefreshPricesCommand_ThenCreateSnapshot_*.
        host.Session.ProjectInfo.ValorUfClp = 1m;
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "GL", Cantidad = 1m });

        // Agregar material al catálogo
        var mat = new MaterialRow { Codigo = "M1", Descripcion = "Catalogo Desc", Unidad = "UD", MatClp = 10m };
        host.Session.Materiales.Add(mat);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = vm.Rows[0];
        row.Codigo = "M1";
        row.Descripcion = "Manual Desc"; // Ya tiene descripción manual

        // Ejecutamos la resolución automática
        vm.OnRowEdited(row);

        Assert.Equal("Manual Desc", row.Descripcion); // No debe pisarse
        Assert.Equal(10m, row.MatUf); // Se rellena el precio UF que era 0
    }

    [Fact]
    public void QuantityFormulaRounding_CeilMode_WorksCorrectly()
    {
        var row = new CubicacionRow
        {
            RedondeoMultiplo = 5m,
            RedondeoModo = "ARRIBA"
        };
        var svc = new CubicacionTakeoffQueryService();

        var val = svc.ApplyRounding(row, 12.3m);

        Assert.Equal(15m, val);
    }

    [Fact]
    public void QuantityFormulaRounding_FloorMode_WorksCorrectly()
    {
        var row = new CubicacionRow
        {
            RedondeoMultiplo = 5m,
            RedondeoModo = "ABAJO"
        };
        var svc = new CubicacionTakeoffQueryService();

        var val = svc.ApplyRounding(row, 12.3m);

        Assert.Equal(10m, val);
    }

    [Fact]
    public void QuantityFormulaRounding_RoundMode_WorksCorrectly()
    {
        var row = new CubicacionRow
        {
            RedondeoMultiplo = 5m,
            RedondeoModo = "CERCANO"
        };
        var svc = new CubicacionTakeoffQueryService();

        var val = svc.ApplyRounding(row, 12.3m); // 12.3 / 5 = 2.46 -> Round to 2 -> 10
        var val2 = svc.ApplyRounding(row, 12.7m); // 12.7 / 5 = 2.54 -> Round to 3 -> 15

        Assert.Equal(10m, val);
        Assert.Equal(15m, val2);
    }

    [Fact]
    public void InsertRow_InsertsRowAtSelectedPosition()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-insert");

        var itemId = "P1";
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = itemId,
            Item = "1.1",
            Descripcion = "Partida",
            Unidad = "UN",
            Cantidad = 1m
        });

        var row1 = new CubicacionRow { Id = "R1", ItemId = itemId, Codigo = "C1", Descripcion = "Material 1", Tipo = "MAT" };
        var row2 = new CubicacionRow { Id = "R2", ItemId = itemId, Codigo = "C2", Descripcion = "Material 2", Tipo = "MAT" };
        host.Session.Cubicacion.Add(row1);
        host.Session.Cubicacion.Add(row2);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == itemId));

        var rowVm1 = vm.Rows.First(r => r.Id == "R1");
        vm.SelectedRow = rowVm1;

        Assert.True(vm.InsertRowCommand.CanExecute(null));
        vm.InsertRowCommand.Execute(null);

        Assert.Equal(3, host.Session.Cubicacion.Count(x => x.ItemId == itemId));

        var firstRow = vm.Rows[0];
        Assert.NotEqual("R1", firstRow.Id);
        Assert.NotEqual("R2", firstRow.Id);

        Assert.Equal("R1", vm.Rows[1].Id);
        Assert.Equal("R2", vm.Rows[2].Id);
    }

    [Fact]
    public void UfSubtotalCalculado_IsCalculatedAndNotifiedCorrectly()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("uf-subtotal-test");
        host.Session.Data.Proyecto.ValorUfClp = 40000m;

        var row = new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "C1",
            Cantidad = 5m,
            MatClp = 40000m,
            MoClp = 0m
        };

        var rowVm = new QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionRowVm(row, true);
        rowVm.ValorUfClp = 40000m;

        Assert.Equal(1m, rowVm.UfUnitCalculado);
        Assert.Equal(5m, rowVm.UfSubtotalCalculado);

        bool subtotalNotified = false;
        rowVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(rowVm.UfSubtotalCalculado))
                subtotalNotified = true;
        };

        rowVm.Cantidad = 10m;
        Assert.Equal(10m, rowVm.UfSubtotalCalculado);
        Assert.True(subtotalNotified);
    }

    [Fact]
    public async Task RealProject_Formula_Test()
    {
        // Trabaja sobre una COPIA temporal del proyecto de obra: el archivo original es
        // intocable y el test sigue siendo determinista.
        if (!LiveProjectGate.TryCopyProjectToTemp("PEL039A2026 ABIERTO.qce", out string path, out string tempDir))
            return;

        try
        {
            var host = WpfAppHost.Current;
            await host.ProjectWorkspace.OpenAsync(path);

            var vm = new CubicacionPageVm(new FakeShell(), host);
            var p = vm.Partidas.FirstOrDefault(x => x.Descripcion.Contains("Ductos", StringComparison.OrdinalIgnoreCase)) ?? vm.Partidas.First(x => x.EsPartida);
            vm.SelectPartida(p);

            vm.PartidaCantidadText = "c1+c2+c3";
            Assert.Equal(39m, vm.SelectedPartida?.Cantidad);
            Assert.Equal("c1+c2+c3", vm.SelectedPartida?.CantidadFormula);

            vm.PartidaCantidadText = "+c1+c2+c3";
            Assert.Equal(39m, vm.SelectedPartida?.Cantidad);
            Assert.Equal("+c1+c2+c3", vm.SelectedPartida?.CantidadFormula);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task AutocompletarUnidadYCantidadActual_SetsUnidadAndCantidadFromFirstMaterial()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-autocompletar-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-autocompletar");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Alimentador",
            Unidad = "",
            Cantidad = 0m
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "R1",
            ItemId = "P1",
            Codigo = "CAB-01",
            Descripcion = "Cable Cu 4mm2",
            Unidad = "m",
            Cantidad = 75m,
            MatClp = 1000m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        Assert.Equal("", vm.SelectedPartida?.Unidad);
        Assert.Equal(0m, vm.SelectedPartida?.Cantidad);
        Assert.True(vm.SelectedPartida?.SinValorizacionGlobal);

        vm.AutocompletarUnidadYCantidadCommand.Execute(null);

        Assert.Equal("m", vm.SelectedPartida?.Unidad);
        Assert.Equal(75m, vm.SelectedPartida?.Cantidad);
        Assert.False(vm.SelectedPartida?.SinValorizacionGlobal);
        Assert.Equal("m", vm.PartidaUnidadText);
    }
}
