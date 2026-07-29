# QUBI COST ELÉCTRICO — FASE 11E
## Dashboard contractual, integración transversal y cierre de la Fase 11

## Fuente única de alcance

Este documento define exclusivamente la Fase 11E.

No iniciar Fase 12.

No agregar nuevos motores de contratos, valorizaciones, cambios, reajustes, garantías, multas o facturación. Esta fase debe **integrar y presentar** las capacidades ya existentes.

---

# Reglas de ahorro de tokens

1. Leer únicamente `AGENTS.md` y este documento.
2. No recorrer todo el repositorio de forma recursiva.
3. Buscar primero por nombre, símbolo o carpeta.
4. Trabajar inicialmente solo en:
   - `Domain/Contracts`
   - `Application/Contracts`
   - `Infrastructure/SQLite`
   - `Composition`
   - `Wpf/Features/Contracts`
   - `Wpf/Features/ContractChanges`
   - `Wpf/Features/ContractAdministration`
   - navegación Shell
   - tests contractuales
5. Leer módulos de presupuesto, planificación, EVM, compras o control de obra únicamente cuando exista una integración directa comprobada.
6. No leer `docs/archive`.
7. No volver a leer archivos sin cambios.
8. No reproducir archivos completos en el chat.
9. Aplicar modificaciones directamente.
10. Resumir cada bloque en máximo ocho líneas.
11. Usar pruebas filtradas durante el desarrollo.
12. Ejecutar la suite completa solo al inicio y al final.
13. Limitar las salidas de terminal a las últimas 25 líneas.
14. No usar subagentes salvo bloqueo técnico real.
15. No crear una cuota artificial de pruebas.
16. No ejecutar Git.
17. Mantener todos los archivos nuevos bajo 400 líneas.
18. No iniciar Fase 12.

---

# Estado de entrada

Estado reportado:

- Fase 11A completada
- Fase 11B completada
- Fase 11C y hardening completados
- Fase 11D completada
- SQLite schema v56
- 3305 pruebas aprobadas
- Build limpio

Antes de modificar, verificar únicamente:

```powershell
dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build |
    Select-Object -Last 25

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release |
    Select-Object -Last 20
```

Registrar:

- total inicial de pruebas
- schema real
- errores o advertencias
- rutas contractuales ya registradas
- servicios contractuales disponibles

---

# Objetivo general

Cerrar la Fase 11 mediante una experiencia contractual ejecutiva e integrada.

Implementar:

- dashboard contractual
- resumen económico del contrato
- consolidación de estados de pago
- consolidación de órdenes de cambio
- consolidación de claims
- consolidación de reajustes
- consolidación de garantías
- consolidación de multas
- consolidación de facturación informativa
- alertas
- snapshots ejecutivos
- comparación entre fechas de estado
- integración con presupuesto
- integración con planificación
- integración con EVM
- integración con compras
- integración con control de obra cuando exista
- reportes contractuales
- validación end-to-end
- cierre documental y técnico de la Fase 11

No duplicar fórmulas ni fuentes de verdad.

---

# 1. Auditoría de integración

Antes de implementar, crear una matriz compacta en `walkthrough.md`:

| Capacidad | Servicio fuente | Modelo fuente | Persistencia | WPF actual | Brecha |
|---|---|---|---|---|---|

Revisar únicamente:

- contratos
- versiones contractuales
- estados de pago
- anticipos
- retenciones
- solicitudes y órdenes de cambio
- obras extraordinarias
- claims
- reajustes
- garantías
- multas
- facturas informativas
- EVM
- planificación
- compras
- presupuesto

No crear una segunda fuente de verdad.

---

# 2. Modelo ejecutivo contractual

Crear modelos de agregación en Application, por ejemplo:

- `ContractExecutiveDashboard`
- `ContractFinancialSummary`
- `ContractTimeSummary`
- `ContractChangeSummary`
- `ContractPaymentSummary`
- `ContractRiskSummary`
- `ContractAlert`
- `ContractDashboardKpi`
- `ContractDashboardTrend`
- `ContractDashboardSnapshot`
- `ContractDashboardComparison`

Adaptar nombres a las convenciones existentes.

Estos modelos deben contener resultados derivados, no lógica de negocio.

---

# 3. Servicio de dashboard contractual

Crear:

- `IContractDashboardService`
- `ContractDashboardService`

Debe consumir servicios y repositorios existentes.

No debe recalcular motores base.

Debe consolidar:

- contrato original
- versión vigente
- plazo original
- plazo vigente
- monto original
- monto vigente
- estados de pago
- cambios
- claims
- reajustes
- garantías
- multas
- facturación
- anticipos
- retenciones
- forecast
- EVM
- planificación
- compras

Debe aceptar:

- ContractId
- ProjectId
- fecha de estado
- moneda de presentación
- filtros
- CancellationToken

---

# 4. KPIs contractuales

Mostrar como mínimo:

## Contrato

- monto original
- monto vigente
- diferencia absoluta
- variación porcentual
- plazo original
- plazo vigente
- días añadidos o reducidos
- fecha original de término
- fecha vigente
- fecha forecast

## Estados de pago

- monto presentado
- monto observado
- monto aprobado
- monto facturado
- monto pagado informativo
- monto acumulado
- saldo por valorizar
- saldo contractual

## Anticipos y retenciones

- anticipo original
- anticipo amortizado
- saldo de anticipo
- retención acumulada
- retención liberada
- saldo retenido

## Cambios

- solicitudes abiertas
- cambios pendientes
- cambios aprobados
- cambios implementados
- monto de cambios pendientes
- monto de cambios aprobados
- días solicitados
- días aprobados

## Claims

- claims abiertos
- claims en negociación
- claims aceptados
- monto solicitado
- monto reconocido
- días solicitados
- días reconocidos

## Administración

- reajustes aprobados
- multas propuestas
- multas aplicadas
- garantías activas
- garantías próximas a vencer
- garantías vencidas
- facturas pendientes
- facturas vencidas

## EVM y forecast

Consumir resultados existentes:

- BAC
- EAC
- ETC
- VAC
- CPI
- SPI
- forecast de término

No recalcular EVM.

---

# 5. Reglas de consolidación

Definir claramente:

```text
Monto vigente =
Monto original
+ cambios aprobados e implementados
+ reajustes aprobados
- reducciones contractuales aprobadas
```

No incluir automáticamente:

- solicitudes pendientes
- claims no reconocidos
- multas propuestas
- facturas no aprobadas
- cambios rechazados

Mostrar estos valores por separado como exposición o riesgo.

Evitar doble contabilización cuando una orden de cambio ya esté reflejada en una versión contractual.

---

# 6. Monedas

Conservar:

- moneda original
- valor original
- tipo de cambio
- fecha del tipo de cambio
- valor convertido

Reutilizar:

- `CurrencyCatalog`
- repositorio de tipos de cambio
- `PrecisionPolicy`

No convertir sin una tasa explícita y válida.

Cuando no exista tasa:

- marcar dato incompleto
- no inventar conversión
- mostrar el valor original

---

# 7. Calidad del dato

Cada resumen debe indicar:

- Complete
- Partial
- Outdated
- Inconsistent
- IntegrityInvalid
- MissingExchangeRate
- MissingBaseline
- MissingStatusDate
- NotApplicable

No mostrar un KPI como fiable cuando falten datos esenciales.

---

# 8. Alertas contractuales

Crear alertas estructuradas para:

- cambio pendiente con impacto alto
- claim sin respuesta
- estado de pago atrasado
- saldo contractual insuficiente
- monto vigente inconsistente
- forecast superior al contrato vigente
- plazo forecast posterior al vigente
- garantía próxima a vencer
- garantía vencida
- anticipo sin amortización
- retención sobre límite
- multa pendiente de decisión
- factura vencida
- reajuste pendiente
- documento con integridad inválida
- contrato próximo a vencer
- contrato vencido no cerrado

Cada alerta debe incluir:

- código
- severidad
- título
- descripción
- contrato
- entidad
- identificador
- fecha
- responsable
- impacto
- acción sugerida
- fuente
- estado

Estados:

- New
- Acknowledged
- Resolved
- Dismissed

Registrar auditoría de cambios de estado.

---

# 9. Resumen por WBS

Consolidar, cuando existan vínculos:

- monto contractual
- cambios
- valorizado
- aprobado
- saldo
- forecast
- claims
- multas
- alertas
- avance físico
- avance económico

No distribuir importes sin un vínculo real.

No promediar índices EVM sin ponderación.

---

# 10. Resumen temporal

Crear series por:

- mes
- período contractual
- fecha de estado

Series mínimas:

- monto contractual vigente
- valorización acumulada
- facturación acumulada
- pago informativo acumulado
- cambios acumulados
- claims reconocidos
- retenciones
- amortización de anticipos
- forecast

La generación de series debe ser determinista y testeable.

---

# 11. Snapshot ejecutivo

Crear snapshot inmutable con:

- ContractId
- fecha de estado
- KPIs
- saldos
- cambios
- claims
- pagos
- garantías
- multas
- facturación
- alertas
- forecast
- versiones de motores fuente
- hashes fuente
- autor
- comentario
- fecha UTC
- SHA-256

No sobrescribir snapshots anteriores.

---

# 12. Comparación de snapshots

Permitir comparar:

- dos fechas de estado
- snapshot actual vs anterior
- contrato original vs vigente
- vigente vs forecast

Detectar:

- cambio de monto
- cambio de plazo
- nuevos cambios
- claims nuevos o resueltos
- variación de valorización
- variación de facturación
- variación de saldo
- garantías vencidas o renovadas
- multas nuevas
- variación EVM
- variación de forecast
- alertas nuevas o resueltas

Resultado estructurado y determinista.

---

# 13. Persistencia

Definir interfaces:

- `IContractDashboardSnapshotRepository`
- `IContractDashboardAlertRepository`
- `IContractDashboardPreferenceRepository`

Crear tablas equivalentes a:

- `contract_dashboard_snapshots`
- `contract_dashboard_alerts`
- `contract_dashboard_preferences`

Persistir solo:

- snapshots
- estados de alertas
- preferencias

No persistir agregaciones reconstruibles salvo snapshots.

---

# 14. Migración

Crear migración posterior a v56.

No asumir el número sin inspección.

Debe incluir:

- tablas
- índices
- foreign keys
- RowVersion
- transacción
- validación
- compatibilidad
- prueba desde v56
- reapertura

No modificar migraciones históricas.

---

# 15. Permisos

Añadir:

- ContractDashboardView
- ContractDashboardExecutiveView
- ContractDashboardSnapshotCreate
- ContractDashboardSnapshotCompare
- ContractDashboardAlertManage
- ContractDashboardExport
- ContractDashboardAuditView

Mantener denegación por defecto.

No usar strings de roles en ViewModels.

---

# 16. Auditoría

Registrar:

- apertura de dashboard
- creación de snapshot
- comparación
- exportación
- alerta reconocida
- alerta resuelta
- alerta descartada
- intento denegado
- fallo de integridad
- inconsistencia contractual

No registrar cargas de lectura masivas innecesarias.

---

# 17. WPF — Dashboard contractual

Crear:

- `ContractDashboardViewModel`
- `ContractDashboardView`

Debe incluir:

## Encabezado

- contrato
- proyecto
- estado
- versión
- fecha de estado
- moneda
- calidad del dato
- última actualización

## Tarjetas KPI

- monto original
- monto vigente
- aprobado
- facturado
- pagado informativo
- saldo
- cambios pendientes
- claims
- garantías
- multas
- forecast
- CPI/SPI

## Secciones

- resumen económico
- resumen de plazo
- estados de pago
- cambios
- claims
- garantías
- multas
- facturación
- alertas
- tendencias
- WBS

No usar color como único indicador.

---

# 18. WPF — Comparación contractual

Crear:

- `ContractDashboardComparisonViewModel`
- `ContractDashboardComparisonView`

Permitir:

- seleccionar dos snapshots
- comparar
- filtrar por categoría
- navegar a entidad
- exportar

Mostrar:

- valor anterior
- valor actual
- diferencia
- porcentaje
- severidad
- explicación

---

# 19. Drill-down y navegación

Desde cada KPI o alerta permitir navegar a:

- contrato
- versión
- estado de pago
- cambio
- obra extraordinaria
- claim
- reajuste
- garantía
- multa
- factura
- planificación
- EVM

Usar `ShellRoute` existente.

No crear un sistema paralelo de navegación.

---

# 20. Exportación y reportes

Crear reportes usando infraestructura existente:

- ficha contractual ejecutiva
- resumen económico
- resumen de plazo
- estados de pago
- cambios y claims
- anticipos y retenciones
- garantías y multas
- facturación
- alertas
- comparación de snapshots

Formatos:

- CSV
- Excel
- PDF
- impresión existente

No recalcular dentro de exportadores.

---

# 21. Reporte contractual imprimible

Debe incluir:

1. Identificación del proyecto y contrato.
2. Fecha de estado.
3. Monto original y vigente.
4. Plazo original y vigente.
5. Estados de pago.
6. Anticipos y retenciones.
7. Cambios.
8. Claims.
9. Reajustes.
10. Garantías.
11. Multas.
12. Facturación.
13. Forecast.
14. Alertas críticas.
15. Calidad del dato.

No generar conclusiones mediante IA.

Usar interpretaciones deterministas.

---

# 22. Integración con presupuesto

Consumir:

- presupuesto original
- presupuesto vigente
- partidas
- WBS
- cambios aprobados
- forecast

No modificar el presupuesto original.

Mostrar diferencias y trazabilidad.

---

# 23. Integración con planificación

Consumir:

- plazo original
- plazo vigente
- ampliaciones aprobadas
- hitos contractuales
- fecha forecast
- camino crítico
- actividades afectadas por cambios y claims

No recalcular CPM.

---

# 24. Integración con EVM

Consumir:

- BAC
- EAC
- ETC
- VAC
- CPI
- SPI
- fecha de estado

No duplicar fórmulas.

Mostrar fuente, fecha y calidad del dato.

---

# 25. Integración con compras

Consumir, cuando exista vínculo contractual:

- órdenes de compra
- compromisos
- recepciones
- facturas informativas
- proveedores
- exposición por moneda

No convertir compras en contratos automáticamente.

---

# 26. Integración con control de obra

Cuando el módulo esté disponible, consumir:

- cantidades ejecutadas
- avance aprobado
- costos reales
- evidencias
- productividad
- desviaciones

Cuando no exista información:

- marcar `NoData`
- no inventar valores

---

# 27. Rendimiento

Validar con:

- múltiples contratos
- cientos de estados de pago
- cientos de cambios
- cientos de claims
- miles de líneas de valorización
- cientos de alertas
- múltiples snapshots

Evitar consultas N+1.

Aplicar paginación y agregaciones eficientes.

No crear pruebas frágiles basadas en milisegundos exactos.

---

# 28. Tests

No usar una cuota artificial.

Cubrir riesgos y reglas:

## Consolidación

- monto original
- monto vigente
- cambios aprobados
- cambios pendientes excluidos
- reajustes
- reducciones
- prevención de doble conteo
- saldo contractual

## Pagos

- presentado
- aprobado
- facturado
- pagado informativo
- anticipos
- retenciones

## Claims y cambios

- abiertos
- aceptados
- parciales
- rechazados
- impacto de plazo
- impacto de costo

## Alertas

- creación
- prioridad
- reconocimiento
- resolución
- persistencia
- permisos

## Snapshots

- hash
- inmutabilidad
- versión
- reapertura
- comparación
- alteración

## Integraciones

- presupuesto
- planificación
- EVM
- compras
- ausencia de datos

## WPF

- carga
- filtros
- selección
- comparación
- navegación
- exportación
- cancelación
- no reentrancia
- permisos
- estado vacío

## Arquitectura

- WPF sin fórmulas
- Application orquesta
- Infrastructure persiste
- exportadores no recalculan
- archivos bajo 400 líneas
- Composition resuelve servicios

Durante el desarrollo:

```powershell
dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
    -c Release `
    --filter "FullyQualifiedName~ContractDashboard|FullyQualifiedName~ContractIntegration|FullyQualifiedName~ContractSnapshot" |
    Select-Object -Last 30
```

---

# 29. Validación end-to-end

Ejecutar realmente, cuando sea posible:

1. Abrir contrato aprobado.
2. Ver monto original.
3. Crear estado de pago.
4. Aprobar valorización.
5. Aplicar anticipo y retención.
6. Aprobar orden de cambio.
7. Verificar monto vigente.
8. Registrar claim.
9. Registrar reajuste.
10. Registrar garantía.
11. Registrar multa.
12. Asociar factura.
13. Abrir dashboard.
14. Revisar KPIs.
15. Crear snapshot.
16. Cambiar fecha de estado.
17. Crear segundo snapshot.
18. Comparar.
19. Exportar.
20. Cerrar.
21. Reabrir.
22. Verificar persistencia e integridad.
23. Probar usuario sin permiso.

No afirmar pasos manuales no ejecutados.

---

# 30. Documentación mínima

Actualizar:

- `task.md`
- `walkthrough.md`

Crear:

- `docs/active/contractual-dashboard-specification.md`
- `docs/active/contractual-integration-specification.md`
- `docs/active/contractual-reporting-specification.md`
- `docs/active/phase-11e-report.md`
- `docs/active/phase-11-final-report.md`

No modificar `AGENTS.md` salvo que cambie una regla permanente.

No crear documentación redundante.

---

# 31. Validación automática final

Ejecutar únicamente al final:

```powershell
dotnet restore ".\QubiCostElectrical.Wpf.sln"

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Debug --warnaserror |
    Select-Object -Last 25

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build |
    Select-Object -Last 30

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release |
    Select-Object -Last 20
```

Resultado requerido:

- Debug: 0 errores
- Debug: 0 advertencias
- Release: 0 errores
- Release: 0 advertencias
- pruebas fallidas: 0
- pruebas omitidas: 0
- arquitectura: aprobada
- persistencia: aprobada
- reapertura: aprobada
- navegación: aprobada
- permisos: aprobados
- exportación: aprobada

El total debe ser superior a 3305 pruebas.

---

# Criterio de cierre

Declarar exactamente:

**FASE 11 — GESTIÓN CONTRACTUAL PROFESIONAL COMPLETADA**

únicamente cuando:

- dashboard contractual funcione
- KPIs sean correctos
- consolidaciones no dupliquen importes
- alertas funcionen
- snapshots funcionen
- comparación funcione
- integraciones funcionen
- reportes funcionen
- persistencia y reapertura funcionen
- WPF esté integrada
- navegación funcione
- permisos estén aplicados
- auditoría funcione
- todos los archivos nuevos estén bajo 400 líneas
- Debug y Release estén limpios
- suite global esté verde
- documentación final esté completa

No iniciar Fase 12.
