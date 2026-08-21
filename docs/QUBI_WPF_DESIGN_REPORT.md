# QUBI WPF Design Report — Fase 1

## Fuente visual

`prototipo-presupuesto-electrico-pro.html`

## Skill ui-ux-pro-max

- **Ubicación:** `C:\Users\cristobal1\.cursor\skills\ui-ux-pro-max\`
- **Consulta ejecutada:** `--design-system -p "QubiCost Electrical WPF" -f markdown`
- **Resultado skill:** estilo Data-Dense / Financial Dashboard; sugiere Fira Sans/Code y paleta azul genérica.
- **Decisión:** **prototipo HTML gana** en colores (navy `#0E1B2C`, blue `#2563EB`, canvas `#F3F5F8`) y tipografía (Segoe UI / Cascadia Mono como fallback local).
- **Aplicado desde skill:** densidad de datos, focus en tablas, KPI cards, drill-down (cubicación 3 zonas), sin emojis como iconos.

## Tokens → XAML

| Token HTML | Recurso WPF |
|------------|-------------|
| `--navy` | `Brush.Navy` |
| `--blue` / `--blue-2` | `Brush.Blue` / `Brush.Blue2` |
| `--canvas` | `Brush.Canvas` |
| `--app-header` 108px | TopBar 64 + NavBar 44 |
| `--contextbar` 56px | `Layout.ContextBarHeight` |
| `.primary-btn` | `Button.Primary` |
| `.data-table` | `DataGrid.Prototype` |
| `.cubic-layout` | `TakeoffPlaceholderView` grid 220/* /260 |

## Shell

1. **Barra 1:** logo, nombre, proyecto, búsqueda, guardado, alertas, guardar versión, revisar, usuario.
2. **Barra 2:** Resumen, Proyecto▾, Presupuesto▾, Precios y compras▾, Cierre▾, Entrega, hallazgos.
3. **Barra 3:** breadcrumb, título, estado, versión, acciones.

Sin sidebar.

## Fase 3 — Itemizado

| Elemento prototipo | Implementación WPF |
|--------------------|-------------------|
| Toolbar importar/pegar/agregar | `ItemizadoView` header + barra |
| Filtros chips | Todos / Incompletos / Completos / Sin cubicar / Sin precio |
| `data-table` con badges y progreso | `DataGrid.Workspace` + templates |
| Metric strip inferior | 4 KPIs + strip UF cubicadas |
| Sin sidebar | Mantenido |

Skill `ui-ux-pro-max` (Fase 3): confirmación destructiva, data-dense grid, focus visible, empty state con CTA.

## Diferencias conocidas vs prototipo

- Fuentes Inter/JetBrains Mono no empaquetadas (Segoe UI / Cascadia Mono).
- Iconos SVG simplificados a texto/glyphs.
- Login no implementado (arranque directo en shell).

## Fase 2 — Pantallas funcionales

| Vista | Componentes visuales |
|-------|---------------------|
| Dashboard | `Card.Kpi` ×4, stepper workflow 6 pasos, panel costos, alertas con CTA, `DataGrid.Workspace` top partidas |
| Proyecto | `TabControl` 4 pestañas, `Form.TextBox`, acciones Aplicar/Cancelar, estado vacío |
| Antecedentes | Filtros + grilla editable, fecha de corte, toolbar de acciones con tooltips |

Estado vacío profesional cuando no hay `.pepro` abierto (sin datos mock).
- Animaciones y gráficos de barras no replicados en placeholders.
- Badges numéricos en menú omitidos en Fase 1.

## Pendiente Fase 2+

Conectar servicios, datos reales, Command Palette Ctrl+K, gráficos, workflow stepper completo.
