# Especificación — Gestión de proveedores

## Entidad `Supplier`

Identificador interno `SupplierId` (no el código visible). Estados: Prospecto, Activo, Suspendido, Bloqueado, Inactivo, Archivado. Sin borrado físico con historial.

## Contactos

Múltiples por proveedor. Un solo contacto principal activo. Validación de correo y duplicados. Contacto inactivo no usable en RFQ.

## Categorías

Catálogo tipado `SupplyCategoryCatalog` (materiales, equipos, herramientas, subcontratos, transporte, servicios, arriendo, ingeniería, otros) + personalizadas (`Custom`).

## Servicios

`ISupplierService`: Create, Update, Disable, Search, UpsertContact, DeactivateContact. Permisos `Procurement.Supplier*`. Auditoría en `procurement_audit`.
