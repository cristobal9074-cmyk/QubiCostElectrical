# Cubicacion electrica

## Objetivo

La cubicacion debe permitir transformar partidas del itemizado en recursos medibles: materiales, mano de obra, cantidades y totales.

La pantalla principal de cubicacion trabaja por partida y usa `CubicacionRow`. La pantalla `C.SC` / `C.SEC` cubica servicios comunes por tableros y circuitos, generando materiales agregados que pueden enviarse al presupuesto.

## Modelo base

`CubicacionRow` contiene:

- `ItemId`: partida asociada.
- `Codigo`: codigo de material o recurso.
- `Descripcion`.
- `Unidad`.
- `Cantidad`.
- `MatUf`.
- `MoUf`.
- `Area`.
- `QtyExpressions`: expresiones originales de cantidad.
- `TotalExpression`: formula opcional de total.
- Politicas de merma, herramientas y redondeo.

## Cubicacion normal

Debe ser rapida y parecida a Excel:

- agregar material;
- duplicar fila;
- editar cantidad;
- aceptar cantidades negativas;
- autocompletar por codigo y descripcion;
- recalcular totales sin bloquear UI;
- navegar por teclado;
- mantener seleccion de partida sin parpadeos.

## Cubicacion C.SEC / C.SC

Modelo especializado:

- `CuadroCargaSCRow`: tablero/cuadro de carga.
- `CircuitoSCRow`: circuito con tipo, estado, distancia, largo, tuberia, conductor y artefactos.
- `ArtefactoSCRow`: protecciones, enchufes u otros elementos por circuito.

Reglas actuales:

- Circuito Activo: cubica canalizacion, conductores y artefactos.
- Reserva equipada: cubica solo proteccion/artefactos.
- Disponible: no cubica, solo ocupa espacio.
- El resumen agregado se envia a una partida `CUBICACION SERVICIOS COMUNES (C.SC)`.

## Mejoras ergonomicas esperadas

- Trabajar por tablero activo.
- Crear circuitos por lote.
- Usar plantillas por tipo de circuito.
- Evitar pedir datos repetidos en cada fila.
- Mantener resumen ejecutivo visible.
- Separar detalle avanzado del grid principal.

## Riesgos

- Recalcular todo el resumen por cada celda puede sentirse lento en proyectos grandes.
- Buscar materiales con precio debe usar indices o diccionarios temporales.
- La UI no debe perder seleccion al refrescar grillas.
- El envio a presupuesto debe ser idempotente: reemplaza el resumen C.SC anterior sin duplicarlo.

