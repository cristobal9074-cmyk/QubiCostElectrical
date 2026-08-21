# Contrato de identificadores — Itemizado

## Tres claves distintas

| Campo | Rol | ¿Cambia al editar? |
|-------|-----|-------------------|
| `PartidaId` (Guid) | Persistencia SQLite, vínculos APU/cubicación a largo plazo | **Nunca** |
| `Id` (string, puntos) | Jerarquía WBS interna (`1.0`, `1.0.00001`) | Solo al crear/mover/importar |
| `Item` (string, comas) | Código visible en presupuesto (`1,0`, `1,1`) | Editable por el usuario |

## Reglas

- Capítulos: `Id = N.0`, `Item = N,0`, `EsTitulo = true`, `Cantidad = 0`.
- Partidas: `Id = {capítulo}.{secuencia:00000}`, `Item = N,M` secuencial bajo el capítulo.
- `WBSCode` sigue a `Item` normalizado (comas → puntos).
- `ParentId` apunta al `Id` del capítulo padre.
- Al duplicar: nuevo `Id` y `PartidaId`; `Item` único (sufijo `(2)`, etc.).
- Al eliminar/reemplazar/vaciar: se limpian cubicación, cabeceras de cantidad, capas de composición **y APU** cuyo `ItemId` coincide.
- Importación: máximo **10.000** filas; preview unificado Excel/pega; códigos WBS duplicados se rechazan en vista previa.
- **Edición manual de `Item` (partida):** actualiza `WBSCode` y `ParentId` según el capítulo del código visible; `Id` y `PartidaId` no cambian.
- **Edición manual de `Item` (título):** solo formato equivalente al mismo capítulo (no renumera capítulos).
- **Grilla:** códigos de partida duplicados se rechazan con mensaje en celda.

Lógica centralizada en `ItemizadoHierarchyRules` (Domain) y `ItemizadoGridValidation` (Application).
