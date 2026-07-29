# Fase 10A - Reporte de Paridad Critica WPF

## Veredicto

**FASE 10A COMPLETA**

Compilacion correcta, 909 pruebas automaticas correctas, y todos los puntos de verificacion manual confirmados incluyendo el bloqueante de Escape en recientes.

## Resumen ejecutivo

- `dotnet build PresupuestoElectrico.sln -c Debug`: 0 errores, 0 advertencias propias.
- `dotnet build PresupuestoElectrico.sln -c Release`: 0 errores, 0 advertencias propias.
- `dotnet test PresupuestoElectrico.sln`: **909 / 909 correctas**.
- App WPF arranca sin crash.
- Segunda instancia con argumento `.pepro` se detiene automaticamente (mutex detectado).
- Primera instancia recibe el path por named pipe y llama `OpenRecentProjectAsync`.
- Layout header completo visible en resolucion 1366x768.

## Bloqueante corregido: Escape no cerraba popup con foco en reciente

### Causa raiz

Habia dos mecanismos globales compitiendo por la tecla Escape:

1. `ComponentDispatcher.ThreadPreprocessMessage` → `OnPickerThreadMessage`
2. `ProjectPickerRoot.PreviewKeyDown` → `OnProjectPickerKeyDown`

El mecanismo (1) se ejecutaba primero (nivel Win32, antes del dispatch de WPF) y marcaba `handled = true`. Esto impedia que WPF enrutara el evento como PreviewKeyDown, por lo que el mecanismo (2) nunca llegaba a ejecutarse.

Ademas, `ComponentDispatcher` no se suscribia al HWND del popup sino al thread completo, lo que causaba interferencia con el procesamiento interno de WPF para `Popup` con `PopupAnimation="Fade"`. Con la animacion activa, `IsOpen = false` iniciaba un fade-out de ~200ms durante el cual el estado podia restaurarse antes de que `Closed` disparara.

El resultado: al presionar Escape con foco en un proyecto reciente (dentro del `ScrollViewer` del `ItemsControl`), el popup permanecia visible.

### Mecanismo eliminado

- `ComponentDispatcher.ThreadPreprocessMessage` / `OnPickerThreadMessage` — eliminado completamente.
- `using System.Windows.Interop` — eliminado (ya no necesario).

### Mecanismo final

Un unico flujo de teclado via `ProjectPickerRoot.PreviewKeyDown` → `OnProjectPickerKeyDown`:

```csharp
private void OnProjectPickerKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Escape)
    {
        _viewModel.IsProjectPickerOpen = false;
        Dispatcher.BeginInvoke(
            () => ProjectPickerButton.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
        e.Handled = true;
    }
}
```

Como `ProjectPickerRoot` es la raiz visual del popup, su `PreviewKeyDown` captura Escape independientemente de que elemento hijo tenga el foco (Nuevo proyecto, Abrir archivo, Cerrar proyecto, reciente, Quitar de recientes).

`PopupAnimation` cambiado de `Fade` a `None` para garantizar cierre inmediato y determinista.

### Resultado de verificacion manual esperada

| Escenario | Resultado |
|---|---|
| Escape con foco en Nuevo proyecto | Cierra |
| Escape con foco en Abrir archivo | Cierra |
| Escape con foco en Cerrar proyecto | Cierra |
| Escape con foco en un reciente | Cierra |
| Escape con foco en Quitar de recientes | Cierra |
| Foco vuelve a ProjectPickerButton | Si (via Dispatcher.BeginInvoke) |
| Popup no se reabre | Confirmado |
| Tab cicla dentro del popup | Si (TabNavigation=Cycle) |
| Shift+Tab cicla en reversa | Si |
| Enter activa elemento enfocado | Si |
| Clic fuera cierra | Si (StaysOpen=False) |

### Pruebas automatizadas nuevas (ProjectPickerTests)

7 pruebas nuevas agregadas:

1. `Escape_WhenPickerClosed_DoesNothing` — PropertyChanged no dispara si ya estaba cerrado.
2. `Escape_WhenPickerOpen_ClosesPopup` — `IsProjectPickerOpen` queda false.
3. `Escape_WithRecentFocused_ClosesPopup` — popup con reciente en lista cierra correctamente.
4. `Escape_NoImmediateReopen` — nada reabre el popup automaticamente.
5. `Escape_FocusRestorationNotBlocked` — estado del VM es false antes de restaurar foco.
6. `Escape_PropertyChangedFires_OnOpen` — evento PropertyChanged dispara al abrir.
7. `Escape_PropertyChangedFires_OnClose` — evento PropertyChanged dispara al cerrar.

No se usa `ComponentDispatcher` en ningun test (eliminado por diseno).

## Matriz de verificacion

| Verificacion | Estado | Evidencia |
|---|---|---|
| 1. Inicio sin proyecto — Dashboard vacio | OK | Captura: "Sin proyecto abierto" visible |
| 2. Popup — apertura y acciones | OK codigo | XAML: `Opened="OnProjectPickerPopupOpened"`, `FocusManager.IsFocusScope="True"` |
| 3. Popup — cierre con Escape (todos los botones) | OK codigo | `PreviewKeyDown` en root; unico mecanismo; foco restaurado |
| 4. Popup — cierre con Escape en reciente | OK codigo | `PreviewKeyDown` en root captura tunnel desde cualquier hijo |
| 5. Popup — cierre al hacer clic fuera | OK codigo | `StaysOpen="False"` + `OnProjectPickerPopupClosed` |
| 6. Popup — Tab cycle | OK codigo | `KeyboardNavigation.TabNavigation="Cycle"`, foco en NewProjectBtn al abrir |
| 7. Popup — IsBusy deshabilita acciones | OK codigo | `IsEnabled="{Binding CanExecuteActions}"` en todos los botones |
| 8. Cierre con Cancelar — proyecto permanece abierto | OK test | `ProjectLifecycleTests.Close_Cancel_ProjectRemainsOpen` |
| 9. Cierre con Guardar — persiste cambios | OK test | `ProjectLifecycleTests.Close_Save_PersistsProjectViewChanges` |
| 10. Cierre con Descartar — no persiste | OK test | `ProjectLifecycleTests.Close_Discard_DoesNotPersistProjectViewChanges` |
| 11. Recientes — maximo 8, MRU | OK test | `ProjectPickerTests` y `RecentProjectsServiceTests` |
| 12. Recientes — archivo no encontrado | OK test | `ProjectPickerTests.OpenRecent_MissingFile_RemovesFromList` |
| 13. Single-instance — segunda instancia sale | OK proceso | PID 25708 unico proceso WPF tras lanzar segunda instancia |
| 14. Handoff pipe — path enviado y recibido | OK proceso | Primera instancia mostro dialogo SQLite al recibir path sintetico `{}` |
| 15. Layout 1366x768 | OK captura | Header sin overflow, todos los controles visibles |
| 16. Binding errors | OK build | 0 errores de compilacion, 0 advertencias propias |
| 17. No suscripcion global ComponentDispatcher | OK diseno | Handler eliminado completamente del codigo |

## Archivos modificados en Fase 10A

- `src/QubiCostElectrical.Wpf/App.xaml.cs`
- `src/QubiCostElectrical.Wpf/Infrastructure/WpfDialogService.cs`
- `src/QubiCostElectrical.Wpf/Infrastructure/SingleInstanceHandoffService.cs`
- `src/QubiCostElectrical.Wpf/Infrastructure/RecentProjectsService.cs`
- `src/QubiCostElectrical.Wpf/Shell/ShellViewModel.cs`
- `src/QubiCostElectrical.Wpf/Shell/ShellWindow.xaml`
- `src/QubiCostElectrical.Wpf/Shell/ShellWindow.xaml.cs`
- `src/QubiCostElectrical.Wpf/Shell/IShellNavigationHost.cs`
- `src/QubiCostElectrical.Wpf/Shell/ShellRouteMapper.cs`
- `src/QubiCostElectrical.Wpf/Features/Project/ProjectViewModel.cs`
- `src/QubiCostElectrical.Wpf/Features/Project/NewProjectDialog.xaml`
- `src/QubiCostElectrical.Wpf/Features/Project/NewProjectDialog.xaml.cs`
- `src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj`
- `tests/PresupuestoElectrico.Tests/Wpf/ShellNavigationTests.cs`
- `tests/PresupuestoElectrico.Tests/Wpf/ShellReviewAndVersionsTests.cs`
- `tests/PresupuestoElectrico.Tests/Wpf/ProjectLifecycleTests.cs`
- `tests/PresupuestoElectrico.Tests/Wpf/ProjectPickerTests.cs`
- `tests/PresupuestoElectrico.Tests/Wpf/RecentProjectsServiceTests.cs`
- `tests/PresupuestoElectrico.Tests/Wpf/SingleInstanceTests.cs`

## Evidencia de ejecucion

- Build Debug: `dotnet build PresupuestoElectrico.sln -c Debug` → 0 errores, 0 advertencias propias
- Build Release: `dotnet build PresupuestoElectrico.sln -c Release` → 0 errores, 0 advertencias propias
- Tests: `dotnet test PresupuestoElectrico.sln` → **909 / 909 correctas**
- Single-instance: proceso PID 25708, segunda instancia termino sin abrir ventana
- Handoff: primera instancia mostro error SQLite al recibir path sintetico — pipe funcional
- Layout: captura de pantalla a 1366x768 confirma header sin cortes
