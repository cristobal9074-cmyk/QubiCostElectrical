# Banco de precios

## Objetivo

El banco de precios es el catalogo tecnico y economico que alimenta cubicacion, APU, revision, compras y exportacion.

Modelo principal:

- `MaterialRow`.

Servicio principal:

- `MaterialSearchService`.

## Campos principales

- `Codigo`.
- `Descripcion`.
- `Unidad`.
- `Categoria`.
- `Subcategoria`.
- `Subcategoria2`.
- `TipoInstalacion`.
- `EspecificacionTecnica`.
- `MatUf`.
- `MoUf`.
- `MonedaPrecio`.
- `UfUsadaClp`.
- `Proveedor`.
- `Marca`.
- `EquivalenciaKey`.
- `OrigenPrecio`.
- `FechaPrecio`.
- `UsuarioPrecio`.
- `EstadoPrecio`.

## Busqueda

La busqueda debe ser tolerante:

- ignora mayusculas/minusculas;
- remueve espacios, puntos, guiones y acentos;
- permite buscar por codigo, descripcion, categoria, jerarquia, proveedor, marca y equivalencia;
- usa indice invertido para mejorar rendimiento con catalogos grandes.

## Reglas de calidad

- Todo material debe tener codigo unico.
- Todo material debe tener descripcion.
- La unidad debe estar informada.
- Precio negativo es error.
- Material sin `MatUf` ni `MoUf` es advertencia.
- La fecha de precio debe permitir trazabilidad y vencimiento.

## Uso en cubicacion

Cuando se selecciona un material:

- copiar codigo, descripcion y unidad;
- cargar `MatUf` y `MoUf`;
- conservar cantidad editable;
- marcar partida como modificada;
- recalcular solo lo necesario.

## Uso en APU

Los recursos de APU pueden usar materiales del banco como fuente de precio, pero el APU debe guardar el precio usado para preservar trazabilidad historica.

## Mejoras pendientes

- Importador robusto desde Excel/CSV.
- Normalizacion de unidades.
- Control de equivalencias tecnicas.
- Historial de cambios de precio.
- Alertas de precio vencido.

