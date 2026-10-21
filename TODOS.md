# TODOS — Qubi Cost Electrical

Versión vigente y detalle operativo: `AGENTS.md` y `docs/PRODUCT.md`.
Histórico de releases y decisiones cerradas: `CHANGELOG.md`.

## Pendientes de smoke humano (único gate real de persistencia)

- [ ] Semana A firmada (`scripts/Record-SmokeSemanaA.ps1`) — P0 dinero/amistad
- [ ] Smoke humano Cubicación: persistencia, barra de fórmulas, pegar desde Excel, undo, rol Visor en solo lectura — checklist en `docs/SMOKE_TEST.md`
- [ ] Smoke humano Cubicación SC + Tomar SC — `docs/SMOKE_TEST.md`
- [ ] Smoke humano GUI / roles (Admin + Estimador + Visor, 20 min) — `docs/FIRST_RUN_TEAM_ONBOARDING.md`
- [ ] Smoke: logout→login, DPI 125/150 %, abrir `.qce` de esquema viejo
- [ ] Smoke post-polish: Dashboard → Cubicar → Tomar SC → Export

## Producto

- [ ] Atajos configurables más allá de Cubicación (SC / APU / shell) vía Preferencias
- [ ] SC modo entrada/avanzado (columnas)
- [ ] Migración masiva de `FontSize` literales restantes (Costo/SC/MO) a tokens
- [ ] Tokens hotpath Project/Inicio
- [ ] Tag release + instalador firmado + validación de instalación; monetización/licencia (fuera de ciclo)

## Vigilar

- [ ] `ApuWorkspaceVm.ApplyFilter` reconstruye toda la colección por tecla (`Clear()` + `Add()`).
      Volumen bajo hoy (cientos de partidas, no miles); reoptimizar solo si crece.

## Lecciones registradas (zona Cubicación)

1. La premisa "3 secuencias post-edición idénticas para consolidar" en
   `RowMutations/Clipboard/Sectors` resultó falsa al grep directo (7+ secuencias con orden
   distinto). No proponer consolidar sin releer el código real primero.
2. Antes de nombrar una clase nueva, grepear el nombre en todo `src/`:
   `CubicacionClipboardService` (Application, estático) ya existía; por eso el coordinador
   se llama `CubicacionClipboardCoordinator`.

## Histórico cerrado (2026-10-20)

Sesiones 1–3, olas 1–4 de endurecimiento, contrato UI/UX, deuda de arquitectura
CubicacionViewModel, 6 bugs preexistentes (3 críticos) y las limpiezas de código muerto
(2026-08 y 2026-10) — verificadas con build + suite de tests. Detalle: `CHANGELOG.md`.
