# Budget Engine Design — Fase 5A

## Modelo de Recursos Profesional

### ResourceType

```csharp
public enum ResourceType { Material, Labor, Equipment, Tool, Subcontract, Transport, Auxiliary, Other }
```

- Mapeable a `ApuTipoRecurso` del modelo existente (MAT, MO, EQ, HERR, SC, TR, HH)
- Extensión `ToApuTipo()` / `FromApuTipo()` para compatibilidad bidireccional

### Resource record

```csharp
public sealed record Resource
{
    string Id              // GUID único estable
    string Code            // Código del recurso (ej: "CABLE-001")
    string Description     // Descripción
    ResourceType Type      // Tipo de recurso
    string Unit            // Unidad de medida (código del catálogo)
    decimal BasePrice      // Precio base en moneda del recurso
    string Currency        // Moneda (ISO, default "CLP")
    DateTime? PriceDate    // Fecha de vigencia del precio
    string Supplier        // Proveedor opcional
    decimal Yield          // Rendimiento
    decimal Waste          // Desperdicio (%)
    decimal AdjustmentFactor  // Factor de ajuste (default 1.0)
    bool IsActive          // Estado activo (default true)
    string Notes           // Observaciones
    string PriceSource     // Fuente del precio
    DateTime CreatedAt     // Fecha de creación
    DateTime ModifiedAt    // Fecha de modificación
    string CreatedBy       // Usuario creador
    string ModifiedBy      // Usuario responsable
}
```

Propiedades calculadas: `EffectivePrice` (BasePrice * AdjustmentFactor), `EffectiveYield` (Yield > 0 ? Yield : 1), `HasPrice`, `IsValidForUse`

---

## Unidades de Medida

### UnitOfMeasure record

```csharp
public sealed record UnitOfMeasure
{
    string Code       // Código único (UN, ML, M2, M3, KG, TON, LTR, HH, JOR, LTE, PCT, GL)
    string Name       // Nombre descriptivo
    string Symbol     // Símbolo (un, ml, m², m³, kg, ton, L, h, jor, lote, %, gl)
    UnitCategory Category  // Quantity, Length, Area, Volume, Mass, Time, Percentage, Global
    bool IsActive     // default true
}
```

### UnitOfMeasureCatalog

Catálogo centralizado con 12 unidades predefinidas. Búsqueda por código (case-insensitive). Validación con `IsValid()`, `Find()`. Arquitectura abierta a unidades personalizadas.

---

## Política de Precisión

### PrecisionPolicy

| Concepto | Decimales | Método |
|----------|-----------|--------|
| Cantidades | 4 | `MidpointRounding.ToEven` |
| Precios unitarios | 4 | `MidpointRounding.ToEven` |
| Importes | 2 | `MidpointRounding.ToEven` |
| Porcentajes | 2 | `MidpointRounding.ToEven` |
| Factores | 6 | `MidpointRounding.ToEven` |
| Tasas de cambio | 6 | `MidpointRounding.ToEven` |

Método de redondeo: **Banker's rounding** (MidpointRounding.ToEven). Estándar financiero IEEE 754. 

Justificación: En construcción, los costos unitarios pueden tener tres o cuatro decimales. El redondeo bancario evita el sesgo sistemático del redondeo "siempre hacia arriba" que podría inflar presupuestos. Para costos unitarios finales, la precisión a 4 decimales con ToEven es el estándar en ingeniería de costos.

Fórmulas de redondeo:
- `RoundImporte(x) = Math.Round(x, 2, MidpointRounding.ToEven)`
- `RoundPrecioUnitario(x) = Math.Round(x, 4, MidpointRounding.ToEven)`
- `RoundCantidad(x) = Math.Round(x, 4, MidpointRounding.ToEven)`

---

## Monedas y Tipos de Cambio

### Currency record

```csharp
public sealed record Currency
{
    string IsoCode         // CLP, USD, EUR, UF
    string Symbol          // $, US$, €, UF
    string Name            // Peso chileno, Dólar estadounidense, etc.
    int DecimalPlaces      // 0 (CLP), 2 (USD, EUR, UF)
    bool IsActive          // default true
}
```

### CurrencyCatalog

4 monedas predefinidas: CLP, USD, EUR, UF. Catálogo extensible. Búsqueda case-insensitive.

### ExchangeRate record

```csharp
public sealed record ExchangeRate
{
    string Id                // GUID único
    string SourceCurrency    // Moneda origen
    string TargetCurrency    // Moneda destino
    decimal Rate             // Tasa de cambio
    DateTime Date            // Fecha de vigencia
    string Source            // Fuente (Manual)
    string UserId            // Usuario responsable
    DateTime CreatedAt       // Fecha de registro
    bool IsActive            // default true
}
```

Reglas de conversión:
- `CurrencyCatalog.Convert(amount, rate) = amount * rate.Rate`, redondeado a 2 decimales
- Rate debe ser positivo
- Misma moneda origen/destino no requiere conversión

---

## Historial de Precios

### ResourcePriceHistoryEntry record

```csharp
public sealed record ResourcePriceHistoryEntry
{
    string Id                // GUID único
    string ResourceId        // Recurso asociado
    decimal Price            // Precio histórico
    string Currency          // Moneda
    DateTime EffectiveDate   // Fecha de vigencia
    string Supplier          // Proveedor
    string Source            // Fuente (Manual, CSV, etc.)
    string UserId            // Usuario responsable
    string Comment           // Comentario
    string OriginProject     // Proyecto o catálogo de origen
    string Status            // Vigente, Histórico
    DateTime CreatedAt       // Fecha de registro
}
```

Reglas:
- No sobrescribir precios históricos (solo append)
- Precios conservados en versión aprobada
- Consulta por recurso con filtro de fechas
- Último precio vigente seleccionable

---

## Validaciones de Negocio

### ResourceValidator

| Validación | Error o Warning |
|-----------|-----------------|
| Código vacío | Error |
| Descripción vacía | Error |
| Unidad inválida | Error |
| Precio negativo | Error |
| Rendimiento cero | Error |
| Rendimiento negativo | Error |
| Desperdicio negativo | Error |
| Factor <= 0 | Error |
| Moneda inválida | Error |
| Precio sin fecha | Warning |
| Desperdicio > 100% | Warning |
| Recurso en porcentaje con precio | Warning |

### ExchangeRateValidator

| Validación | Error o Warning |
|-----------|-----------------|
| Moneda origen inválida | Error |
| Moneda destino inválida | Error |
| Tasa <= 0 | Error |
| Misma moneda | Warning |

---

## Persistencia

Los modelos de Fase 5A son **complementarios** al modelo existente. No migran ni reemplazan:

- `MaterialRow` (catálogo de precios existente)
- `ApuRecursoRow` (recursos dentro de APU)
- `ProjectFileData` (persistencia del proyecto)

### Estrategia

- `Resource`, `ResourcePriceHistoryEntry`, `ExchangeRate` → nuevas colecciones en `ProjectFileData`
- `UnitOfMeasureCatalog` → catálogo en memoria (no persiste)
- `PrecisionPolicy` → constante (no persiste)
- `CurrencyCatalog` → catálogo en memoria (no persiste)

Para habilitar persistencia completa en Fase 5B:
- `IResourceRepository` → implementación JSON sobre `ProjectFileData.ResourceCatalog`
- `IPriceHistoryRepository` → implementación JSON sobre `ProjectFileData.PriceHistory`
- `IExchangeRateRepository` → implementación JSON sobre `ProjectFileData.ExchangeRates`

---

## Archivos Nuevos (Fase 5A)

| Archivo | Capa | Propósito |
|---------|------|-----------|
| `Domain/BudgetEngine/ResourceType.cs` | Domain | Enum ResourceType + extensiones ApuTipoRecurso |
| `Domain/BudgetEngine/UnitOfMeasure.cs` | Domain | UnitOfMeasure record + UnitOfMeasureCatalog |
| `Domain/BudgetEngine/PrecisionPolicy.cs` | Domain | Política centralizada de redondeo |
| `Domain/BudgetEngine/Currency.cs` | Domain | Currency record + ExchangeRate + CurrencyCatalog |
| `Domain/BudgetEngine/Resource.cs` | Domain | Resource record + ResourcePriceHistoryEntry |
| `Domain/BudgetEngine/ResourceValidation.cs` | Domain | ResourceValidator, ResourceValidationResult |
| `Application/Services/ResourceService.cs` | Application | ResourceService (CRUD + price history + exchange) |
| `Application/Services/IResourceRepository.cs` | Application | Interfaces IResourceRepository, IPriceHistoryRepository, IExchangeRateRepository |

---

## Archivos Modificados (Fase 5A)

Ninguno. Todos los archivos de Fase 5A son nuevos y complementarios.

---

## Pruebas Añadidas (60 nuevas)

| Archivo | Tests | Área |
|---------|-------|------|
| `ResourceModelTests.cs` | 11 | Creación, EffectivePrice, IsValidForUse, tipo mappings |
| `UnitOfMeasureTests.cs` | 10 | Catálogo, búsqueda case-insensitive, validación |
| `PrecisionPolicyTests.cs` | 11 | Redondeo por precisión, banker's rounding, constantes |
| `CurrencyTests.cs` | 11 | Catálogo, conversión, validación, exchange rate |
| `ResourceValidationTests.cs` | 17 | Validaciones de recurso, price history, exchange rate |

---

## Resultados de Validación

| Medición | Valor |
|----------|-------|
| Errores Debug | 0 |
| Advertencias Debug | 0 |
| Errores Release | 0 |
| Advertencias Release | 0 |
| Pruebas existentes | 1789 |
| Pruebas nuevas | 60 |
| Pruebas totales | **1849** |
| Pruebas fallidas | 0 |
| Pruebas omitidas | 0 |
| Arquitectura | 1/1 |

---

## Limitaciones de Fase 5A

1. **Sin persistencia JSON** — los modelos Resource, PriceHistory, ExchangeRate no se serializan en ProjectFileData todavía (pendiente Fase 5B).
2. **Sin implementación de repositorios concretos** — interfaces definidas, implementación con JSON o SQLite pendiente.
3. **Sin UI WPF** — los modelos existen en Domain/Application, no hay vistas para recursos, monedas o historial.
4. **Sin migración desde MaterialRow** — los datos existentes en MaterialRow no se migran automáticamente al modelo Resource.
5. **Sin importación/exportación CSV** — las interfaces están definidas, la implementación concreta pendiente.

---

## Alcance Previsto para Fase 5B

- Fórmulas completas de APU con el nuevo modelo
- Factores configurables (inflación, localización, dificultad)
- Dependencias entre APU con detección de ciclos
- Implementación de repositorios concretos (JSON/SQLite)
- Servicio de recálculo determinista
- Validaciones de APU (circularidad, versión cerrada)
- Migración desde modelo existente
- UI de editor de APU

---

## Fase 5B — Motor Determinista de APU

### ApuDefinition (Domain)

APU profesional: código, descripción, unidad, cantidad, lista de componentes (inmutable).

```csharp
public sealed record ApuDefinition
{
    string Id           // GUID
    string Code         // Código (ej: "APU-001")
    string Description
    string Unit
    decimal Quantity    // Cantidad de la partida (default 1)
    List<ApuComponent> Components
    bool IsActive
    string Notes
}
```

### ApuComponent (Domain)

Componente de APU: puede ser un recurso directo o una APU auxiliar.

```csharp
public enum ApuComponentType { Resource, AuxiliaryApu }

public sealed record ApuComponent
{
    string Id
    ApuComponentType ComponentType
    string ReferenceId       // Resource.Id o ApuDefinition.Id
    string Description
    decimal Yield            // Rendimiento (cantidad por unidad de APU)
    decimal WastePercent     // Desperdicio %
    decimal AdjustmentFactor // Factor de ajuste adicional
    string Unit
    int SortOrder            // Orden de procesamiento
}
```

### ApuCalculationInput (Domain)

Entrada inmutable del cálculo:

```csharp
public sealed record ApuCalculationInput
{
    IReadOnlyList<ApuDefinition> ApuDefinitions
    IReadOnlyList<Resource> Resources
    IReadOnlyList<ExchangeRate> ExchangeRates
    string? TargetCurrency
    decimal? OverrideLaborRate
}
```

### ApuCalculationResult (Domain)

Resultado estructurado con desglose por categoría y por componente:

```csharp
public sealed record ApuCalculationResult
{
    string ApuId, ApuCode, ApuDescription, ApuUnit, Currency
    CostCategoryBreakdown CategoryBreakdown
    IReadOnlyList<ComponentCostDetail> Components
    decimal TotalDirectCost, TotalUnitPrice, TotalLinePrice
    ApuCalculationTrace Trace
}
```

`CostCategoryBreakdown` agrupa: Materials, Labor, Equipment, Tools, Subcontract, Transport, AuxiliaryApus.

`ComponentCostDetail` incluye: BasePrice, Yield, WastePercent, AdjustmentFactor, EffectiveQuantity, Subtotal, Explanation (fórmula legible) y opcionalmente AuxiliaryComponents (sub-árbol).

### ApuCycleDetector (Domain)

Detección de ciclos con DFS (colores blanco/gris/negro):

- `HasCycle()` — bool
- `FindCyclePath()` — path del ciclo
- `ApuCycleValidationResult` — resumen con APUs afectadas y paths

### ApuCalculationEngine (Domain)

Motor estático determinista:

- `Calculate(apu, input, ct)` → `ApuCalculationResult`
  - Valida ciclos primero
  - Procesa componentes por SortOrder
  - Recurso: busca en input.Resources, aplica EffectivePrice × EffectiveQuantity
  - APU auxiliar: recursivo, acumula en AuxiliaryApus
  - ConvertCurrency cuando difiere moneda
  - OverrideLaborRate para MO
  - `PrecisionPolicy` en cada paso (RoundCantidad → RoundImporte)
  - Nunca modifica objetos de entrada (inmutable)
- `ValidateNoCycles(definitions)` → `ApuCycleValidationResult`

### ApuCalculationService (Application)

```csharp
public interface IApuCalculationService
{
    ApuCalculationResult Calculate(string apuDefinitionId, ApuCalculationInput, CancellationToken);
    IReadOnlyList<ApuCalculationResult> CalculateAll(ApuCalculationInput, CancellationToken);
    IReadOnlyList<ApuCalculationResult> CalculateMany(IReadOnlyList<string> apuIds, ApuCalculationInput, CancellationToken);
    ApuCycleValidationResult ValidateAll(ApuCalculationInput);
}
```

### Archivos creados (Fase 5B)

| Archivo | Capa | Propósito |
|---------|------|-----------|
| `Domain/BudgetEngine/ApuComponent.cs` | Domain | Enumeración + record de componente |
| `Domain/BudgetEngine/ApuDefinition.cs` | Domain | Definición de APU profesional |
| `Domain/BudgetEngine/ApuCalculationInput.cs` | Domain | Input inmutable |
| `Domain/BudgetEngine/ApuCalculationResult.cs` | Domain | Resultado + breakdown + trace |
| `Domain/BudgetEngine/ApuCycleDetector.cs` | Domain | DFS cycle detection |
| `Domain/BudgetEngine/ApuCalculationEngine.cs` | Domain | Motor de cálculo determinista |
| `Application/Services/IApuCalculationService.cs` | Application | Interfaz |
| `Application/Services/ApuCalculationService.cs` | Application | Implementación |
| `tests/Domain/ApuCalculationEngineTests.cs` | Tests | 63 tests |

### Reglas del motor

1. **Inmutabilidad:** nunca modificar objetos de entrada
2. **Determinismo:** mismo input → mismo resultado (PrecisionPolicy fijo)
3. **Redondeo:** Banker's rounding (MidpointRounding.ToEven)
4. **Categorización:** cada componente se clasifica por ResourceType
5. **Traza:** cada componente tiene Explanation legible
6. **Cancelación:** CancellationToken propagado a cada paso
7. **Ciclos:** detectados antes de calcular, reportados en trace

### Próximos pasos (Fase 5C — postergada)

- UI de editor de APUs (ApuDefinition)
- Vinculación con modelo existente ApuRow/ApuRecursoRow
- Almacenamiento persistente
- Integración con BudgetEngine
- Batch recalculation
- Sensitivity analysis sobre APU

---

---

## Fase 5C — Capa Económica Profesional

### EconomicConcept (Domain)

Concepto económico con categoría, tipo de cálculo, base, valor y orden:

```csharp
public sealed record EconomicConcept
{
    string Id, Code, Name, Description
    EconomicCategory Category       // 11 categorías (IndirectCost..InformativeWithholding)
    CalculationType CalculationType // Percentage, FixedAmount, AmountPerUnit, etc.
    CalculationBase CalculationBase // 16 bases (DirectCost, Materials, PreviousSubtotal, etc.)
    decimal Value                   // Tasa (%) o importe fijo
    int Order                       // Orden de aplicación
    bool IsActive, AffectsFinalPrice, IsInformative
    int RoundingDecimals            // Default 4
    DateTime EffectiveDate
}
```

### EconomicInput / EconomicResult (Domain)

Input inmutable con costo directo + subtotales por categoría + `EconomicConfig` (lista de conceptos).
Resultado con desglose por categoría + `ConceptResult` por cada concepto aplicado + `EconomicCalculationTrace`.

### EconomicEngine (Domain)

Motor determinista, stateless, sin `double`, sin scripts ni expresiones dinámicas:
- Aplica conceptos en orden por `Order`
- `ResolveBase` traduce `CalculationBase` a valor concreto (incluye `DirectCostPlusIndirect`)
- `ComputeValue` aplica fórmula según `CalculationType`
- Cada paso redondea con `PrecisionPolicy.Apply(value, decimals)`
- Discounts restan, todo lo demás suma (o es informativo)
- Acumula totales por categoría
- Genera DeterministicHash (SHA256 de conceptos aplicados)

### ScenarioEngine (Domain)

- `CalculateScenario`: aplica overrides (modificar tasa, activar/desactivar concepto) y recalcula
- `Compare`: diferencia entre dos resultados económicos (variación, conceptos modificados)
- Overrides tipados (no diccionarios de strings)

### Validaciones (EconomicValidation)

17 códigos de error/warning: `EMPTY_CONFIG`, `MISSING_CODE`, `DUPLICATE_ORDER`, `NEGATIVE_VALUE`, `EXCESSIVE_PCT`, `INFORMATIVE_AFFECTS_PRICE`, `TAX_OVER_100`, `DISCOUNT_OVER_100`, `BASE_BEFORE_EXISTS`, `ZERO_AMOUNT`, etc.

### Archivos creados (Fase 5C)

| Archivo | Capa | Propósito |
|---------|------|-----------|
| `Domain/BudgetEngine/EconomicConcept.cs` | Domain | Concepto económico |
| `Domain/BudgetEngine/EconomicConfig.cs` | Domain | Configuración + factory (default, legacy) |
| `Domain/BudgetEngine/EconomicInput.cs` | Domain | Input inmutable |
| `Domain/BudgetEngine/EconomicResult.cs` | Domain | Resultado con desglose + traza |
| `Domain/BudgetEngine/EconomicEngine.cs` | Domain | Motor de cálculo económico |
| `Domain/BudgetEngine/EconomicValidation.cs` | Domain | Validaciones estructuradas |
| `Domain/BudgetEngine/Scenario.cs` | Domain | Escenario, override, comparación |
| `Domain/BudgetEngine/ScenarioEngine.cs` | Domain | Motor de escenarios |
| `Application/Services/IEconomicCalculationService.cs` | Application | Interfaz |
| `Application/Services/EconomicCalculationService.cs` | Application | Implementación |
| `tests/Domain/EconomicEngineTests.cs` | Tests | 46 tests |
| `tests/Domain/ScenarioEngineTests.cs` | Tests | 16 tests |

### Próximos pasos (Fase 5D — no iniciar)

- UI de configuración económica (panel de conceptos, orden, valores)
- UI de escenarios (selector, overrides, comparación)
- Exportación actualizada con desglose económico
- Vinculación con BudgetEngine legacy
- Persistencia de configuración económica en .qce
- Integración con IApuCalculationService para flujo completo

---

*Fin del documento — Fase 5A + 5B + 5C completadas. No iniciar Fase 5D.*
