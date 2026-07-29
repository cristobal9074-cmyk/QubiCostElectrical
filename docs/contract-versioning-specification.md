# Especificación de Versionamiento Contractual

## Lógica de Versionamiento
- Toda versión aprobada del contrato genera un snapshot inmutable serializado como JSON.
- Se computa un hash SHA-256 determinista a partir de los datos estructurados del snapshot.
- Se previene la manipulación manual de base de datos comparando el hash calculado dinámicamente con el hash almacenado en la base de datos (`VerifyIntegrity`).
- Las modificaciones posteriores deben generar nuevas revisiones incrementales, anexos u órdenes de cambio.
