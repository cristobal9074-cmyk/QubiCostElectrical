# Auditoría app perfecta · 2026-08-26 (v2)

**Alcance:** calidad de uso. **Fuera:** instalador, Authenticode, SKU, monetización.

Canvas: `auditoria-app-perfecta-2026-08-26-v2.canvas.tsx`  
Plan ROI: 6 días en este doc (sección abajo). Plan previo 7.5: `docs/PLAN_NOTA_7_5.md`.

## Notas (honestas post-revisión)

| Eje | /10 |
|-----|-----|
| Confianza uso | **7.7** (golden Viáticos→GO→Excel) |
| UX | **7.2** (copy SMB 1-writer honesto) |
| Ingeniería | **7.3** |
| Deuda percibida | **7.1** (purge ResumenProyecto) |
| **Producto (compuesta)** | **~7.5** |
| Meta tras Semana A | **≥8.0** |

**One-liner:** cadena Costo/Viáticos congelada + copy SMB honesto; falta Semana A humana para cruzar 8.

## Cerrado (no reabrir)

- Visor RO cabecera Cub (Unidad/Cant/Obs + test)
- ShellChromeAlerts 4/4 (cache)
- Prefs empty+skeleton; SC sin emoji; ArchitectureTests en `.sln`
- Costo/APU/EPP/MO FontSize = 0; dual-path = 0

## P0 ahora

1. [ ] **Semana A humana** — `Record-SmokeSemanaA.ps1` (único gap humano)
2. [x] **SC Visor CellTemplate** — Ubicacion + BeginningEdit + renombrar
3. [x] **Circuito/Cuadro/Artefacto CanEdit + MarkDirty** (`ProjectDirtyScope.CubicacionSc`)
4. [x] **Visor demo-proof 6 módulos** — AllowEdit Itemizado/Cub/Costo + markups APU + Oferta panel; `VisorSixModulesReadOnlyTests`

## P1

- Tokens hotpath Project/Inicio (no masivo); títulos spine → `Text.PageTitle`
- [x] Golden Costo/Viáticos — `CostoViaticosGoldenTests` (11 653 000 CLP → Excel)
- [x] Purge ResumenProyecto + copy SMB 1-writer (Login/Prefs/diálogos/docs)

## Plan 6 días → ≥8.0

| Día | Foco |
|-----|------|
| 1 | Firmar Semana A (o triage FAIL) |
| 2 | ~~Visor SC + 6 módulos demo-proof~~ ✅ |
| 3 | ~~Golden Costo/Viáticos~~ ✅ |
| 4 | Tokens hotpath Project/Inicio |
| 5 | ~~Purge ResumenProyecto + copy SMB~~ ✅ |
| 6 | Re-smoke + freeze nota |

**Regla:** sin Semana A PASS, no polish de tokens.

## No hacer

Authenticode, Inno, SKU, FontSize masivo Project, cablear Scenario, refactor Cub Methods sin FAIL smoke.
