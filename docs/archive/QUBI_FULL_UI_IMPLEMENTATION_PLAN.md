# Plan de Implementación Visual QUBI — Presupuesto Eléctrico PRO

## Resumen

Este plan detalla la migración visual del shell y todas las pantallas de Presupuesto
Eléctrico PRO para que coincidan con el boceto aprobado **Qubi Cost Electrical -
Standalone.html**, manteniendo toda la lógica de negocio, cálculos y exportaciones
existentes.

---

## Design System (ui-ux-pro-max)

Se ejecutaron las siguientes consultas de `ui-ux-pro-max`:

| Consulta | Resultado |
|----------|-----------|
| `--design-system "construction industrial engineering budgeting dashboard professional winforms desktop"` | Minimalism & Swiss Style. Paleta: Primary #64748B, CTA #F97316, Bg #F8FAFC, Text #334155. Tipografía: Fira Code + Fira Sans. |
| `--domain ux "dashboard kpi cards data tables"` | Recomendaciones de overflow-x en tablas, bulk actions multi-select. |
| `--domain typography "professional sans-serif technical monospace"` | Poppins + Open Sans (modern professional), Space Mono (técnico). |
| `--domain web "economic budget proposal export pricing cost breakdown"` | Sin resultados específicos. |

Las recomendaciones de ui-ux-pro-max son genéricas. Los colores exactos se
extraen del HTML referencia Qubi (ver abajo), que tiene prioridad.

### Patrones visuales aplicados de ui-ux-pro-max

- **Minimalism & Swiss Style**: clean, spacious, high contrast, grid-based
- **Hover feedback**: transiciones suaves (150-300ms), cursor pointer
- **Tabla overflow**: scroll horizontal gestionado
- **Bulk actions**: checkbox column + action bar donde aplica

---

## Colores extraídos de Qubi Cost Electrical - Standalone.html

### Header y navegación (fondo oscuro #0C1828)

| Token | Hex | Uso |
|-------|-----|-----|
| `qubi-header-top` | `#0C1828` | Header gradient start |
| `qubi-header-bot` | `#0F2241` | Header gradient end |
| `qubi-nav-bg` | `#091424` | Nav bar background |
| `qubi-blue` | `#3B78F5` | Botón primario, Q logo, active accent |
| `qubi-blue-hover` | `#1A52D8` | Hover de botones azules |
| `qubi-nav-text` | `#8AAABE` | Texto inactivo en navbar |
| `qubi-on-primary` | `#FFFFFF` | Texto sobre fondo oscuro |
| `qubi-save-green` | `#34D399` | Indicador de guardado |
| `qubi-subtitle` | `#607A96` | Subtítulo (ELECTRICAL ESTIMATING) |
| `qubi-project-name` | `#DCECFF` | Nombre de proyecto activo |
| `qubi-search-text` | `#C8DCF0` | Texto en buscador |

### Canvas y workspace (fondo claro)

| Token | Hex | Uso |
|-------|-----|-----|
| `qubi-canvas` | `#EAECF2` | Fondo del workspace principal |
| `qubi-card` | `#FFFFFF` | Fondos de tarjetas, paneles, inputs |
| `qubi-context-bg` | `#FFFFFF` | Context bar background |
| `qubi-context-border` | `#E4E8EF` | Context bar bottom border |
| `qubi-page-title` | `#0E1B2E` | Título de página |

### Texto

| Token | Hex | Uso |
|-------|-----|-----|
| `qubi-text-dark` | `#0E1B2E` | Títulos y valores importantes |
| `qubi-text-muted` | `#94A3B8` | Texto secundario, headers de tabla |
| `qubi-text-body` | `#374B63` | Cuerpo de texto, valores de celda |
| `qubi-text-code` | `#8A9CB5` | Códigos de ítem |
| `qubi-text-formula` | `#294666` | Fórmula en cubicación |
| `qubi-text-result` | `#1A52D8` | Resultado de fórmula |
| `qubi-text-description` | `#1E3050` | Descripción de partida |

### Bordes

| Token | Hex | Uso |
|-------|-----|-----|
| `qubi-border` | `#E4E8EF` | Bordes de tarjetas, tablas, inputs |
| `qubi-border-soft` | `#EEF1F8` | Bordes suaves, shadow lines |
| `qubi-header-border` | `rgba(255,255,255,.07)` | Borde inferior del header |
| `qubi-row-border` | `#EEF1F8` | Borde entre filas de tabla |

### Estados

| Token | Hex | Uso |
|-------|-----|-----|
| `qubi-success` | `#059669` | Verde éxito, badge "Completo" |
| `qubi-success-soft` | `#ECFDF5` | Fondo de badge success |
| `qubi-warning` | `#D97706` | Ámbar, badge "En revisión" |
| `qubi-warning-soft` | `#FFFBEB` | Fondo de badge warning |
| `qubi-error` | `#DC2626` | Rojo, badge "Sin precio" |
| `qubi-error-soft` | `#FEF2F2` | Fondo de badge error |
| `qubi-info` | `#1A4ED8` | Azul info, badge "Cubicando" |
| `qubi-info-soft` | `#EBF1FF` | Fondo de badge info |

### Gradientes

| Gradiente | Uso |
|-----------|-----|
| `linear-gradient(175deg, #0C1828, #0F2241)` | Header background |
| `linear-gradient(135deg, #3B78F5, #1A52D8)` | Q logo, primary buttons |
| `linear-gradient(90deg, #1A52D8, #3B78F5)` | KPI card Oferta Total accent |
| `linear-gradient(90deg, #7C3AED, #A855F7)` | KPI card Costo Directo accent |
| `linear-gradient(90deg, #059669, #34D399)` | KPI card Margen accent |
| `linear-gradient(90deg, #D97706, #FBBF24)` | KPI card Avance accent |

### Sombras

| Sombra | Uso |
|--------|-----|
| `0 4px 24px rgba(8,16,32,.32)` | Header |
| `0 16px 48px rgba(8,16,40,.14)` | Dropdowns |
| `0 1px 4px rgba(14,27,46,.05)` | Tarjetas KPI, chart, alerts |
| `0 4px 12px rgba(37,99,235,.24)` | Primary buttons |
| `0 0 0 4px #EBF1FF` | Active workflow step glow |

---

## Layout de referencia (Qubi HTML)

```
┌──────────────────────────────────────────────────────────┐
│ HEADER (dark navy gradient, 48px)                         │
│ [Q] QUBI COST | [Proyecto activo] | [Search] | ... |[CS]│
├──────────────────────────────────────────────────────────┤
│ NAV BAR (30px, dark)                                      │
│ Resumen Proyecto▼ Presupuesto▼ Precios▼ Cierre▼ Entrega  │
├──────────────────────────────────────────────────────────┤
│ CONTEXT BAR (40px, white)                                 │
│ Inicio > Proyecto | Título página | [Badge] [V05] [Btn]  │
├──────────────────────────────────────────────────────────┤
│ MAIN WORKSPACE (scroll, canvas #EAECF2)                   │
│                                                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐    │
│  │KPI Card 1│ │KPI Card 2│ │KPI Card 3│ │KPI Card 4│    │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘    │
│                                                          │
│  Workflow Stepper (5 steps)                               │
│  ┌────────────────────────────────────────────────────┐   │
│  │ Chart + Alerts side by side                        │   │
│  └────────────────────────────────────────────────────┘   │
│                                                          │
│  Table: Partidas con mayor impacto                        │
│ ┌────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┐  │
│ │ #  │Ítem  │Desc. │Estado│Avance│Mat.  │M.Obra│Total │  │
│ ├────┼──────┼──────┼──────┼──────┼──────┼──────┼──────┤  │
│ │... │      │      │[bad]│ ████ │      │      │      │  │
│ └────┴──────┴──────┴──────┴──────┴──────┴──────┴──────┘  │
│                                                          │
├──────────────────────────────────────────────────────────┤
│ STATUS BAR (34px, soft gray)                              │
│ Listo · Guardado · Sin conexión                          │
└──────────────────────────────────────────────────────────┘
```

---

## Mapeo pestañas → pantallas reales existentes

| Pestaña Qubi | Pantalla real existente | Ruta/Clase | Estado |
|-------------|------------------------|------------|--------|
| Resumen / Dashboard | Dashboard | Features/Dashboard/DashboardWorkspacePage.cs | Existe |
| Proyecto | Proyecto | Features/Proyecto/ProyectoWorkspacePage.cs | Existe |
| Itemizado | Itemizado | Features/Itemizado/ItemizadoWorkspacePage.cs | Existe |
| Cubicación | Cubicacion | Features/Cubicacion/CubicacionWorkspacePage.cs | Existe |
| Cubicación SC | CubicacionSC | Features/CubicacionSC/CubicacionSCWorkspacePage.cs | Existe |
| Hoja de Costo (APU) | HojaCosto | Features/HojaCosto/HojaCostoWorkspacePage.cs | Existe |
| Plantillas | Plantillas | Features/Plantillas/PlantillasWorkspacePage.cs | Existe |
| Conjuntos Eléctricos | ConjuntosElectricos | Features/ConjuntosElectricos/ConjuntosElectricosWorkspacePage.cs | Existe |
| Banco de Precios | Buscador | Features/Buscador/BuscadorWorkspacePage.cs | Existe |
| Lista de Compra | ListaCompra | Features/ListaCompra/ListaCompraWorkspacePage.cs | Existe |
| Auditoría Precios | AuditoriaPrecios | Features/AuditoriaPrecios/AuditoriaPreciosWorkspacePage.cs | Existe |
| Monedas | Monedas | Features/Monedas/MonedasWorkspacePage.cs | Existe |
| Revisión Final | RevisionFinal | Features/RevisionFinal/RevisionFinalWorkspacePage.cs | Existe |
| Versiones | Versiones | Features/Versiones/VersionesWorkspacePage.cs | Existe |
| Órdenes de Cambio | OrdenesCambio | Features/OrdenesCambio/OrdenesCambioWorkspacePage.cs | Existe |
| Exportar | Exportar | Features/Exportar/ExportarWorkspacePage.cs | Existe |
| Preferencias | Preferencias | (modal existente) | Existe |

**Conclusión**: No faltan pantallas. Toda la navegación ya existe y está cableada.

---

## Archivos a modificar

### Fase 1 — Shell visual y navegación

| Archivo | Cambio |
|---------|--------|
| `AppTheme.cs` + `AppTheme.ThemeManagement.cs` | Ajustar colores a Qubi exactos (#EAECF2 canvas, #fff cards, #E4E8EF borders, etc.) |
| `AppNavigationBar.cs` | Ya tiene Qubi styling. Ajustar colores a exactos: #0C1828 header, #091424 nav. Conectar botones a funciones reales. |
| `AppTitleBar.cs` | Reducir/alts. a Qubi. |
| `ShellCompositionService.cs` | Ajustar BuildShell, BuildMain, BuildSidebar. |
| `ShellLayoutBuilder.cs` | Ajustar colores de contenedores base (body, sidebar, main). |
| `AppPageHeader.cs` | Rediseñar como context bar Qubi (breadcrumb, título, status badge, version badge, botón guardar). |
| `AppStatusBar.cs` | Ajustar colores y layout. |

### Fase 2 — Dashboard

| Archivo | Cambio |
|---------|--------|
| `DashboardWorkspacePage.cs` | Rediseñar con 4 KPI cards, workflow stepper, chart, alerts, tabla. |
| `AppTheme.cs` (Surfaces) | Añadir helpers para KPI cards con accent stripes. |

### Fase 3 — Itemizado

| Archivo | Cambio |
|---------|--------|
| `ItemizadoWorkspacePage.cs` | Rediseñar toolbar, tabla, summary strip Qubi-style. |
| (varios) | Ajustes de colores y spacing. |

### Fase 4 — Cubicación

| Archivo | Cambio |
|---------|--------|
| `CubicacionWorkspacePage.cs` | Rediseñar 3-panel layout Qubi. |

### Fase 5 — Precios y compras

| Archivo | Cambio |
|---------|--------|
| `BuscadorWorkspacePage.cs`, `ListaCompraWorkspacePage.cs`, etc. | Ajustar colores y layout. |

### Fase 6 — Revisión y versiones

| Archivo | Cambio |
|---------|--------|
| `RevisionFinalWorkspacePage.cs`, `VersionesWorkspacePage.cs` | Rediseñar con progress ring, checklist, comparación. |

### Fase 7 — Exportación

| Archivo | Cambio |
|---------|--------|
| `ExportarWorkspacePage.cs` | Rediseñar con export type cards, delivery package list. |

---

## Plan por fases

### Fase 1: Shell visual y navegación
- Ajustar `AppTheme` colores a Qubi exactos
- Ajustar `AppNavigationBar` colores exactos y conectar botones faltantes
- Rediseñar `AppPageHeader` como context bar Qubi (breadcrumb, título, badge, versión)
- Ajustar `AppStatusBar` al estilo Qubi
- Asegurar que todas las pestañas del nav disparan navegación real

### Fase 2: Dashboard
- Implementar KPI cards con accent stripes y gradientes
- Implementar workflow stepper (5 pasos)
- Implementar bar chart
- Implementar panel de alertas
- Implementar tabla de partidas con mayor impacto

### Fase 3: Itemizado
- Toolbar con filtros (Mostrar todos, Incompletos, En revisión, Reordenar)
- Tabla con columnas Qubi, status badges, progress bars
- Summary strip

### Fase 4: Cubicación
- Layout 3 columnas (240px | 1fr | 280px)
- Left panel: lista de partidas con search
- Center: grid con fórmula bar y tabla de recursos
- Right: detail panel

### Fase 5: Precios y compras
- Banco de Precios: categorías + tabla con Qubi styling
- Lista de Compra: tabla + summary
- Auditoría Precios: tabla comparativa
- Monedas: grid de valores

### Fase 6: Cierre
- Revisión Final: score ring, checklist, blockers
- Versiones: tabla de versiones con comparación
- Órdenes de Cambio: tabla

### Fase 7: Entrega
- Export type cards (PDF, XLS, DOC)
- Delivery package list
- Conexión con exportadores reales existentes

---

## Riesgos

1. **AppTitleBar actual es custom chrome**: Si se deshabilita (FormBorderStyle != None), el title bar nativo de Windows aparece. Qubi no usa custom chrome — el header está dentro del cliente. Habrá que coordinar.
2. **AppNavigationBar y AppTitleBar compiten**: Actualmente AppTitleBar se crea en ShellCompositionService y AppNavigationBar en BuildNavigationBar. Asegurar que no haya duplicación.
3. **Sidebar izquierdo existe**: El código todavía tiene referencias a sidebar (SidebarNavButton, SidebarModuleGroup). Qubi no tiene sidebar principal. Deben ocultarse o eliminarse sin romper nada.
4. **PageHeaderHeight = 0**: AppTheme.Metrics.PageHeaderHeight es 0, el context bar está desactivado. Habrá que habilitarlo a 40px.
5. **DPI**: Todos los controles deben escalar correctamente a 100%, 125% y 150%.

---

## Criterios de aceptación

1. La app compila y pasa pruebas después de cada fase
2. El shell (header + nav + context bar) coincide visualmente con Qubi HTML
3. Todas las pestañas del nav navegan a pantallas reales existentes
4. Cada botón visible ejecuta una función real (no eventos vacíos)
5. Dashboard KPI cards muestran datos reales del presupuesto
6. Tablas y grids mantienen sus datos, solo cambia el estilo visual
7. Las exportaciones (Excel, PDF, Word) siguen funcionando
8. La app se ve correctamente en 100%, 125% y 150% DPI
9. No hay regresiones en cálculos, cubicación, itemizado, APU
