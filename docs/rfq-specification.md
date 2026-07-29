# Especificación — RFQ (Fase 9A)

## Agregado `RequestForQuotation`

Creación desde requerimientos **aprobados** (o ya en cotización). Ítems copiados (snapshot); el PR no se reescribe al crear la RFQ salvo transición a `InQuotation`.

## Invitaciones

Estados: Pendiente, Enviada, Confirmada, Rechazada, Sin respuesta, Respondida. Proveedor debe ser elegible (Activo/Prospecto). Contacto inactivo inválido. Sin invitaciones duplicadas.

## Emisión y respuesta

Emitir exige ≥1 proveedor. Respuesta inicial: moneda, vigencia, plazo, documento, hash de integridad. Sin comparador comercial ni adjudicación (9B+).
