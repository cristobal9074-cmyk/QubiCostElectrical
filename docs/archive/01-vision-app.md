# Vision de la aplicacion

## Objetivo

Presupuesto Electrico PRO es una aplicacion de escritorio para preparar presupuestos electricos profesionales de edificios, reemplazando el flujo disperso de Excel por un sistema local, trazable y rapido.

La aplicacion debe permitir trabajar desde el itemizado de la constructora hasta la oferta final, manteniendo el detalle tecnico interno separado del resumen para cliente.

## Alcance funcional

- Datos del proyecto, cliente, moneda, UF, estado y parametros comerciales.
- Itemizado jerarquico con partidas, titulos, cantidades y unidades.
- Cubicacion por partida con materiales, cantidades, precios, mano de obra y totales.
- Cubicacion especializada de servicios comunes (`C.SC` / `C.SEC`) por tableros y circuitos.
- Banco de precios con busqueda rapida por codigo, descripcion y jerarquia tecnica.
- APU por partida con materiales, mano de obra, equipos, herramientas, mermas e imprevistos.
- Revision final de errores y advertencias antes de exportar.
- Exportacion profesional a Excel, PDF y documentos auxiliares cuando aplique.
- Persistencia local en SQLite con migraciones y guardado incremental.

## Arquitectura real

El proyecto esta organizado en capas:

- `PresupuestoElectrico.Domain`: modelos, motores de calculo y reglas puras.
- `PresupuestoElectrico.Application`: servicios de aplicacion, validacion y view models.
- `PresupuestoElectrico.Infrastructure`: SQLite, Excel, PDF, Word y adaptadores externos.
- `PresupuestoElectrico.WinForms`: interfaz de escritorio WinForms, navegacion y composicion visual.
- `PresupuestoElectrico.Tests`: pruebas unitarias de calculo, APU, validacion y servicios.

## Versionado vigente

La fuente de verdad esta en `Directory.Build.props` (`<VersionPrefix>`).

| Componente | Version |
| --- | --- |
| Producto | 1.0.0-rc.2 |
| Base de datos SQLite | 20 |
| Formato de proyecto | 6 |
| Motor de calculo | 2 |

Las notas `Hist.` en comentarios son referencias historicas de implementacion, no version vigente del producto. Nuevos cambios no deben agregar versiones en comentarios: deben actualizar `ProductVersionInfo` y, si corresponde, documentar el cambio aqui.

## Principios de producto

- La app debe sentirse como una herramienta profesional de presupuestos, no como una maqueta.
- La UI debe ser rapida, legible y util en pantallas medianas y grandes.
- Los datos internos de cubicacion y APU no deben mezclarse con la oferta al cliente.
- La persistencia debe evitar perdida de datos y mantener compatibilidad hacia atras.
- Cada cambio debe mantener compilacion, pruebas basicas y apertura de la aplicacion.

## Criterio de exito

Una mejora esta lista cuando:

- Compila sin errores.
- No rompe datos existentes.
- El flujo principal sigue usable.
- La UI no corta textos ni controles.
- Los calculos siguen saliendo desde una fuente de verdad.
- Se puede explicar donde vive la logica y como probarla.
