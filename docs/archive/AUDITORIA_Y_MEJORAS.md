# Estado funcional y mejoras pendientes - Presupuesto Electrico PRO

Version de referencia: 3.140.2 - DB segun `ProductVersionInfo.DatabaseSchemaVersion` - Formato segun `ProductVersionInfo.ProjectFormatVersion` - Motor 2.

## Producto comercial actual

- Centro de proyectos.
- Datos del proyecto.
- Itemizado.
- Cubicacion.
- Cubicacion S.C. como herramienta secundaria.
- APU como herramienta contextual.
- Precios, busqueda, lista de compra y auditoria de precios.
- Revision final.
- Exportacion Excel / PDF / Word.
- Persistencia local en SQLite con migraciones.

## Fuera del producto comercial

Estos modulos no deben aparecer en navegacion, paleta global ni documentacion comercial hasta que tengan pruebas, flujo funcional completo y soporte:

- CRM.
- Estados de pago.
- Reajuste polinomico.
- Varianza como pagina independiente.
- Jornadas.
- Importacion inteligente.
- Calculadora como pagina independiente.
- Precios vencidos como pagina independiente.
- Analisis Pro / Live.

## Eliminado del flujo productivo

- Pagina Itemizado nueva no usada.
- Servicio `ItemizadoPageService` no usado.
- Paginas antiguas de Analisis Pro / Live.
- Rutas independientes `AnalisisPro`, `AnalisisLive` y `Sensibilidad`.
- Pantalla independiente de Mediciones. Las mediciones deben vivir dentro de Cubicacion.

## Pendientes de mayor impacto

| Prioridad | Area | Trabajo |
|---|---|---|
| Alta | Persistencia | Verificar que cada dato nuevo entra a `ProjectSession.CreateSnapshot()` y `ApplySnapshot()`. |
| Alta | Cubicacion | Completar lineas de medicion dentro de Cubicacion con trazabilidad de plano, revision, piso y sector. |
| Alta | Motor costos | Agregar pruebas para cantidades negativas, neto cero y combinacion Cubicacion/APU. |
| Media | UI | Reemplazar `MessageBox.Show` frecuentes por notificaciones no bloqueantes. |
| Media | UI | Eliminar colores/fuentes directas fuera de `AppTheme`. |
| Media | Exportacion | Consolidar PDF cliente con portada, alcance, condiciones, totales y versionado. |

## Regla de producto

Antes de sumar dashboards, gauges, animaciones o modulos nuevos, debe quedar garantizado que:

- los datos se guardan;
- los totales se reproducen;
- los cambios tienen pruebas;
- la navegacion visible solo muestra funciones mantenibles;
- el PDF/Excel exportado coincide con el calculo interno.
