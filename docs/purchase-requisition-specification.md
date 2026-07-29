# Especificación — Requerimientos de compra

## Agregado `PurchaseRequisition`

Número visible (`PR-#####`) distinto de `PurchaseRequisitionId`. Estados: Borrador → Pendiente revisión → Aprobado/Rechazado → En cotización → … → Cancelado/Archivado.

## Ítems y orígenes

Cada ítem conserva `NeedOrigin` (manual, presupuesto, APU, actividad, cronograma, stock, cambio, otro) con id, versión, cantidad original, usuario y fecha. La consolidación no pierde trazabilidad de orígenes en el JSON del ítem.

## Generación

`PreviewFromBudget` / `PreviewFromSchedule` + `ConfirmGenerationAsync` (requiere confirmación explícita). No muta presupuesto ni CPM.

## Aprobación

SoD: con `ProcurementSoDPolicy.CreatorCannotApprove` el solicitante no aprueba. Permisos `RequisitionSubmit|Approve|Reject`.
