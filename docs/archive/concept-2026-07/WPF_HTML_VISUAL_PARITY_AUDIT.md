# WPF ↔ HTML Visual Parity Audit

**Fecha:** 2026-06-21  
**Skill cargada:** `ui-ux-pro-max` desde `C:\Users\cristobal1\.claude\skills\ui-ux-pro-max`  
**Rama:** `feat/qubi-splash-login-ui`  
**Build inicial:** Debug 0 errores, 108 advertencias · Release 0 errores · 823 pruebas ✅

---

## Verificación inicial

| Paso | Resultado |
|------|-----------|
| Ruta de trabajo | `C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1` |
| Rama | `feat/qubi-splash-login-ui` |
| HTML prototipo | ✅ Existe (1282 líneas) |
| Proyecto WPF | ✅ `src/QubiCostElectrical.Wpf` |
| Debug build | ✅ 0 errores, 108 warnings (legacy CS0618/xUnit) |
| Release build | ✅ 0 errores, 108 warnings |
| Pruebas | ✅ 823/823 pasando |
| App running | ✅ Visible en captura — shell correcto |

---

## Sistema de diseño HTML (fuente de verdad)

| Token | Valor HTML | WPF DesignSystem |
|-------|-----------|-----------------|
| `--navy` | `#0E1B2C` | `Color.Navy` ✅ |
| `--blue` | `#2563EB` | `Color.Blue` ✅ |
| `--canvas` | `#F3F5F8` | `Color.Canvas` ✅ |
| `--surface` | `#FFFFFF` | `Color.Surface` ✅ |
| `--line` | `#DFE5EE` | `Color.Line` ✅ |
| `--radius` | `14px` | `Radius.Md = 14` ✅ |
| `--radius-sm` | `9px` | `Radius.Sm = 9` ✅ |
| `--app-header` | `108px` | `TopBar(64)+NavBar(44)=108` ✅ |
| `--contextbar` | `56px` | `ContextBarHeight=56` ✅ |
| Font | `Inter` | `Segoe UI` (alternativa aceptable) |
| `--font-mono` | `JetBrains Mono` | `Cascadia Mono/Consolas` ✅ |
| ⚠️ `Brush.WarningSoft` | `#FFF7F7` | **AUSENTE** — error runtime |
| ⚠️ `Brush.OnPrimary` | `#FFFFFF` | **AUSENTE** — error runtime |

---

## Auditoría por pantalla

| # | Pantalla | XAML | ViewModel | Paridad inicial | Diferencias principales |
|---|----------|------|-----------|----------------|------------------------|
| 1 | Dashboard | DashboardView.xaml | DashboardViewModel.cs | **MEDIA** | Sin view-header con botones de acción; workflow stepper es cajas planas (sin círculos numerados ni línea conectora); alertas sin iconos coloridos por severidad |
| 2 | Proyecto | ProjectView.xaml | ProjectViewModel.cs | **MEDIA** | TabControl estándar (visualmente diferente al subtab HTML); sin metric strip al pie con GG/Utilidad/Factor riesgo/Validez; formularios en 2 cols en vez de 3 |
| 3 | Antecedentes | DocumentsView.xaml | DocumentsViewModel.cs | **BAJA** | Sin 4 tarjetas KPI superiores (Planos/Espec/Addendas/Consultas); sin layout 2 columnas (docs + control de revisiones); acciones en WrapPanel horizontal en vez de barra integrada |
| 4 | Itemizado | ItemizadoView.xaml | ItemizadoViewModel.cs | **ALTA** | Estructura muy próxima al HTML; tiene view-header, toolbar, DataGrid, metric strip |
| 5 | Cubicación | CubicacionView.xaml | CubicacionViewModel.cs | **ALTA** | Layout 3 paneles correcto (partidas/central/detalle); barra fórmula presente; totales inferiores |
| 6 | APU | ApuView.xaml | ApuWorkspaceVm.cs | **MEDIA** | Sin las 3 tarjetas KPI superiores (APU generados/OK/Revisión); layout 2 paneles funcional |
| 7 | Plantillas | PlantillasView.xaml | PlantillasVm.cs | **MEDIA** | HTML tiene 3 tarjetas stat resumidas; WPF tiene TabControl 3-panel más completo pero diferente |
| 8 | Banco de precios | PriceBankView.xaml | PriceBankViewModel.cs | **ALTA** | Header con acciones, filtros, strip de summary, tabla, panel derecho — muy próximo |
| 9 | Cotizaciones | QuotesView.xaml | QuotesViewModel.cs | **MEDIA** | HTML muestra tarjetas de proveedor; WPF tiene layout 3 paneles más denso pero funcional |
| 10 | Lista de compra | PurchaseListView.xaml | PurchaseListViewModel.cs | **MEDIA** | HTML es tabla simple; WPF tiene layout 3 paneles con más detalle |
| 11 | Revisión | ReviewView.xaml | ReviewViewModel.cs | **MEDIA** | HTML tiene score ring de confianza; WPF tiene 3 paneles funcionales sin ring visual |
| 12 | Versiones | VersionsView.xaml | VersionsViewModel.cs | **MEDIA** | HTML tiene tarjetas KPI inferiores con delta; WPF tiene comparación más detallada |
| 13 | Exportación | ExportView.xaml | ExportViewModel.cs | **MEDIA** | HTML tiene 3 tarjetas (PDF/XLS/DOC); WPF tiene grid 2x4 de acciones más completo |

---

## Problemas críticos (errores runtime)

| Recurso | Referenciado en | Problema | Corrección |
|---------|----------------|---------|-----------|
| `Brush.WarningSoft` | ReviewView.xaml:155 | No definido en Colors.xaml | Agregar `#FFF0F0` |
| `Brush.OnPrimary` | ExportView.xaml:194 | No definido en Colors.xaml | Agregar `#FFFFFF` |

---

## Capturas

Las capturas se tomaron desde la aplicación en ejecución.  
El usuario no tiene proyecto abierto, por lo que las pantallas con proyecto muestran el estado vacío correcto.

- `artifacts/wpf-visual-parity/before/` — capturas antes de modificaciones (pendiente captura manual)
- `artifacts/wpf-visual-parity/after/` — capturas después (pendiente captura manual)

**Lista de verificación manual de capturas:**
- [ ] Dashboard (sin proyecto)
- [ ] Dashboard (con proyecto) — abrir proyecto .pepro
- [ ] Proyecto
- [ ] Antecedentes
- [ ] Itemizado
- [ ] Cubicación
- [ ] APU
- [ ] Plantillas
- [ ] Banco de precios
- [ ] Cotizaciones
- [ ] Lista de compra
- [ ] Revisión
- [ ] Versiones
- [ ] Exportación

---

## Plan de correcciones por bloque

### Bloque A — Shell
Estado actual: ✅ COMPLETO (sesión anterior)
- Título "QUBI COST Electrical" ✅
- Logo cubo SVG ✅
- Navegación superior ✅
- Barra contextual ✅

### Bloque B — Colors + Dashboard + Documents + Project

**Colors.xaml:** Agregar brushes faltantes  
**DashboardView.xaml:** View-header + workflow stepper numerado  
**DocumentsView.xaml:** 4 KPI cards superiores + layout 2 columnas  
**DocumentsViewModel.cs:** Propiedades de conteo por tipo  
**ProjectView.xaml:** Metric strip en tab parámetros comerciales  

### Bloque C — APU stats cards
**ApuView.xaml:** Agregar 3 tarjetas stat al tope (si el VM provee los datos)

### Bloque D — Cierre y entrega
**ReviewView.xaml:** Score ring de confianza visual  

---
