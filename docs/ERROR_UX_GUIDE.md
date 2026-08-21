# Guía única de tono de error (WPF)

Objetivo: ningún usuario final debe ver mensajes técnicos crudos.  
Cada error visible debe responder tres cosas: qué pasó, qué hacer ahora y (opcional) cómo rastrearlo.

## Plantilla estándar

Mensaje recomendado:

1. **Qué pasó** (lenguaje simple, sin excepción técnica ni stack trace).
2. **Qué hacer ahora** (acción concreta: reintentar, cerrar archivo bloqueado, guardar copia, abrir logs).
3. **ID de error (opcional)** para soporte cuando exista correlación en logs.

Formato:

`<Mensaje claro>\n\nQué hacer ahora: <acción concreta>\n\nID de error: <id>`

## Reglas obligatorias

- Usar `OperationFailureMessages.ToUserMessage(ex)` para errores recuperables en UI.
- No mostrar `ex.Message`, `ex.GetType().Name`, `StackTrace` ni `InnerException` en diálogos.
- En errores críticos de guardado/exportación:
  - indicar acción inmediata (`Reintenta`, `Cierra el archivo`, `Guarda una copia`);
  - sugerir `abrir carpeta de logs` cuando la falla persista.
- Si hay `errorId`, incluirlo siempre al final del mensaje.

## Estado de pantalla tras error

- Preview/export nunca debe quedar en estado ambiguo.
- Si falla la generación de vista previa:
  - `PreviewStatusText` debe quedar en estado de error claro;
  - `PreviewFileText` debe volver a `"Sin vista previa generada."`;
  - limpiar referencia de preview (`ExportResult.SetPreview(null)`).

## Checklist de revisión rápida

- `rg "WpfDialogService\\.Error\\([^\\n]*ex\\.Message"` en `src/QubiCostElectrical.Wpf` sin resultados.
- `rg "GetType\\(\\)\\.Name|StackTrace|InnerException"` en `src/QubiCostElectrical.Wpf` sin uso en diálogos.
- Flujos de preview/export verifican estado explícito en `catch` y fallos parciales.
