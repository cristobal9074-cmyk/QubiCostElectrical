# Informe Fase 9B — Comparación de cotizaciones y adjudicación

## Resumen

Sobre 9A: ofertas detalladas, evaluación técnica/comercial, normalización de monedas, costo total de adquisición (TAC), comparativo matricial, recomendación determinista (confirmación obligatoria), adjudicación total/parcial/multi-proveedor con SoD y trazabilidad.

## Schema

Migraciones **45–47** → `DatabaseSchemaVersion = 47`.

Tablas: `procurement_offers`, `procurement_offer_items`, `procurement_offer_conditions`, `procurement_tech_evaluations`, `procurement_commercial_evaluations`, `procurement_bid_comparisons`, `procurement_awards`, `procurement_award_items`.

## WPF

Menú Compras: Ofertas, Comparativo, Evaluación técnica, Adjudicación. Comparativo matricial con marcador textual «RECOMENDADA» (color no es el único indicador).

## Pruebas

- Cobertura 9B: ≥100 casos (`BidComparisonAndAwardTests`, `AwardPersistenceAndMigrationTests`, `Phase9BCoverageTests`)
- Suite completa validada: **2935** tests, 0 fallas
- Build Debug + Release: 0 errores / 0 advertencias

## Fuera de alcance

Órdenes de compra (9C+). Contratos no forman parte de esta fase.
