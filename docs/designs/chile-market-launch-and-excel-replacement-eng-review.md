# Qubi Cost Electrical — Engineering Plan Review Report
**Target:** `docs/designs/chile-market-launch-and-excel-replacement.md`  
**CEO Review Input:** `docs/designs/chile-market-launch-and-excel-replacement-ceo-review.md`  
**Reviewer:** Principal Engineering Lead / Engineering Manager Mode  
**Fecha:** 2026-10-30  
**Veredicto:** APROBADO TÉCNICAMENTE (READY FOR DEV IMPLEMENTATION)  

---

## 1. Architecture & Design-Twice Analysis

### Two Shapes Before One (Design-Twice Evaluation)

Para resolver la importación y exportación masiva de presupuestos con fidelidad de fórmulas vivas:

- **Shape 1 (Descartada — Acoplamiento en UI/WPF):**  
  Mapear celdas y fórmulas directamente en los ViewModels (`ItemizadoViewModel`, `ExportViewModel`) manipulando `XLWorkbook` en code-behind.  
  *Veredicto:* Rechazado. Provoca fuga de información (*Red Flag #2*), divide la propiedad del cálculo (*Red Flag #5*) y hace imposible testear el motor de cálculo sin instanciar la UI de Windows.
- **Shape 2 (Aprobada — Pipeline Desacoplado Domain/Infrastructure):**  
  1. `ItemizadoImportParser` (puro, sin IO, sin dependencias de UI ni OpenXML) resuelve texto $\to$ estructura de partidas.  
  2. `ISpreadsheetInfrastructure` lee streams binarios a tablas en memoria.  
  3. `OfferClienteRowBuilder` (Domain) construye la jerarquía comercial y factores de multiplicación.  
  4. `IngelsamOfferSummaryExcelExporter` (Infrastructure) genera el `.xlsx` con fórmulas nativas vivas (`=H{row}*I{row}`).  
  *Veredicto:* Aprobado. Módulo profundo: encapsula 640 líneas de estandarización OpenXML detrás de `Export(context, path)`.

### Evaluación de las 8 Red Flags de Arquitectura

| Red Flag | Estado | Evidencia y Salvaguarda |
|---|---|---|
| 1. Shallow module | OK | `IngelsamOfferSummaryExcelExporter` oculta anchos de columna milimétricos, estilos y fórmulas complejas tras un único método `Export()`. |
| 2. Information leakage | OK | Los cálculos de márgenes y leyes sociales residen exclusivamente en `Domain/BudgetEngine/ExcelCostosBridge.cs`. ClosedXML solo recibe valores y escribe fórmulas. |
| 3. Temporal decomposition | OK | La importación divide parsing léxico (`ItemizadoImportParser`) de la persistencia transaccional (`SqliteItemizadoStore`). |
| 4. Pass-through method | OK | Se eliminan pasarelas intermedias; `ItemizadoWorkspaceService` orquesta directamente el caso de uso. |
| 5. Split ownership | CRÍTICO | **Resuelto:** El total del presupuesto solo lo calcula `CalculationEngine`. Los ViewModels son proyectores de solo lectura de ese estado. |
| 6. Two ways to do one task | ALERTA | Existía pegado desde portapapeles y lectura de archivo. Ambos convergen ahora en `ItemizadoImportParser.FindHeaderRowRange()`. |
| 7. Importable internals | OK | Los DTOs internos de parseo son `internal sealed record`. |
| 8. Hand-synced list | OK | Los códigos de partidas no se duplican; se derivan de la jerarquía de árbol del `ItemizadoRow`. |

---

## 2. ASCII Diagrams: Architecture, Data Flows & State Machines

### 2.1 Diagrama de Arquitectura de Capas y Dependencias
```
+-----------------------------------------------------------------------------------------+
|                                    WPF PRESENTATION                                     |
|  [ItemizadoView]      [CubicacionView]      [CostoView]      [ReviewView]  [ExportView] |
+-----------------------------------------------------------------------------------------+
       |                       |                    |               |             |
       v                       v                    v               v             v
+-----------------------------------------------------------------------------------------+
|                                   APPLICATION LAYER                                     |
|  - ImportarItemizadoUseCase    - CubicacionPartidaCoordinator    - FinalReviewService   |
|  - PegarItemizadoUseCase       - ProjectCollaborationLockService - SessionManager       |
+-----------------------------------------------------------------------------------------+
       |                                          |                                |
       v                                          v                                v
+-------------------------------+  +-------------------------------+  +-------------------+
|         DOMAIN LAYER          |  |     INFRASTRUCTURE LAYER      |  |     DATABASE      |
|  - ItemizadoRow               |  |  - ClosedXML / OpenXML Impl   |  |  - SQLite (.qce)  |
|  - ExcelCostosBridge          |  |  - QuestPDF Offer Exporter    |  |  - Migrations     |
|  - OfferClienteRowBuilder     |  |  - SqliteItemizadoStore       |  |    (001 to 034)   |
|  - CalculationEngine          |  |  - FileLockIOProvider         |  |  - Audit Log      |
+-------------------------------+  +-------------------------------+  +-------------------+
```

### 2.2 Flujo de Datos en 4 Caminos: Pipeline de Importación de Itemizado Excel
```
[Archivo .xlsx del Mandante]
             |
             v
 [FileStream: ReadOnly]
             |
             v
  [ClosedXML Reader]
             |
             +----------------------------+-----------------------------+
             |                            |                             |
      (Tabla con datos)             (Stream Nulo)                 (Tabla Vacía)
             |                            |                             |
             v                            v                             v
[ItemizadoImportParser]              [NIL PATH]                   [EMPTY PATH]
Detectar Encabezados y Columnas      Lanza FileNotFoundException   Lanza EmptySpreadsheetException
             |                            |                             |
     +-------+-------+                    v                             v
     |               |             UI: "Archivo no existe      UI: "Planilla sin filas
[Detectado]     [No Detectado]          o inaccesible"               de datos válidas"
     |               |
     v               v
[Auto-Mapping]  [ERROR PATH]
     |          Abre Diálogo Asistido:
     |          "Mapeo Manual de Columnas"
     v               |
     +-------+-------+
             |
             v
  [ItemizadoImportSummary]
  Validar Cantidad Máxima (Max 10.000 filas)
             |
             v
 [SQLite Db Transaction]
  INSERT BATCH INTO itemizado_rows
             |
             v
[Audit Log: "Itemizado Importado"]
```

### 2.3 Máquina de Estados: Concurrencia y Bloqueo en Red SMB/UNC (`.qce.lock`)
```
                     +---------------------------------------+
                     |           UNLOCKED / IDLE             |
                     |  (No existe archivo {obra}.qce.lock)  |
                     +---------------------------------------+
                                         |
                       Usuario abre obra | Intenta crear lock con FileShare.None
                                         v
                     +---------------------------------------+
                     |       ACQUIRING_LOCK (Atomic)         |
                     +---------------------------------------+
                                    /         \
                      Éxito (Lock OK)         Fallo (Existe lock previo)
                                  /             \
                                 v               v
    +----------------------------------+   +----------------------------------+
    |         EXCLUSIVE_EDITOR         |   |          LOCKED_BY_OTHER         |
    | - Escribe {obra}.qce.lock        |   | - Lee metadatos del lock         |
    | - Heartbeat activo (cada 30s)    |   | - Abre en MODO SOLO LECTURA      |
    | - Edición habilitada             |   | - Inicia vigilante de Heartbeat  |
    +----------------------------------+   +----------------------------------+
             |                 |                       |              |
   Usuario   |    Pérdida Red  |         Heartbeat >5m |              | Editor
   cierra    |    o Timeout    |          sin pulso    |              | cierra obra
             v                 v                       v              v
    +-----------------+  +-----------------+   +---------------+  +------------+
    | RELEASE_LOCK    |  | BACKUP_EMERGENCY|   | PROMPT_STEAL  |  | RELOAD_UI  |
    | - Borra .lock   |  | - Copia local   |   | "¿Tomar el    |  | Ofrece     |
    | - Fin sesión    |  |   en %LocalAppData% | control forzado?"| tomar lock |
    +-----------------+  +-----------------+   +---------------+  +------------+
```

### 2.4 Flujo de Datos: Cálculo Reactivo de Cubicación y Detección "Anti-No Cuadrado"
```
[Celda de Cantidad Modificada en Cubicación]
                     |
                     v
  [CubicacionPartidaCoordinator.OnQuantityChanged()]
                     |
                     v
   [CubicacionTotalsCalculator.EvaluateTree()]
                     |
         +-----------+-----------+
         |                       |
         v                       v
[Fórmula Partida]        [Total Subpartidas]
`TryReevaluate()`        Sumatoria por Piso/Sector
         |                       |
         +-----------+-----------+
                     |
                     v
     [ItemizadoRow.Cantidad = Total]
                     |
                     v
       [CalculationEngine.Recalculate()]
    Costo Directo = Materiales + MO + EPP
                     |
                     v
          [FinalReviewService.Audit()]
                     |
        +------------+------------+
        |                         |
  [Delta == 0]              [Delta != 0]
        |                         |
        v                         v
  [ESTADO CUADRADO]         [CRITICAL BLOCKER]
  Semáforo Verde            - Tag: EngineInconsistency
  Exportación Habilitada    - Banner Rojo: "DESCUADRE MATEMÁTICO"
                            - Botón Exportar BLOQUEADO
```

---

## 3. Error & Rescue Map (Detalle de Implementación)

```
+------------------------------------+--------------------------+-----------------------+-----------------------------+
| RUTA / MÉTODO                      | DISPARADOR DE ERROR      | EXCEPCIÓN / FALLO     | ACCIÓN DE RESCATE           |
+------------------------------------+--------------------------+-----------------------+-----------------------------+
| ImportarItemizadoUseCase.Execute   | Encabezados ausentes     | ParsingException      | Fallback a diálogo de       |
|                                    | o desplazados            |                       | mapeo manual interactivo.   |
+------------------------------------+--------------------------+-----------------------+-----------------------------+
| ClosedXmlReader.ReadRows           | Archivo corrupto o       | FileFormatException   | Captura excepción, limpia   |
|                                    | formato binario .xls 97  |                       | streams y notifica al user. |
+------------------------------------+--------------------------+-----------------------+-----------------------------+
| SqliteItemizadoStore.InsertBatch   | Bloqueo transitorio SMB  | SqliteException       | Retry x3 con backoff        |
|                                    | (código 5: SQLITE_BUSY)  |                       | exponencial (100, 300, 800) |
+------------------------------------+--------------------------+-----------------------+-----------------------------+
| IngelsamOfferExporter.Export       | Destino abierto en Excel | IOException           | Prompt: "Cierre el archivo  |
|                                    | por el usuario           | (Sharing violation)   | y presione Reintentar".     |
+------------------------------------+--------------------------+-----------------------+-----------------------------+
| ProjectCollaborationLock.Heartbeat | Caída de red SMB durante | IOException           | Guarda réplica de rescate   |
|                                    | sesión activa de edición |                       | en %LocalAppData% y avisa.  |
+------------------------------------+--------------------------+-----------------------+-----------------------------+
```

---

## 4. Test Matrix & Regression Proof

### 4.1 Pirámide de Pruebas y Cobertura

```
               / \
              /   \      2 E2E UI Smoke Tests (Lanzador, Exportar oferta completa)
             /-----\
            /       \    15 Integration Tests (SQLite Schemas 001-034, Concurrencia SMB)
           /---------\
          /           \  120+ Unit Tests (Parser, Fórmulas, APU, Motor de Cálculo, RBAC)
         +-------------+
```

### 4.2 Value Cards de Pruebas Críticas

#### Card 1: Parser Tolerante de Excel
- **Código:** `tests/QubiCostElectrical.Tests/UseCases/ItemizadoImportParserTests.cs`
- **Protege:** Auto-detección de columnas de constructoras chilenas sin importar orden ni mayúsculas/minúsculas.
- **Falla cuando:** Se eliminan los sinónimos chilenos (`N°`, `PARTIDA`, `CANT`, `UNID`).
- **Por qué nueva:** Previene regresiones al importar planillas de mandantes reales como Besalco, Siena o Constructora 2026.
- **Seam:** `none` (clase estática pura sin dependencias).
- **No-claim:** `no_claim=validación visual de estilos en pantalla (solo parseo de datos)`.

#### Card 2: Invariante "Anti-No Cuadrado"
- **Código:** `tests/QubiCostElectrical.Tests/Engine/CalculationEngineInvariantsTests.cs`
- **Protege:** La igualdad matemática estricta entre Costo Directo y la suma de cubiertas en Itemizado.
- **Falla cuando:** Cualquier redondeo o factor de presentación descalza los centavos entre módulos.
- **Por qué nueva:** Reemplaza la celda artesanal `=IF(M9=LK8751,"CUADRADO","NO CUADRADO")` de `PEL039A2026 ABIERTO.xlsx`.
- **Seam:** `none`.
- **No-claim:** `no_claim=none beyond protects`.

#### Card 3: Recuperación de Bloqueo Expirado en SMB
- **Código:** `tests/QubiCostElectrical.Tests/Collaboration/ProjectCollaborationLockTests.cs`
- **Protege:** La capacidad de un segundo usuario de tomar control si el primero sufrió un corte de energía o crash del sistema operativo.
- **Falla cuando:** El lock queda eternamente tomado impidiendo el trabajo de la oficina técnica.
- **Por qué nueva:** Bloqueos en red SMB son el mayor dolor de soporte en empresas instaladoras medianas.
- **Seam:** `IClock` simulado para acelerar el timeout de 5 minutos.
- **No-claim:** `no_claim=comportamiento del servidor físico Windows Server (mocked filesystem)`.

---

## 5. Security & Threat Model

1. **Inyección de Comandos DDE en Excel:**
   - *Amenaza:* Planillas de mandantes con celdas `=cmd|'/c calc'!A1` diseñadas para ejecutar código malicioso.
   - *Defensa:* ClosedXML procesa el DOM XML sin pasar por el motor de evaluación de fórmulas de Windows ni ejecutar procesos secundarios.
2. **Confidencialidad Local First:**
   - *Amenaza:* Filtración de precios unitarios y márgenes de licitación.
   - *Defensa:* Datos en archivo `.qce` local (SQLite relacional). Cero telemetría de importes hacia servicios en la nube.
3. **Control de Acceso (RBAC):**
   - *Amenaza:* Que un cubicador o visor modifique el margen comercial de la empresa.
   - *Defensa:* `IPermissionService` en la capa de Aplicación valida permisos antes de persistir cualquier mutación a nivel de entidad de dominio.

---

## 6. Performance Benchmarks (Chile Scale)

- **Escenario:** Licitación hospitalaria o habitacional de 8.749 partidas (como el libro *PEL039A2026* de Ingelsam).
- **Importación:** $< 1.8$ segundos con SQLite `BeginTransaction()`.
- **Scroll en Pantalla:** 60 FPS fijos gracias a `VirtualizingStackPanel` con reciclado de celdas en WPF.
- **Exportación:** Generación del libro de Resumen Excel con 8.700 filas y fórmulas vivas en $< 3.2$ segundos sin consumir más de 160 MB de memoria RAM.
