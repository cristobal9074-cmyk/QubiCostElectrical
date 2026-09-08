# Auditoría comercial · 2026-08-26

Canvas vivo: abrir junto al chat en Cursor (`auditoria-comercial-2026-08-26.canvas.tsx`).

## Nota

| Eje | /10 |
|-----|-----|
| Confianza datos | 7.5 |
| Arquitectura | 7.1 |
| Seguridad SMB | 7.0 |
| UX premium | 6.5 |
| Deuda | 6.0 |
| Comercial | 5.5 |
| Release / listo cobrar | **4.0** |
| **Compuesta (ponderada venta)** | **~6.0** |

**One-liner:** motor serio; empaque incompleto. Sin smoke humano firmado ni instalador Authenticode, no vale plata premium frente a Excel.

## P0 que bloquean cobrar

1. Firmar Semana A (`docs/SMOKE_TEST.md`)
2. Restaurar `deploy/` (Sign-Authenticode + build) — carpeta vacía hoy
3. Instalador Inno firmado + máquina limpia SmartScreen
4. SKU/precio CLP + cablear o declarar licencia (LicenseService no gated)
5. `THIRD_PARTY_NOTICES.txt` + tag limpio `Validate-Release`

## Top ROI (5 días de dinero)

1. Smoke humano firmado (S)
2. Firma + instalador (M)
3. Precio/SKU (S, decisión)
4. Kit demo seed (S)
5. Ficha SMB 1-writer honesta (S)

## No hacer

SSO, BC3, obra/estados de pago, co-edición Google Docs, más polish UI sin smoke, vender Contratos como core ICP.

## Fortalezas (no reabrir)

Lock/lease/sidecar, dirty tipado, flush X, freeze export, Olas 1–4, contrato UI + tokens Costo/APU/EPP/MO, Excel-nav.

## Deuda flash

- ~475 `FontSize` literales (hotspots: CubSC, Project, Preferences, Inicio)
- God-files: `CubicacionView.xaml.Methods` ~1691
- `ArchitectureTests` **fuera** del `.sln`
- Huérfanos: ScenarioWorkspace (WIP keep); About/ResumenProyecto purgados
