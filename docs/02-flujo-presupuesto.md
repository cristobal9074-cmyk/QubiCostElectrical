# Flujo de presupuesto

## Flujo principal

1. Crear o abrir proyecto.
2. Completar datos del proyecto: cliente, obra, moneda, UF, fecha, estado y parametros comerciales.
3. Cargar o editar itemizado.
4. Cubicar partidas con materiales y cantidades.
5. Completar banco de precios y corregir recursos sin precio.
6. Generar o revisar APU cuando corresponda.
7. Ejecutar revision final.
8. Exportar oferta, detalle interno y formatos auxiliares.
9. Guardar proyecto en SQLite.

## Entidades principales

- `ProjectInfoRow`: datos generales, moneda, UF, estado y parametros comerciales.
- `ItemizadoRow`: partidas, titulos, jerarquia, unidades y cantidades.
- `CubicacionRow`: recursos cubicados asociados a una partida.
- `MaterialRow`: banco de precios y metadata tecnica.
- `ApuRow`: analisis de precio unitario por partida.
- `BudgetVersionRow`: snapshots/versiones de presupuesto.
- `ChangeOrderRow`: ordenes de cambio.

## Relacion entre modulos

El itemizado es el eje del presupuesto. Cada linea de cubicacion apunta a una partida mediante `ItemId`. Los APU tambien se asocian a una partida mediante `ItemId`.

La cubicacion puede valorizarse directamente con `MatUf` y `MoUf`, o puede delegar la valorizacion al APU si la politica de costos lo indica.

El banco de precios alimenta:

- autocompletado de cubicacion;
- precios unitarios de cubicacion;
- recursos de APU;
- revision de materiales sin precio;
- exportacion y trazabilidad.

## Estados esperados

- Proyecto: Borrador, En revision, Aprobado o estado equivalente definido por UI.
- Partida: vacia, en proceso, cubicada, incompleta o revisada.
- Material: OK, sin precio, vencido o revisar.
- APU: OK, falta precio o revisar rendimiento.

## Reglas de trabajo

- No borrar partidas al recalcular.
- No reemplazar datos del usuario sin confirmacion.
- No enviar automaticamente al cliente informacion interna de control.
- Antes de exportar, usar revision final para detectar problemas.
- Los totales deben venir del motor de costos, no de sumas duplicadas en UI.

