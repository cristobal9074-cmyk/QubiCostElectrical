# QUBI COST ELÉCTRICO — FASE 12CDE
## Recepción definitiva, entrega, lecciones aprendidas, dashboard final y archivo histórico

## Fuente única de alcance

Este documento define exclusivamente las fases 12C, 12D y 12E.

Ejecutar únicamente después de que la Fase 12AB esté implementada y validada.

No iniciar la Fase 13.

---

# Reglas de ahorro de tokens

1. Leer únicamente `AGENTS.md` y este documento.
2. No recorrer todo el repositorio de manera recursiva.
3. Buscar primero por nombre, símbolo o carpeta.
4. Trabajar inicialmente solo en:
   - `Domain/Closeout`
   - `Application/Closeout`
   - `Infrastructure/SQLite`
   - `Composition`
   - `Wpf/Features/Closeout`
   - navegación Shell
   - tests de cierre
5. Leer Planning, Procurement, Contracts, Budget, EVM o Progress solo por integración directa.
6. No leer `docs/archive`.
7. No volver a leer archivos sin cambios.
8. No reproducir archivos completos en el chat.
9. Aplicar cambios directamente.
10. Resumir cada bloque en máximo ocho líneas.
11. Usar pruebas filtradas durante el desarrollo.
12. Ejecutar la suite completa solo al inicio y al final.
13. Limitar salidas de terminal a las últimas 25 líneas.
14. No usar subagentes salvo bloqueo técnico real.
15. No establecer una cuota artificial de pruebas.
16. Usar teorías parametrizadas para reglas equivalentes.
17. No ejecutar Git.
18. Mantener todos los archivos bajo 400 líneas.
19. No duplicar motores ni fuentes de verdad.
20. No iniciar Fase 13.

---

# Estado inicial

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

- schema SQLite real
- total inicial de pruebas
- estado real de 12AB
- servicios y repositorios de cierre existentes
- rutas WPF ya implementadas

No modificar código antes de verificar que 12AB esté completa.

---

# Objetivo general

Completar el cierre formal del proyecto mediante:

- recepción provisoria
- recepción definitiva
- entrega de obra
- cierre de observaciones
- documentación As Built
- entrega de manuales y certificados
- liberación de garantías
- liberación de retenciones
- conciliación final
- cierre de contratos
- cierre de compras e inventario
- lecciones aprendidas
- indicadores finales
- dashboard de cierre
- snapshot final inmutable
- reporte final
- archivo histórico
- bloqueo de edición del proyecto cerrado

El proyecto cerrado debe conservarse íntegro, verificable y consultable.

---

# PARTE C — RECEPCIÓN Y ENTREGA

# 1. Recepción del proyecto

Crear o completar:

- `ProjectReception`
- `ProjectReceptionId`
- `ReceptionType`
- `ReceptionStatus`
- `ReceptionInspection`
- `ReceptionObservation`
- `ReceptionApproval`

Tipos mínimos:

- Partial
- Provisional
- Final
- ClientHandover
- InternalHandover

Estados:

- Draft
- Scheduled
- InInspection
- Observed
- PendingCorrection
- PendingApproval
- Approved
- Rejected
- Completed
- Cancelled

Campos mínimos:

- ReceptionId
- ProjectId
- CloseoutId
- tipo
- número
- título
- fecha programada
- fecha realizada
- cliente
- contratista
- inspector
- responsables
- alcance
- observaciones
- documentos
- estado
- aprobación
- CreatedUtc
- CreatedBy
- ModifiedUtc
- ModifiedBy
- RowVersion

---

# 2. Reglas de recepción

No permitir recepción definitiva cuando exista alguno de estos bloqueos sin excepción aprobada:

- checklist obligatorio incompleto
- punch crítico abierto
- no conformidad crítica abierta
- documento bloqueante pendiente
- conciliación económica inconsistente
- contrato pendiente de resolución
- actividad crítica abierta
- recepción provisoria no aprobada
- integridad inválida
- excepción vencida

La recepción provisoria puede admitir pendientes expresamente documentados.

La recepción definitiva debe exigir cierre o aceptación formal de dichos pendientes.

---

# 3. Inspecciones

Cada inspección debe registrar:

- fecha
- ubicación
- alcance
- asistentes
- inspector
- resultado
- ítems revisados
- observaciones
- fotografías
- documentos
- punch items generados
- no conformidades generadas
- decisión
- firma o aprobación informativa
- auditoría

No implementar un motor general de inspecciones fuera del cierre.

---

# 4. Actas de recepción y entrega

Crear:

- `ReceptionRecord`
- `HandoverRecord`
- `AcceptanceCertificate`

Cada acta debe incluir:

- proyecto
- contrato
- tipo de recepción
- fecha
- partes
- alcance entregado
- observaciones
- reservas
- pendientes aceptados
- garantías
- documentación entregada
- aprobadores
- firmas informativas
- versión
- SHA-256
- estado

Una acta aprobada debe ser inmutable.

---

# 5. Documentación As Built

Crear o completar un control específico para:

- planos As Built
- memorias
- esquemas
- listados finales
- protocolos
- resultados de pruebas
- certificados
- manuales de operación
- manuales de mantenimiento
- fichas técnicas
- repuestos
- garantías
- capacitaciones
- actas

Cada entregable debe incluir:

- requisito
- versión requerida
- versión entregada
- autor
- responsable
- fecha
- aprobación
- integridad
- ubicación
- comentario
- estado

Estados:

- Required
- InPreparation
- Submitted
- UnderReview
- Approved
- Rejected
- Superseded
- Archived

---

# 6. Entrega y capacitación

Permitir registrar:

- sesión de capacitación
- tema
- instructor
- asistentes
- fecha
- duración
- documentación
- evaluación informativa
- evidencia
- conformidad
- pendientes

No construir un LMS.

Limitarlo al proceso de entrega del proyecto.

---

# 7. Garantías de cierre

Consumir las garantías contractuales existentes.

Registrar para el cierre:

- garantía
- inicio
- vencimiento
- responsable
- cobertura
- condición de liberación
- documento
- renovación
- estado
- alerta

No duplicar el modelo contractual de garantías.

Crear solo vínculos o registros de liberación cuando corresponda.

---

# 8. Liberación de garantías

Crear:

- `GuaranteeReleaseRequest`
- `GuaranteeReleaseApproval`

Reglas:

- validar condiciones contractuales
- validar cierre de observaciones
- validar documentación requerida
- aplicar SoD
- registrar motivo
- conservar documento
- auditar
- no eliminar garantía original

---

# 9. Liberación de retenciones

Consumir retenciones de estados de pago.

Crear flujo para:

- solicitar liberación
- validar saldo
- validar condición contractual
- aprobar
- rechazar
- liberar parcial o totalmente
- conservar trazabilidad

No liberar automáticamente por fecha.

---

# 10. Cierre contractual

El cierre contractual debe verificar:

- estados de pago resueltos
- anticipos amortizados o conciliados
- retenciones resueltas
- cambios implementados
- claims cerrados o aceptados como pendientes
- garantías documentadas
- multas resueltas
- reajustes incorporados
- facturas conciliadas
- saldo contractual definido

No modificar contratos aprobados.

Registrar el cierre mediante transición y snapshot.

---

# 11. Cierre de compras e inventario

Verificar:

- órdenes de compra cerradas o canceladas
- recepciones conciliadas
- devoluciones resueltas
- materiales en tránsito resueltos
- inventario final conciliado
- sobrantes clasificados
- stock transferido, devuelto, vendido o dado de baja
- diferencias justificadas

Crear un resultado de conciliación final de inventario.

No eliminar movimientos históricos.

---

# 12. Materiales sobrantes

Registrar disposición:

- TransferToProject
- ReturnToSupplier
- ReturnToWarehouse
- ClientHandover
- Sale
- Scrap
- Loss
- Other

Cada disposición debe incluir:

- recurso
- cantidad
- unidad
- ubicación
- destino
- costo
- responsable
- autorización
- evidencia
- auditoría

---

# PARTE D — LECCIONES APRENDIDAS

# 13. Modelo de lección aprendida

Crear:

- `LessonLearned`
- `LessonLearnedId`
- `LessonCategory`
- `LessonStatus`
- `LessonEvidence`
- `LessonRecommendation`

Campos:

- LessonId
- ProjectId
- CloseoutId
- título
- descripción
- categoría
- evento
- causa raíz
- impacto
- resultado
- recomendación
- aplicabilidad
- WBS
- actividad
- contrato
- proveedor
- recurso
- autor
- revisor
- fecha
- evidencia
- confidencialidad
- estado
- versión
- RowVersion

Estados:

- Draft
- Submitted
- UnderReview
- Approved
- Rejected
- Published
- Archived

---

# 14. Categorías

Categorías mínimas:

- Estimación
- Presupuesto
- APU
- Planificación
- Productividad
- Compras
- Proveedor
- Contratos
- Cambios
- Claims
- Calidad
- Seguridad
- Ingeniería
- Documentación
- Cliente
- Riesgo
- Costo
- Plazo
- Comunicación
- Tecnología
- Otro configurable

No usar strings dispersos en WPF.

---

# 15. Fuentes de lecciones

Permitir sugerir lecciones desde:

- desviación económica
- atraso
- cambio
- claim
- punch
- no conformidad
- baja productividad
- problema de proveedor
- riesgo ocurrido
- decisión exitosa
- innovación
- excepción de cierre

La generación debe ser explícita.

No generar lecciones automáticamente mediante IA.

---

# 16. Productividades y rendimientos finales

Consolidar, cuando existan datos:

- productividad planificada
- productividad real
- horas por unidad
- costo por unidad
- consumo teórico
- consumo real
- desviación
- contexto
- cuadrilla
- ubicación
- periodo

No alterar el banco de precios ni APU automáticamente.

Permitir publicar una recomendación para futura revisión.

---

# 17. Conocimiento corporativo

Preparar un catálogo consultable de lecciones aprobadas.

Debe permitir:

- búsqueda
- filtros
- categorías
- proyecto origen
- fecha
- impacto
- aplicabilidad
- etiquetas
- estado
- evidencia
- exportación

No implementar todavía colaboración corporativa ni sincronización central de Fase 13.

Debe funcionar localmente dentro de la arquitectura actual.

---

# 18. Aprobación y privacidad

Aplicar permisos para:

- crear
- revisar
- aprobar
- publicar
- archivar
- consultar información confidencial

No publicar automáticamente información marcada como sensible.

Aplicar SoD cuando corresponda.

---

# PARTE E — DASHBOARD, SNAPSHOT Y ARCHIVO

# 19. Resultado final del proyecto

Crear modelos Application:

- `ProjectFinalResult`
- `ProjectFinalFinancialResult`
- `ProjectFinalScheduleResult`
- `ProjectFinalContractResult`
- `ProjectFinalProcurementResult`
- `ProjectFinalQualityResult`
- `ProjectFinalKpi`
- `ProjectFinalAlert`

Consumir fuentes existentes.

No recalcular motores base.

---

# 20. KPIs finales

## Economía

- presupuesto original
- presupuesto vigente
- costo final
- resultado final
- variación absoluta
- variación porcentual
- comprometido final
- valorizado final
- facturado final
- pagado informativo final
- saldo final
- VAC final
- CPI final

## Plazo

- fecha inicio planificada
- fecha inicio real
- fecha término baseline
- fecha término vigente
- fecha término real
- duración planificada
- duración real
- variación
- SPI final

## Contratos

- contratos totales
- contratos cerrados
- cambios
- claims
- reajustes
- multas
- garantías
- retenciones

## Compras

- monto comprado
- monto recibido
- órdenes cerradas
- entregas tardías
- ahorro o sobrecosto
- material sobrante

## Técnico y documental

- checklist final
- punch final
- no conformidades
- documentos entregados
- documentos aprobados
- recepción provisoria
- recepción definitiva

## Conocimiento

- lecciones creadas
- lecciones aprobadas
- productividades registradas
- recomendaciones publicadas

---

# 21. Reglas de resultado final

Cada KPI debe incluir:

- valor
- unidad
- fecha
- fuente
- versión
- calidad del dato
- interpretación determinista

No presentar información incompleta como definitiva.

No mezclar monedas sin conversión explícita.

Evitar doble contabilización.

---

# 22. Dashboard final de cierre

Crear:

- `FinalCloseoutDashboardViewModel`
- `FinalCloseoutDashboardView`

Secciones:

- resumen ejecutivo
- resultado económico
- resultado de plazo
- resultado contractual
- compras
- calidad
- documentación
- recepción
- garantías y retenciones
- lecciones aprendidas
- alertas
- calidad del dato

Permitir drill-down a módulos existentes.

---

# 23. Snapshot final

Crear snapshot inmutable:

- ProjectId
- CloseoutId
- fecha de cierre
- datos del proyecto
- conciliación final
- KPIs
- recepciones
- contratos
- compras
- EVM final
- documentos
- garantías
- retenciones
- lecciones
- alertas
- hashes de fuentes
- versiones de motores
- autor
- comentario
- CreatedUtc
- SHA-256

Solo puede existir un snapshot final oficial por versión de cierre.

Una reapertura debe crear una nueva versión, no alterar el snapshot previo.

---

# 24. Integridad final

Verificar:

- snapshot
- documentos
- actas
- conciliaciones
- hashes fuente
- versiones

Cuando exista alteración:

- bloquear cierre
- registrar auditoría
- mostrar error estructurado
- no regenerar silenciosamente

---

# 25. Estado de proyecto cerrado

Crear o reutilizar transición de proyecto:

- Active
- Closing
- ProvisionallyClosed
- Closed
- Reopened
- Archived

Reglas:

- `Closed` obliga modo solo lectura
- solo usuario autorizado puede reabrir
- reapertura requiere motivo
- reapertura crea versión
- snapshot oficial anterior permanece inmutable
- operaciones bloqueadas deben fallar también en Application

No confiar solo en deshabilitar botones.

---

# 26. Archivo histórico

Crear un manifiesto del archivo:

- proyecto
- versión
- fecha
- responsable
- ubicación
- documentos
- snapshot final
- hashes
- schema SQLite
- versión de aplicación
- observaciones
- política de retención
- estado

No mover ni borrar físicamente archivos sin acción explícita del usuario.

Preparar el proyecto para archivo, pero no automatizar acciones destructivas.

---

# 27. Paquete de cierre

Permitir generar un paquete de cierre con referencias a:

- reporte final
- actas
- As Built
- manuales
- certificados
- garantías
- estados de pago
- contratos
- cambios
- claims
- conciliaciones
- inventario
- lecciones
- snapshot
- manifiesto

Generar índice y manifest con SHA-256.

No duplicar archivos grandes cuando sea posible.

---

# 28. Reporte ejecutivo final

El reporte debe incluir:

1. Portada.
2. Identificación del proyecto.
3. Resumen ejecutivo.
4. Resultado económico.
5. Resultado de plazo.
6. Contratos.
7. Compras.
8. Cambios y claims.
9. Calidad y punch list.
10. Documentación.
11. Recepciones.
12. Garantías y retenciones.
13. Lecciones aprendidas.
14. KPIs finales.
15. Alertas o pendientes aceptados.
16. Calidad de datos.
17. Firmas informativas.
18. Hash del snapshot.

No usar IA para redactar conclusiones.

---

# 29. Comparación final

Permitir comparar:

- baseline inicial vs resultado final
- presupuesto inicial vs costo final
- plazo inicial vs plazo real
- contrato original vs contrato final
- forecast anterior vs resultado final
- snapshot de preparación vs snapshot final

Resultado estructurado con:

- valor inicial
- valor final
- diferencia
- porcentaje
- severidad
- fuente
- explicación

---

# 30. Persistencia

Definir repositorios:

- `IProjectReceptionRepository`
- `IReceptionRecordRepository`
- `IGuaranteeReleaseRepository`
- `IRetentionReleaseRepository`
- `IInventoryCloseoutRepository`
- `ILessonLearnedRepository`
- `IFinalCloseoutSnapshotRepository`
- `IProjectArchiveManifestRepository`

Combinar repositorios cuando sea coherente.

No crear uno por cada tabla sin necesidad.

---

# 31. SQLite

Crear tablas equivalentes a:

- `closeout_receptions`
- `closeout_reception_inspections`
- `closeout_reception_records`
- `closeout_handover_records`
- `closeout_training_records`
- `closeout_guarantee_releases`
- `closeout_retention_releases`
- `closeout_inventory_reconciliation`
- `closeout_surplus_disposals`
- `closeout_lessons_learned`
- `closeout_lesson_evidence`
- `closeout_final_snapshots`
- `closeout_archive_manifests`

Adaptar a convenciones existentes.

Requisitos:

- foreign keys
- índices
- transacciones
- RowVersion
- soft delete
- cancelación
- reapertura
- concurrencia
- paginación

---

# 32. Migraciones

Crear migraciones posteriores al schema real de 12AB.

No asumir números.

Separar cuando mejore seguridad:

- recepción y entrega
- liberaciones e inventario
- lecciones
- snapshot y archivo

No modificar migraciones históricas.

Probar actualización desde el schema anterior real.

---

# 33. Permisos

Añadir:

- ReceptionView
- ReceptionCreate
- ReceptionInspect
- ReceptionApprove
- ReceptionReject
- FinalReceptionApprove
- HandoverManage
- AsBuiltManage
- TrainingRecordManage
- GuaranteeReleaseRequest
- GuaranteeReleaseApprove
- RetentionReleaseRequest
- RetentionReleaseApprove
- InventoryCloseoutManage
- LessonLearnedView
- LessonLearnedCreate
- LessonLearnedReview
- LessonLearnedApprove
- LessonLearnedPublish
- FinalCloseoutView
- FinalCloseoutApprove
- FinalCloseoutReopen
- FinalSnapshotCreate
- FinalCloseoutExport
- ProjectArchivePrepare
- CloseoutFinalAuditView

Denegación por defecto.

---

# 34. SoD

Políticas mínimas:

- creador de recepción no aprueba
- inspector no realiza aprobación final cuando esté configurado
- solicitante de liberación no aprueba
- autor de lección no la publica directamente
- creador de snapshot no aprueba cierre
- último editor no aprueba
- usuario deshabilitado no actúa
- integridad inválida bloquea cierre
- cierre definitivo bloquea edición
- reapertura requiere usuario distinto cuando la política lo exija

---

# 35. Auditoría

Registrar:

- recepción creada
- inspección
- observación
- aprobación
- rechazo
- acta emitida
- documento As Built aprobado
- capacitación
- garantía liberada
- retención liberada
- inventario conciliado
- sobrante dispuesto
- lección creada/revisada/aprobada/publicada
- dashboard consultado
- snapshot final creado
- cierre aprobado
- proyecto reabierto
- archivo preparado
- exportación
- intento denegado
- conflicto
- fallo de integridad

---

# 36. Servicios Application

Crear servicios especializados, todos bajo 400 líneas:

- `IProjectReceptionService`
- `ProjectReceptionService`
- `IHandoverService`
- `HandoverService`
- `ICloseoutReleaseService`
- `CloseoutReleaseService`
- `IInventoryCloseoutService`
- `InventoryCloseoutService`
- `ILessonLearnedService`
- `LessonLearnedService`
- `IFinalCloseoutService`
- `FinalCloseoutService`
- `IProjectArchiveService`
- `ProjectArchiveService`

No crear un servicio monolítico.

---

# 37. WPF — Recepciones

Crear:

- `ProjectReceptionWorkspaceViewModel`
- `ProjectReceptionWorkspaceView`

Permitir:

- crear
- programar
- inspeccionar
- añadir observación
- vincular punch
- adjuntar evidencia
- aprobar
- rechazar
- emitir acta
- filtrar
- exportar

---

# 38. WPF — Entrega y documentos

Crear:

- `ProjectHandoverViewModel`
- `ProjectHandoverView`

Mostrar:

- As Built
- manuales
- certificados
- capacitaciones
- actas
- garantías
- pendientes
- integridad

---

# 39. WPF — Garantías y retenciones

Crear:

- `CloseoutReleasesViewModel`
- `CloseoutReleasesView`

Permitir:

- solicitar liberación
- revisar condiciones
- aprobar
- rechazar
- liberar parcial
- exportar

---

# 40. WPF — Lecciones aprendidas

Crear:

- `LessonsLearnedWorkspaceViewModel`
- `LessonsLearnedWorkspaceView`
- `LessonLearnedEditorViewModel`
- `LessonLearnedEditorView`

Permitir:

- crear
- vincular fuente
- revisar
- aprobar
- publicar
- buscar
- filtrar
- exportar

---

# 41. WPF — Dashboard final

Crear:

- `FinalCloseoutDashboardViewModel`
- `FinalCloseoutDashboardView`

Permitir:

- consultar KPIs
- revisar calidad
- navegar a detalle
- crear snapshot
- comparar
- aprobar cierre
- reabrir con permiso
- generar paquete
- exportar reporte

---

# 42. Navegación

Bajo **Cierre del proyecto**, añadir:

- Recepciones
- Entrega y As Built
- Garantías y retenciones
- Lecciones aprendidas
- Dashboard final
- Archivo histórico

Integrar:

- ShellRoute
- catálogo
- navegación
- ShellWindow
- búsqueda rápida
- permisos
- proyecto activo
- modo solo lectura

---

# 43. Exportación

Exportar:

- acta de recepción
- acta de entrega
- As Built index
- manuales y certificados
- garantías
- retenciones
- inventario final
- materiales sobrantes
- lecciones aprendidas
- KPIs
- comparación final
- reporte ejecutivo
- manifiesto
- paquete de cierre

Usar infraestructura existente.

No recalcular en exportadores.

---

# 44. Tests

No establecer una cuota artificial.

Cubrir:

## Recepción

- estados
- observaciones
- bloqueos
- aprobación
- rechazo
- provisional
- definitiva
- SoD
- inmutabilidad

## Documentos

- As Built
- versiones
- integridad
- aprobación
- documento faltante

## Liberaciones

- garantía
- retención
- parcial
- total
- condición incumplida
- SoD

## Inventario

- conciliación
- sobrante
- transferencia
- devolución
- baja
- doble disposición

## Lecciones

- creación
- revisión
- publicación
- privacidad
- vínculo con fuente
- productividad
- permisos

## Resultado final

- economía
- plazo
- contratos
- compras
- EVM
- doble conteo
- moneda
- calidad

## Snapshot

- hash
- inmutabilidad
- versión
- alteración
- reapertura

## Cierre

- aprobación
- bloqueo
- modo solo lectura
- reapertura
- versión
- auditoría

## Archivo

- manifiesto
- hashes
- índice
- referencias
- cancelación

## Persistencia

- guardar
- actualizar
- reapertura
- migración
- concurrencia
- cancelación
- transacción

## WPF

- carga
- filtros
- comandos
- permisos
- navegación
- no reentrancia
- errores
- solo lectura
- estado vacío

## Arquitectura

- archivos bajo 400 líneas
- Domain independiente
- Application sin WPF
- WPF sin reglas
- exportadores sin cálculos
- Composition resuelve servicios

Tests filtrados:

```powershell
dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
    -c Release `
    --filter "FullyQualifiedName~Reception|FullyQualifiedName~FinalCloseout|FullyQualifiedName~LessonLearned|FullyQualifiedName~ProjectArchive" |
    Select-Object -Last 30
```

---

# 45. Validación manual

Ejecutar realmente cuando sea posible:

1. Abrir cierre preparado.
2. Crear recepción provisoria.
3. Ejecutar inspección.
4. Registrar observaciones.
5. Generar punch.
6. Corregir observaciones.
7. Aprobar recepción provisoria.
8. Registrar As Built.
9. Aprobar manuales.
10. Registrar capacitación.
11. Solicitar liberación de garantía.
12. Aprobar con otro usuario.
13. Solicitar liberación de retención.
14. Conciliar inventario.
15. Disponer sobrantes.
16. Crear lección aprendida.
17. Revisar y publicar.
18. Abrir dashboard final.
19. Revisar KPIs.
20. Crear snapshot.
21. Intentar cerrar con bloqueo.
22. Resolver bloqueo.
23. Aprobar recepción definitiva.
24. Cerrar proyecto.
25. Verificar modo solo lectura.
26. Generar reporte.
27. Generar manifiesto.
28. Cerrar y reabrir aplicación.
29. Verificar persistencia.
30. Reabrir proyecto con autorización.
31. Verificar que snapshot previo no cambió.
32. Probar usuario sin permiso.

No afirmar pruebas manuales no ejecutadas.

---

# 46. Documentación mínima

Actualizar:

- `task.md`
- `walkthrough.md`

Crear únicamente:

- `docs/active/project-reception-specification.md`
- `docs/active/project-handover-specification.md`
- `docs/active/closeout-release-specification.md`
- `docs/active/lessons-learned-specification.md`
- `docs/active/final-closeout-specification.md`
- `docs/active/project-archive-specification.md`
- `docs/active/phase-12cde-report.md`
- `docs/active/phase-12-final-report.md`

No modificar `AGENTS.md` salvo cambio permanente.

No crear documentación redundante.

---

# 47. Validación automática final

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

Resultado obligatorio:

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
- modo solo lectura: aprobado
- navegación: aprobada
- permisos y SoD: aprobados
- exportación: aprobada
- archivo histórico: aprobado

El total debe superar el total inicial.

---

# Criterio de cierre

Declarar exactamente:

**FASE 12 — CIERRE INTEGRAL DEL PROYECTO COMPLETADA**

únicamente cuando:

- recepción provisoria funcione
- recepción definitiva funcione
- inspecciones y actas funcionen
- entrega y As Built funcionen
- capacitaciones funcionen
- liberación de garantías funcione
- liberación de retenciones funcione
- inventario final funcione
- disposición de sobrantes funcione
- cierre contractual funcione
- lecciones aprendidas funcionen
- catálogo de conocimiento funcione
- resultado final sea correcto
- dashboard final funcione
- snapshot final sea inmutable
- cierre bloquee edición
- reapertura cree una nueva versión
- archivo histórico y manifiesto funcionen
- paquete de cierre funcione
- persistencia y reapertura funcionen
- WPF esté integrada
- navegación funcione
- permisos y SoD funcionen
- auditoría funcione
- exportaciones funcionen
- todos los archivos nuevos estén bajo 400 líneas
- Debug y Release estén limpios
- suite global esté verde
- documentación final esté completa

No iniciar Fase 13.