# Qubi Cost Electrical — Fuente de verdad del producto

> Documento operativo sincronizado con `Directory.Build.props`. Si hay conflicto, gana este archivo y `Directory.Build.props`.

## Identidad

| Campo | Valor |
|-------|--------|
| Nombre comercial | Qubi Cost Electrical |
| Empresa | Qubi Cost |
| Ejecutable | `QubiCostElectrical.exe` |
| Extensión de proyecto | `.qce` (canónica) |
| Extensión legacy | `.pepro` (solo apertura/importación) |
| Carpeta de datos | `%LOCALAPPDATA%\QubiCostElectrical` |
| Carpeta legacy migrada | `%LOCALAPPDATA%\QubiCostElectricalPRO` |

## Versiones vigentes

Leer siempre desde `Directory.Build.props`:

| Componente | Propiedad |
|------------|-----------|
| Producto | `InformationalVersion` |
| SQLite schema | `DatabaseSchemaVersion` |
| Formato proyecto | `ProjectFormatVersion` |
| Motor de cálculo | `CalculationEngineVersion` |

## Stack técnico

- .NET 10 (`net10.0-windows`)
- UI: WPF únicamente (WinForms retirado)
- Persistencia: SQLite con migraciones versionadas
- Solución: `QubiCostElectrical.Wpf.sln`

## Arquitectura

```
QubiCostElectrical.Domain        → modelos y motores puros
QubiCostElectrical.Application   → casos de uso, presenters, sesión
QubiCostElectrical.Infrastructure → SQLite, Excel, PDF, Word, auth
QubiCostElectrical.Composition   → wiring DI
QubiCostElectrical.Wpf           → shell, features, design system
```

Reglas:
- Lógica de negocio en Domain/Application, nunca en WPF
- Pantallas en `Wpf/Features/{Modulo}/`
- Presenters (`ItemizadoPresenter`, `CubicacionPresenter`, `ExportPresenter`) orquestan la UI
- Design system en `Wpf/DesignSystem/`; diálogos con `Dialogs.xaml` + XAML

## Flujo presupuestario

Proyecto → Itemizado → Cubicación → APU → Precios → Revisión → Oferta/Export

## Comandos

```powershell
dotnet build QubiCostElectrical.Wpf.sln
dotnet test QubiCostElectrical.Wpf.sln --no-build
dotnet run --project src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj
.\scripts\Update-DesktopShortcut.ps1 -Build
```

## Documentación histórica

Informes de fases WPF, paridad WinForms y auditorías antiguas están en `docs/archive/`. No usar como fuente operativa.

## Operación de identidad y seguridad

- Política PIN y criterios de seguridad: `docs/SECURITY_PIN_POLICY.md`
- Onboarding primer arranque de equipos 3–8 usuarios: `docs/FIRST_RUN_TEAM_ONBOARDING.md`
- Evaluación comercial del modelo de identidad: `docs/AUTH_MODEL_REVIEW.md`
