# Especificación de Gestión de Reclamos (Fase 11C)

## Resumen

Módulo para la gestión de reclamos contractuales con soporte para eventos, evidencias, evaluaciones y respuestas.

## Flujo de Vida

1. **Borrador**: Creación del reclamo con datos iniciales
2. **Notificado**: Notificación formal a la contraparte
3. **En Evaluación**: Evaluación técnica/económica del reclamo
4. **Respondido**: Respuesta formal (Aceptado, Aceptado Parcialmente, Rechazado)
5. **Aprobado**: Aprobación final del reclamo
6. **Cerrado**: Cierre administrativo

## Sub-entidades

- `ClaimEvent`: Eventos asociados al reclamo (hitos, comunicaciones)
- `ClaimEvidence`: Documentos de respaldo
- `ClaimEvaluation`: Evaluación técnica con montos y días evaluados
- `ClaimResponse`: Respuesta formal con oferta

## Servicios

```csharp
Task<ClaimEvaluation?> GetClaimEvaluationAsync(ClaimId claimId, CancellationToken ct = default);
Task<ClaimResponse?> GetClaimResponseAsync(ClaimId claimId, CancellationToken ct = default);
```

## Exportación

Los reclamos se exportan a CSV incluyendo: número, tipo, descripción, estado, montos reclamados y adjudicados, días, responsable y fechas.
