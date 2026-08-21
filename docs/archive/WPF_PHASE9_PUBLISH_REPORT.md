# Fase 9 — Informe de publicación

## Modos de publicación

| Modo | Comando | Tamaño aprox. | Requisitos runtime |
|---|---|---|---|
| Framework-dependent (fd) | `dotnet publish -c Release` | ~500 KB | .NET 8.0 Runtime |
| Self-contained (sc) | `dotnet publish -c Release --self-contained true -p:PublishSingleFile=true` | ~80 MB | Ninguno |

## Scripts

| Script | Propósito |
|---|---|
| `scripts/Publish-Wpf.ps1` | Publica QubiCostElectrical.Wpf en modo fd o sc |
| `scripts/Publish-Release.psis` | Publica WinForms + ZIP + checksum (existente) |
| `scripts/Validate-Release.ps1` | Validación pre-release (build + test) |

## Output

| Modo | Directorio |
|---|---|
| Framework-dependent | `artifacts/publish/wpf-frameworkdependent/` |
| Self-contained | `artifacts/publish/wpf-selfcontained/` |

## .gitignore

`artifacts/` y `**/publish/` ya están incluidos en `.gitignore`.

## Identity metadata

```
Company:    QubiCost Electrical
Product:    Presupuesto Eléctrico PRO
Copyright:  © QubiCost Electrical 2024-2026
Description: Software profesional de presupuestos eléctricos con cubicación, APU,
             cotizaciones y ofertas comerciales.
```

## Requisitos

- Windows 10+ (x64)
- .NET 8.0 Runtime (solo modo fd)
- No requiere Microsoft Office, WebView2 ni Inno Setup
