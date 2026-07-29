# Especificación de Análisis de Impacto (Fase 11C)

## Resumen

El análisis de impacto permite evaluar las consecuencias económicas y de plazo de una Orden de Cambio o Solicitud de Cambio antes de su aprobación.

## Modelo

`ImpactAnalysisResult` contiene:

- **Impacto Económico**: Cálculo detallado de costos directos, indirectos, overhead y markup.
- **Impacto en Plazo**: Días de extensión calculados vs. solicitados.
- **Líneas de Impacto**: Desglose por ítem con cantidad, precio unitario y total.
- **Resumen**: Totales consolidados.

## Métodos del Servicio

```csharp
Task<ImpactAnalysisResult> CalculateImpactAsync(ChangeOrderId orderId, CancellationToken ct = default);
Task<ImpactAnalysisResult> CalculateChangeRequestImpactAsync(ChangeRequestId requestId, CancellationToken ct = default);
```

## Uso

El análisis se integra en el flujo de aprobación y puede visualizarse desde las vistas WPF. Los resultados se exportan junto con los CSV de cambios contractuales.
