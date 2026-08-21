# Persistencia SQLite

## Objetivo

SQLite es la base local principal del proyecto. JSON queda reservado para configuracion simple o compatibilidad, no como base de datos principal.

Archivos principales:

- `QubiCostElectrical.Infrastructure/SQLite/SQLiteProjectStore.cs`
- `QubiCostElectrical.Infrastructure/SQLite/SQLiteSchemaMigrator.cs`
- `QubiCostElectrical.Infrastructure/SQLite/ProjectRepository.cs`
- `QubiCostElectrical.Infrastructure/SQLite/ProjectExchangePackageService.cs`

## Version de esquema

La version actual del esquema esta definida en:

```csharp
SQLiteSchemaMigrator.LatestSchemaVersion
```

La version actual es la que declara `ProductVersionInfo.DatabaseSchemaVersion` — no duplicar el numero aqui. Esa constante es la fuente de verdad.

El formato de proyecto vigente es el que declara `ProductVersionInfo.ProjectFormatVersion`.

## Tablas principales

- `project_info`.
- `itemizado`.
- `materiales`.
- `cubicacion`.
- `plantillas`.
- `qty_headers`.
- `calculadora`.
- `versiones`.
- `apu`.
- `ordenes_cambio`.
- `app_meta`.
- `schema_migrations`.

## Estrategia de almacenamiento

Cada tabla guarda columnas livianas para busqueda/listado y un campo `json` con el objeto completo. Esto permite:

- compatibilidad hacia atras;
- migraciones menos fragiles;
- consultas rapidas por campos claves;
- conservar propiedades nuevas sin rehacer toda la tabla.

## Guardado

Existen dos modos:

- `SaveSnapshot`: reemplaza snapshot completo dentro de transaccion.
- `SaveIncremental`: actualiza solo scopes sucios.

Scopes comunes:

- `project_info`.
- `itemizado`.
- `materiales`.
- `cubicacion`.
- `qty_headers`.
- `plantillas`.
- `calculadora`.
- `versiones`.
- `apu`.
- `ordenes_cambio`.

## Migraciones

Toda migracion debe:

- ser idempotente cuando sea posible;
- correr dentro de transaccion;
- actualizar `PRAGMA user_version`;
- registrar en `schema_migrations`;
- crear backup si corresponde;
- no perder datos del JSON existente.

## Salud de base

La app debe poder ejecutar:

- `PRAGMA quick_check`;
- lectura de `saved_at`;
- migracion a ultima version;
- carga de snapshot completo.

## Reglas para futuros cambios

- No agregar rutas absolutas quemadas.
- No guardar credenciales.
- No borrar tablas sin migracion explicita.
- Agregar indices cuando una pantalla haga busquedas frecuentes.
- Mantener compatibilidad con proyectos antiguos.

## Datos legacy (Calculadora, Versiones, OrdenesCambio, EstadosPago)

Modulos retirados del producto estable siguen persistidos en SQLite para abrir proyectos antiguos sin perdida:

| Dato | Tabla / campo | UI activa | Escritura SQLite |
| --- | --- | --- | --- |
| Calculadora | `calculadora` | No | `SaveSnapshot` y scope `calculadora` |
| Versiones | `versiones` | No | `SaveSnapshot` y scope `versiones` |
| Ordenes de cambio | `ordenes_cambio` | No | `SaveSnapshot` y scope `ordenes_cambio` |
| Estados de pago | `estados_pago` (JSON en `project_info`) | No | scope `project_info` |

La Calculadora ya no tiene pantalla, pero sus celdas se leen al abrir y se reescriben al guardar snapshot completo o al marcar scope `calculadora`. Esto evita perder datos al copiar o migrar una base antigua a una nueva.

### Migracion futura (mayor)

Cuando se decida retirar por completo estos datos del esquema:

1. Exportar o archivar proyectos que aun contengan filas en `calculadora`, `versiones` u `ordenes_cambio`.
2. Agregar migracion que mueva esos JSON a una tabla de archivo (`legacy_archive`) o los elimine tras confirmacion.
3. Subir `DatabaseSchemaVersion` y documentar la ruptura en CHANGELOG.
4. Mantener lectura de solo lectura al menos una version intermedia antes del drop definitivo.
