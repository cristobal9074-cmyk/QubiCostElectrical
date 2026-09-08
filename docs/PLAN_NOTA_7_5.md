# Plan meta nota ≥ 7.5 — app perfecta (sin instalador)

**Fecha:** 2026-08-26  
**Baseline:** ~6.2 producto (confianza uso 5.8 · UX 6.6 · eng 6.2 · deuda 6.0)  
**Meta:** ≥ **7.5** en ejes de producto (no release/cobro)  
**Fuera de alcance:** Authenticode, Inno, SKU, LicenseService

## Cómo se sube la nota

| Eje | Ahora | Meta | Palanca |
|-----|-------|------|---------|
| Confianza uso | 5.8 | ≥ 7.5 | Visor RO real + tests RO setters + chrome alerts estable |
| UX | 6.6 | ≥ 7.5 | SC sin emoji + tokens hotpath; Preferences chrome/tokens |
| Ingeniería | 6.2 | ≥ 7.0 | MaxLineCount stem cerrado; Scenario fuera del gate fantasma |
| Deuda percibida | 6.0 | ≥ 7.0 | Huérfanos documentados/purge; Prefs alineadas |

Semana A humano sigue siendo el techo hacia 8+; este plan llega a **7.5 sin mentir PASS humano**.

## Oleadas (orden ROI)

### O1 — Confianza (bloquea 7.5) — obligatorio
1. Cabecera Cubicación RO: `UnidadHeaderBox` / `CantHeaderBox` / `ObservacionHeaderBox` → `IsEnabled`/`IsReadOnly` ligado a `IsReadOnly`
2. Setters `PartidaUnidadText` / `PartidaCantidadText` / `PartidaObservacionesText` → early-return si `session.IsReadOnly`
3. Tests: Visor no muta cabecera; ampliar `CommercialReadinessSmokeTests`
4. `ShellChromeAlertsTests`: `ClearAlertCache()` en cada Fact + assert estable

### O2 — UX demo (sube percepción)
5. Quitar emoji SC (💡⭐⚡) y ❌ vergonzosos en Solicitudes si están en hotpath demo
6. CubSC: título/`Text.PageTitle` + KPIs `Text.KpiInline` / `Font.Size.*` (hotpath, no migración masiva 109)
7. Preferences: `WorkspacePageChrome` o Empty+Skeleton; tokens tipográficos rail/labels; StatusMessage → `Text.Helper` / ModuleFeedback

### O3 — Ingeniería limpia
8. MaxLineCount: stem que no escape por `.Autocomplete`/`.Clipboard`/etc.
9. ScenarioWorkspace: sacar de `RequiredViews` del gate **o** marcar WIP explícito (no chrome fantasma)
10. Ratchet exemptions MaxLine a medidos actuales (sin headroom)

### Congelación
11. Build Release + ArchitectureTests + filtros Visor/Chrome/CommercialReadiness verdes
12. Actualizar `docs/AUDIT_APP_PERFECTA_2026-08-26.md` con nota post-fix estimada

## Done when (checklist 7.5)

- [x] Visor no edita cabecera Cub (UI + setter + test)
- [x] ShellChromeAlerts 4/4 estables (cache aislada)
- [x] 0 emoji en CubSC grilla/diálogos hotpath
- [x] Preferences con empty/skeleton o chrome + tokens en cabecera/rail
- [x] MaxLineCount no escapable por sufijos conocidos
- [x] Scenario no cuenta como “core” falso en el gate
- [x] Suite filtrada verde; build 0 errores (ArchitectureTests 2/2; Shell ratchet 1669)

## No hacer en este sprint

- Migración FontSize masiva Project/Inicio (post 7.5)
- Refactor `CubicacionView.xaml.Methods` completo
- Cablear Scenario al menú
- Instalador / firma / precio
