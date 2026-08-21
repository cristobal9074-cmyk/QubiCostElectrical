# Reglas UI

## Objetivo

La interfaz debe sentirse como una aplicacion profesional de escritorio: rapida, densa pero legible, estable y coherente entre modulos.

## Principios visuales

- Usar jerarquia clara: titulo, acciones, filtros, tabla, resumen y estado.
- Evitar textos cortados.
- Evitar controles superpuestos.
- Evitar botones que parezcan campos de texto.
- Evitar exceso de colores. Azul corporativo para accion principal; verde solo para correcto; rojo/ambar para alertas.
- Usar espacios compactos y consistentes.
- Mantener informacion importante visible sin scroll innecesario.

## Grillas

- Encabezados claros y cortos.
- Columnas con ancho fijo o proporcional segun uso.
- No mostrar demasiadas columnas si existe panel de detalle.
- Las columnas calculadas deben ser de solo lectura y tener color suave.
- Las celdas editables deben parecer editables.
- Navegacion por teclado debe ser prioritaria.
- Evitar refrescar toda la grilla al cambiar una celda.

## Navegacion

- Las pestañas principales deben ser predecibles:
  - Proyecto.
  - Itemizado.
  - Cubicacion.
  - C.SEC.
  - APU.
  - Materiales.
  - Revision.
  - Resumen.
  - Exportar.
  - Config.
- Cambiar de pestaña no debe provocar parpadeo visible.
- Conservar seleccion cuando sea posible.
- Las rutas viven en el enum `ShellRoute` (`Wpf/Navigation/ShellRoute.cs`); cada pantalla nueva agrega un caso en `ShellViewModel.Navigation.cs` (`Wpf/Shell/ShellViewModel.Navigation.cs`) que crea su `View` + `ViewModel` y los cachea en `pageCache`.
- `ShellWindow`/`ShellViewModel` actúan como shell: guardan estado visual, preparan la página y delegan el render.

## Arquitectura UI

Estructura vigente (WPF, post-retiro de WinForms):

- `Shell`: `ShellWindow` (contenedor) + `ShellViewModel` (partial: `.Navigation`, `.Project`, `.Workflow`) implementando `IShellNavigationHost`.
- `Navigation`: `ShellRoute` (enum de rutas) + el switch de creación de páginas en `ShellViewModel.Navigation.cs`. No existe una factory desacoplada — el registro de rutas nuevas requiere editar ese switch directamente.
- `Features/{Modulo}`: cada módulo trae su `View` (XAML) + `ViewModel` propio (ej. `Features/Cubicacion`, `Features/Itemizado`).
- `Presenters` (en Application): `ItemizadoPresenter`, `CubicacionPresenter`, `ExportPresenter` — preparan estado y casos de uso para la UI.
- `ViewModels`: datos listos para mostrar. Deben mantenerse delgados; la lógica de grilla/edición pertenece al ViewModel, no al code-behind (`.xaml.cs`).
- `Controls`: componentes reutilizables.
- `DesignSystem`: `Theme.xaml`, `Colors.xaml`, `Colors.Dark.xaml`, `Typography.xaml`, `Spacing.xaml`, `Buttons.xaml`, `Forms.xaml`, `DataGrid.xaml`, `Cards.xaml`, `Dialogs.xaml`, `Menus.xaml`, `Animations.xaml`, `ScrollBars.xaml`, `Converters.xaml`, `ThemeManager.cs`. Todo estilo de color/tipografía/espaciado debe salir de aquí, no hardcodearse en la vista.

Flujo de navegación:

1. UI dispara `RequestNavigate(ShellRoute, context?, force?)` en `ShellViewModel`.
2. Si hay cambios sin guardar en la página actual (`IPageWithPendingChanges`), se confirma descarte antes de continuar.
3. El switch en `ShellViewModel.Navigation.cs` resuelve la `View`/`ViewModel` para la ruta (desde `pageCache` si ya existe) y actualiza `currentView`/`currentPageVm`.

Notificaciones: sistema de toasts propio (`Controls/ToastHost`, orquestado por `Infrastructure/ShellNotificationService.cs`). No usar `MessageBox` (ver Puerta Fase 1, `docs/PRE_WEEK5_GATE.md`).

## Acciones

- `Guardar` y `Abrir` deben estar arriba.
- Acciones destructivas deben pedir confirmacion.
- Acciones frecuentes visibles; acciones secundarias en menu.
- Los botones deben usar verbos claros: Agregar, Duplicar, Quitar, Validar, Exportar.

## Estados

- Mostrar estado del proyecto y guardado.
- Mostrar alertas numericas sin invadir la pantalla.
- Mostrar errores con mensaje accionable.
- Nunca mostrar mensajes tecnicos crudos al usuario final (`ex.Message`, tipos de excepcion, stack trace).
- Seguir la guia unica de tono y estructura en `docs/ERROR_UX_GUIDE.md`.

## Rendimiento visual

- Usar doble buffer en grillas/paneles con repintado frecuente.
- Suspender layout durante cambios masivos.
- Usar debounce para busquedas y resumenes costosos.
- Evitar reconstruir controles completos cuando solo cambia un valor.

## Regla general

Si una pantalla parece una planilla Excel pegada, revisar si corresponde:

- panel de contexto;
- tabla principal limpia;
- panel de detalle;
- resumen ejecutivo;
- acciones rapidas por lote.
