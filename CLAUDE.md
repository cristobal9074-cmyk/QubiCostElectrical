# gstack

Use the `/browse` skill from gstack for all web browsing. Never use `mcp__claude-in-chrome__*` tools directly.

---

# Qubi Cost Electrical

Aplicación de escritorio WPF (.NET 10) para presupuestos eléctricos profesionales de edificios.

**Fuente de verdad del producto:** `docs/PRODUCT.md` y `Directory.Build.props`.

## Estructura del proyecto

- `src/QubiCostElectrical.Domain` — modelos, motores de cálculo y reglas puras
- `src/QubiCostElectrical.Application` — servicios, presenters, validación y casos de uso
- `src/QubiCostElectrical.Infrastructure` — SQLite, Excel, PDF, Word
- `src/QubiCostElectrical.Wpf` — UI WPF, navegación y design system
- `tests/QubiCostElectrical.Tests` — pruebas unitarias
- `docs/PRODUCT.md` — identidad, versiones y arquitectura operativa
- `docs/PRE_WEEK5_GATE.md` — checklist Fase 1 antes de semana 5

## Comandos

```powershell
dotnet build QubiCostElectrical.Wpf.sln
dotnet test QubiCostElectrical.Wpf.sln --no-build
dotnet run --project src/QubiCostElectrical.Wpf/QubiCostElectrical.Wpf.csproj
.\scripts\Update-DesktopShortcut.ps1 -Build
```

## Versión vigente

| Componente | Versión |
|---|---|
| Producto | 1.0.0 |
| Base de datos SQLite | 25 |
| Formato de proyecto | 6 |
| Motor de cálculo | 2 |

## Reglas de arquitectura

- La lógica de negocio va en Domain o Application, nunca en WPF
- Las pantallas van en `Wpf/Features/{Modulo}/`
- Presenters en Application (`*Presenter`) orquestan ViewModels delgados
- `MainForm`/shell no agrega lógica nueva
- Identidad unificada: `ProductIdentity`, `ProductPaths`, extensión `.qce`

## Reglas UI

- Design system en `Wpf/DesignSystem/`; diálogos en XAML con estilos `Dialog.*`
- Jerarquía: título → acciones → filtros → tabla → resumen → estado
- Azul corporativo para acción principal; verde solo para correcto; rojo/ámbar para alertas
- Navegación por teclado prioritaria en grillas
- Botones con verbos claros: Agregar, Duplicar, Quitar, Validar, Exportar

## Reglas generales

- Siempre responder en español
- No agregar features fuera del alcance pedido
- Preferir editar archivos existentes antes de crear nuevos
- Tras builds con cambios: actualizar acceso directo con `scripts/Update-DesktopShortcut.ps1 -Build`

## Skill routing

When the user's request matches an available skill, invoke it via the Skill tool. When in doubt, invoke the skill.

Key routing rules:
- Product ideas/brainstorming → invoke /office-hours
- Strategy/scope → invoke /plan-ceo-review
- Architecture → invoke /plan-eng-review
- Design system/plan review → invoke /design-consultation or /plan-design-review
- Full review pipeline → invoke /autoplan
- Bugs/errors → invoke /investigate
- QA/testing site behavior → invoke /qa or /qa-only
- Code review/diff check → invoke /review
- Visual polish → invoke /design-review
- Ship/deploy/PR → invoke /ship or /land-and-deploy
- Save progress → invoke /context-save
- Resume context → invoke /context-restore
- Author a backlog-ready spec/issue → invoke /spec
