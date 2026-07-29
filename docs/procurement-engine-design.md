# Motor de compras y abastecimiento (Fase 9A)

## Alcance

Base profesional de **proveedores**, **requerimientos de compra (PR)** y **solicitudes de cotización (RFQ)** con respuesta inicial. Fuera de alcance: adjudicación, OC, recepciones, inventario, ERP.

## Capas

| Capa | Responsabilidad |
|------|-----------------|
| Domain | Agregados, enums, validación, consolidación, política de fecha de solicitud |
| Application | Servicios, permisos, auditoría, exportación CSV, interfaces de repositorio |
| Infrastructure | SQLite (JSON + columnas índice), migraciones 41–44 |
| Composition | `AppServices.SetProcurementServices` |
| WPF | Workspaces Proveedores / Requerimientos / RFQ + menú Compras |

## Flujos

1. Alta de proveedor y contactos → desactivación lógica.
2. PR manual o generación confirmada desde presupuesto/planificación (preview → confirm).
3. Consolidación de ítems compatibles (conflicto si specs incompatibles).
4. Envío / aprobación SoD / rechazo / cancelación.
5. RFQ desde PR aprobados → invitación → emisión → respuesta inicial.

## Política de fecha sugerida

`fecha_requerida - lead_proveedor - margen_seguridad` en `ProcurementNeedDatePolicy` (nunca en WPF).

## Persistencia

Schema **v44**. Tablas `procurement_*`. Agregados PR/RFQ en `json_data`; tablas satélite para índices y orígenes/documentos/auditoría.
