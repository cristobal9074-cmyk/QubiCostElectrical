# Walkthrough — Fase 9 Compras y abastecimiento (9A–9E)

## Matriz post-implementación

| Capacidad | Domain | Application | Infra | WPF | Schema | Estado |
| --------- | ------ | ----------- | ---- | --- | ------ | ------ |
| Proveedores / RFQ (9A) | Sí | Sí | Sí | Sí | 41–44 | Completada |
| Ofertas / comparación / adjudicación (9B) | Sí | Sí | Sí | Sí | 45–47 | Completada |
| Órdenes de compra (9C) | `PurchaseOrder` | `PurchaseOrderService` | Sqlite PO | PurchaseOrderWorkspace | 48 | Completada |
| Recepciones / inventario (9D) | `GoodsReceipt`, `InventoryEngine` | `GoodsReceiptService` | Sqlite GR/Inv | GoodsReceiptWorkspace | 49–50 | Completada |
| Compromisos / dashboard (9E) | `ProcurementCommitmentEngine` | `ProcurementCommitmentService` | Snapshots | ProcurementDashboard | 51 | Completada |

## Validación

Tests **3017**, schema **51**, sin Fase 10.
