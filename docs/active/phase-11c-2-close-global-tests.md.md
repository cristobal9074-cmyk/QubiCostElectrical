# QUBI COST ELÉCTRICO — FASE 11C.2
## Cierre de los cuatro tests globales fallidos

## Fuente única de alcance

Este documento define exclusivamente la investigación y corrección de los cuatro tests globales fallidos detectados después de la Fase 11C.1.

No implementar nuevas funcionalidades.

No iniciar Fase 11D hasta obtener:

- 0 pruebas fallidas
- 0 pruebas omitidas
- Debug limpio
- Release limpio
- arquitectura aprobada

---

# Reglas de ahorro de tokens

1. No recorrer todo el repositorio.
2. Ejecutar primero la suite para obtener los nombres exactos de los cuatro tests.
3. Después trabajar únicamente sobre:
   - archivos de prueba fallidos
   - clases directamente implicadas
   - configuración necesaria
4. No leer `docs/archive`.
5. No leer especificaciones de fases anteriores.
6. No reproducir archivos completos en el chat.
7. No usar subagentes.
8. No ejecutar Git.
9. Limitar salidas a las últimas 40 líneas.
10. Ejecutar tests individuales o filtrados durante el diagnóstico.
11. Ejecutar la suite completa solo al inicio y al final.
12. No debilitar assertions para hacer pasar pruebas.
13. No agregar skips ni desactivar tests.
14. No clasificar un fallo como “preexistente” sin evidencia reproducible.

---

# Objetivo

Identificar, clasificar y corregir exactamente los cuatro tests globales fallidos.

Para cada fallo registrar:

- nombre completo
- proyecto de pruebas
- mensaje
- stack trace relevante
- causa raíz
- si es determinista o intermitente
- si depende del entorno
- corrección aplicada
- prueba de no regresión

---

# 1. Obtener los fallos exactos

Ejecutar:

```powershell
dotnet test ".\QubiCostElectrical.Wpf.sln" `
    -c Release `
    --no-build `
    --logger "console;verbosity=normal" |
    Tee-Object ".\TestResults\phase-11c2-global-test-output.txt" |
    Select-Object -Last 80
```

Crear la carpeta si no existe:

```powershell
New-Item -ItemType Directory -Path ".\TestResults" -Force
```

Extraer nombres y mensajes:

```powershell
Select-String `
    -Path ".\TestResults\phase-11c2-global-test-output.txt" `
    -Pattern "Failed|Error Message|Stack Trace|Total tests"
```

No modificar código antes de conocer los cuatro nombres exactos.

---

# 2. Ejecutar cada test de forma aislada

Para cada prueba:

```powershell
dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
    -c Release `
    --no-build `
    --filter "FullyQualifiedName=<NOMBRE_COMPLETO>" `
    --logger "console;verbosity=detailed" |
    Select-Object -Last 80
```

Ejecutar cada una al menos dos veces si existe sospecha de intermitencia.

---

# 3. Clasificación de causa

Clasificar cada test en una de estas categorías:

- ruta o raíz del repositorio
- dependencia del orden de ejecución
- estado global compartido
- base SQLite compartida
- archivo temporal compartido
- zona horaria o cultura
- fecha/hora del sistema
- paralelismo
- recurso embebido
- configuración Debug/Release
- nullable o serialización
- regresión funcional real
- otra causa demostrada

No adivinar.

---

# 4. Reglas de corrección

## Rutas

No depender exclusivamente de:

- `.git`
- directorio de trabajo actual
- ruta absoluta local
- nombre de usuario

Resolver la raíz mediante archivos estables como:

- `QubiCostElectrical.Wpf.sln`
- `Directory.Build.props`
- estructura del proyecto

## Archivos temporales

Usar carpetas únicas por test:

```csharp
Path.Combine(
    Path.GetTempPath(),
    "QubiCostElectrical.Tests",
    Guid.NewGuid().ToString("N"))
```

Liberar recursos en `finally` o `Dispose`.

## SQLite

Cada prueba debe usar:

- base temporal propia, o
- nombre único

No compartir estado mutable entre tests paralelos.

## Cultura y zona horaria

Definir explícitamente:

- `CultureInfo.InvariantCulture`
- zona horaria
- fechas de entrada

No depender de configuración regional del equipo.

## Tiempo

No usar `DateTime.Now` como expectativa exacta.

Inyectar reloj o comparar dentro de tolerancia razonable cuando corresponda.

## Paralelismo

Corregir estado global o aislamiento.

No desactivar paralelismo global salvo justificación fuerte.

## Recursos embebidos

Cargar ensamblados y recursos de manera determinista.

No depender de que la aplicación se haya ejecutado antes.

---

# 5. Prohibiciones

No resolver fallos mediante:

- `[Fact(Skip = "...")]`
- exclusión del proyecto
- reducción de cobertura
- eliminación del test
- `Assert.True(true)`
- captura genérica que ignore errores
- aumentar tolerancias sin fundamento
- modificar el gate de arquitectura para omitir fallos
- convertir errores reales en warnings

---

# 6. Pruebas filtradas

Después de cada corrección:

```powershell
dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
    -c Release `
    --no-build `
    --filter "FullyQualifiedName~<CLASE_O_MODULO>" |
    Select-Object -Last 40
```

Si se modifica código fuente, compilar antes:

```powershell
dotnet build ".\QubiCostElectrical.Wpf.sln" `
    -c Release `
    --warnaserror |
    Select-Object -Last 25
```

---

# 7. Validación de estabilidad

Cuando las cuatro pruebas pasen aisladas:

1. Ejecutar juntas mediante filtro.
2. Ejecutarlas tres veces seguidas.
3. Ejecutar suite completa.
4. Ejecutar arquitectura.

Ejemplo de repetición:

```powershell
1..3 | ForEach-Object {
    dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
        -c Release `
        --no-build `
        --filter "<FILTRO_DE_LOS_CUATRO_TESTS>" |
        Select-Object -Last 15
}
```

---

# 8. Validación final

Ejecutar:

```powershell
dotnet restore ".\QubiCostElectrical.Wpf.sln"

dotnet build ".\QubiCostElectrical.Wpf.sln" `
    -c Debug `
    --warnaserror |
    Select-Object -Last 25

dotnet build ".\QubiCostElectrical.Wpf.sln" `
    -c Release `
    --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" `
    -c Release `
    --no-build |
    Select-Object -Last 30

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" `
    -c Release |
    Select-Object -Last 20
```

Resultado obligatorio:

- Debug: 0 errores
- Debug: 0 advertencias
- Release: 0 errores
- Release: 0 advertencias
- pruebas fallidas: 0
- pruebas omitidas: 0
- arquitectura: aprobada

---

# 9. Documentación mínima

Actualizar únicamente:

- `task.md`
- `walkthrough.md`
- `docs/active/phase-11c-1-hardening-report.md`

Crear:

- `docs/active/phase-11c-2-global-tests-report.md`

El reporte debe contener una tabla:

| Test | Causa raíz | Corrección | Resultado aislado | Resultado global |
|---|---|---|---|---|

No modificar `AGENTS.md` salvo que se descubra una regla permanente nueva.

---

# Criterio de cierre

Declarar únicamente:

**FASE 11C.2 — SUITE GLOBAL RESTAURADA A CERO FALLOS**

cuando:

- los cuatro tests estén identificados
- sus causas estén demostradas
- las correcciones no debiliten pruebas
- pasen aislados
- pasen juntos
- la suite global tenga 0 fallos
- no existan omitidos
- Debug y Release estén limpios
- arquitectura esté aprobada

No iniciar Fase 11D.