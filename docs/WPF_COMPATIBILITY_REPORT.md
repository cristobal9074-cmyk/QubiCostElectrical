# WPF Compatibility Report — Qubi Cost Electrical

> Actualizado para **1.0.0**. Producto WPF-only; WinForms retirado.

## Resumen

Qubi Cost Electrical usa una única fuente de verdad de datos:

- `BudgetProjectSession`, `IProjectRepository`, `ProjectJsonSerializer`
- Formato nativo `.qce` (SQLite)
- `.pepro` solo para compatibilidad legacy (abrir/importar proyectos antiguos)

## Estado funcional WPF (2026-06)

| Módulo | Estado |
|--------|--------|
| Shell / navegación | Implementado (stepper línea de producción + C.SC) |
| Login / sesión | Implementado |
| Dashboard | Implementado |
| Proyecto + auditoría | Implementado |
| Itemizado | Implementado |
| Cubicación | Implementado |
| Cubicación SC | Implementado (match precios por código + descripción) |
| APU | Implementado |
| Banco de precios / cotizaciones | Implementado (ruta UNC configurable) |
| Revisión final | Implementado |
| Documentos / antecedentes | Implementado |
| Oferta y documentos (wizard PDF, XLSX, DOCX) | Implementado |
| Versiones (comparación A vs B) | Implementado |
| Preferencias | Implementado |
| Órdenes de cambio | Implementado |

## Pruebas automatizadas (representativas)

| Área | Cobertura |
|------|-----------|
| Persistencia `.qce` | Roundtrip proyecto, itemizado, documentos |
| Cubicación | Rendimiento 3000 filas, fórmulas, ViewModel |
| Exportación | Export VM, freeze de precios, PDF totales |
| Itemizado | ViewModel, import use cases |
| Shell | Navegación, ciclo de vida, stepper producción, alertas |
| Versiones | Comparación A vs B + vs estado actual |
| Banco precios | Store integración SQLite |

Ejecutar suite completa:

```powershell
dotnet test QubiCostElectrical.Wpf.sln -c Release
```

## Compatibilidad de datos

- Itemizado, cubicación, APU y oferta comparten el mismo modelo Domain/Application.
- Ediciones en WPF persisten en `.qce` con esquema SQLite **v21**.
- Importar `.pepro` migra a `.qce` cuando corresponde.

## Verificación manual recomendada (pre-release)

1. Abrir `.pepro` legacy → verificar migración a `.qce`.
2. Flujo completo: proyecto → itemizado → cubicación → revisión → exportar PDF/XLSX (con gate de precios congelados).
3. Comparar dos versiones guardadas en módulo Versiones (modo A vs B y vs estado actual).
4. Stepper «Línea de producción»: navegar Datos → Exportar; C.SC accesible.
5. Guardar, cerrar, reabrir: sin pérdida de datos.
6. DPI 100 % / 125 % / 150 % en pantallas con grillas.
7. Instalador firmado en máquina limpia (sin SmartScreen bloqueante).

Checklist detallado: `docs/SMOKE_TEST.md`. Puerta Fase 2: `docs/PHASE2_WEEKS_5_8_REPORT.md`.

## Limitaciones conocidas

- **BC3:** no soportado en 1.0.0 — ver `docs/BC3_SCOPE.md`.
- Cubicación SC: paridad mejorada; validar flujo completo en smoke manual.
- Columnas de zona (q1..qN): edición por zona individual limitada en panel derecho.
- Banco en red: un editor de escritura a la vez (SQLite).

## Referencias históricas

Informes `docs/archive/WPF_PHASE*.md` documentan la migración WinForms → WPF. Para operación y releases: `AGENTS.md`, `CLAUDE.md`, `docs/RELEASE_PROCESS.md`.
