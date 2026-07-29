# BC3 — Alcance de producto (1.0.0)

## Decisión

**Importación BC3 no está incluida en Qubi Cost Electrical 1.0.0.**

## Motivo

- El flujo core del producto es presupuesto eléctrico en `.qce` con itemizado, cubicación, APU y export PDF/Excel.
- BC3 implica parser normativo, mapeo de capítulos y reglas de interoperación con ingeniería civil/arquitectura — alcance de producto distinto.
- El mercado objetivo inicial (instaladoras eléctricas 3–8 estimadores) trabaja con Excel, PDF de ingeniería y itemizado propio.

## Alternativas soportadas hoy

- Importar itemizado desde **Excel** (plantilla o pegado).
- Export **PDF / Excel / DOCX** para entrega al mandante.
- Formato nativo **`.qce`** (SQLite) con migración desde `.pepro` legacy.

## Roadmap (no comprometido en 1.0)

- Import BC3 MVP: lectura de partidas y cantidades → itemizado (sin mediciones BIM).
- Posible integración post-GA si hay cliente ancla que financie el desarrollo.

## Comunicación comercial

No prometer compatibilidad BC3 en fichas de producto ni demos hasta que exista un release que lo declare explícitamente en `CHANGELOG.md`.
