# Especificación de Persistencia Contractual

## Esquema de Base de Datos SQLite (v48)
Se han añadido las siguientes tablas en SQLite:
- `contracts`: Datos del contrato, incluyendo visible_number, status, montos y metadatos JSON.
- `contract_versions`: Historial de versiones y hashes SHA-256.
- `contract_parties`: Stakeholders y roles asignados a cada contrato.
- `contract_scope_links`: Relación del contrato con el WBS, actividades CPM y recursos del presupuesto.
- `contract_milestones`: Hitos contractuales, plazos y requerimientos de evidencia.
- `contract_documents`: Bases, pólizas de garantía y otros anexos relacionados.
- `contract_audit`: Bitácora inmutable de auditoría para trazabilidad de acciones.
