# Especificación de Gestión de Contratos

## Propósito
Este documento describe el motor y la lógica de negocio para la base contractual en **Qubi Cost Electrical**.

## Flujo de Estados
Un contrato transita por los siguientes estados:
- **Borrador**: Estado inicial. Permite edición completa de importes, fechas y alcances.
- **Pendiente de revisión**: El contrato ha sido enviado a revisión. Ediciones bloqueadas.
- **Vigente / Aprobado**: El contrato ha sido aprobado por un usuario autorizado. Se bloquea la edición directa; cualquier modificación requiere un anexo, versión o cambio.
- **Suspendido**: Ejecución temporalmente pausada.
- **En cierre / Cerrado / Terminado / Rescindido**: Estados de ciclo de vida final.

## Validación Contractual
Se validan reglas como:
1. Monto y tasas no negativos.
2. Fecha de inicio anterior a la de término.
3. Porcentajes de anticipo y retención dentro de rangos válidos (0-100%).
4. Presencia obligatoria de identificador visible y título.
