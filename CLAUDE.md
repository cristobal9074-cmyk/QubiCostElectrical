# gstack

Use the `/browse` skill from gstack for all web browsing. Never use `mcp__claude-in-chrome__*` tools directly.

---

# Qubi Cost Electrical

AplicaciÃ³n de escritorio WPF (.NET 10) para presupuestos elÃ©ctricos profesionales de edificios.

**Fuente de verdad del producto:** `docs/PRODUCT.md` y `Directory.Build.props`.

## Estructura del proyecto

- `src/QubiCostElectrical.Domain` â€” modelos, motores de cÃ¡lculo y reglas puras
- `src/QubiCostElectrical.Application` â€” servicios, presenters, validaciÃ³n y casos de uso
- `src/QubiCostElectrical.Infrastructure` â€” SQLite, Excel, PDF, Word
- `src/QubiCostElectrical.Wpf` â€” UI WPF, navegaciÃ³n y design system
- `tests/QubiCostElectrical.Tests` â€” pruebas unitarias
- `docs/PRODUCT.md` â€” identidad, versiones y arquitectura operativa
- `docs/PRE_WEEK5_GATE.md` â€” checklist Fase 1 antes de semana 5

## Comandos

```powershell
dotnet build QubiCostElectrical.Wpf.sln
dotnet test QubiCostElectrical.Wpf.sln --no-build
dotnet run --project src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj
.\scripts\Update-DesktopShortcut.ps1 -Build
```

## VersiÃ³n vigente

| Componente | VersiÃ³n |
|---|---|
| Producto | 1.1.7 |
| Base de datos SQLite | 33 |
| Formato de proyecto | 6 |
| Motor de cÃ¡lculo | 5 |

## Reglas de arquitectura

- La lÃ³gica de negocio va en Domain o Application, nunca en WPF
- Las pantallas van en `Wpf/Features/{Modulo}/`
- Presenters en Application (`*Presenter`) orquestan ViewModels delgados
- `MainForm`/shell no agrega lÃ³gica nueva
- Identidad unificada: `ProductIdentity`, `ProductPaths`, extensiÃ³n `.qce`

## Reglas UI

- Design system en `Wpf/DesignSystem/`; diÃ¡logos en XAML con estilos `Dialog.*`
- JerarquÃ­a: tÃ­tulo â†’ acciones â†’ filtros â†’ tabla â†’ resumen â†’ estado
- Azul corporativo para acciÃ³n principal; verde solo para correcto; rojo/Ã¡mbar para alertas
- NavegaciÃ³n por teclado prioritaria en grillas
- Botones con verbos claros: Agregar, Duplicar, Quitar, Validar, Exportar

## Reglas generales

- Siempre responder en espaÃ±ol
- No agregar features fuera del alcance pedido
- Preferir editar archivos existentes antes de crear nuevos
- Tras builds con cambios: compilar y publicar Ãºnicamente en `publish/win-x64` para probar con `Qubi Cost Electrical (Dev).lnk`. NUNCA tocar la instalaciÃ³n oficial de producciÃ³n en `AppData/Local/QubiCostElectrical/current`.

## Skill routing

When the user's request matches an available skill, invoke it via the Skill tool. When in doubt, invoke the skill.

Key routing rules:
- Product ideas/brainstorming â†’ invoke /office-hours
- Strategy/scope â†’ invoke /plan-ceo-review
- Architecture â†’ invoke /plan-eng-review
- Design system/plan review â†’ invoke /design-consultation or /plan-design-review
- Full review pipeline â†’ invoke /autoplan
- Bugs/errors â†’ invoke /investigate
- QA/testing site behavior â†’ invoke /qa or /qa-only
- Code review/diff check â†’ invoke /review
- Visual polish â†’ invoke /design-review
- Ship/deploy/PR â†’ invoke /ship or /land-and-deploy
- Save progress â†’ invoke /context-save
- Resume context â†’ invoke /context-restore
- Author a backlog-ready spec/issue â†’ invoke /spec
