# Especificación — Ofertas, comparación y adjudicación (9B)

## Oferta (`SupplierOffer`)

Versión, moneda, impuestos, descuentos, flete, seguro, plazo, pago, garantía, líneas con precio original/base, alternativas y condiciones comerciales.

## Comparación

`BidComparisonEngine` construye matriz por ítem RFQ: PU, totales, TAC, plazos, cumplimiento técnico, riesgo, desviación presupuestaria y score. No adjudica sola.

## Normalización

`OfferCurrencyNormalizer` + `PrecisionPolicy`. Conserva original y base con tasa y fecha explícitas.

## Adjudicación

`AwardService` crea desde recomendación (requiere confirmación), parcial, envía, aprueba/rechaza con SoD (`AwardCreatorCannotApprove`).
