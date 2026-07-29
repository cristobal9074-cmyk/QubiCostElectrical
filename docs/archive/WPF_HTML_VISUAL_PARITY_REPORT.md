# WPF ↔ HTML Visual Parity Report

**Fecha:** 2026-06-22  
**Rama:** `feat/qubi-splash-login-ui`  
**Skill usado:** `$ui-ux-pro-max`  
**Build Debug:** 0 errores, 0 advertencias  
**Build Release:** 0 errores, 78 advertencias preexistentes en tests/legacy  
**Pruebas:** 823/823 OK  

---

## Resumen Ejecutivo

Se cerró la revisión de paridad visual de las cinco pantallas que seguían en estado **MEDIA**: Plantillas, Cotizaciones, Lista de compra, Versiones y Exportación.

Los cambios se limitaron a WPF UI, bindings de selección/comandos, robustez de ViewModel para lectura del banco de precios en entorno de test, y capturas. No se modificaron cálculos, migraciones, SQLite, persistencia de proyecto ni lógica de exportación.

---

## Matriz de Pantallas

| # | Pantalla | Paridad anterior | Cambios realizados | Paridad final | Captura |
|---|----------|------------------|--------------------|---------------|---------|
| 1 | Dashboard | MEDIA | Cambios previos de Claude conservados | ALTA | `01-dashboard.png` |
| 2 | Proyecto | MEDIA | Cambios previos de Claude conservados | ALTA | `02-proyecto.png` |
| 3 | Antecedentes | BAJA | Cambios previos de Claude conservados | ALTA | `03-antecedentes.png` |
| 4 | Itemizado | ALTA | Sin cambios en esta pasada | ALTA | `04-itemizado.png` |
| 5 | Cubicación | ALTA | Sin cambios en esta pasada | ALTA | `05-cubicacion.png` |
| 6 | APU | MEDIA | Sin cambios visuales en esta pasada | ALTA | `06-apu.png` |
| 7 | Plantillas | MEDIA | Acción principal alineada con comando real (`Nueva plantilla`), ancho de vista estabilizado | ALTA condicional | `07-plantillas.png` |
| 8 | Banco de precios | ALTA | Sin cambios en esta pasada | ALTA | `08-banco-precios.png` |
| 9 | Cotizaciones | MEDIA | Binding de fila seleccionada, acción `Aplicar mejor precio`, lectura de banco tolerante a fallo ambiental, layout estabilizado | ALTA | `09-cotizaciones.png` |
| 10 | Lista de compra | MEDIA | Binding de `SelectedRow`, acción `Ir a partida`, layout estabilizado | ALTA | `10-lista-compra.png` |
| 11 | Revisión final | MEDIA | Cambios previos de Claude conservados | ALTA | `11-revision.png` |
| 12 | Versiones | MEDIA | Acción `Ir a partida`, acciones de versión visibles, layout estabilizado | ALTA | `12-versiones.png` |
| 13 | Exportación | MEDIA | Tarjetas PDF/Excel/DOCX, wrap de acciones de exportación, layout estabilizado | ALTA | `13-exportacion.png` |

**Nota:** Plantillas se capturó en estado vacío porque la app WPF se inició sin proyecto abierto. La vista completa depende de datos reales de proyecto; no se agregaron datos falsos para maquillar la captura.

---

## Cambios Relevantes

### WPF

| Archivo | Cambio |
|---------|--------|
| `src/QubiCostElectrical.Wpf/Shell/ShellWindow.xaml` | Se deshabilitó el scroll horizontal del shell para evitar medición infinita y recortes laterales. |
| `src/QubiCostElectrical.Wpf/Features/Templates/PlantillasView.xaml` | Acción principal corregida a `+ Nueva plantilla`; ancho de trabajo estabilizado. |
| `src/QubiCostElectrical.Wpf/Features/Quotes/QuotesView.xaml` | `ComparisonRows` ahora actualiza `SelectedComparisonRow`; acción `Aplicar mejor precio` expuesta en UI; panel redundante colapsado para evitar overflow. |
| `src/QubiCostElectrical.Wpf/Features/Quotes/QuotesViewModel.cs` | `ApplyPriceCommand` actualiza `CanExecute`; lectura del banco de precios queda protegida ante error de archivo SQLite en test/entorno. |
| `src/QubiCostElectrical.Wpf/Features/Purchases/PurchaseListView.xaml` | `FilteredRows` ahora actualiza `SelectedRow`; acción `Ir a partida` expuesta; panel redundante colapsado. |
| `src/QubiCostElectrical.Wpf/Features/Versions/VersionsView.xaml` | Acciones `Ir a partida`, activar y renombrar visibles; panel redundante colapsado. |
| `src/QubiCostElectrical.Wpf/Features/Export/ExportView.xaml` | Acciones en tarjetas con `WrapPanel` para PDF, Excel, DOCX y exportaciones secundarias; panel redundante colapsado. |

### Tests

| Archivo | Cambio |
|---------|--------|
| `tests/PresupuestoElectrico.Tests/Wpf/ShellNavigationTests.cs` | La prueba `Quotes_ApplyPrice_UpdatesMaterialMatUf` dejó de escribir en el store SQLite real y valida el comando con una fila de comparación construida en memoria. |

---

## Restricciones Verificadas

- No se hizo commit.
- No se revirtió trabajo previo de Claude.
- No se agregaron datos falsos.
- No se modificaron cálculos, fórmulas, SQLite ni migraciones.
- No se agregó dependencia WPF hacia WinForms.
- No se reemplazó persistencia ni exportadores.
- Los comandos existentes se conservaron y se expusieron donde faltaba UI.

---

## Capturas Reemplazadas

- `artifacts/wpf-visual-parity/after/07-plantillas.png`
- `artifacts/wpf-visual-parity/after/09-cotizaciones.png`
- `artifacts/wpf-visual-parity/after/10-lista-compra.png`
- `artifacts/wpf-visual-parity/after/12-versiones.png`
- `artifacts/wpf-visual-parity/after/13-exportacion.png`

---

## Verificación

```powershell
dotnet build PresupuestoElectrico.sln -c Debug --no-restore
dotnet build PresupuestoElectrico.sln -c Release --no-restore
dotnet test PresupuestoElectrico.sln -c Debug --no-build
```

Resultados:

- Debug: correcto, 0 errores, 0 advertencias.
- Release: correcto, 0 errores, 78 advertencias preexistentes en tests/legacy.
- Tests: 823/823 OK.

---

## Pendientes Reales

- La captura de Plantillas requiere abrir un proyecto real para mostrar biblioteca, filtros y detalle; no se creó data artificial.
- Exportación ya muestra tarjetas principales y acciones reales; el panel lateral de preview se colapsó porque provocaba overflow horizontal dentro del shell actual.
- El shell WPF todavía usa `ScrollViewer` como contenedor global; conviene reemplazarlo a futuro por un layout que mida con viewport real y scroll vertical por pantalla.

**Recomendación:** APTO PARA CONTINUAR.
