# Fase 8 — Informe de compatibilidad de exportación

## Resumen

Verificación de que los 6 documentos de entrega se exportan correctamente desde la nueva UI WPF reutilizando exportadores existentes.

## Matriz de compatibilidad

| Documento | Exportador | Formato | Librería | Requiere MS Office | Reutilizado |
|---|---|---|---|---|---|
| Oferta comercial PDF | `QuestPdfProjectExporter` | PDF | QuestPDF 2025.6 | No | ✅ |
| Excel interno control | `ExcelExportInfrastructure` | XLSX | ClosedXML 0.104 | No | ✅ |
| Carta presentación | `PresentationLetterDocxExporter` | DOCX | DocumentFormat.OpenXml 3.2 | No | ✅ |
| Memoria cubicación Excel | `CubicacionReportExcelExporter` | XLSX | ClosedXML 0.104 | No | ✅ |
| Memoria cubicación PDF | `QuestPdfCubicacionReportExporter` | PDF | QuestPDF 2025.6 | No | ✅ |
| Reporte técnico C.SEC | `CubicacionSCTechnicalReportExcelExporter` | XLSX | ClosedXML 0.104 | No | ✅ |

## Dependencias

| Librería | Versión | Uso | Licencia |
|---|---|---|---|
| QuestPDF | 2025.6+ | PDF generation | Community (gratuita) |
| ClosedXML | 0.104 | Excel (.xlsx) | MIT |
| DocumentFormat.OpenXml | 3.2+ | Word (.docx) | MIT |

## Datos de entrada

Todos los exportadores reciben `ExportProjectContext` con:
- `Data`: snapshot JSON del proyecto (`ProjectFileData`)
- `OfferSettings`: configuración de oferta (`OfferPdfSettings`)
- `CorporateTheme`: tema corporativo desde preferencias de usuario
- `ReportTemplate`: plantilla de informe desde preferencias de usuario

## Flujo de datos

```
Session → CreateSnapshot() → ExportProjectContext → Exportador → Archivo
```

Los datos se congelan al momento de la exportación via:
- `session.CreateSnapshot()`: deep clone JSON del proyecto completo (`ProjectFileData`)
- `dashboard.Alerts` clonado en `FinalReviewSummary` independiente (`Issues.ToList()`) dentro de `GetSelectedContext()`

No hay riesgo de modificación concurrente ni referencias mutables al dashboard.

## Apertura de archivos

| Resultado | Significado |
|---|---|
| `Success` | Archivo generado y abierto con visor del SO |
| `ViewerFailed` | Archivo generado pero el visor no pudo abrirlo (extension no soportada, visor ausente) |
| `FileNotFound` | Archivo no encontrado en la ruta esperada |

No se muestran stack traces al usuario. `IFileOpenService` abstrae la lógica para testabilidad.

## Limpieza temporal

- Las previews se almacenan en `%TEMP%\QubiCostElectrical\Preview\{GUID}\`
- `CleanupOldPreviews()` elimina solo carpetas con más de 24 horas de antigüedad
- Las carpetas recientes no se eliminan
- Si un archivo está bloqueado, se registra con `Debug.WriteLine` sin interrumpir la preview
- No existen `catch {}` silenciosos

## Límites conocidos

- Sin manejo de rutas >260 caracteres (prefix `\\?\`)
- `PresentationLetterDocxExporter` sobrescribe archivo existente sin preguntar
- Sin versión seleccionable: se exporta el estado actual del proyecto

## Pruebas automatizadas

- Build: 0 errores (Debug + Release)
- Tests: **842/842** aprobadas, 0 omitidas, 0 fallidas
- Tests nuevos:
  - **10 casos** en `DeliveryViewModelTests.cs` (estados, Blockers, IsBusy, CanExport, Refresh, vista previa correcta/fallida, entrega correcta/fallida, vaciado de errores)
  - **9 casos** en `DeliveryFrozenContextTests.cs` (ReviewSummary congelado: 2; FileOpenResult: 4; apertura en VM: 1; CleanupOldPreviews: 2)

## Certificación manual

Verificado con proyecto real el 22-jun-2026. Monitor 1920×1080, 96 DPI.

### Resultados por documento

| Documento | Exportador | Formato | ¿Se abrió? | Totales | Datos | Visual | Resultado |
|---|---|---|---|---|---|---|---|
| Oferta comercial PDF | `QuestPdfProjectExporter` | PDF | ✅ Visor predet. | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Excel interno control | `ExcelExportInfrastructure` | XLSX | ✅ Excel | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Carta presentación | `PresentationLetterDocxExporter` | DOCX | ✅ Word | N/A | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Memoria cubicación Excel | `CubicacionReportExcelExporter` | XLSX | ✅ Excel | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Memoria cubicación PDF | `QuestPdfCubicacionReportExporter` | PDF | ✅ Visor predet. | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |
| Reporte técnico C.SEC | `CubicacionSCTechnicalReportExcelExporter` | XLSX | ✅ Excel | ✅ Coinciden | ✅ Correctos | ✅ Sin problemas | **APROBADO** |

### Comportamiento adicional

| Escenario | Resultado |
|---|---|
| Bloqueo con hallazgos críticos → botón Entregar deshabilitado | ✅ |
| Cancelación del selector de carpeta → no se exporta nada | ✅ |
| Botón "Abrir carpeta de preview" → abre Explorer | ✅ |
| Persistencia OfferPdfSettings al reabrir `.pepro` | ✅ |
| Modificación del proyecto → nueva exportación con datos actualizados | ✅ |

**Veredicto: 6/6 documentos aprobados. Flujo de exportación certificado funcionalmente en el entorno y proyecto de prueba documentados.**
