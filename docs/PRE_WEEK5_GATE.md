# Puerta Fase 1 — Antes de semana 5

Checklist obligatorio antes de iniciar **Fase 2 (semanas 5–8: percepción)**.

## Automatizado (CI / agente)

```powershell
dotnet build QubiCostElectrical.Wpf.sln -c Release
dotnet test QubiCostElectrical.Wpf.sln -c Release --no-build
.\scripts\Verify-ProductIdentity.ps1 -Strict
```

Tests de puerta: `WpfPhase1GateTests`, `ProductIdentityTests`, `ExportPriceFreezePolicyTests`, `ShellChromeAlertsTests`.

## Semana 1 — Limpieza

- [x] Sin sesión hardcodeada en `WpfAppHost.Initialize()`
- [x] `DeliveryView` / `DeliveryViewModel` retirados; servicios en `Features/Delivery/` conservados para Export
- [x] Botón Alertas → Revisión con badge
- [x] Empty state Cubicación sin emoji

## Semana 2 — Congelamiento export

- [x] `ExportPriceFreezePolicy` + diálogo WPF (congelar / riesgo / cancelar)
- [x] Gate en todos los exports y `DeliverPackageAsync`
- [x] Auditoría `ExportSinCongelar` al continuar con riesgo

## Semana 3 — Tests de regresión

- [x] `ItemizadoViewModelTests`
- [x] `CubicacionViewModelTests`
- [x] `PriceBankStoreIntegrationTests`
- [x] `ExportViewModelTests` + wizard

## Semana 4 — Diálogos de marca

- [x] `AppMessageDialog` en Confirm / Error / Info / bloqueo / freeze / guardar-descartar
- [x] `GlobalErrorHandler` sin `MessageBox`
- [x] `NewProjectDialog` validación inline

## Humano (30–45 min)

Ejecutar flujo mínimo con proyecto real `.qce`:

1. Login → nuevo proyecto → guardar
2. Itemizado: 3 partidas → cubicación: 5 filas con fórmula
3. Revisión: ver alertas → clic en una lleva al módulo
4. Oferta y documentos: paso 1 validar → export PDF (con gate freeze si aplica)
5. Cerrar app → reabrir → mismos totales

Registrar en `docs/SMOKE_TEST.md` sección Aprobación.

## Criterio de salida

| Gate | Estado |
|------|--------|
| `dotnet test` verde | Requerido |
| Cero `MessageBox` en WPF | Requerido |
| Legacy Delivery retirado | Requerido |
| Smoke humano flujo core | Requerido antes de demo comercial |

Tras marcar todo: iniciar **Semana 5** (navegación = línea de producción).
