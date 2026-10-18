using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;
using System;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionViewModelTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Takeoff;
        public string ActiveProjectLabel => "";

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) =>
            RequestNavigate(route, force);

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }

    public CubicacionViewModelTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public async Task Load_WhenPartidaHasNoRows_IsEmpty()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-empty-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-empty");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        Assert.True(vm.HasPartida);
        Assert.Equal(8, vm.Rows.Count);
        Assert.True(vm.IsEmpty);
        Assert.Empty(host.Session.Cubicacion);
        Assert.All(vm.Rows, r => Assert.False(r.IsSessionBacked));
    }

    [Fact]
    public async Task ReadOnly_DisablesAddRow()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-readonly-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-readonly");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Tablero",
            Unidad = "UN",
            Cantidad = 1m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);
        host.Session.SetRoleReadOnly(true, "Prueba solo lectura");

        try
        {
            var vm = new CubicacionPageVm(new FakeShell(), host);

            Assert.False(vm.AddRowCommand.CanExecute(null));
            Assert.False(vm.SaveCommand.CanExecute(null));
        }
        finally
        {
            // La sesión es compartida (WpfAppHost.Current): sin este reset el read-only
            // se filtra al resto de la colección WpfHost.
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void ReadOnly_PartidaHeaderSetters_DoNotMutateOrMarkDirty()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-header-readonly");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m,
            Observaciones = "Obs original"
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        Assert.False(host.Session.IsDirty);
        Assert.False(vm.HasPendingChanges);

        try
        {
            host.Session.SetRoleReadOnly(true, "Rol Visor");
            Assert.True(vm.IsReadOnly);

            vm.PartidaUnidadText = "GL";
            vm.PartidaCantidadText = "99";
            vm.PartidaObservacionesText = "intentado por visor";

            Assert.Equal("ML", vm.PartidaUnidadText);
            Assert.Equal("ML", host.Session.Itemizado.Single(i => i.Id == "P1").Unidad);
            Assert.Equal(10m, host.Session.Itemizado.Single(i => i.Id == "P1").Cantidad);
            Assert.Equal("Obs original", vm.PartidaObservacionesText);
            Assert.False(host.Session.IsDirty);
            Assert.False(vm.HasPendingChanges);
        }
        finally
        {
            host.Session.SetRoleReadOnly(false);
        }
    }

    [Fact]
    public void PartidaCantidadText_Formula_C1C2C3_Evaluation()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("test-formula-c1c2c3");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Partida Test",
            Unidad = "UN",
            Cantidad = 0m
        });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "R1", ItemId = "P1", Tipo = "MAT", Codigo = "A1", Descripcion = "Mat 1", QtyExpressions = new[] { "10" } });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "R2", ItemId = "P1", Tipo = "MAT", Codigo = "A2", Descripcion = "Mat 2", QtyExpressions = new[] { "20" } });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "R3", ItemId = "P1", Tipo = "MAT", Codigo = "A3", Descripcion = "Mat 3", QtyExpressions = new[] { "30" } });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        Assert.Equal(10m, vm.Rows[0].Cantidad);
        Assert.Equal(20m, vm.Rows[1].Cantidad);
        Assert.Equal(30m, vm.Rows[2].Cantidad);

        vm.PartidaCantidadText = "c1+c2+c3";
        Assert.Equal(60m, vm.SelectedPartida?.Cantidad);
        Assert.Equal("60,00", vm.PartidaCantidadText);
        Assert.Equal("c1+c2+c3", vm.SelectedPartida?.CantidadFormula);

        // Leading plus and fallback for empty/non-existent row (c10)
        vm.PartidaCantidadText = "+c1+c2+c10";
        Assert.Equal(30m, vm.SelectedPartida?.Cantidad);
        Assert.Equal("30,00", vm.PartidaCantidadText);
        Assert.Equal("+c1+c2+c10", vm.SelectedPartida?.CantidadFormula);

        // Setting formatted string doesn't wipe formula
        vm.PartidaCantidadText = "30,00";
        Assert.Equal("+c1+c2+c10", vm.SelectedPartida?.CantidadFormula);
    }

    [Fact]
    public void HeaderTemplate_Binding_UpdateSource_Works()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var host = WpfAppHost.Current;
            host.Session.NewProject("test-binding");
            host.Session.Itemizado.Add(new ItemizadoRow
            {
                Id = "P1",
                Item = "1.1",
                Descripcion = "Test",
                Unidad = "UN",
                Cantidad = 0m
            });
            host.Session.Cubicacion.Add(new CubicacionRow { Id = "R1", ItemId = "P1", Tipo = "MAT", Codigo = "A1", Descripcion = "Mat 1", QtyExpressions = new[] { "10" } });
            host.Session.Cubicacion.Add(new CubicacionRow { Id = "R2", ItemId = "P1", Tipo = "MAT", Codigo = "A2", Descripcion = "Mat 2", QtyExpressions = new[] { "20" } });
            host.Session.Cubicacion.Add(new CubicacionRow { Id = "R3", ItemId = "P1", Tipo = "MAT", Codigo = "A3", Descripcion = "Mat 3", QtyExpressions = new[] { "30" } });

            var vm = new CubicacionPageVm(new FakeShell(), host);
            vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

            var tb = new System.Windows.Controls.TextBox();
            var binding = new System.Windows.Data.Binding("PartidaCantidadText")
            {
                Source = vm,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus
            };
            tb.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);

            tb.Text = "c1+c2+c3";
            System.Windows.Data.BindingOperations.GetBindingExpression(tb, System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

            Assert.Equal(60m, vm.SelectedPartida?.Cantidad);
            Assert.Equal("60,00", vm.PartidaCantidadText);
        });
    }


    [Fact]
    public async Task SelectPartida_LoadsRowsForItem()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-partida-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-partida");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "A", Unidad = "ML", Cantidad = 1m });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "B", Unidad = "ML", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "C1", ItemId = "P1", Tipo = "MAT", Codigo = "A1", Descripcion = "Mat A", Unidad = "ML", Cantidad = 1m });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        var second = vm.Partidas.FirstOrDefault(p => p.Id == "P2");
        Assert.NotNull(second);
        vm.SelectPartida(second!);

        Assert.Equal(8, vm.Rows.Count);
        Assert.True(vm.IsEmpty);
        Assert.False(host.Session.Cubicacion.Any(c => c.ItemId == "P2"));
    }

    // Regresión: la matriz de sectores ya NO pagina en ventanas de 15 casillas — las 300
    // columnas existen todas a la vez (SectorWindowSize == MaximumSectorCount), así que
    // escribir directamente en cualquier offset absoluto (incluido el último, 299) debe
    // funcionar sin necesidad de "mover la ventana" primero.
    [Fact]
    public async Task SectorMatrix_EditsAnyAbsoluteOffsetDirectly_SinPaginarEnVentanas()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-sectors-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-sectors");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 1m
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C1",
            ItemId = "P1",
            Tipo = "MAT",
            Codigo = "PVC20",
            Descripcion = "Tubo PVC",
            Unidad = "ML"
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = Assert.Single(vm.Rows, r => !string.IsNullOrWhiteSpace(r.Codigo));

        row.SectorValues[0] = "10";
        Assert.Equal(10m, row.Cantidad);

        // Sin ningún concepto de "ventana": se escribe directo en el último offset absoluto.
        row.SectorValues[299] = "5";

        Assert.Equal(15m, row.Cantidad);
        Assert.Equal(300, row.Source.QtyExpressions.Length);
        Assert.Equal("5", row.Source.QtyExpressions[299]);
    }

    // Regresión: la observación de la partida (PartidaObservacionesText) es la que viaja al
    // Resumen/oferta — vive en ItemizadoRow.Observaciones, distinta de la nota interna por
    // recurso (CubicacionRowVm.NotaInterna), que nunca se exporta.
    [Fact]
    public async Task PartidaObservacionesText_SeGuardaEnItemizadoRow_YEsDistintaDeLaNotaInternaDelRecurso()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-observaciones-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-observaciones");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "UN", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "C1", ItemId = "P1", Tipo = "MAT", Codigo = "PVC20", Descripcion = "Tubo PVC", Unidad = "ML" });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = Assert.Single(vm.Rows, r => !string.IsNullOrWhiteSpace(r.Codigo));

        vm.PartidaObservacionesText = "Se cotiza con PVC-LH según especificación entregada.";
        row.NotaInterna = "Confirmar stock con bodega antes de instalar.";

        var itemRow = host.Session.Itemizado.Single(i => i.Id == "P1");
        Assert.Equal("Se cotiza con PVC-LH según especificación entregada.", itemRow.Observaciones);
        Assert.Equal("Confirmar stock con bodega antes de instalar.", row.Source.NotaInterna);
        Assert.NotEqual(itemRow.Observaciones, row.Source.NotaInterna);

        // Cambiar de partida y volver debe conservar ambos valores.
        vm.SelectedPartida = null;
        Assert.Equal("", vm.PartidaObservacionesText);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        Assert.Equal("Se cotiza con PVC-LH según especificación entregada.", vm.PartidaObservacionesText);
    }

    // Regresión: un sector con una fórmula básica (suma, resta, multiplicación, división) debe
    // mostrar el RESULTADO calculado en la celda (SectorDisplayValues), no el texto de la
    // fórmula — igual que una celda de Excel. El texto crudo sigue disponible en SectorValues
    // para cuando se vuelve a editar la celda.
    [Fact]
    public async Task SectorValues_ConFormulaBasica_MuestraElResultadoCalculado()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-sector-formula-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-sector-formula");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "UN", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "C1", ItemId = "P1", Tipo = "MAT", Codigo = "PVC20", Descripcion = "Tubo PVC", Unidad = "ML" });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = Assert.Single(vm.Rows, r => !string.IsNullOrWhiteSpace(r.Codigo));

        row.SectorValues[0] = "2+3";
        Assert.Equal("2+3", row.SectorValues[0]);
        Assert.Equal("5", row.SectorDisplayValues[0]);

        row.SectorValues[1] = "10-4";
        Assert.Equal("6", row.SectorDisplayValues[1]);

        row.SectorValues[2] = "4*3";
        Assert.Equal("12", row.SectorDisplayValues[2]);

        row.SectorValues[3] = "20/4";
        Assert.Equal("5", row.SectorDisplayValues[3]);

        Assert.Equal(28m, row.Cantidad); // 5 + 6 + 12 + 5

        // Borrar la celda limpia también el valor mostrado, no deja el resultado anterior.
        row.SectorValues[0] = "";
        Assert.Equal("", row.SectorDisplayValues[0]);
    }

    [Fact]
    public void OnRowEdited_MaterializesPaddingRowInSession()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-materialize");
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
        var padding = vm.Rows[0];
        Assert.False(padding.IsSessionBacked);

        padding.Codigo = "PVC20";
        vm.OnRowEdited(padding);

        Assert.True(padding.IsSessionBacked);
        Assert.Single(host.Session.Cubicacion);
        Assert.False(vm.IsEmpty);
    }

    // Regresión de bug reportado por el usuario: aplicar un material desde el autocompletado
    // (la forma más común de "agregar un código" en la grilla) y cambiar de partida hacía que
    // el código/descripción desaparecieran al volver, mientras una cantidad escrita en una
    // casilla de sector SÍ sobrevivía. Root cause: session.Cubicacion (lista "legacy") y
    // CubicacionRecursos/Valorizaciones/Zonas (composición "normalizada") son dos
    // representaciones separadas. CreateSnapshot() (llamado por CADA punto de Undo y por
    // Guardar) ejecuta EnsureIntegrity(), que RECONSTRUYE la lista legacy desde la
    // normalizada — si una edición nunca sincronizó hacia la normalizada, ese CreateSnapshot()
    // la revierte en silencio. ApplySectorExpression (casillas) y OnRowEdited (vía
    // CubicacionPresenter.AfterRowEdited) ya sincronizaban; CubicacionRowMutationService.
    // ApplyMaterial (aplicar una sugerencia de autocompletado) NO lo hacía — es el único de los
    // tres caminos de edición de una fila que dejaba la composición sin sincronizar.
    [Fact]
    public void ApplyMaterialCommand_ThenCreateSnapshot_PersistsCodigoAndDescripcion()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-sync-composicion-material");
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
        var padding = vm.Rows[0];
        vm.SelectedRow = padding;

        var materialA = new MaterialRow { Codigo = "PVC20", Descripcion = "PVC 20 mm", Unidad = "ML", MatUf = 1m };
        vm.ApplyMaterialCommand.Execute(materialA);
        Assert.True(padding.IsSessionBacked);

        // Primer CreateSnapshot(): con CubicacionRecursos todavía vacío en esta sesión nueva,
        // EnsureProject toma la rama "descomponer TODO desde legacy" — que por definición
        // refleja el estado legacy actual. Esto por sí solo NO expone el bug (crea el recurso
        // normalizado con "PVC20" sin importar si ApplyMaterial sincroniza o no).
        host.Session.CreateSnapshot();
        Assert.Equal("PVC20", host.Session.CubicacionRecursos.Single(r => r.Id == padding.Id).Codigo);

        // Segunda edición sobre la MISMA fila (ya tiene un recurso normalizado desde el paso
        // anterior). A partir de aquí, EnsureProject solo decompone filas "huérfanas" (sin
        // recurso) — esta fila ya no calificaría, así que si ApplyMaterial no sincroniza
        // explícitamente, el recurso se queda con el material viejo.
        var materialB = new MaterialRow { Codigo = "COND30", Descripcion = "Conductor 3x10", Unidad = "ML", MatUf = 2m };
        vm.ApplyMaterialCommand.Execute(materialB);

        // Simula exactamente lo que dispara cada punto de Undo (host.UndoService.PushUndoPoint)
        // y cada Guardar: session.CreateSnapshot() corre EnsureIntegrity() sobre los datos EN
        // VIVO antes de clonar, no solo sobre la copia devuelta.
        host.Session.CreateSnapshot();

        var persisted = host.Session.Cubicacion.Single(r => r.Id == padding.Id);
        Assert.Equal("COND30", persisted.Codigo);
        Assert.Equal("Conductor 3x10", persisted.Descripcion);
    }

    // Mismo patrón de bug que ApplyMaterialCommand_ThenCreateSnapshot_PersistsCodigoAndDescripcion,
    // aplicado a "Actualizar precio" (RefreshPricesForSelectedRows): si esa acción no
    // sincronizara hacia la composición normalizada, el precio actualizado se revertiría en
    // silencio en el próximo CreateSnapshot() (cualquier punto de Undo o Guardar posterior).
    [Fact]
    public void RefreshPricesCommand_ThenCreateSnapshot_PersistsUpdatedPrice()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-sync-composicion-precio");
        // El precio se ingresa SIEMPRE en pesos (MatClp/MoClp) — ver MaterialRow.RecalcularUf;
        // MatUf/MoUf quedan derivados con el valor de UF del proyecto. Con ValorUfClp=1, MatClp
        // y MatUf coinciden numéricamente, así que el resto del test (pensado en UF) no cambia.
        host.Session.ProjectInfo.ValorUfClp = 1m;
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
        var padding = vm.Rows[0];
        vm.SelectedRow = padding;

        var material = new MaterialRow { Codigo = "PVC20", Descripcion = "PVC 20 mm", Unidad = "ML", MatClp = 1m, MoClp = 0.5m };
        vm.ApplyMaterialCommand.Execute(material);

        // Primer CreateSnapshot(): bootstrapea el recurso normalizado (rama "descomponer TODO
        // desde legacy" porque CubicacionRecursos está vacío).
        host.Session.CreateSnapshot();
        Assert.Equal(1m, host.Session.CubicacionValorizaciones.Single(v => v.RecursoId == padding.Id).MatUf);

        // Sube el precio en el catálogo del proyecto y actualiza vía "Actualizar precio". A
        // partir de aquí la fila ya tiene un recurso normalizado (no es huérfana), así que si
        // RefreshPricesForSelectedRows no sincroniza explícitamente, el recurso se queda con el
        // precio viejo.
        material.MatClp = 3m;
        material.MoClp = 1.5m;
        padding.IsSelected = true;
        vm.RefreshPricesCommand.Execute(null);

        // Simula el próximo punto de Undo o Guardar.
        host.Session.CreateSnapshot();

        var persisted = host.Session.Cubicacion.Single(r => r.Id == padding.Id);
        Assert.Equal(3m, persisted.MatUf);
        Assert.Equal(1.5m, persisted.MoUf);
    }

    // Regresión: pegar varios valores en varias casillas de sector de una fila NUEVA (sin
    // Código ni Cantidad aún, "fila de relleno") — como al seleccionar varias celdas y pegar
    // desde Excel. Antes, editar una fila de relleno SOLO por sus casillas (sin tocar antes
    // Código/Descripción/Cantidad) nunca llamaba a EnsureRowInSession: la fila quedaba
    // "huérfana", nunca se agregaba a session.Cubicacion. Cada nueva escritura de casilla
    // volvía a pasar por el mismo camino sin persistir la fila anterior, así que al terminar
    // el pegado (fila con contenido en varias casillas) la fila igual no estaba en la sesión y
    // se perdía apenas se navegaba a otra partida o se recargaba — daba la impresión de que
    // "solo se pegó la última casilla".
    [Fact]
    public void OnSectorExpressionChanged_MaterializesPaddingRowInSession_AlEditarSoloCasillas()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-materialize-sector");
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
        var padding = vm.Rows[0];
        Assert.False(padding.IsSessionBacked);

        // Simula pegar un mismo valor en varias casillas seleccionadas de la misma fila,
        // como hace DataGridExcelNavigation.TryPasteIntoSelection.
        padding.SectorValues[0] = "10";
        padding.SectorValues[1] = "10";
        padding.SectorValues[2] = "10";

        Assert.True(padding.IsSessionBacked);
        Assert.Single(host.Session.Cubicacion);
        Assert.Same(padding.Source, host.Session.Cubicacion[0]);
        Assert.Equal("10", padding.Source.QtyExpressions[0]);
        Assert.Equal("10", padding.Source.QtyExpressions[1]);
        Assert.Equal("10", padding.Source.QtyExpressions[2]);
        Assert.Equal(30m, padding.Cantidad);
    }

    // Regresión real: si el usuario le pone datos a una fila visual de más ABAJO antes que a
    // una de más ARRIBA (ej. edita la casilla 5 y después la 3), la fila recién materializada
    // debe insertarse en session.Cubicacion en su posición VISUAL, no al final — si no, el
    // orden persistido diverge del orden que ve el usuario en la columna "#", y una referencia
    // entre casillas por número de fila (F{fila}C{columna}) termina apuntando a otra fila.
    // Reportado en uso real como "sumo 100 y 10 y me da 60" — el token apuntaba a la fila
    // equivocada porque el orden persistido no coincidía con el visual.
    [Fact]
    public void OnRowEdited_InsertaFilaMaterializadaEnSuPosicionVisual_NoAlFinal()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-order");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida", Unidad = "UN", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "REAL-A", ItemId = "P1", Codigo = "MAT-A", Descripcion = "A", Unidad = "UN" });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "REAL-B", ItemId = "P1", Codigo = "MAT-B", Descripcion = "B", Unidad = "UN" });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        // Vm.Rows: [REAL-A(0), REAL-B(1), padding(2), padding(3), padding(4), ...]
        var rowAtIndex4 = vm.Rows[4];
        var rowAtIndex2 = vm.Rows[2];
        Assert.False(rowAtIndex4.IsSessionBacked);
        Assert.False(rowAtIndex2.IsSessionBacked);

        // Se edita PRIMERO la fila visual de más abajo (índice 4)...
        rowAtIndex4.Codigo = "MAT-D";
        vm.OnRowEdited(rowAtIndex4);
        // ...y DESPUÉS la de más arriba (índice 2).
        rowAtIndex2.Codigo = "MAT-C";
        vm.OnRowEdited(rowAtIndex2);

        // session.Cubicacion debe reflejar el orden VISUAL (A, B, C, D), no el orden de edición
        // (A, B, D, C).
        var idsInSessionOrder = host.Session.Cubicacion
            .Where(r => string.Equals(r.ItemId, "P1", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Codigo)
            .ToList();
        Assert.Equal(new[] { "MAT-A", "MAT-B", "MAT-C", "MAT-D" }, idsInSessionOrder);
    }

    [Fact]
    public void SyncGridRowSelection_MarksRowsSelected()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-delete");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Tablero",
            Unidad = "UN",
            Cantidad = 1m
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C1",
            ItemId = "P1",
            Tipo = "MAT",
            Codigo = "TB01",
            Descripcion = "Tablero",
            Unidad = "UN",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = Assert.Single(vm.Rows, r => r.HasContent);
        row.IsSelected = false;

        vm.SyncGridRowSelection([row]);

        Assert.True(row.IsSelected);
        Assert.True(vm.DeleteRowCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteRowCommand_UsesSelectedRowWhenNothingMarkedSelected()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-delete-fallback");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Tablero",
            Unidad = "UN",
            Cantidad = 1m
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C1",
            ItemId = "P1",
            Tipo = "MAT",
            Codigo = "TB01",
            Descripcion = "Tablero",
            Unidad = "UN",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = Assert.Single(vm.Rows, r => r.HasContent);
        foreach (var r in vm.Rows)
            r.IsSelected = false;

        vm.SelectedRow = row;

        Assert.True(vm.DeleteRowCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("CÓDIGO", 0)]
    [InlineData("DESCRIPCIÓN", 1)]
    [InlineData("UD", 2)]
    [InlineData("CANT", 3)]
    [InlineData("CANTIDAD", 3)]
    public void ResolveAnchoredPasteColumn_MapsGridHeaders(string header, int expected)
    {
        Assert.Equal(expected, CubicacionPageVm.ResolveAnchoredPasteColumn(header));
    }

    [Fact]
    public void ResolveAnchoredPasteColumn_MapsSectorColumnSlot()
    {
        var col = new QubiCostElectrical.Wpf.Controls.DisplayBindingTextColumn { Header = "P1", SectorSlot = 2 };
        Assert.Equal(CubicacionPageVm.FixedPasteColumnCount + 2, CubicacionPageVm.ResolveAnchoredPasteColumn(col));
        Assert.False(CubicacionPageVm.IsFixedPasteColumn(col));
    }

    [Fact]
    public void ExcludedWithoutNote_LocksGrid_DoesNotShowNoteBanner()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-excluded");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Excluida",
            Unidad = "UN",
            Cantidad = 1m,
            ExcluidoCubicacion = true
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        Assert.True(vm.ExcluidoCubicacion);
        Assert.True(vm.IsReadOnly);
        Assert.False(vm.HasPartidaNote);
        Assert.Equal("Partida vacía", vm.PartidaEstadoText);
        Assert.False(vm.AddRowCommand.CanExecute(null));
    }

    [Fact]
    public void ObservationWithoutExclusion_KeepsGridEditable()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-obs-libre");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        vm.MotivoNoCubicarText = "Revisar diámetro con arquitectura";
        Assert.True(vm.HasPartidaNote);
        Assert.False(vm.ExcluidoCubicacion);
        Assert.False(vm.IsReadOnly);
        Assert.Equal("Revisar diámetro con arquitectura", vm.PartidaEstadoText);
        Assert.True(vm.AddRowCommand.CanExecute(null));
    }

    [Fact]
    public void CtrlE_TogglesExclusion_IndependentOfObservation()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-ctrl-e");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        vm.MotivoNoCubicarText = "Nota libre";
        Assert.False(vm.ExcluidoCubicacion);

        Assert.True(vm.TryExecuteShortcut(CubicacionShortcutCatalog.NoCubicar));
        Assert.True(vm.ExcluidoCubicacion);
        Assert.True(vm.IsReadOnly);
        Assert.Equal("Nota libre", vm.MotivoNoCubicarText);

        Assert.True(vm.TryExecuteShortcut(CubicacionShortcutCatalog.NoCubicar));
        Assert.False(vm.ExcluidoCubicacion);
        Assert.False(vm.IsReadOnly);
        Assert.Equal("Nota libre", host.Session.Itemizado[0].MotivoNoCubicar);
    }

    [Fact]
    public void CtrlShiftE_FocusesObservation_WithoutExcluding()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-obs-focus");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        bool focused = false;
        vm.RequestFocusMotivoNoCubicar += () => focused = true;

        Assert.True(vm.TryExecuteShortcut(CubicacionShortcutCatalog.Observacion));
        Assert.True(focused);
        Assert.False(vm.ExcluidoCubicacion);
    }

    [Fact]
    public void CtrlPlus_OnPartidasPanel_AddsPartidaBelowSelection()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-add-partida");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "1.0.00001",
            Item = "1.1",
            Descripcion = "Existente",
            Unidad = "UN",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "1.0.00001"));

        Assert.True(vm.AddPartidaCommand.CanExecute(null));
        vm.AddPartidaCommand.Execute(null);

        Assert.True(host.Session.Itemizado.Count >= 2);
        Assert.Contains(host.Session.Itemizado, x => !x.EsTitulo && x.Descripcion == "Nueva partida");
        Assert.Equal("Nueva partida", vm.SelectedPartida?.Descripcion);
    }

    [Fact]
    public void DeletePartidaCommand_CanExecute_WhenPartidaSelected()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-del-partida");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "1.0.00001",
            Item = "1.1",
            Descripcion = "A borrar",
            Unidad = "UN",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "1.0.00001"));

        Assert.True(vm.DeletePartidaCommand.CanExecute(null));
    }

    [Fact]
    public void SessionItemizadoChange_RefreshesPartidaList()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-session");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Original",
            Unidad = "UN",
            Cantidad = 1m
        });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        Assert.Single(vm.Partidas);

        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P2",
            Item = "1.2",
            Descripcion = "Nueva",
            Unidad = "UN",
            Cantidad = 1m
        });
        host.Session.MarkDirty("itemizado");

        Assert.Equal(2, vm.Partidas.Count);
    }

    [Fact]
    public void PendingTakeoffNavigation_IsClearedAfterApply()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-nav");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Destino",
            Unidad = "UN",
            Cantidad = 1m
        });
        host.Session.PendingTakeoffNavigation = new TakeoffNavigationContext("P1", "1.1", "Destino");

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        Assert.Equal("P1", vm.SelectedPartida?.Id);
    }


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
        string path = @"C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL039A2026 ABIERTO.qce";
        if (!File.Exists(path)) return;

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
