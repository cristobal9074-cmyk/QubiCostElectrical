# Fase 8 — Informe de entrega (Delivery)

## Resumen

La Fase 8 implementa la pantalla de **preparación y exportación de documentos de oferta** en la UI WPF. Usa exportadores reales (QuestPDF, ClosedXML, OpenXML) sin depender de Microsoft Office ni WebView2.

## Archivos nuevos/modificados

### Nuevos (en `src/QubiCostElectrical.Wpf/Features/Delivery/`)

| Archivo | Propósito |
|---|---|
| `DeliveryViewModel.cs` | ViewModel principal: proyecto, alertas, exportación |
| `DeliveryView.xaml` | Layout: info proyecto, selector documentos, acciones, resultados |
| `DeliveryView.xaml.cs` | Code-behind mínimo |
| `DeliveryDocumentSelectorVm.cs` | 6 documentos seleccionables con eventos |
| `DeliveryPreviewService.cs` | Preview PDF + exportación individual por documento |
| `DeliveryExportResultVm.cs` | Resultados visibles en UI |
| `OfferSettingsEditorVm.cs` | Editor de 28 campos de configuración de oferta |

### Nuevos (en `src/QubiCostElectrical.Wpf/Infrastructure/`)

| Archivo | Propósito |
|---|---|
| `IFileDialogService.cs` | Abstracción para diálogos WPF (Save, Open, Folder) |
| `WpfFileDialogService.cs` | Implementación con `SaveFileDialog`, `OpenFileDialog`, `OpenFolderDialog` |
| `IFileOpenService.cs` | Abstracción testeable para abrir archivos/carpetas con el SO |
| `WpfFileOpenService.cs` | Implementación via `Process.Start` + `UseShellExecute = true` |

### Modificados

| Archivo | Cambio |
|---|---|
| `WpfAppHost.cs` | `+QuestPdfBootstrap.EnsureCommunityLicense()`, `+IFileDialogService`, `+IFileOpenService` |
| `WpfDialogService.cs` | `+Warning()` method |
| `ShellRoute.cs` | `+Delivery` enum y catálogo |
| `ShellViewModel.cs` | `+case Delivery`, `+IsDeliveryActive`, quick search |
| `ShellWindow.xaml` | `+Botón "Oferta"` en navbar |
| `ShellWindow.xaml.cs` | `+OnDeliveryClick`, navegación visual, dropdown Cierre |

## Formas de entrega

| Documento | Formato | Exportador | Dependencia |
|---|---|---|---|
| Oferta comercial PDF | PDF | `QuestPdfProjectExporter` | QuestPDF |
| Excel interno control | XLSX | `ExcelExportInfrastructure` | ClosedXML |
| Carta presentación | DOCX | `PresentationLetterDocxExporter` | OpenXML SDK |
| Memoria cubicación Excel | XLSX | `CubicacionReportExcelExporter` | ClosedXML |
| Memoria cubicación PDF | PDF | `QuestPdfCubicacionReportExporter` | QuestPDF |
| Reporte técnico C.SEC | XLSX | `CubicacionSCTechnicalReportExcelExporter` | ClosedXML |

Ninguno requiere Microsoft Office instalado.

## Flujo de exportación

1. Usuario selecciona documentos en checklist
2. Configura datos de oferta (28 campos en `OfferPdfSettings`)
3. Botón "Vista previa" → genera PDF temporal y abre con visor predeterminado
4. Botón "Entregar" → elige carpeta → exporta documentos seleccionados en paralelo

Los datos se congelan vía `session.CreateSnapshot()` (deep clone JSON). Además, `ReviewSummary` se clona independientemente (`Issues.ToList()`) dentro de `GetSelectedContext()` para evitar referencias mutables al dashboard.

## Decisiones técnicas

- `IFileDialogService` abstrae diálogos WPF para testabilidad
- `IFileOpenService` + `WpfFileOpenService` abstraen apertura de archivos/carpetas (evita `Process.Start` directo en ViewModel)
- `WpfFileDialogService` usa `Microsoft.Win32.OpenFolderDialog` (.NET 8), no WinForms
- Vista previa usa `IFileOpenService.OpenFile()` que internamente usa `Process.Start` con `UseShellExecute = true`
- `CleanupOldPreviews()` elimina solo carpetas >24h en `%TEMP%\QubiCostElectrical\Preview\`; errores se registran con `Debug.WriteLine` sin interrumpir
- Resultado de apertura de archivo se diferencia: `Success`, `FileNotFound`, `ViewerFailed`
- Botón "Abrir carpeta de preview" disponible después de generar preview

## Hallazgos corregidos (QA post-impl)

| ID | Hallazgo | Corrección |
|---|---|---|
| B1 | `CanExport()` no verificaba `Blockers > 0` | Agregado `Blockers == 0` |
| B2 | Sin `IsBusy` — exportaciones simultáneas posibles | Agregado `IsBusy` + try/finally |
| B3 | `catch {}` vacío silenciaba error de visor PDF | Agregado `Debug.WriteLine` |
| B4 | Archivos temporales nunca se limpiaban | `CleanupOldPreviews()` al generar preview (solo >24h) |
| B5 | `RefreshFromSnapshot()` vacío | Eliminado |
| B6 | Sin cobertura de pruebas para Delivery | 10 tests en `DeliveryViewModelTests.cs` |
| B7 | Sin reportes de documentación | `WPF_PHASE8_DELIVERY_REPORT.md`, `WPF_PHASE8_EXPORT_COMPATIBILITY_REPORT.md` |
| B8 | Overload `ExportAllAsync(ctx, folder)` no usado | Eliminado |
| D1 | `Process.Start` en ViewModel no testeable; errores de visor sin diagnóstico | `IFileOpenService` abstraído; tres resultados (Success/ViewerFailed/FileNotFound); Warning sin stack trace; botón "Abrir carpeta" |
| D2 | `ReviewSummary` compartía referencia mutable con dashboard | Clonado en `GetSelectedContext()`; prueba de congelamiento agregada |
| D3 | `CleanupOldPreviews()` sin filtro temporal; errores silenciados | Filtro >24h; `Debug.WriteLine` diagnóstico; no bloquea en archivos bloqueados |

## Pruebas

- **10 tests** en `DeliveryViewModelTests.cs` (estados, Blockers, IsBusy, CanExport, Refresh, vista previa correcta/fallida, entrega correcta/fallida, vaciado de errores)
- **9 tests** en `DeliveryFrozenContextTests.cs` (ReviewSummary congelado: 2; FileOpenResult: 4; apertura en VM: 1; CleanupOldPreviews: 2)
- **842/842** pruebas totales aprobadas

## Certificación manual

Verificado con proyecto real el 22-jun-2026. Monitor 1920×1080, 96 DPI.

### Resultados por documento

| Documento | Archivo | Formato | Exportador | ¿Se abrió? | Totales | Datos | Visual | Resultado |
|---|---|---|---|---|---|---|---|---|
| Oferta comercial PDF | `{proyecto}-Oferta.pdf` | PDF | `QuestPdfProjectExporter` | ✅ Visor predet. | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Excel interno control | `{proyecto}-Control.xlsx` | XLSX | `ExcelExportInfrastructure` | ✅ Excel | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Carta presentación | `{proyecto}-Carta.docx` | DOCX | `PresentationLetterDocxExporter` | ✅ Word | N/A | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Memoria cubicación Excel | `{proyecto}-Memoria-Cubicacion.xlsx` | XLSX | `CubicacionReportExcelExporter` | ✅ Excel | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Memoria cubicación PDF | `{proyecto}-Memoria-Cubicacion.pdf` | PDF | `QuestPdfCubicacionReportExporter` | ✅ Visor predet. | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Reporte técnico C.SEC | `{proyecto}-Reporte-CSEC.xlsx` | XLSX | `CubicacionSCTechnicalReportExcelExporter` | ✅ Excel | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |

### Comportamiento adicional

| Escenario | Resultado |
|---|---|
| Bloqueo con hallazgos críticos: botón "Entregar" deshabilitado | ✅ Correcto |
| Cancelación del selector de carpeta: no se exporta nada | ✅ Correcto |
| Botón "Abrir carpeta de preview" | ✅ Abre carpeta en Explorer |
| Persistencia de OfferPdfSettings al reabrir `.pepro` | ✅ Campos recuperados |
| Modificar proyecto después de exportar: contexto actualizado en nueva exportación | ✅ Correcto |

## Riesgos documentados

- No hay selector de versión (`BudgetVersion`): se exporta desde el estado vivo
- `SaveOfferSettings()` marca dirty pero no persiste explícitamente
- Sin validación de campos obligatorios en `OfferPdfSettings`
