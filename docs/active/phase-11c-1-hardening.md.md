# QUBI COST ELÉCTRICO — FASE 11C.1
## Hardening de arquitectura y reducción de archivos a ≤400 líneas

## Fuente única de alcance

Este documento define exclusivamente la Fase 11C.1.

No implementar nuevas funciones contractuales.

No iniciar Fase 11D.

---

# Objetivo

Eliminar la deuda técnica introducida durante 11C, especialmente:

- `ContractChangeService.cs` con aproximadamente 1529 líneas
- archivos nuevos de 11C que superen 400 líneas
- exenciones de arquitectura introducidas por 11C
- responsabilidades excesivamente concentradas
- duplicación interna
- métodos demasiado largos
- dependencias innecesarias
- complejidad ciclomática evitable

La funcionalidad observable debe permanecer igual.

---

# Reglas de ahorro de tokens

1. No recorrer todo el repositorio.
2. Buscar primero dentro de:
   - `Domain/Contracts`
   - `Application/Contracts`
   - `Infrastructure/SQLite`
   - `Wpf/Features/ContractChanges`
   - tests contractuales
   - pruebas de arquitectura
3. No leer `docs/archive`.
4. No leer especificaciones de fases anteriores.
5. No reproducir archivos completos en el chat.
6. Aplicar cambios directamente.
7. Usar tests filtrados durante el desarrollo.
8. Ejecutar la suite completa solo al inicio y al final.
9. Limitar salidas de terminal a 25 líneas.
10. No usar subagentes.
11. No crear planes extensos.
12. No ejecutar Git.
13. Resumir cada bloque en máximo ocho líneas.
14. No modificar comportamiento público salvo corrección demostrada.

---

# Estado inicial

Antes de modificar:

1. Confirmar el total actual de pruebas.
2. Confirmar Debug y Release.
3. Ejecutar arquitectura.
4. Identificar todos los archivos `.cs` nuevos o modificados por 11C que superen 400 líneas.
5. Identificar exenciones de arquitectura relacionadas con 11C.

Comandos sugeridos:

```powershell
dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build |
    Select-Object -Last 25

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release |
    Select-Object -Last 20
```

Para localizar archivos largos:

```powershell
Get-ChildItem ".\src" -Recurse -Filter *.cs -File |
    Where-Object {
        $_.FullName -match "Contracts|ContractChanges"
    } |
    ForEach-Object {
        [pscustomobject]@{
            File  = $_.FullName
            Lines = (Get-Content -LiteralPath $_.FullName).Count
        }
    } |
    Where-Object Lines -gt 400 |
    Sort-Object Lines -Descending
```

---

# 1. ContractChangeService

## Problema

`ContractChangeService.cs` concentra aproximadamente 1529 líneas y múltiples responsabilidades:

- solicitudes de cambio
- órdenes de cambio
- obras extraordinarias
- reclamos
- impacto
- exportación
- auditoría
- validación
- integridad

Debe reducirse a menos de 400 líneas por archivo.

## Estrategia preferida

Priorizar separación real por responsabilidad.

Crear servicios especializados:

- `IChangeRequestService`
- `ChangeRequestService`
- `IChangeOrderService`
- `ChangeOrderService`
- `IExtraWorkService`
- `ExtraWorkService`
- `IContractClaimService`
- `ContractClaimService`
- `IChangeImpactAnalysisService`
- `ChangeImpactAnalysisService`

Mantener `IContractChangeService` y `ContractChangeService` únicamente como fachada de compatibilidad si la UI o tests existentes dependen de esa interfaz.

La fachada debe:

- delegar
- no duplicar reglas
- no contener lógica compleja
- permanecer bajo 400 líneas

## Alternativa aceptable

Cuando separar interfaces rompa demasiadas dependencias, usar una clase parcial organizada por responsabilidad:

- `ContractChangeService.cs`
- `ContractChangeService.ChangeRequests.cs`
- `ContractChangeService.ChangeOrders.cs`
- `ContractChangeService.ExtraWorks.cs`
- `ContractChangeService.Claims.cs`
- `ContractChangeService.Impact.cs`
- `ContractChangeService.Export.cs`
- `ContractChangeService.Validation.cs`

Cada archivo debe quedar bajo 400 líneas.

No usar `.Methods.cs` como simple ocultamiento de 1000 líneas sin estructura clara.

---

# 2. Métodos extensos

Identificar métodos mayores de aproximadamente 60–80 líneas.

Extraer responsabilidades como:

- autorización
- validación
- carga de entidades
- transición
- creación de auditoría
- persistencia
- exportación
- construcción de resultados
- comparación
- canonicalización de hash

Los métodos públicos deben expresar el caso de uso claramente.

Ejemplo conceptual:

```text
Authorize
ValidateInput
LoadAggregate
ApplyTransition
PersistTransaction
WriteAudit
ReturnResult
```

No fragmentar en métodos triviales sin significado.

---

# 3. Dependencias compartidas

Revisar el constructor de `ContractChangeService`.

Si contiene demasiadas dependencias:

- agrupar repositorios solo mediante servicios especializados
- evitar service locator
- no introducir contenedores genéricos de dependencias
- no crear objetos manualmente en ViewModels

Mantener DI explícita.

Registrar servicios nuevos en Composition.

---

# 4. Reglas de negocio

Garantizar una única implementación para:

- transiciones de estado
- SoD
- integridad
- contrato cerrado
- documento aprobado inmutable
- actualización de monto vigente
- actualización de plazo vigente
- prevención de doble aplicación
- auditoría
- control de concurrencia

No repetir estas reglas en fachada, servicios y ViewModels.

---

# 5. Transacciones

Verificar que cada operación compuesta siga siendo atómica.

Casos prioritarios:

- convertir solicitud en orden
- aprobar orden
- implementar orden
- actualizar monto y plazo vigentes
- registrar obra extraordinaria
- aprobar valorización extraordinaria
- aceptar parcialmente un claim
- registrar auditoría

No dejar persistencia parcial tras el refactor.

---

# 6. Exportación

Separar exportación de los servicios de negocio cuando actualmente esté mezclada.

Crear, según convención existente:

- `IContractChangeExporter`
- `ContractChangeCsvExporter`

La exportación debe:

- consumir modelos existentes
- no modificar entidades
- no recalcular motores
- ser cancelable cuando corresponda

---

# 7. Análisis de impacto

Aislar el análisis de impacto en:

- `IChangeImpactAnalysisService`
- `ChangeImpactAnalysisService`

Debe consumir resultados existentes de:

- contrato
- presupuesto
- planificación
- EVM
- estados de pago

No duplicar cálculos de esos módulos.

---

# 8. Auditoría

Centralizar la construcción de eventos mediante una fábrica o helper tipado cuando exista repetición.

Ejemplo:

- `ContractChangeAuditFactory`

No ocultar la persistencia de auditoría.

Cada operación sensible debe seguir registrando evento.

---

# 9. Integridad SHA-256

Separar hashing y canonicalización de los servicios CRUD.

Crear o reutilizar:

- `ChangeOrderIntegrityHasher`
- `ContractClaimIntegrityHasher`
- `ExtraWorkIntegrityHasher`

Mantener:

- determinismo
- orden explícito
- cultura invariante
- exclusión de campos volátiles

---

# 10. WPF

Revisar que los ViewModels de 11C:

- no contengan reglas contractuales
- no accedan directamente a SQLite
- usen servicios especializados o fachada
- mantengan comandos no reentrantes
- respeten solo lectura
- liberen suscripciones
- no superen 400 líneas

Dividir ViewModels extensos por responsabilidad cuando sea necesario:

- estado y propiedades
- comandos
- carga
- filtros
- navegación

No cambiar la experiencia de usuario.

---

# 11. Infrastructure

Revisar:

- `ContractChangeStores.cs`
- `ExtraWorkStores.cs`
- `ContractClaimStores.cs`
- `ContractChangeAuditStore.cs`

Cada archivo debe quedar bajo 400 líneas.

Separar por repositorio o entidad cuando corresponda.

No cambiar el schema v55 salvo que exista un defecto real.

No crear migraciones solo por refactorización.

---

# 12. Exenciones de arquitectura

Inspeccionar las exenciones vigentes del gate de 400 líneas.

Reglas:

- ninguna clase nueva de 11C debe depender de una exención
- eliminar exenciones añadidas específicamente para 11C
- conservar exenciones antiguas solo cuando sean realmente legacy
- documentar el motivo de cada exención restante

No modificar el test para hacerlo menos estricto.

---

# 13. Compatibilidad

Mantener:

- interfaces públicas usadas por WPF
- navegación
- formatos CSV
- persistencia v55
- hashes existentes cuando el algoritmo no cambie
- workflows
- permisos
- códigos de auditoría
- tests existentes

Cuando sea inevitable modificar una interfaz:

- adaptar Composition
- adaptar WPF
- adaptar tests
- mantener una fachada de compatibilidad cuando sea razonable

---

# 14. Pruebas

No generar una cuota artificial de tests.

Añadir pruebas únicamente para proteger el refactor:

- fachada delega correctamente
- cada servicio especializado aplica permisos
- SoD sigue funcionando
- aprobación es atómica
- implementación no se duplica
- auditoría sigue registrándose
- exportación conserva formato
- integridad conserva hash
- concurrencia
- cancelación
- Composition resuelve servicios
- ViewModels siguen operativos

Durante desarrollo:

```powershell
dotnet test ".\tests\QubiCostElectrical.Tests\QubiCostElectrical.Tests.csproj" `
    -c Release `
    --filter "FullyQualifiedName~ContractChange|FullyQualifiedName~ChangeRequest|FullyQualifiedName~ChangeOrder|FullyQualifiedName~ExtraWork|FullyQualifiedName~ContractClaim" |
    Select-Object -Last 30
```

---

# 15. Comprobación de líneas

Antes de cerrar:

```powershell
Get-ChildItem ".\src" -Recurse -Filter *.cs -File |
    Where-Object {
        $_.FullName -match "Contracts|ContractChanges"
    } |
    ForEach-Object {
        [pscustomobject]@{
            File  = $_.FullName
            Lines = (Get-Content -LiteralPath $_.FullName).Count
        }
    } |
    Where-Object Lines -gt 400 |
    Sort-Object Lines -Descending
```

El resultado no debe contener archivos nuevos de 11C.

---

# 16. Validación final

Ejecutar una sola vez al terminar:

```powershell
dotnet restore ".\QubiCostElectrical.Wpf.sln"

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Debug --warnaserror |
    Select-Object -Last 25

dotnet build ".\QubiCostElectrical.Wpf.sln" -c Release --warnaserror |
    Select-Object -Last 25

dotnet test ".\QubiCostElectrical.Wpf.sln" -c Release --no-build |
    Select-Object -Last 25

dotnet test ".\tests\ArchitectureTests\ArchitectureTests.csproj" -c Release |
    Select-Object -Last 20
```

Reportar:

- archivos largos encontrados inicialmente
- estrategia aplicada
- servicios creados
- archivos resultantes y cantidad de líneas
- exenciones eliminadas
- exenciones legacy restantes
- Debug
- Release
- pruebas aprobadas/fallidas/omitidas
- arquitectura
- schema SQLite
- compatibilidad
- riesgos restantes

---

# 17. Documentación

Actualizar únicamente:

- `task.md`
- `walkthrough.md`
- `docs/active/phase-11c-report.md`

Crear:

- `docs/active/phase-11c-1-hardening-report.md`

No crear documentación adicional.

No modificar `AGENTS.md` salvo que cambie una regla permanente.

---

# Criterio de cierre

Declarar únicamente:

**FASE 11C.1 — HARDENING DE ARQUITECTURA COMPLETADO**

cuando:

- `ContractChangeService.cs` quede bajo 400 líneas
- todos los archivos nuevos de 11C queden bajo 400 líneas
- no existan exenciones nuevas para 11C
- las responsabilidades estén separadas
- comportamiento y compatibilidad se conserven
- permisos y SoD sigan funcionando
- transacciones sigan siendo atómicas
- exportación conserve resultados
- Build Debug esté limpio
- Build Release esté limpio
- todas las pruebas pasen
- arquitectura esté aprobada

No iniciar Fase 11D.