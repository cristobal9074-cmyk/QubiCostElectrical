# QUBI COST ELÉCTRICO — FASE 12AB
## Preparación del cierre y conciliación técnica, económica y contractual

## Fuente única de alcance

Este documento define exclusivamente las fases 12A y 12B.

No iniciar las fases 12C, 12D o 12E.

El objetivo es determinar, con evidencia verificable, si un proyecto está preparado para iniciar su cierre formal.

---

# Reglas de ahorro de tokens

1. Leer únicamente `AGENTS.md` y este documento.
2. No recorrer todo el repositorio recursivamente.
3. Buscar primero por nombre, símbolo o carpeta.
4. Trabajar inicialmente solo en:
   - `Domain/Closeout`
   - `Application/Closeout`
   - `Infrastructure/SQLite`
   - `Composition`
   - `Wpf/Features/Closeout`
   - navegación Shell
   - tests de cierre
5. Leer Budget, Planning, Procurement, Contracts, EVM o Progress solo para integraciones directas.
6. No leer `docs/archive`.
7. No reproducir archivos completos en el chat.
8. Aplicar cambios directamente.
9. Resumir cada bloque en máximo ocho líneas.
10. Usar pruebas filtradas durante el desarrollo.
11. Ejecutar la suite completa únicamente al inicio y al final.
12. Limitar salidas de terminal a las últimas 25 líneas.
13. No usar subagentes salvo bloqueo técnico real.
14. No crear una cuota artificial de pruebas.
15. Usar teorías parametrizadas para reglas equivalentes.
16. No ejecutar Git.
17. Mantener cada archivo nuevo bajo 400 líneas.
18. No modificar motores estables ni duplicar fórmulas.
19. No crear una nueva fuente de verdad para datos existentes.
20. No iniciar fases posteriores.

---

# Estado inicial

Estado reportado:

- Fase 11 completa
- SQLite schema v57
- 3305 pruebas aprobadas
- Debug y Release limpios

Antes de modificar, verificar solamente:

```powershell
dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build |
    Select-Object -Last 25

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release |
    Select-Object -Last 20
```

Registrar:

- versión real del schema
- total inicial de pruebas
- rutas disponibles
- repositorios y servicios reutilizables
- módulos existentes relacionados con cierre, calidad o documentos

---

# Objetivo general

Implementar una solución profesional para:

- preparar el cierre del proyecto
- controlar requisitos de cierre
- administrar punch list
- registrar no conformidades de cierre
- controlar documentos y evidencias
- consolidar pendientes técnicos
- conciliar presupuesto, compras, contratos, pagos y ejecución
- conciliar plazo planificado, vigente y real
- detectar bloqueos de cierre
- calcular nivel de preparación
- crear snapshots inmutables
- presentar dashboard de preparación
- exportar reportes verificables
- persistir y reabrir toda la información

La fase no debe cerrar definitivamente el proyecto. Solo debe determinar y documentar si está listo para pasar a recepción y cierre formal.

---

# 1. Auditoría previa

Crear una matriz compacta en `walkthrough.md`:

| Capacidad | Fuente existente | Reutilizable | Brecha | Acción |
|---|---|---|---|---|

Revisar únicamente:

- proyecto y estado
- documentos
- WBS y actividades
- seguimiento
- contratos
- estados de pago
- cambios y claims
- compras
- recepciones e inventario
- presupuesto
- EVM
- auditoría
- permisos
- attachments o referencias documentales

No duplicar entidades ya existentes.

---

# PARTE A — PREPARACIÓN DEL CIERRE

# 2. Agregado de cierre

Crear un agregado, por ejemplo:

- `ProjectCloseout`
- `ProjectCloseoutId`
- `CloseoutStatus`
- `CloseoutAuditEvent`
- `CloseoutSnapshot`

Campos mínimos:

- CloseoutId
- ProjectId
- código
- nombre
- descripción
- responsable
- fecha objetivo
- fecha de estado
- estado
- porcentaje de preparación
- nivel de riesgo
- checklist activo
- conteo de punch items
- conteo de no conformidades
- documentos pendientes
- bloqueos
- comentario
- versión
- hash SHA-256
- CreatedUtc
- CreatedBy
- ModifiedUtc
- ModifiedBy
- RowVersion

Estados mínimos:

- Draft
- InPreparation
- UnderReview
- Blocked
- ReadyForProvisionalReception
- ApprovedForReception
- Rejected
- Cancelled
- Archived

No implementar recepción definitiva en esta fase.

---

# 3. Checklist de cierre

Crear:

- `CloseoutChecklist`
- `CloseoutChecklistItem`
- `CloseoutChecklistTemplate`
- `CloseoutChecklistCategory`

Cada checklist debe soportar:

- plantilla
- versión
- categorías
- ítems obligatorios y opcionales
- responsables
- vencimientos
- evidencias
- comentarios
- dependencias
- bloqueos
- aprobación
- auditoría

Categorías iniciales:

- Ingeniería
- Calidad
- Seguridad
- Medio ambiente
- Construcción
- Documentación
- Compras
- Contratos
- Costos
- Planificación
- Cliente
- Entregables
- Otra configurable

No fijar categorías en ViewModels.

---

# 4. Ítems del checklist

Campos mínimos:

- ItemId
- ChecklistId
- código
- título
- descripción
- categoría
- obligatorio
- bloqueante
- responsable
- fecha límite
- estado
- evidencia requerida
- evidencias vinculadas
- comentario
- verificador
- fecha de verificación
- RowVersion

Estados:

- Pending
- InProgress
- SubmittedForVerification
- Verified
- Rejected
- NotApplicable
- Blocked
- Overdue

Reglas:

- un ítem bloqueante no verificado impide declarar preparación completa
- `NotApplicable` requiere motivo y autorización
- ítem rechazado vuelve a trabajo pendiente
- evidencia obligatoria debe existir antes de verificar
- autor de evidencia no verifica cuando la política SoD esté activa

---

# 5. Plantillas

Permitir:

- plantilla corporativa
- plantilla por tipo de proyecto
- plantilla específica del proyecto
- clonación controlada
- versión
- activación y obsolescencia

Una plantilla aplicada no debe cambiar retroactivamente si la plantilla original se modifica.

---

# 6. Punch list

Crear:

- `PunchList`
- `PunchItem`
- `PunchItemEvidence`
- `PunchItemVerification`

Campos mínimos:

- PunchItemId
- ProjectId
- CloseoutId
- número
- título
- descripción
- categoría
- prioridad
- severidad
- WBS
- actividad
- contrato
- responsable
- fecha detectada
- fecha límite
- fecha corregida
- fecha verificada
- estado
- impacto estimado en costo
- impacto estimado en plazo
- evidencias
- comentario
- RowVersion

Estados:

- Open
- Assigned
- InProgress
- WaitingVerification
- Rejected
- Closed
- Reopened
- Cancelled

Reglas:

- cerrar requiere verificación
- reabrir requiere motivo
- ítems críticos abiertos bloquean cierre
- no eliminar físicamente ítems con historial
- conservar evidencias anteriores

---

# 7. No conformidades de cierre

Crear:

- `CloseoutNonConformance`
- `CorrectiveAction`
- `PreventiveAction`
- `NonConformanceEvidence`

Campos:

- identificador
- proyecto
- cierre
- origen
- descripción
- severidad
- categoría
- responsable
- fecha
- fecha límite
- causa raíz
- corrección
- acción correctiva
- acción preventiva
- evidencia
- costo asociado
- impacto en plazo
- estado
- verificador
- auditoría
- RowVersion

Estados:

- Open
- UnderAnalysis
- ActionRequired
- InCorrection
- WaitingVerification
- Closed
- Reopened
- Cancelled

No construir un módulo general de calidad nuevo. Limitarlo al cierre.

---

# 8. Documentación de cierre

Crear o adaptar:

- `CloseoutDocument`
- `CloseoutDocumentRequirement`
- `CloseoutDocumentVersion`
- `CloseoutDocumentApproval`

Tipos mínimos:

- AsBuilt
- Manual
- Certificate
- TestProtocol
- CommissioningRecord
- Warranty
- TechnicalDataSheet
- InspectionRecord
- Photograph
- Video
- MeetingMinute
- DeliveryRecord
- TrainingRecord
- Other

Cada requisito debe definir:

- requerido
- bloqueante
- versión mínima
- responsable
- fecha límite
- formato o referencia
- estado
- documento vinculado
- integridad
- aprobación

No guardar binarios grandes en SQLite si el sistema usa referencias de archivo.

---

# 9. Evidencias

Crear una referencia común de evidencia, reutilizando infraestructura existente cuando sea posible.

Campos:

- EvidenceId
- tipo
- entidad
- entidad origen
- ruta o referencia
- nombre
- versión
- fecha
- autor
- comentario
- hash
- estado
- metadatos

Evitar copias duplicadas del mismo archivo.

---

# 10. Reuniones y revisiones de cierre

Permitir registrar:

- reunión
- fecha
- participantes
- agenda
- acuerdos
- pendientes
- responsables
- fechas límite
- documentos
- acta
- estado

No construir un sistema de reuniones corporativo. Limitarlo al proceso de cierre.

---

# PARTE B — CONCILIACIÓN

# 11. Motor de conciliación

Crear un servicio Application de agregación, por ejemplo:

- `ICloseoutReconciliationService`
- `CloseoutReconciliationService`

Debe consumir resultados existentes sin recalcular motores base.

Entrada:

- ProjectId
- CloseoutId
- fecha de estado
- moneda de presentación
- configuración
- CancellationToken

Salida estructurada:

- conciliación económica
- conciliación contractual
- conciliación de compras
- conciliación de planificación
- conciliación documental
- conciliación técnica
- alertas
- bloqueos
- calidad del dato
- trazabilidad de fuentes

---

# 12. Conciliación económica

Consolidar:

- presupuesto original
- presupuesto vigente
- cambios aprobados
- compras comprometidas
- recepciones
- costo real disponible
- valorizado
- aprobado
- facturado
- pagado informativo
- anticipos
- retenciones
- multas
- reajustes
- EAC
- ETC
- VAC
- costo final estimado

No inventar costo real cuando el módulo fuente no tenga datos.

Cada valor debe incluir:

- monto
- moneda
- fecha
- fuente
- versión
- calidad
- convertido o original
- tipo de cambio aplicado

---

# 13. Reglas económicas

Definir claramente y documentar:

```text
Presupuesto vigente =
presupuesto original
+ cambios presupuestarios aprobados
```

```text
Comprometido =
órdenes de compra aprobadas
+ contratos vigentes
- anulaciones
```

```text
Exposición pendiente =
compromisos no ejecutados
+ cambios pendientes
+ claims en evaluación
```

```text
Resultado estimado =
presupuesto vigente
- costo final estimado
```

Evitar doble contabilización cuando:

- una orden de compra esté vinculada a un contrato
- un cambio ya esté incluido en el monto contractual vigente
- una factura derive de un estado de pago
- una recepción ya esté reflejada en costo real

Cada regla debe tener pruebas de prevención de doble conteo.

---

# 14. Conciliación contractual

Consolidar:

- contratos activos
- contratos en cierre
- contratos cerrados
- monto original
- monto vigente
- cambios pendientes
- órdenes de cambio aprobadas
- claims abiertos
- claims reconocidos
- estados de pago pendientes
- garantías activas
- garantías vencidas
- retenciones pendientes
- multas pendientes
- reajustes pendientes
- facturas pendientes

Definir bloqueos configurables.

Bloqueantes predeterminados:

- contrato vigente sin proceso de cierre
- estado de pago presentado sin resolución
- claim aceptado sin incorporación contractual
- garantía vencida sin resolución
- retención pendiente sin condición documentada
- orden de cambio aprobada no implementada

---

# 15. Conciliación de compras e inventario

Consolidar:

- requerimientos abiertos
- RFQ abiertas relevantes
- adjudicaciones pendientes
- órdenes de compra abiertas
- entregas pendientes
- recepciones parciales
- devoluciones pendientes
- inventario disponible
- inventario reservado
- material sobrante
- material en tránsito
- diferencias ordenado/recibido

Bloqueantes configurables:

- material crítico en tránsito
- recepción con rechazo sin resolver
- orden abierta con saldo significativo
- devolución pendiente
- inventario sin conciliación

---

# 16. Conciliación de planificación

Consumir:

- baseline
- plan vigente
- fecha de estado
- actividades abiertas
- actividades críticas abiertas
- hitos pendientes
- progreso
- forecast de término
- fecha contractual vigente
- camino crítico
- restricciones pendientes

Resultados:

- plazo original
- plazo vigente
- plazo forecast
- variación
- actividades restantes
- hitos restantes
- porcentaje completado
- alertas

No recalcular CPM.

Bloquear preparación cuando existan actividades críticas abiertas, salvo excepción aprobada.

---

# 17. Conciliación documental

Calcular:

- documentos requeridos
- entregados
- aprobados
- rechazados
- vencidos
- sin integridad
- pendientes por categoría
- porcentaje documental

No considerar entregado un documento sin versión válida y referencia accesible.

---

# 18. Conciliación técnica

Consolidar:

- checklist
- punch items
- no conformidades
- evidencias
- verificaciones
- actividades abiertas
- pruebas y protocolos
- documentos técnicos

Calcular:

- porcentaje técnico
- bloqueos críticos
- pendientes por responsable
- pendientes vencidos
- riesgo técnico de cierre

---

# 19. Calidad del dato

Estados mínimos:

- Complete
- Partial
- Outdated
- Inconsistent
- MissingSource
- MissingExchangeRate
- IntegrityInvalid
- NotApplicable

Cada conciliación debe exponer:

- estado de calidad
- fuentes faltantes
- fuentes desactualizadas
- inconsistencias
- fecha de última actualización

No presentar un valor incompleto como definitivo.

---

# 20. Preparación de cierre

Crear resultado:

- `CloseoutReadinessResult`
- `CloseoutReadinessScore`
- `CloseoutBlockingIssue`
- `CloseoutReadinessPolicy`

Dimensiones:

- Técnica
- Documental
- Económica
- Contractual
- Compras
- Planificación

Cada dimensión debe tener:

- porcentaje
- estado
- bloqueos
- advertencias
- evidencia
- explicación

No usar una media simple si existen dimensiones bloqueantes.

Política predeterminada:

- ningún bloqueo crítico
- checklist obligatorio al 100 %
- documentos bloqueantes aprobados
- punch críticos cerrados
- no conformidades críticas cerradas
- conciliación económica sin inconsistencia crítica
- conciliación contractual sin bloqueos
- planificación sin actividad crítica abierta no autorizada

---

# 21. Excepciones de cierre

Permitir excepción autorizada para un bloqueo.

Cada excepción debe incluir:

- identificador
- bloqueo
- motivo
- riesgo aceptado
- responsable
- aprobador
- fecha
- vigencia
- evidencia
- condición
- estado
- auditoría

Aplicar SoD:

- quien solicita no aprueba
- autor del bloqueo no aprueba cuando la política lo prohíba

No eliminar el bloqueo original; marcarlo como exceptuado.

---

# 22. Alertas

Crear alertas para:

- ítem obligatorio pendiente
- punch crítico abierto
- no conformidad vencida
- documento bloqueante ausente
- contrato abierto
- estado de pago pendiente
- claim abierto
- garantía vencida
- orden de compra abierta
- recepción rechazada
- actividad crítica abierta
- forecast posterior al plazo vigente
- diferencia económica
- inconsistencia de moneda
- integridad inválida
- excepción próxima a vencer

Cada alerta:

- código
- severidad
- título
- descripción
- entidad
- identificador
- responsable
- fecha
- impacto
- acción sugerida
- estado
- auditoría

---

# 23. Snapshot de preparación

Crear snapshot inmutable con:

- fecha de estado
- checklist
- punch list
- no conformidades
- documentos
- conciliaciones
- score
- bloqueos
- excepciones
- alertas
- hashes de fuentes
- versión de servicios
- autor
- comentario
- CreatedUtc
- SHA-256

No sobrescribir snapshots anteriores.

---

# 24. Comparación de snapshots

Comparar:

- porcentaje de preparación
- checklist
- punch
- no conformidades
- documentos
- conciliación económica
- conciliación contractual
- compras
- planificación
- bloqueos
- excepciones
- alertas
- calidad de datos

Mostrar:

- agregado
- resuelto
- reabierto
- modificado
- impacto
- tendencia
- explicación

Resultado determinista.

---

# 25. Repositorios

Definir interfaces internas para:

- `IProjectCloseoutRepository`
- `ICloseoutChecklistRepository`
- `IPunchListRepository`
- `ICloseoutNonConformanceRepository`
- `ICloseoutDocumentRepository`
- `ICloseoutEvidenceRepository`
- `ICloseoutSnapshotRepository`
- `ICloseoutAlertRepository`
- `ICloseoutAuditRepository`

Usar agregados cuando reduzca complejidad; no crear repositorios innecesariamente granulares.

---

# 26. Persistencia SQLite

Crear tablas equivalentes a:

- `project_closeouts`
- `closeout_checklist_templates`
- `closeout_checklists`
- `closeout_checklist_items`
- `closeout_punch_items`
- `closeout_non_conformances`
- `closeout_corrective_actions`
- `closeout_document_requirements`
- `closeout_documents`
- `closeout_evidence`
- `closeout_meetings`
- `closeout_exceptions`
- `closeout_snapshots`
- `closeout_alerts`
- `closeout_audit`

Adaptar nombres a las convenciones reales.

Requisitos:

- FK
- índices
- transacciones
- RowVersion
- soft delete cuando corresponda
- CancellationToken
- reapertura
- concurrencia
- paginación
- compatibilidad

---

# 27. Migraciones

Crear migraciones posteriores al schema real.

No asumir el número antes de inspeccionar.

Separar, si mejora seguridad:

- núcleo de cierre
- checklist/punch/NC
- documentos/snapshots/alertas

No modificar migraciones históricas.

Probar desde v57 real.

---

# 28. Permisos

Añadir:

- CloseoutView
- CloseoutCreate
- CloseoutEdit
- CloseoutSubmit
- CloseoutReview
- CloseoutApproveReadiness
- CloseoutChecklistManage
- CloseoutChecklistVerify
- PunchListManage
- PunchListVerify
- CloseoutNonConformanceManage
- CloseoutDocumentManage
- CloseoutDocumentApprove
- CloseoutReconciliationView
- CloseoutReconciliationRun
- CloseoutExceptionRequest
- CloseoutExceptionApprove
- CloseoutSnapshotCreate
- CloseoutSnapshotCompare
- CloseoutAlertManage
- CloseoutAuditView
- CloseoutExport

Denegación por defecto.

No usar strings de roles en ViewModels.

---

# 29. SoD y autorización

Políticas mínimas:

- creador del cierre no aprueba preparación
- último editor no aprueba
- autor de evidencia no verifica su propio ítem
- responsable del punch no realiza verificación final
- solicitante de excepción no la aprueba
- usuario deshabilitado no actúa
- integridad inválida bloquea aprobación
- proyecto archivado bloquea cambios

Aplicar en Application, no solo en WPF.

---

# 30. Auditoría

Registrar:

- creación de cierre
- cambio de estado
- checklist aplicado
- ítem actualizado
- ítem verificado
- punch creado/corregido/verificado/reabierto
- no conformidad creada/cerrada/reabierta
- documento registrado/aprobado/rechazado
- conciliación ejecutada
- bloqueo detectado
- excepción solicitada/aprobada/rechazada
- snapshot creado
- comparación
- exportación
- intento denegado
- conflicto de concurrencia
- fallo de integridad

---

# 31. Servicios Application

Crear servicios especializados, manteniendo cada archivo bajo 400 líneas:

- `IProjectCloseoutService`
- `ProjectCloseoutService`
- `ICloseoutChecklistService`
- `CloseoutChecklistService`
- `IPunchListService`
- `PunchListService`
- `ICloseoutDocumentService`
- `CloseoutDocumentService`
- `ICloseoutReconciliationService`
- `CloseoutReconciliationService`
- `ICloseoutReadinessService`
- `CloseoutReadinessService`

Evitar un servicio monolítico.

Puede existir una fachada compacta solo para compatibilidad.

---

# 32. Composition

Registrar:

- repositorios
- servicios
- evaluadores
- hashers
- comparadores
- ViewModels
- navegación

No usar service locator.

No instanciar repositorios desde ViewModels.

---

# 33. WPF — Closeout Workspace

Crear:

- `CloseoutWorkspaceViewModel`
- `CloseoutWorkspaceView`

Mostrar:

- proyecto
- responsable
- fecha objetivo
- estado
- porcentaje de preparación
- score por dimensión
- bloqueos
- alertas
- última conciliación
- último snapshot
- calidad del dato

Acciones:

- crear
- editar
- ejecutar conciliación
- revisar preparación
- crear snapshot
- exportar
- navegar a detalle

---

# 34. WPF — Checklist

Crear:

- `CloseoutChecklistViewModel`
- `CloseoutChecklistView`

Permitir:

- seleccionar plantilla
- filtrar categorías
- asignar responsable
- actualizar estado
- adjuntar evidencia
- enviar a verificación
- verificar
- rechazar
- marcar no aplicable con autorización

Mostrar progreso y bloqueos.

---

# 35. WPF — Punch list

Crear:

- `PunchListWorkspaceViewModel`
- `PunchListWorkspaceView`

Permitir:

- crear
- asignar
- actualizar
- adjuntar evidencia
- enviar a verificación
- cerrar
- rechazar
- reabrir
- filtrar por prioridad, responsable, estado y vencimiento

---

# 36. WPF — Conciliación

Crear:

- `CloseoutReconciliationViewModel`
- `CloseoutReconciliationView`

Pestañas o secciones:

- Económica
- Contractual
- Compras
- Planificación
- Documental
- Técnica

Mostrar para cada valor:

- valor
- moneda
- fuente
- fecha
- calidad
- diferencia
- estado
- trazabilidad

No usar color como único indicador.

---

# 37. WPF — Dashboard de preparación

Crear:

- `CloseoutReadinessDashboardViewModel`
- `CloseoutReadinessDashboardView`

KPIs:

- preparación global
- checklist
- punch abiertos
- NC abiertas
- documentos pendientes
- bloqueos técnicos
- bloqueos contractuales
- órdenes abiertas
- actividades críticas abiertas
- desviación económica
- variación de plazo
- excepciones activas
- riesgo de cierre

Permitir drill-down.

---

# 38. Navegación

Agregar grupo:

**Cierre del proyecto**

Rutas mínimas:

- Preparación de cierre
- Checklist
- Punch list
- Conciliación
- Dashboard de preparación

Integrar:

- ShellRoute
- catálogo
- ShellViewModel.Navigation
- ShellWindow
- búsqueda rápida
- permisos
- sesión
- proyecto activo
- modo solo lectura

No crear navegación paralela.

---

# 39. Exportación

Exportar:

- checklist
- punch list
- no conformidades
- documentos pendientes
- conciliación económica
- conciliación contractual
- conciliación de compras
- conciliación de planificación
- bloqueos
- excepciones
- snapshot
- dashboard de preparación

Formatos:

- CSV
- Excel
- PDF
- impresión existente

No recalcular en exportadores.

---

# 40. Reporte imprimible

Crear un reporte de preparación para cierre con:

1. Proyecto.
2. Fecha de estado.
3. Estado de preparación.
4. Score global y por dimensión.
5. Checklist.
6. Punch críticos.
7. No conformidades.
8. Documentos faltantes.
9. Conciliación económica.
10. Conciliación contractual.
11. Compras pendientes.
12. Actividades pendientes.
13. Bloqueos.
14. Excepciones.
15. Alertas.
16. Calidad del dato.
17. Firmas o aprobaciones informativas.

No generar conclusiones mediante IA.

---

# 41. Rendimiento

Validar con:

- miles de checklist items
- cientos de punch items
- cientos de documentos
- múltiples contratos
- miles de líneas de estados de pago
- cientos de órdenes de compra
- WBS profunda
- múltiples snapshots
- cientos de alertas

Evitar consultas N+1.

No hacer tests frágiles por milisegundos exactos.

---

# 42. Tests

No establecer una cuota artificial.

Cubrir riesgos reales:

## Checklist

- plantilla
- versión
- bloqueante
- evidencia
- verificación
- rechazo
- no aplicable
- SoD

## Punch list

- estados
- cierre
- rechazo
- reapertura
- criticidad
- vencimiento
- evidencia
- SoD

## No conformidades

- creación
- acciones
- cierre
- reapertura
- evidencia
- bloqueo

## Documentos

- requisito
- versión
- integridad
- aprobación
- ausencia
- vencimiento

## Conciliación económica

- presupuesto
- comprometido
- ejecutado
- valorizado
- facturado
- forecast
- doble conteo
- monedas
- datos faltantes

## Conciliación contractual

- contratos
- cambios
- claims
- pagos
- garantías
- multas
- reajustes
- bloqueos

## Compras

- orden abierta
- recepción parcial
- rechazo
- inventario
- material en tránsito

## Planificación

- actividad crítica abierta
- hito pendiente
- forecast
- plazo vigente
- excepción

## Readiness

- score
- bloqueos
- dimensiones
- excepción
- aprobación

## Snapshots

- hash
- versión
- integridad
- comparación
- reapertura

## Persistencia

- guardar
- actualizar
- reabrir
- migración
- concurrencia
- cancelación
- transacción

## WPF

- carga
- filtros
- comandos
- navegación
- permisos
- solo lectura
- no reentrancia
- error visible
- estado vacío

## Arquitectura

- archivos bajo 400 líneas
- Domain independiente
- Application sin WPF
- WPF sin fórmulas
- Composition resuelve servicios
- exportadores sin recálculo

Durante el desarrollo usar:

```powershell
dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
    -c Release `
    --filter "FullyQualifiedName~Closeout|FullyQualifiedName~PunchList|FullyQualifiedName~Reconciliation" |
    Select-Object -Last 30
```

---

# 43. Validación manual

Ejecutar realmente cuando sea posible:

1. Crear preparación de cierre.
2. Aplicar plantilla.
3. Completar ítem.
4. Adjuntar evidencia.
5. Verificar con otro usuario.
6. Crear punch crítico.
7. Corregirlo.
8. Rechazar verificación.
9. Corregir nuevamente.
10. Cerrar punch.
11. Crear no conformidad.
12. Registrar acción correctiva.
13. Cerrar NC.
14. Registrar documento.
15. Aprobar documento.
16. Ejecutar conciliación.
17. Revisar economía.
18. Revisar contratos.
19. Revisar compras.
20. Revisar planificación.
21. Ver bloqueos.
22. Solicitar excepción.
23. Aprobar con otro usuario.
24. Crear snapshot.
25. Modificar datos.
26. Crear segundo snapshot.
27. Comparar.
28. Exportar.
29. Cerrar y reabrir.
30. Verificar persistencia e integridad.
31. Probar usuario sin permiso.

No afirmar validaciones manuales no ejecutadas.

---

# 44. Documentación mínima

Actualizar:

- `task.md`
- `walkthrough.md`

Crear únicamente:

- `docs/active/closeout-preparation-specification.md`
- `docs/active/closeout-checklist-punch-specification.md`
- `docs/active/closeout-reconciliation-specification.md`
- `docs/active/closeout-readiness-specification.md`
- `docs/active/phase-12ab-report.md`

No modificar `AGENTS.md` salvo cambio de regla permanente.

No crear documentación redundante.

---

# 45. Validación automática final

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
- migraciones: aprobadas
- persistencia: aprobada
- reapertura: aprobada
- navegación: aprobada
- permisos y SoD: aprobados
- exportación: aprobada

El total final debe ser superior al total inicial.

---

# Criterio de cierre

Declarar exactamente:

**FASE 12AB — PREPARACIÓN Y CONCILIACIÓN PARA EL CIERRE COMPLETADA**

únicamente cuando:

- preparación de cierre funcione
- checklist funcione
- plantillas funcionen
- punch list funcione
- no conformidades funcionen
- documentos y evidencias funcionen
- conciliación económica funcione
- conciliación contractual funcione
- conciliación de compras funcione
- conciliación de planificación funcione
- conciliación documental y técnica funcione
- readiness score funcione
- bloqueos y excepciones funcionen
- snapshots y comparación funcionen
- dashboard funcione
- persistencia y reapertura funcionen
- WPF esté integrada
- navegación funcione
- permisos y SoD funcionen
- auditoría funcione
- exportación funcione
- todos los archivos nuevos estén bajo 400 líneas
- Debug y Release estén limpios
- suite global esté verde
- documentación esté completa

No iniciar Fase 12CDE.