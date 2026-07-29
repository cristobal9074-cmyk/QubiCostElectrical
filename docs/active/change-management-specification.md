# Especificación de Gestión de Cambios Contractuales (Fase 11C)

## Resumen

Implementación del módulo de **Gestión de Cambios Contractuales**, que incluye:

- **Solicitudes de Cambio** (`ChangeRequest`): Gestión de solicitudes de modificación contractual con ciclo de vida completo (Borrador → Enviado → En Análisis → Aprobado/Rechazado/Cancelado → Convertido a OC).
- **Órdenes de Cambio** (`ChangeOrder`): Ejecución formal de cambios aprobados con versionado, líneas de detalle y snapshots.
- **Obras Extraordinarias** (`ExtraWork`): Trabajos no contemplados en el contrato original, con flujo desde instrucción hasta cierre.
- **Reclamos** (`ContractClaim`): Gestión de reclamos contractuales con eventos, evidencias, evaluaciones y respuestas.

## Arquitectura

### Capas

| Capa | Ubicación | Responsabilidad |
|------|-----------|-----------------|
| Domain | `QubiCostElectrical.Domain/Contracts/` | Modelos, IDs, enums, status transitions |
| Application | `QubiCostElectrical.Application/Contracts/` | Servicios, repositorios, permisos |
| Infrastructure | `QubiCostElectrical.Infrastructure/SQLite/` | Persistencia, migración v55 |
| Composition | `QubiCostElectrical.Composition/` | Registro DI |
| WPF | `QubiCostElectrical.Wpf/Features/ContractChanges/` | Vistas y ViewModels |

### Modelos de Dominio

- `ChangeRequest` → `ChangeRequestId`, `ChangeRequestStatus`, `ChangeRequestSource`, `ChangeRequestUrgency`
- `ChangeOrder` → `ChangeOrderId`, `ChangeOrderStatus`, `ChangeOrderLine`
- `ExtraWork` → `ExtraWorkId`, `ExtraWorkStatus`, `ExtraWorkOrigin`
- `ContractClaim` → `ClaimId`, `ClaimStatus`, `ClaimType`, `ClaimEvent`, `ClaimEvidence`, `ClaimEvaluation`, `ClaimResponse`

### Transiciones de Estado

Cada entidad tiene un diccionario de transiciones válidas definido en:
- `ChangeRequestStatusTransitions.cs`
- `ExtraWorkStatusTransitions.cs`
- `ChangeOrderStatusTransitions.cs`
- `ClaimStatusTransitions.cs`

### Seguridad

- `ContractChangePermissions.cs`: Permisos estáticos por rol
- `ContractChangePermissionChecker.cs`: Verificador de permisos + SoD (Segregation of Duties)
- `SoDPolicyConfig.cs`: Políticas como CreatorCannotApprove, ApprovedOrObsoleteCannotEdit

### Persistencia

- Esquema SQLite v55 con 14 tablas nuevas
- Almacenamiento JSON en columna `json_data` con `INSERT...ON CONFLICT DO UPDATE`
- `RowVersion` para control de concurrencia optimista
- Auditoría en `ContractChangeAuditEvent` con detalle de cambios

## WPF

Tres vistas tipo workspace con:
- DataGrid con columnas relevantes
- Búsqueda por texto
- Botón de exportación CSV
- Integración en navegación ShellRoute
