# Qubi Cost Electrical

## Proyecto

Qubi Cost Electrical es una aplicación profesional de estimación, presupuesto, planificación y gestión del ciclo de vida de proyectos eléctricos.

Tecnologías:

- .NET 10
- WPF
- SQLite
- Clean Architecture
- MVVM

Proyecto raíz:

```
C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1
```

---

# Arquitectura

Capas:

- Domain
- Application
- Infrastructure
- Composition
- WPF

Reglas obligatorias:

- Domain nunca depende de Application, Infrastructure o WPF.
- Application nunca depende de WPF.
- Infrastructure implementa persistencia e integraciones.
- WPF solo contiene presentación.
- La lógica de negocio pertenece exclusivamente a Domain y Application.

---

# Calidad

Mantener siempre:

- 0 errores de compilación.
- 0 advertencias.
- Build Debug y Release.
- Todos los tests pasando.
- Gate de arquitectura aprobado.
- Máximo 400 líneas por archivo.

Cuando un archivo supere el límite permitido, dividirlo utilizando clases parciales (`*.Methods.cs`) o separando responsabilidades.

---

# Persistencia

Motor:

- SQLite

Toda modificación del esquema debe realizarse mediante migraciones.

Nunca modificar migraciones históricas.

Mantener compatibilidad con proyectos existentes.

---

# Cálculos

Todos los cálculos económicos deben utilizar:

- decimal
- PrecisionPolicy
- CurrencyCatalog
- Banker's Rounding

Nunca utilizar double para valores monetarios.

---

# Seguridad

Mantener:

- AuthorizationGuard
- SoD (Segregation of Duties)
- Auditoría inmutable
- SHA-256 determinista donde corresponda

No eliminar historial de auditoría.

---

# Desarrollo

Antes de implementar:

1. Revisar únicamente los archivos relacionados.
2. No recorrer todo el repositorio innecesariamente.
3. Reutilizar modelos existentes.
4. No duplicar código.
5. No crear motores paralelos cuando exista uno.

Durante el desarrollo:

- Mantener CancellationToken.
- Mantener operaciones deterministas.
- Mantener compatibilidad con versiones anteriores.

---

# Navegación

La navegación utiliza ShellRoute.

Los nuevos módulos deben registrarse correctamente en:

- ShellRoute
- ShellRouteCatalog
- ShellViewModel.Navigation
- ShellWindow

---

# WPF

Seguir MVVM estrictamente.

No colocar lógica de negocio en:

- code-behind
- XAML

Toda lógica pertenece a ViewModel o Application.

---

# Tests

Al finalizar cada tarea importante ejecutar:

```powershell
dotnet build ".\QubiCostElectrical.Wpf.sln" -c Debug --warnaserror

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror

dotnet publish ".\src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj" -c Release --self-contained -r win-x64 -o ".\publish\win-x64"

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release
```

Durante el desarrollo utilizar tests filtrados cuando sea posible.

---

# Uso de documentación

No leer toda la carpeta `docs`.

Leer únicamente la documentación relacionada con la tarea actual.

Ignorar `docs/archive` salvo que el usuario lo solicite expresamente.

---

# Internet

No utilizar búsquedas web ni recursos externos salvo que el usuario lo solicite explícitamente.

Trabajar únicamente con:

- código fuente
- documentación del proyecto
- conocimiento interno

---

# Alcance

Implementar únicamente la fase solicitada.

No comenzar fases futuras sin autorización del usuario.

No modificar funcionalidades estables salvo necesidad técnica demostrada.



## Documentación activa

Toda la planificación de desarrollo se encuentra en:

docs/active/

Reglas:

- Leer únicamente el archivo solicitado por el usuario.
- No leer otros archivos de docs/active salvo dependencia directa.
- No leer docs/archive salvo solicitud explícita.
- No inspeccionar toda la carpeta docs.
- La carpeta docs/active representa el trabajo actualmente en desarrollo.

---

# Historial de sesiones

## 2026-07-27 — Cálculo por piso (per‑zone template slaves)

### Feature: Recalcular esclavos al editar sector del maestro

Se implementó `RecalculateSlavesForZone` en `CubicacionSectorService.cs`.
Cuando el usuario edita una celda de sector/piso en un recurso MAESTRO,
los recursos ESCLAVO de la misma expansión recalculan su cantidad para
ese piso usando el snapshot TplRecursoSnapshot (Rendimiento × Factor).

### Bug conocido: validación "Cantidad es cero." en esclavos

Los esclavos se crean con `Cantidad=0` en `ConjuntoElectricoService.PerZona.cs`.
Al cargar la partida, `ValidateRows()` marca "Cantidad es cero." en el VM.
Cuando `RecalculateSlavesForZone` actualiza `Source.Cantidad` y
`Source.QtyExpressions`, el VM conserva el error stale.

Fix: `CubicacionSectorService.cs:316-324` — después de actualizar el esclavo,
se limpia el estado de validación anterior (`ApplyCellValidation(null...)` +
`HasError=false`) y se re-valida con `parent.ValidateRow(sibling)`.

### Bug fix: precio falso "Precio unitario es cero." en esclavos

`ConjuntoElectricoService.cs` y `ConjuntoElectricoService.PerZona.cs` creaban
`CubicacionRow` con `MatUf`/`MoUf` desde el material pero **sin copiar**
`MatClp`/`MoClp`. `MatClpConFallback` retornaba 0 (porque `MatUf` real es
`< 1000`) y la validación marcaba "Precio unitario es cero." aunque el
material tuviera precio en CLP.

Fix: se agregó `MatClp = material?.MatClp ?? 0m` y `MoClp = material?.MoClp
?? 0m` en ambos `ExpandToCubicacion` y en `MergeRows`.

### Nuevo: Resumen SC — Tomar Material (Alt+C)

Se implementó el diálogo **"Resumen SC · Tomar Material"** que permite
al usuario navegar el catálogo de materiales (PriceBankStore) agrupado
por categoría, con cantidades editables por piso/zona, y tomar un material
a la cubicación actual vía Alt+C.

Archivos nuevos:
- `ResumenScTomarDialog.xaml` + `.xaml.cs` — diálogo con DataGrid dinámico,
  columnas de zona generadas desde `ZonasProyecto`, filtro por sistema
  (A/E/CL/C/F), y botón "Tomar" por fila.
- `ResumenScTomarViewModel.cs` — ViewModel con flat rows (grupos + materiales),
  búsqueda por texto, filtro por sistema, y selección.
- `ResumenScTomarItem.cs` — modelos: `ResumenScTomarItem` (material con
  `ZonaQtyWrapper` por zona), `FlatRowVm`/`GroupHeaderRowVm`/`MaterialRowVm`
  para la grilla plana.

Modificaciones:
- `CubicacionViewModel.Sectors.cs` — nuevo `OpenScTomarCommand` + handler
- `CubicacionView.xaml` — botón "Tomar de SC…" en la barra de herramientas
- `CubicacionView.xaml.Methods.cs` — Alt+C redirigido al nuevo diálogo
  (reemplaza el anterior que abría el resumen SC de solo lectura)




