# Exportacion

## Objetivo

La exportacion debe producir archivos profesionales, limpios y separados por destino: control interno, oferta cliente, compras, auditoria y respaldos tecnicos.

Archivos principales:

- `QubiCostElectrical.Infrastructure/Excel/ExcelExportInfrastructure.cs`
- `QubiCostElectrical.Infrastructure/Excel/CubicacionReportExcelExporter.cs`
- `QubiCostElectrical.Infrastructure/Pdf/QuestPdfProjectExporter.cs`
- `QubiCostElectrical.Infrastructure/Pdf/QuestPdfCubicacionReportExporter.cs`
- `QubiCostElectrical.Infrastructure/Word/PresentationLetterDocxExporter.cs`

## Excel principal

El exportador Excel genera hojas como:

- `00_CONTROL`.
- `01_DATOS_PROYECTO`.
- `02_ITEMIZADO_CLIENTE`.
- `03_CUBICACION_DETALLE`.
- `04_APU`.
- `05_LISTA_PRECIOS`.
- `06_HOJA_COSTO`.
- `07_SENSIBILIDAD`.
- `08_REVISION_FINAL`.
- `09_OFERTA_CLIENTE`.
- `10_AUDITORIA_PRECIOS`.
- `11_FORMATO_COMPRA`.

La hoja `00_CONTROL` debe registrar siempre:

- version producto;
- version base datos;
- version formato proyecto;
- version motor calculo;
- usuario y fecha de exportacion.

## Reglas de exportacion

- Usar `ExportProjectContext` como entrada.
- No tomar datos directamente desde controles de la vista WPF; usar los ViewModels/Presenters.
- Calcular costos con `BudgetCalculationService`.
- Convertir monedas con `CurrencyConversionService`.
- Usar `ClosedXmlExportStyler` para formato consistente.
- Ajustar columnas, encabezados, formatos numericos y tablas.
- Separar hojas internas de hojas para cliente.

## Oferta cliente

Debe mostrar solo informacion entregable:

- datos del proyecto;
- cliente;
- fecha;
- itemizado valorizado;
- totales;
- moneda;
- condiciones comerciales si aplica.

## PDF de oferta

El PDF de oferta es el documento principal para cliente y debe tratarse con la misma importancia que el Excel interno.

Estructura minima:

- portada con identidad visual;
- datos empresa, cliente, obra y ubicacion;
- folio interno;
- hash SHA-256 corto de control;
- version producto, formato proyecto y motor de calculo;
- resumen ejecutivo;
- alcance incluido;
- exclusiones;
- condiciones comerciales;
- observaciones;
- plazo;
- validez de oferta;
- tabla de partidas;
- total UF;
- total referencial CLP;
- firma;
- anexos de areas, marcas/especificaciones y planos.

El PDF no debe ser una descarga de texto plano. Debe tener encabezados, separadores, bloques de totales, pie de pagina y trazabilidad.

No debe mostrar:

- errores internos;
- formulas tecnicas;
- datos de depuracion;
- alertas crudas de validacion;
- columnas auxiliares de cubicacion.

## Revision antes de exportar

Antes de exportar se debe revisar:

- partidas sin cubicacion;
- materiales sin precio;
- cantidades cero o sospechosas;
- unidades faltantes;
- APU incompleto;
- moneda y UF configuradas;
- totales en cero.

## Mejoras pendientes

- Plantillas de formato por empresa. [implementado en preferencias, PDF y Excel]
- Portada PDF configurable. [implementado en PDF]
- Marca de agua para borrador. [implementado en PDF]
- Exportacion parcial por partida o area. [implementado en WPF]
- Reporte tecnico de C.SEC. [implementado en WPF]
