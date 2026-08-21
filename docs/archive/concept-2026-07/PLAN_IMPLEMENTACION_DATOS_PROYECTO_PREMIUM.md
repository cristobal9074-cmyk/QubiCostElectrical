# Plan cerrado de implementación · Datos del proyecto

## Regla principal

El archivo [datos-proyecto-propuesta-premium.html](./datos-proyecto-propuesta-premium.html) es el contrato visual y funcional cerrado. La implementación WPF debe reproducirlo sin reinterpretaciones.

No se aceptan:

- Cambios de orden, textos, nombres de secciones o jerarquía.
- Nuevas tarjetas, campos, colores, iconos o paneles no presentes en el HTML.
- Sustituir la navegación de cinco secciones por otra estructura.
- Mostrar datos técnicos internos en la ficha ejecutiva.
- Inventar valores de KPI, fechas, auditoría o indicadores.

Solo se permite adaptar tamaños al área disponible de WPF y conectar los controles a los datos reales existentes.

## Resultado exacto esperado

La pantalla debe tener esta composición, en el mismo orden:

1. Encabezado global `DATOS DEL PROYECTO`.
2. Nombre grande del proyecto.
3. Subtítulo: “Ficha ejecutiva del presupuesto y su gobierno documental.”
4. Chips: estado, moneda, cliente y guardado.
5. Acciones: `Generar ficha PDF` y `Aplicar cambios`.
6. Rail lateral con cinco secciones:
   - `1 · Identidad` — `Nombre, cliente, obra`.
   - `2 · Comercial` — `Moneda, margen, validez`.
   - `3 · Equipo` — `Responsable y rol`.
   - `4 · Alcance` — `Incluye y excluye`.
   - `5 · Auditoría` — `Actividad del archivo`.
7. Cuerpo contextual de una sola sección visible a la vez.
8. Footer de protección del archivo, SQLite, responsable y cambios pendientes.

## Mapeo de datos al ViewModel existente

| Elemento HTML | Binding WPF obligatorio |
|---|---|
| Nombre | `Nombre` |
| Cliente / mandante | `Cliente` |
| Tipo de proyecto | `TipoProyecto` |
| Ubicación | `Ubicacion` |
| Estado | `EstadoPresupuesto`, `SetEstadoCommand` |
| Moneda | `MonedaPresupuesto` |
| Valor UF | `ValorUfClpText` |
| Tipo de cambio USD | `TipoCambioUsdText` |
| Gastos generales | `GastosGeneralesText`, `GastosGeneralesKpi` |
| Utilidad | `UtilidadText`, `UtilidadKpi` |
| IVA | `IvaPctText`, `IvaKpi` |
| Superficie | `MetrosCuadradosText` |
| Validez de oferta | `ValidityDaysText` |
| Responsable | `UsuarioNombre` |
| Rol | `UsuarioRol` |
| Incluye | `ScopeText` |
| No incluye | `ExclusionsText` |
| Archivo | `ArchivoProyectoShortLabel`, `ArchivoProyectoLabel` |
| Fechas | `FechaCreacionLabel`, `FechaModificacionLabel` |
| Auditoría | `AuditEntries` |
| Guardar | `SaveCommand` |
| Descartar | `CancelCommand` |
| PDF | `GenerateFichaPdfCommand` |
| Secciones | `SelectedSection`, `SelectSectionCommand`, `IsSection*` |

## Fases de implementación

### Fase 0 · Congelar el contrato

- Guardar una copia de referencia del HTML.
- Capturar cada estado del HTML: Identidad, Comercial, Equipo, Alcance y Auditoría.
- Definir viewport de aceptación WPF: 1280×800, 1440×900 y 1920×1080.
- Definir escalas Windows: 100%, 125% y 150%.
- Confirmar que todos los datos de ejemplo de la maqueta serán reemplazados por datos reales en la aplicación.

### Fase 1 · Shell de la pantalla

Modificar `src/QubiCostElectrical.Wpf/Features/Project/ProjectView.xaml` para respetar exactamente:

- Fondo canvas claro.
- Cabecera blanca con borde inferior.
- Rail blanco de ancho fijo aproximado de 215 px.
- Panel principal con cards blancas, borde gris claro y radio consistente.
- Tipografía equivalente a la maqueta usando `Font.Ui`.
- Azul primario para selección, CTA y vínculos.
- Verde solo para guardado/correcto.
- Ámbar solo para revisión/cambios pendientes.
- Sin emojis ni símbolos ambiguos.

No crear una ventana nueva: debe permanecer dentro de `ShellWindow` y `CurrentView`.

### Fase 2 · Cabecera cerrada

Implementar la cabecera exactamente como el HTML:

- Texto pequeño `DATOS DEL PROYECTO`.
- Nombre editable con foco visible.
- Chips de estado, moneda, cliente y guardado.
- Subtexto de archivo `.qce` y última modificación.
- Botón PDF secundario.
- Botón `Aplicar cambios` primario.

El botón primario debe estar deshabilitado cuando:

- No hay cambios pendientes.
- El proyecto está en solo lectura.
- No existe un proyecto abierto.

### Fase 3 · Rail de navegación

- Mantener exactamente cinco botones y sus textos del contrato.
- Selección activa con fondo azul suave y línea azul izquierda.
- Hover estable sin mover el layout.
- Navegación por teclado con foco visible.
- En 1280 px, el rail no puede desaparecer ni superponerse al contenido.
- En escalado 150%, debe poder desplazarse verticalmente sin cortar etiquetas.

### Fase 4 · Sección Identidad

Reproducir el HTML en dos columnas:

#### Columna izquierda

- Card `OBRA Y MANDANTE`.
- Nombre del proyecto.
- Cliente / mandante.
- Tipo de proyecto.
- Ubicación de la obra.

#### Columna derecha

- Card `ESTADO Y CONTROL`.
- Selector Borrador / En revisión / Aprobado.
- Fecha de creación.
- Última modificación.
- Archivo QCE.
- Mensaje de control de calidad.

El cambio de estado debe usar exclusivamente `SetEstadoCommand` y conservar las restricciones de rol.

### Fase 5 · Sección Comercial

- Tres tarjetas superiores: Gastos generales, Utilidad e IVA.
- Card de Moneda e indicadores.
- Moneda base, UF, tipo de cambio USD y validez.
- Mensaje `Impacto antes de aplicar`.

Los valores deben venir del ViewModel y actualizarse sin bloquear la interfaz. No mostrar un valor de ejemplo cuando no exista dato.

### Fase 6 · Sección Equipo

- Card `RESPONSABLE DEL PRESUPUESTO`.
- Campos Nombre y Rol / cargo.
- Mensaje de valor de control.
- En solo lectura, los campos deben conservar el aspecto visual, pero no permitir edición.

### Fase 7 · Sección Alcance

- Dos cards paralelas: `INCLUYE` y `NO INCLUYE`.
- Textos multilinea, mínimo 130 px de alto.
- El contenido debe persistir en `ScopeText` y `ExclusionsText`.
- El mensaje debe indicar que el alcance viaja a ficha PDF y oferta.

### Fase 8 · Sección Auditoría

- Card `HISTORIAL RECIENTE`.
- Línea temporal descendente.
- Acción, usuario, máquina, fecha y detalle.
- No mostrar stack traces, IDs técnicos ni información de depuración.
- Mantener `AuditEntries` como fuente única.

### Fase 9 · Estados y errores

Implementar todos los estados del HTML:

- Sin proyecto abierto: EmptyState existente con `Abrir proyecto`.
- Guardado: chip verde y footer protegido.
- Cambios pendientes: chip ámbar, barra de acciones y botones Aplicar/Descartar.
- Solo lectura: controles visibles, edición deshabilitada y razón del bloqueo.
- Validación: mensaje debajo del formulario, no popup invasivo salvo error crítico.
- Error de guardado: mensaje accionable y conservación del contenido en memoria.

## Prohibiciones técnicas

- No cambiar modelos SQLite ni migraciones.
- No mover lógica de negocio al code-behind.
- No crear estados duplicados en la vista.
- No usar valores hardcodeados en producción.
- No cambiar nombres de binding existentes sin adaptador compatible.
- No eliminar la auditoría ni el control de solo lectura.
- No sustituir `SaveCommand`, `CancelCommand` o `SelectSectionCommand`.

## Pruebas obligatorias

### Automatizadas

- Los cinco botones cambian la sección correcta.
- Editar Nombre, Cliente, Ubicación y alcance marca `HasPendingChanges`.
- `CancelCommand` restaura el baseline.
- `SaveCommand` persiste y limpia cambios pendientes.
- Proyecto en solo lectura bloquea edición y guardado.
- Estado se actualiza solo mediante las transiciones permitidas.
- Auditoría se recarga después de guardar.
- Moneda, UF, IVA, utilidad y gastos generales validan entradas inválidas.

### Visuales

- Comparar con el HTML en 1280×800 y 1920×1080.
- Verificar que el rail, cabecera, cards y footer mantengan proporciones.
- Probar textos largos de cliente, ubicación, alcance y exclusiones.
- Probar tema claro y oscuro si el Shell lo permite.
- Probar 100%, 125% y 150% de escala.
- Verificar foco de teclado y contraste AA.

## Criterio de aceptación final

La implementación solo se acepta cuando:

1. El orden y la estructura coinciden con el HTML.
2. Los cinco botones del rail muestran exactamente las secciones definidas.
3. La cabecera muestra nombre, estado, moneda, cliente, guardado, archivo y última modificación.
4. Cada control modifica el binding real correspondiente.
5. El flujo de guardado, descarte, PDF, roles y auditoría sigue funcionando.
6. Build Release pasa sin errores ni advertencias.
7. Las pruebas de Proyecto pasan.
8. No aparecen nuevas excepciones en el log.

## Secuencia de entrega

1. Implementar Fases 0–2 y compilar.
2. Implementar Identidad y validar guardado/estado.
3. Implementar Comercial, Equipo y Alcance.
4. Implementar Auditoría y estados de solo lectura.
5. Ejecutar pruebas automatizadas y visuales.
6. Comparar manualmente contra el HTML y corregir solo desviaciones de implementación.

El HTML es la referencia definitiva. Cualquier discrepancia debe resolverse acercando WPF al HTML, nunca rediseñando el HTML durante la implementación.
