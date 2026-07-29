# Qubi Cost Electrical

Aplicacion profesional de escritorio WPF (.NET 10) para presupuestos electricos de edificios. Reemplaza el flujo de Excel por un sistema local, trazable y rapido.

## Estructura

- `src/QubiCostElectrical.Domain`: modelos, motores de calculo y reglas puras.
- `src/QubiCostElectrical.Application`: servicios de aplicacion, validacion y view models.
- `src/QubiCostElectrical.Infrastructure`: SQLite, Excel, PDF, Word e intercambio.
- `src/QubiCostElectrical.Wpf`: shell, vistas, controles y UI WPF.
- `tests/QubiCostElectrical.Tests`: pruebas unitarias.
- `docs`: documentacion funcional y tecnica.

## Requisitos

- .NET SDK 10.
- Windows 10 o superior.

## Compilar

```powershell
dotnet build QubiCostElectrical.Wpf.sln
```

## Probar

```powershell
dotnet test QubiCostElectrical.Wpf.sln --no-build
```

## Ejecutar

```powershell
dotnet run --project src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj
```

## Version

| Componente | Version |
|---|---|
| Producto | 1.0.0 |
| Base de datos SQLite | 22 |
| Formato de proyecto | 6 |
| Motor de calculo | 2 |

La fuente de verdad de la version esta en `Directory.Build.props` (`<VersionPrefix>` y metadatos del esquema).
