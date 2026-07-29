# WPF Phase 2 Report

**Ruta:** `C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1`  
**Rama:** `master`  
**Fecha:** 2026-06-21  
**Estado:** Implementación lista para revisión (sin commit)

## ui-ux-pro-max

Consultas aplicadas en Fase 1/2 para jerarquía dashboard y formularios:

- `--design-system` → layout KPI + workflow + alertas (paleta navy/blue del prototipo)
- `--domain ux` → formularios con pestañas, estados vacíos y confirmación de cambios

Tokens reutilizados en `DesignSystem/` (`Card.Kpi`, `Card.Section`, `Button.Primary`, `DataGrid.Workspace`).

## Pantallas terminadas

| Pantalla | Estado |
|----------|--------|
| Dashboard | KPIs, workflow, alertas navegables, costos, top partidas, estado vacío |
| Datos del proyecto | 4 pestañas, validación, aplicar/cancelar, cambios pendientes |
| Antecedentes y planos | Grilla, búsqueda, filtros, fecha de corte, CRUD soportado |

## Botones conectados

- Header: Abrir proyecto, Guardar, Guardar como, Revisar, alertas → ruta Review
- Dashboard: Abrir, Datos del proyecto, Continuar cubicación, Ir (alertas), Revisión final
- Proyecto: Aplicar cambios, Cancelar
- Antecedentes: Agregar/Duplicar/Quitar/Vigente/Adjuntar/Sincronizar/Incorporar exclusiones/Guardar

## Build y pruebas (final)

| Métrica | Debug | Release |
|---------|-------|---------|
| Errores | 0 | 0 |
| Warnings | 76 | 76 |
| Pruebas totales | 759 | — |
| Pruebas OK | 756 | — |
| Pruebas fallidas (preexistentes WinForms) | 3 | — |

Fallos preexistentes: `WorkspaceNavigationRegistrationTests`, `NavigationPageCatalogTests` (OrdenesCambio), `WinFormsThemeCompletenessTests`.

## Capturas

Pendientes de verificación manual en estación del usuario (DPI 100/125/150 %). Ejecutar:

```powershell
dotnet run --project src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj
```

## Funciones pendientes

- Itemizado, cubicación, APU, precios, exportación completos en WPF
- Importación masiva de planos desde carpeta
- Login / multiusuario
- Capturas automatizadas CI para WPF
