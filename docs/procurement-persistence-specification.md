# Persistencia de compras — Fase 9A

## Versión

Migraciones **41–44** → `DatabaseSchemaVersion = 44`.

| Migración | Contenido |
|-----------|-----------|
| 41 | `procurement_suppliers`, `procurement_supplier_contacts` + índices |
| 42 | `procurement_requisitions`, `_items`, `_origins` + índices |
| 43 | `procurement_rfqs`, `_items`, `_suppliers`, `procurement_quotation_responses` |
| 44 | `procurement_documents`, `procurement_audit` |

## Modelo de almacenamiento

Documentos JSON en `json_data` con columnas de búsqueda (código, estado, proyecto, fechas, números). Reapertura vía `GetById` / `Search` deserializa el agregado completo.

## Repositorios SQLite

`SqliteSupplierRepository`, `SqliteSupplierContactRepository`, `SqlitePurchaseRequisitionRepository`, `SqliteRequestForQuotationRepository`, `SqliteQuotationResponseRepository`, `SqliteProcurementDocumentRepository`, `SqliteProcurementAuditRepository`.
