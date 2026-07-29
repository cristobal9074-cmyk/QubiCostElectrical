# Fase 2 — Semanas 5–8 (Percepción) — Informe de cierre

> Producto: **Qubi Cost Electrical 1.0.0** · WPF-only · Junio 2026

## Objetivo

Convertir el producto en una **línea de producción guiada** con UI coherente y comparación de versiones vendible, sin tocar motor de cálculo ni esquema SQLite.

## Entregables por semana

| Semana | Foco | Estado |
|--------|------|--------|
| S5 | Design system en flujo core | ✅ |
| S6 | Stepper shell + C.SC visible | ✅ |
| S7 | Versiones A vs B + tests VM | ✅ |
| S8 | Documentación + puerta automatizada | ✅ |

## S5 — Design system

Tokens aplicados en vistas del flujo principal:

- **Form.ComboBox:** Documentos, Monedas, Lista de compra, Cotizaciones, Banco de precios, Cubicación (redondeo), Revisión, Export, Versiones
- **Form.TabControl:** Proyecto, Plantillas, Banco de precios (panel detalle), Cubicación (panel contextual)
- **Form.ProgressBar:** Itemizado (columna avance cubicación)
- **DataGrid.Workspace:** APU, C.SC, Órdenes de cambio (+ resto del flujo ya migrado)

## S6 — Shell y navegación

- Barra **Línea de producción** fija bajo context bar (solo con proyecto abierto)
- Pasos: Datos → Itemizado → Cubicación → **C.SC** → Precios → Revisión → Exportar
- Estado completado / activo / pendiente con navegación directa
- Lógica en `ShellWorkflowLogic` + tests `ShellWorkflowLogicTests`

## S7 — Comparación versiones A vs B

- Modo **versión vs estado actual** (checkbox)
- Modo **versión A vs versión B** (selector Versión B)
- Hint contextual `ComparisonModeHint` en UI
- Tests: `VersionsViewModelTests` (modo A vs B, opciones, delta partidas)

## S8 — Documentación y puerta

- Este informe
- `WpfPhase2GateTests` — verificación automatizada de archivos clave
- `TODOS.md`, `CHANGELOG.md`, `WPF_COMPATIBILITY_REPORT.md`, `SMOKE_TEST.md` actualizados

## Verificación automatizada

```powershell
dotnet build QubiCostElectrical.Wpf.sln -c Release
dotnet test QubiCostElectrical.Wpf.sln -c Release --filter "FullyQualifiedName~WpfPhase2GateTests|ShellWorkflowLogicTests|VersionsViewModelTests"
.\scripts\Verify-ProductIdentity.ps1 -Strict
```

## Smoke humano recomendado (30 min)

1. Abrir proyecto → verificar stepper y navegación por pasos
2. C.SC accesible desde stepper y menú Presupuesto
3. Versiones: crear V1 y V2, comparar A vs B, ir a partida desde delta
4. Revisar ComboBox/tabs en Proyecto, Precios, Cubicación

Checklist completo: `docs/SMOKE_TEST.md`.

## Pendiente post-Fase 2 (Fase 3 / GA)

- Golden-file Excel adicional (si piloto lo pide)
- Smoke manual flujo SC completo
- Tag `v1.0.0`, instalador firmado, smoke GA completo
