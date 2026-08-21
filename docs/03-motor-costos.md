# Motor de costos

## Fuente de verdad

El calculo del presupuesto debe centralizarse en `BudgetEngine`, expuesto a la UI mediante `BudgetCalculationService`.

Version vigente del motor de calculo: `2`, definida en `ProductVersionInfo.CalculationEngineVersion`.

Archivo principal:

- `QubiCostElectrical.Domain/BudgetEngine/BudgetEngine.cs`
- `QubiCostElectrical.Application/Services/BudgetCalculationService.cs`

La UI no debe recalcular margenes, GG, utilidad, IVA o totales finales con logica paralela salvo para mostrar previews simples.

## Entradas

- `IEnumerable<CubicacionRow>`: detalle de recursos cubicados.
- `BudgetSettings`: porcentajes y politicas comerciales.
- `IEnumerable<ApuRow>` opcional: APU disponibles.
- `IEnumerable<ItemizadoRow>` opcional: cantidades y jerarquia de partidas.
- `IEnumerable<ChangeOrderRow>` opcional: ordenes de cambio.

## Salidas

- `CostBreakdown`: costo directo, materiales, mano de obra, gastos generales, utilidad, IVA, total venta y UF/m2.
- Totales por area.
- Totales por material.
- Totales directos para previews.

## Reglas de calculo

- `CubicacionRow.UfUnit = MatUf + MoUf`.
- `CubicacionRow.UfTotal = Cantidad * UfUnit`.
- Los porcentajes comerciales se aplican desde `BudgetSettings`.
- Si existe politica de APU, el motor debe decidir si usar precio de cubicacion, APU o una combinacion definida.
- Cantidades negativas se permiten para descuentos o ajustes, pero deben aparecer en revision.

## Rendimiento

El servicio tiene soporte incremental:

- `MarkDirtyItem(itemId)`.
- `MarkDirtyItems(itemIds)`.
- `ResetIncrementalCache()`.

Cuando se cambia una celda de cubicacion, se debe marcar solo la partida afectada en vez de invalidar todo el presupuesto si no es necesario.

## Reglas para futuros cambios

- No duplicar formulas de totales en la capa WPF (ViewModels/code-behind).
- Agregar pruebas unitarias cuando se modifiquen reglas comerciales.
- Mantener decimal para dinero y cantidades; no usar double para calculo financiero.
- Documentar cualquier cambio en GG, utilidad, IVA, APU o politica de costo.
