# QUBI COST ELÉCTRICO — FASE 11C
# CAMBIOS CONTRACTUALES, OBRAS EXTRAORDINARIAS Y RECLAMOS

Trabaja de forma autónoma hasta completar exclusivamente la Fase 11C.

## Reglas estrictas de ahorro de tokens

1. No recorras todo el repositorio de forma recursiva.
2. Busca primero por nombre, símbolo o carpeta.
3. Lee únicamente:
   - Contracts
   - PaymentCertificates
   - Planning
   - BudgetEngine
   - módulos WPF contractuales
   - SQLite migrations
   - Composition
   - tests contractuales
4. No leas `docs/archive`.
5. No leas documentación de fases anteriores salvo dependencia directa.
6. No reproduzcas archivos completos en el chat.
7. Aplica cambios directamente.
8. Resume cada bloque en máximo 8 líneas.
9. Usa tests filtrados durante el desarrollo.
10. Ejecuta la suite completa solo al inicio y al final.
11. Limita salidas de terminal a las últimas 25 líneas.
12. No generes una cuota artificial de pruebas; cubre reglas y riesgos reales.
13. No uses subagentes salvo bloqueo técnico.
14. No crees `implementation_plan.md` salvo que falte arquitectura esencial.
15. No ejecutes Git.
16. No inicies Fase 11D.

## Estado de entrada

Antes de modificar, verifica solamente:

- versión real del schema SQLite
- total actual de pruebas
- build Debug y Release
- estado de Fase 11A y 11B
- contratos y estados de pago existentes

Comandos iniciales:

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build |
    Select-Object -Last 25

No ejecutes más pruebas globales durante el desarrollo salvo cambio transversal.

## Objetivo

Implementar:

- solicitudes de cambio
- órdenes de cambio
- obras extraordinarias
- instrucciones contractuales
- reclamos
- impacto económico
- impacto de plazo
- negociación
- aprobación
- implementación
- cierre
- persistencia
- permisos
- auditoría
- WPF
- exportación

No modificar contratos originales ni estados de pago aprobados.

## 1. Solicitud de cambio

Crear un agregado tipado con:

- ChangeRequestId
- ContractId
- número
- origen
- título
- descripción
- causa
- solicitante
- fecha
- urgencia
- WBS
- actividades
- partidas
- documentos
- impacto preliminar de costo
- impacto preliminar de plazo
- moneda
- estado
- responsable
- comentario
- RowVersion
- auditoría

Estados:

- Draft
- Submitted
- UnderAnalysis
- QuotationRequired
- Negotiation
- Approved
- Rejected
- ConvertedToChangeOrder
- Cancelled
- Closed

## 2. Orden de cambio

Crear:

- ChangeOrder
- ChangeOrderLine
- ChangeOrderVersion
- ChangeOrderSnapshot
- ChangeOrderAuditEvent

Campos mínimos:

- ChangeOrderId
- ContractId
- ChangeRequestId
- número
- versión
- fecha efectiva
- moneda
- monto del cambio
- días añadidos o reducidos
- partidas añadidas
- partidas modificadas
- partidas eliminadas
- actividades afectadas
- hitos afectados
- impacto de costo
- impacto de plazo
- impacto forecast
- impacto EVM
- estado
- aprobadores
- comentario
- hash SHA-256
- RowVersion

Estados:

- Draft
- PendingReview
- UnderNegotiation
- PendingApproval
- Approved
- Rejected
- Implemented
- Closed
- Cancelled
- Superseded

Reglas:

- contrato original inmutable
- orden aprobada inmutable
- corrección genera nueva versión
- actualización contractual solo después de aprobación
- no aplicar impacto dos veces
- no modificar baseline original
- conservar monto y plazo original
- actualizar monto y plazo vigentes de forma derivada

## 3. Obras extraordinarias

Crear modelo para:

- instrucción
- descripción
- origen
- fecha
- WBS
- actividad
- recursos
- materiales
- mano de obra
- equipos
- cantidades
- horas
- costos
- evidencia
- responsable
- aprobación
- valorización
- estado

Estados:

- Draft
- Instructed
- InExecution
- Submitted
- Approved
- Rejected
- Valued
- Closed

Permitir vincular una obra extraordinaria a una solicitud u orden de cambio.

## 4. Reclamos

Crear:

- ContractClaim
- ClaimEvent
- ClaimEvidence
- ClaimEvaluation
- ClaimResponse

Tipos:

- ScopeChange
- ClientDelay
- Interference
- AccessRestriction
- DesignChange
- ExceptionalWeather
- ForceMajeure
- Suspension
- Acceleration
- ProductivityLoss
- Other

Campos:

- ClaimId
- ContractId
- número
- tipo
- evento
- fecha del evento
- fecha de notificación
- causa
- descripción
- monto solicitado
- días solicitados
- monto reconocido
- días reconocidos
- moneda
- actividades afectadas
- WBS
- documentos
- responsable
- respuesta
- estado
- RowVersion
- auditoría

Estados:

- Draft
- Notified
- Submitted
- UnderReview
- InformationRequested
- Negotiation
- Accepted
- PartiallyAccepted
- Rejected
- Withdrawn
- Closed

## 5. Análisis de impacto

Crear servicio Application que consuma resultados existentes de:

- presupuesto
- contrato
- planificación
- CPM
- EVM
- estados de pago
- seguimiento

No recalcular motores base.

Debe producir un resultado estructurado con:

- impacto de costo
- impacto de plazo
- actividades afectadas
- hitos afectados
- posible cambio de camino crítico
- impacto en BAC/EAC/ETC/VAC
- monto contractual original
- monto vigente
- monto propuesto
- plazo original
- plazo vigente
- plazo propuesto
- advertencias
- calidad de datos

No inventar impacto cuando no existan datos.

## 6. Versionado e integridad

Implementar:

- versiones inmutables
- snapshots
- SHA-256 determinista
- comparación entre versiones
- detección de alteraciones
- verificación al abrir documentos aprobados

## 7. Permisos y SoD

Añadir:

- ChangeRequestView
- ChangeRequestCreate
- ChangeRequestEdit
- ChangeRequestSubmit
- ChangeRequestAnalyze
- ChangeOrderCreate
- ChangeOrderEditDraft
- ChangeOrderNegotiate
- ChangeOrderApprove
- ChangeOrderReject
- ChangeOrderImplement
- ExtraWorkManage
- ClaimView
- ClaimCreate
- ClaimSubmit
- ClaimEvaluate
- ClaimRespond
- ClaimApprove
- ContractChangeAuditView
- ContractChangeExport

Reglas mínimas:

- creador no aprueba
- último editor no aprueba
- analista no altera evidencia
- usuario deshabilitado no actúa
- integridad inválida bloquea aprobación
- contrato cerrado bloquea cambios
- estado de pago aprobado no se altera

## 8. Auditoría

Registrar:

- creación
- edición
- envío
- análisis
- negociación
- aprobación
- rechazo
- implementación
- cierre
- cambio de monto
- cambio de plazo
- obra extraordinaria
- claim
- respuesta
- intento denegado
- conflicto
- fallo de integridad

## 9. Persistencia

Definir repositorios internos para:

- ChangeRequest
- ChangeOrder
- ChangeOrderVersion
- ChangeOrderSnapshot
- ExtraWork
- ContractClaim
- ClaimEvidence
- ClaimEvaluation
- ContractChangeAudit

Crear tablas SQLite equivalentes y migraciones posteriores al schema real.

Requisitos:

- transacciones
- foreign keys
- índices
- RowVersion
- CancellationToken
- reapertura
- concurrencia
- migración probada

## 10. Application

Crear servicios para:

- crear y editar solicitud
- enviar
- analizar
- aprobar/rechazar
- convertir a orden de cambio
- crear y versionar orden
- calcular impacto
- aprobar
- implementar
- cerrar
- gestionar obra extraordinaria
- crear y evaluar claim
- responder
- comparar versiones
- verificar integridad
- consultar historial
- exportar

## 11. WPF

Crear módulos:

- ChangeRequestWorkspace
- ChangeOrderWorkspace
- ExtraWorkWorkspace
- ClaimWorkspace
- ImpactAnalysisView

Cada módulo debe tener:

- ViewModel
- View
- carga cancelable
- comandos no reentrantes
- permisos
- solo lectura
- errores visibles
- navegación ShellRoute
- búsqueda y filtros

Añadir grupo o rutas bajo:

Gestión contractual

No crear navegación paralela.

## 12. Exportación

Exportar:

- solicitud de cambio
- orden de cambio
- comparación de versiones
- análisis de impacto
- obra extraordinaria
- claim
- historial

Usar CSV/Excel/PDF existentes.

No recalcular dentro del exportador.

## 13. Tests

Durante el desarrollo usa filtros:

dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
    -c Release `
    --filter "FullyQualifiedName~ChangeRequest|FullyQualifiedName~ChangeOrder|FullyQualifiedName~ContractClaim|FullyQualifiedName~ExtraWork" |
    Select-Object -Last 30

Cubrir sin duplicar casos:

- estados y transiciones
- monto y plazo
- versionado
- hash
- comparación
- impacto
- contrato cerrado
- orden aprobada inmutable
- SoD
- claims
- obras extraordinarias
- persistencia
- reapertura
- migración
- concurrencia
- cancelación
- ViewModels
- navegación
- arquitectura

Usa Theory parametrizadas cuando varias entradas validen la misma regla.

## 14. Documentación mínima

Actualizar:

- task.md
- walkthrough.md
- AGENTS.md solo si cambian reglas permanentes

Crear únicamente:

- docs/change-management-specification.md
- docs/change-order-impact-specification.md
- docs/claims-management-specification.md
- docs/phase-11c-report.md

No crear documentos redundantes.

## 15. Validación final

Ejecutar únicamente al final:

dotnet restore ".\QubiCostElectrical.Wpf.sln"

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Debug --warnaserror |
    Select-Object -Last 25

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build |
    Select-Object -Last 25

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release |
    Select-Object -Last 20

Reportar solamente:

- errores y advertencias Debug
- errores y advertencias Release
- pruebas aprobadas/fallidas/omitidas
- arquitectura
- schema SQLite
- migraciones
- persistencia y reapertura
- WPF y navegación
- permisos y SoD
- exportación
- limitaciones reales

## Criterio de cierre

Declarar únicamente:

FASE 11C — CAMBIOS CONTRACTUALES Y RECLAMOS COMPLETADA

cuando:

- solicitudes funcionen
- órdenes funcionen
- obras extraordinarias funcionen
- claims funcionen
- análisis de impacto funcione
- versionado e integridad funcionen
- persistencia y reapertura funcionen
- WPF esté integrada
- navegación funcione
- permisos y SoD funcionen
- auditoría funcione
- exportación funcione
- builds y tests estén verdes

No iniciar Fase 11D.