# Plan de implementación · Dashboard Premium aprobado

## Objetivo

Implementar en `QubiCostElectrical.Wpf` el Dashboard mostrado en [dashboard-propuesta-premium.html](./dashboard-propuesta-premium.html), conservando MVVM, SQLite, permisos, cálculos y navegación existentes.

El resultado no se considera terminado por “verse moderno”. Debe ayudar al estimador a detectar el siguiente bloqueo, resolverlo y llegar a una oferta verificable con menos tiempo y menos errores que Excel.

## Contrato visual aprobado

La pantalla debe respetar esta jerarquía, en este orden:

1. Cabecera del Shell: marca, proyecto activo, búsqueda global, guardado, rol.
2. Navegación horizontal: Resumen, Proyecto, Itemizado, Cubicación, APU, Precios, Revisión y Oferta.
3. Encabezado: breadcrumb, saludo/contexto, descripción y CTA principal.
4. Hero de próxima acción: problema más importante, monto afectado, partidas y tiempo estimado.
5. Preparación de oferta: porcentaje, checklist de pasos y estado de bloqueantes.
6. KPIs: monto ofertado, margen, partidas completas y vigencia de precios.
7. Cola de decisiones: bloqueantes, advertencias, decisiones económicas y correctos.
8. Flujo de producción: Proyecto → Itemizado → Cubicación → Precios → Revisión → Oferta.
9. Cobertura por especialidad y variación entre versiones.
10. Barra inferior: guardado, auditoría, responsable y atajos.

No añadir paneles, gráficos o tarjetas si no responden a una decisión del usuario.

## Mapeo al código existente

| Diseño aprobado | Fuente actual | Acción |
|---|---|---|
| Próxima acción | `DashboardViewModel.NextStepTitle`, `NextStepDetail`, `PrimaryCtaLabel`, `PrimaryCtaCommand` | Reutilizar; mejorar el cálculo de prioridad y el texto contextual. |
| Preparación | `WorkflowSteps`, `BlockingAlertCount`, `PartidasReadyLabel` | Reutilizar el modelo existente y convertirlo en checklist visible. |
| Cola de decisiones | `Alerts`, `BuildAlerts`, `FinalReviewSummary` | Ampliar cada alerta con severidad, impacto, regla y comando al origen. |
| KPIs económicos | Propiedades de costos de `DashboardViewModel` y `DashboardExecutiveService` | Mostrar solo métricas verificables; eliminar valores simulados. |
| Flujo | `ShellWorkflowLogic`, `ShellWorkflowStepVm` | Mantener la fuente única de estados y navegación. |
| Navegación | `ShellWindow.xaml` y `ShellViewModel.Navigation.cs` | No crear una segunda ventana; conservar `CurrentView`/cache de páginas. |
| Auditoría | `RecentAuditEntries`, `ProjectAuditEntryVm` | Mostrar resumen compacto y abrir actividad completa bajo demanda. |
| Tema | `DesignSystem/Colors*.xaml`, `Typography.xaml`, `Cards.xaml`, `Buttons.xaml` | Crear tokens/componentes; no introducir colores inline. |

## Fases de implementación

### Fase 0 · Línea base y datos reales

- Capturar la vista actual con proyecto vacío, proyecto completo, bloqueantes y modo solo lectura.
- Registrar el contrato de cada propiedad del `DashboardViewModel` y sus unidades CLP/UF/porcentaje.
- Confirmar qué métricas existen realmente en `DashboardExecutiveService`; no inventar margen, cobertura o “tiempo estimado”.
- Definir dataset de aceptación: mínimo 500 partidas, 5.000 recursos, precios vencidos, APUs incompletos y dos versiones.

Salida: documento de datos, capturas actuales y fixtures reproducibles.

### Fase 1 · Componentes visuales reutilizables

Crear en `src/QubiCostElectrical.Wpf/Controls` o `DesignSystem`:

- `DashboardHeroCard`: título, detalle, impacto, CTA y estado.
- `ReadinessCard`: porcentaje, checklist y estado textual.
- `MetricCard`: etiqueta, valor, variación y severidad.
- `DecisionQueue`: `ItemsControl` virtualizable con filas de alerta.
- `WorkflowProgress`: pasos navegables y estados complete/current/blocked.
- `CoverageBars`: barras con valor textual, no solo color.
- `VersionDeltaCard`: diferencias explicables entre snapshots.
- `SaveAuditFooter`: guardado, usuario, auditoría y atajos.

Todos deben tener estados vacío, cargando, error, solo lectura y foco de teclado.

### Fase 2 · ViewModel y contrato de presentación

Añadir modelos de presentación pequeños, sin meter lógica en XAML:

```csharp
public sealed record DashboardMetricVm(
    string Label, string Value, string Delta, MetricTone Tone);

public sealed record DashboardDecisionVm(
    string Title, string Detail, string Impact, DecisionSeverity Severity,
    string ActionLabel, ICommand ActionCommand);
```

En `DashboardViewModel`:

- Exponer `NextAction`, `Readiness`, `Metrics`, `DecisionQueue`, `WorkflowSteps`, `CoverageBySpecialty`, `VersionDelta`.
- Derivar `NextAction` por el bloqueante de mayor impacto económico; usar una regla explícita y testeable.
- Conservar los comandos actuales como compatibilidad o adaptadores.
- Emitir notificaciones solo cuando cambie el snapshot relevante; evitar recalcular toda la página por una celda sin relación.
- No utilizar textos simulados como “8 min” si no existe telemetría o estimación calculada; mostrar “estimación no disponible” en su lugar.

### Fase 3 · Reestructuración de `DashboardView.xaml`

- Reemplazar el layout actual por `ScrollViewer` vertical con ancho completo.
- Mantener una sola columna de contenido y una grilla responsive: hero 60/40, trabajo 55/45, análisis 50/50.
- Fijar CTA y estado de guardado dentro del encabezado de página, no en una zona perdida al final.
- Utilizar los tokens del Design System para navy, azul, success, warning, danger, espacios y radios.
- Sustituir emojis/símbolos ambiguos por iconos vectoriales consistentes.
- Asegurar `TextTrimming`, mínimos de ancho y escala 100/125/150%.
- El texto de severidad debe existir junto al color: “Bloquea”, “Revisar”, “Correcto”.

### Fase 4 · Integración de navegación y estados

- Cada fila de `DecisionQueue` debe navegar directamente a Itemizado, Cubicación, APU, Banco de precios o Revisión con contexto.
- El CTA del hero debe ejecutar la misma ruta que `PrimaryCtaCommand`.
- El flujo debe usar `ShellWorkflowLogic` como autoridad, sin duplicar reglas en Dashboard.
- En proyecto vacío: mostrar estado de inicio con “Crear proyecto” y “Abrir proyecto”, sin KPIs falsos.
- En solo lectura: mantener navegación y análisis, deshabilitar edición, indicar el rol y la causa.
- Si no hay dato de margen/cobertura/versiones, ocultar el bloque o mostrar estado “No disponible”; nunca cero engañoso.

### Fase 5 · Rendimiento y accesibilidad

- Cargar el snapshot una vez y aplicar actualizaciones incrementales.
- Evitar `UpdateLayout`/animaciones costosas; animaciones 150–250 ms y respetar reducción de movimiento.
- Probar navegación con teclado: `Ctrl+K`, `F8`, `Alt+O`, Tab y Enter.
- Contraste AA, foco visible, AutomationProperties y orden lógico de lectura.
- Verificar que listas grandes no generen cientos de elementos visuales innecesarios.

### Fase 6 · Validación antes de declarar terminado

#### Pruebas automatizadas

- Dashboard vacío: CTA abre/crea proyecto.
- Próxima acción: bloqueante de mayor impacto produce ruta correcta.
- `BlockingAlertCount`, porcentaje de preparación y estados de workflow coinciden con `FinalReviewSummary`.
- Cambiar un precio actualiza solo las métricas relacionadas.
- Proyecto de solo lectura no permite comandos de edición.
- Datos faltantes no generan métricas ficticias.
- Auditoría y guardado muestran el usuario/estado correcto.

#### Pruebas visuales

- 1366×768, 1920×1080 y 2560×1440.
- Escala Windows 100%, 125% y 150%.
- Tema claro y oscuro.
- Proyecto vacío, normal, con errores y listo para exportar.
- Comparación contra la maqueta HTML aprobada: orden, proporción, densidad, jerarquía y textos.

#### Criterio de negocio

- Un estimador nuevo identifica el siguiente paso sin explicación externa.
- Un estimador experimentado encuentra y resuelve un bloqueante sin recorrer todas las pestañas.
- El responsable puede explicar por qué el total cambió entre versiones.
- La oferta no se puede marcar lista con bloqueantes críticos sin resolver.
- Medir tiempo y errores frente al flujo actual de Excel antes y después.

## Archivos previstos

### Modificar

- `src/QubiCostElectrical.Wpf/Features/Dashboard/DashboardView.xaml`
- `src/QubiCostElectrical.Wpf/Features/Dashboard/DashboardViewModel.cs`
- `src/QubiCostElectrical.Wpf/DesignSystem/Cards.xaml`
- `src/QubiCostElectrical.Wpf/DesignSystem/DataGrid.xaml` (solo si se reutilizan estilos)
- `src/QubiCostElectrical.Wpf/Shell/ShellWindow.xaml` (solo ajustes de contexto/altura si la maqueta lo requiere)

### Crear, si no existe componente equivalente

- `src/QubiCostElectrical.Wpf/Controls/DashboardHeroCard.xaml`
- `src/QubiCostElectrical.Wpf/Controls/DashboardMetricCard.xaml`
- `src/QubiCostElectrical.Wpf/Controls/DashboardDecisionQueue.xaml`
- `src/QubiCostElectrical.Wpf/Controls/DashboardWorkflowProgress.xaml`
- `tests/QubiCostElectrical.Tests/Wpf/DashboardViewModelTests.cs`
- `tests/QubiCostElectrical.Tests/Wpf/DashboardVisualContractTests.cs` (contratos de datos, no screenshots frágiles)

## Riesgos y límites

- No cambiar SQLite, migraciones, fórmulas, permisos o exportadores para conseguir un efecto visual.
- No introducir KPIs que el dominio no pueda calcular y explicar.
- No convertir el Dashboard en un ERP: su trabajo es orientar y desbloquear el presupuesto.
- No medir valor por cantidad de tarjetas; medir tiempo de tarea, errores evitados y ofertas entregadas.
- “20 billones” no es una propiedad que pueda garantizar una interfaz. El Dashboard solo puede contribuir a una propuesta de valor defendible; la valoración exige producto usado, mercado, retención, margen y distribución.

## Orden recomendado de ejecución

1. Fase 0 y contrato de datos.
2. Fase 1: componentes y tokens.
3. Fase 2: ViewModel, reglas y pruebas.
4. Fase 3: XAML y navegación contextual.
5. Fase 5: rendimiento/accesibilidad.
6. Fase 6: validación con estimadores y dataset real.

El siguiente paso autorizado es implementar Fase 0–1 y entregar una primera versión compilable del Dashboard con datos reales, antes de modificar cálculos o persistencia.
