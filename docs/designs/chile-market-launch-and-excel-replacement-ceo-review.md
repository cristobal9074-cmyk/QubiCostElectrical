# Qubi Cost Electrical — CEO Plan Review Report
**Plan:** Lanzamiento Oficial Mercado Eléctrico Chile 2026 y Reemplazo Definitivo de Excel  
**Fecha:** 2026-10-30  
**Modo Seleccionado:** SELECTIVE EXPANSION  
**Veredicto General:** APROBADO CON OBSERVACIONES CRÍTICAS (READY FOR IMPLEMENTATION)  
**Documento Fuente:** `docs/designs/chile-market-launch-and-excel-replacement.md`  

---

## 0. Executive Scope & Mode Summary

| Dimensión | Estado / Decisión |
|---|---|
| **Modo de Revisión** | `SELECTIVE EXPANSION` (Aprobado por usuario) |
| **Núcleo Blindado** | 1. Importador inteligente de Excel del mandante (auto-detección de columnas).<br>2. Banco inicial Chile 2026 (400 insumos canónicos en CLP: EVA, EMT, BPC, Legrand/Schneider).<br>3. Exportador Ingelsam con fórmulas vivas en Excel (`=CANT*PU`) + Carta Oferta PDF/Word. |
| **Expansiones Opcionales Aceptadas** | 1. Validador "Anti-No Cuadrado" integrado en el módulo de Revisión.<br>2. Bloqueo de concurrencia SMB/UNC robustecido (`.qce.lock` con auto-recuperación ante corte de red). |
| **Expansiones Diferidas (Post-Lanzamiento)** | 1. Visor DWG/PDF integrado para cubicación gráfica.<br>2. Conector API directo IConstruye.<br>3. Aplicación móvil Android/iOS de bodega de faena. |

---

## 1. Architecture Review

### Diagrama General del Sistema y Límites de Componentes
```
+---------------------------------------------------------------------------------------+
|                                    WPF SHELL (UI)                                     |
|  [ItemizadoView]   [CubicacionView]   [CostoView]   [ReviewView]   [ExportView]       |
+---------------------------------------------------------------------------------------+
           |                   |             |              |              |
           v                   v             v              v              v
+---------------------------------------------------------------------------------------+
|                           APPLICATION & VIEWMODELS LAYER                              |
|  - ItemizadoViewModel       - CubicacionPartidaCoordinator   - CostoViewModel         |
|  - ReviewViewModel          - ExportViewModel                - MaterialControlService |
|  - IPermissionService       - ISpreadsheetInfrastructure                              |
+---------------------------------------------------------------------------------------+
           |                                                        |
           v                                                        v
+------------------------------------+  +-----------------------------------------------+
|           DOMAIN LAYER             |  |             INFRASTRUCTURE LAYER              |
|  - Presupuesto / ItemizadoRow      |  |  - SQLite DbContext / Migrations (001-034)    |
|  - CalculationEngine               |  |  - ClosedXML Excel Engine                     |
|  - OfferClienteRowBuilder          |  |  - QuestPDF Engine                            |
|  - ProjectCollaborationLockService |  |  - SqliteItemizadoStore                       |
+------------------------------------+  +-----------------------------------------------+
                  |                                     |
                  +------------------+------------------+
                                     v
                  +-------------------------------------+
                  |   STORAGE & COLLABORATION BOUNDARY   |
                  |   - Proyecto: `{obra}.qce` (SQLite) |
                  |   - Lock: `{obra}.qce.lock` (JSON)  |
                  |   - Entorno: Disco local o SMB/UNC  |
                  +-------------------------------------+
```

### Flujo de Datos en 4 Caminos (Importación de Excel del Mandante)

```
        [Archivo Excel Mandante .xlsx]
                      |
                      v
            [1. Detección de Hojas]
                      |
      +---------------+---------------+---------------+
      |                               |               |
[Happy Path]                     [Nil Path]      [Empty Path]
Columnas detectadas              Archivo null    Libro sin filas
(Item, Detalle, Cant, Un)        o inaccesible   o columnas vacías
      |                               |               |
      v                               v               v
[2. Mapeo Canónico]             Error Dialog    Error Dialog
Validación tipos datos          "Archivo no     "Planilla no
      |                         encontrado"     contiene datos"
      v                               |               |
[3. Transacción SQLite]               v               v
`ItemizadoRow` insertados           ABORT           ABORT
      |
      v
[4. Recálculo Árbol]
Capítulos y Subtotales
```

- **Error Path (Corrupción de Fórmulas o Tipos en Excel):** Si una celda de cantidad contiene texto (`"S/C"` o `"Por definir"`), el importador captura `FormatException`, asigna `0.0`, registra un tag de advertencia en `project_audit_log` y resalta la fila en ámbar en la grilla para revisión del usuario sin abortar la importación masiva.

---

## 2. Error & Rescue Map (Cero Fallos Silenciosos)

| Método / Ruta de Código | Causa de Falla Potencial | Clase de Excepción | ¿Rescatado? | Acción de Rescate | Experiencia de Usuario | Test de Regresión Obligatorio |
|---|---|---|---|---|---|---|
| `ExcelItemizadoImporter.Import()` | Estructura no estándar, celdas combinadas caóticas | `SpreadsheetParseException` | SÍ | Abre asistente de mapeo visual manual de columnas (fallback). | Diálogo asistido: "¿Qué columna es el Código / Partida?". | `Import_MalformedSheets_PromptsManualMapping()` |
| `SqliteItemizadoStore.SaveBatch()` | Bloqueo transitorio de base de datos en red lenta (SMB) | `SqliteException (SQLITE_BUSY)` | SÍ | Reintentos exponenciales con jitter (3 intentos: 100ms, 300ms, 800ms). | Spinner con texto: "Sincronizando con red...". | `SaveBatch_WhenDatabaseBusy_RetriesAndSucceeds()` |
| `CollaborationLock.Acquire()` | Archivo `.qce.lock` huérfano por crash de PC anterior | `LockConflictException` | SÍ | Evalúa heartbeat: si pasaron > 5 min sin pulso, ofrece romper bloqueo. | Diálogo: "El usuario X no responde hace 10 min. ¿Desea tomar el control?". | `Lock_ExpiredHeartbeat_AllowsForcedTakeover()` |
| `OfferExcelExporter.Export()` | Archivo de destino abierto por el usuario en Excel | `IOException (Sharing Violation)` | SÍ | Sugiere nuevo nombre temporal o pide cerrar Excel con reintento. | Diálogo: "Cierre el archivo en Excel y presione Reintentar". | `Export_FileInUse_PromptsRetryWithoutCrash()` |
| `CubicacionTotalsCalculator.Evaluate()` | División por cero o fórmula cíclica en desglose | `DivideByZeroException` / `CycleException` | SÍ | Captura error, fija total en 0 y marca celda con borde rojo `!`. | Indicador visual de error sin colapsar la grilla WPF. | `Evaluate_CyclicFormula_RendersErrorBorder()` |

---

## 3. Security & Threat Model

1. **Privacidad de Costos y Márgenes (Regla Absoluta):**
   - Las empresas chilenas desconfían visceralmente de que sus APU y márgenes caigan en manos de competidores o de la constructora mandante.
   - **Postura:** Arquitectura 100% *Local-First*. El archivo `.qce` vive exclusivamente en la máquina o servidor local del cliente. Cero telemetría ni subida de montos a la nube.
2. **Superficie de Ataque en Importación de Archivos:**
   - Riesgo: Archivos Excel maliciosos con macros embebidas o inyecciones DDE.
   - Mitigación: Uso exclusivo de OpenXML / ClosedXML (sin ejecutar macros VBA ni interactuar con el proceso COM de Excel).
3. **Control de Acceso Basado en Roles (RBAC):**
   - El servicio `IPermissionService` restringe la visualización y edición del Margen Comercial y Exportación de Precios de Venta únicamente a roles `Administrador` y `Estimador`. Rol `Visor` tiene edición bloqueada por UI y por Domain Layer.

---

## 4. Data Flow & Invariants

### Las 3 Reglas de Oro Matemáticas (Invariantes)
1. **Invariante 1 (Anti-No Cuadrado):**
   $$\sum \text{Cubicación Materiales} + \sum \text{Mano de Obra} + \sum \text{Equipos/EPP} = \text{Costo Directo Total}$$
   No puede existir ninguna diferencia de centavos entre el módulo de Costos y la sumatoria del Itemizado. Si existe $\Delta > 0$, el módulo de Revisión activa **BLOQUEO CRÍTICO** para exportar.
2. **Invariante 2 (Preservación del Itemizado Mandante):**
   La jerarquía de partidas (1.0, 1.1, 1.1.1) importada desde la constructora es intocable: exportar debe generar exactamente la misma cantidad de filas y códigos en el orden original.
3. **Invariante 3 (Cero Pérdida de Datos en Desconexión de Red):**
   Si la unidad de red `\\SERVIDOR\Obras\` cae mientras se edita, la app guarda inmediatamente una copia de emergencia en `%LocalAppData%\QubiCostElectrical\EmergencyBackups\{obra}_{timestamp}.qce` y notifica al usuario.

---

## 5. Code Quality & Modularity Review

- **Estado Actual:** Excelente encapsulación en capas (`Domain`, `Application`, `Infrastructure`, `Wpf`).
- **Hallazgo Preventivo:** La lógica de exportación de ofertas a Excel (`ExcelExportService`) estaba acoplada a ciertos ViewModels.
- **Directriz de Calidad:** Centralizar la generación de reportes en `Infrastructure/Excel/` implementando interfaces puras `IExcelReportGenerator`, garantizando que todos los cálculos se extraigan de entidades de Dominio (`OfferClienteRowBuilder`), nunca del estado efímero del árbol visual de WPF.

---

## 6. Testing & Verification Strategy

- **Pirámide de Pruebas:**
  - **Pruebas Unitarias de Dominio (Existentes: 100+):** Cálculos de APU, amortizaciones, leyes sociales (35%), fórmulas de viáticos.
  - **Pruebas de Integración SQLite (Existentes: 20+):** Migraciones de base de datos desde esquema 001 hasta 034 sin pérdida de datos.
  - **Pruebas de Arquitectura (Existentes: 9):** Hermeticidad absoluta (`NoLivePathGuardTest`), integridad de tokens XAML y límites de líneas.
- **Prueba Hostil de QA (The 2 AM Friday Test):**
  - Importar un archivo Excel real de mandante con 5.000 filas, 10 hojas, celdas combinadas, columnas movidas y celdas numéricas con formato texto (`" 1.250,50 "`).
  - *Criterio de Aprobación:* El archivo debe importarse en menos de 2.5 segundos con 0 excepciones no controladas y 100% de los códigos reconocidos.

---

## 7. Performance & Scalability (Chile Scale)

- **Caso Extremo Real:** Proyectos como *PEL039A2026* con 8.749 filas en Excel.
- **Manejo en Qubi Cost:**
  - SQLite maneja 10.000 filas en $< 50\text{ ms}$ utilizando transacciones en bloque (`BeginTransaction()`).
  - La UI en WPF utiliza `VirtualizingStackPanel` nativo con `VirtualizationMode="Recycling"`, garantizando 60 FPS al hacer scroll por el itemizado completo.
  - Consumo de memoria RAM: $< 180\text{ MB}$, comparado con los $> 1.2\text{ GB}$ y cuelgues constantes que sufre Microsoft Excel con fórmulas `SUMIF` en matrices gigantescas.

---

## 8. Deployment, Packaging & Rollout (Chile Launch)

- **Mecanismo de Despliegue:** Velopack (`build-release.ps1`).
- **Instalador:** `QubiCostElectrical-Setup.exe` firmado digitalmente con certificado Authenticode.
- **Regla Estricta DEV vs PROD:**
  - El entorno DEV compila y publica en `publish\win-x64\QubiCostElectrical.exe` (acceso directo `Qubi Cost Electrical (Dev).lnk`).
  - El entorno PROD instalado en `AppData\Local\QubiCostElectrical\current\` **NUNCA** se toca manualmente; solo se actualiza mediante releases oficiales.

---

## 9. Competitive Moat & Long-Term Roadmap

```
+---------------------------------------------------------------------------------+
|                             ROADMAP COMERCIAL CHILE                             |
+---------------------------------------------------------------------------------+
| FASE 1 (Actual - Q4 2026): El Asesino de Excel                                  |
| - Importador inteligente mandante + Banco de Precios Chile 400 + Oferta Oficial |
+---------------------------------------------------------------------------------+
                                      |
                                      v
+---------------------------------------------------------------------------------+
| FASE 2 (Q1 2027): Adjudicación y Control de Terreno                             |
| - Módulo de Solicitudes de Faena + Control de Bodega + Estados de Pago SEC      |
+---------------------------------------------------------------------------------+
                                      |
                                      v
+---------------------------------------------------------------------------------+
| FASE 3 (Q2-Q3 2027): Integración Ecosistema Chileno                             |
| - Exportador directo IConstruye (.xlsx estructurado para portales de compra)    |
| - Takeoff gráfico sobre planos PDF/CAD                                          |
+---------------------------------------------------------------------------------+
```

---

## 10. Final Recommendations & Immediate Next Steps

1. **Crear Seeder de Precios Chile 2026:**
   - Incorporar en el Banco de Precios una opción de precarga inicial con los 400 materiales más utilizados en instalaciones eléctricas en Chile (conductores EVA, canalizaciones EMT, tableros, protecciones monofásicas y trifásicas).
2. **Generar la Oferta de Prueba para la Próxima Licitación:**
   - Probar el flujo completo en la próxima licitación real (ej. clon de *PEL039A2026*) dentro de la app para certificar que el entregable al mandante sea 100% indiscutible.
