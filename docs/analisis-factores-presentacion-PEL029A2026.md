# Factores de presentación — análisis PEL029A2026

**Fuente:** `PEL029A2026 ABIERTO (1).xlsx` · hoja `-=COSTOS=-`  
**Producto:** Qubi Cost Electrical · pestaña Costos (coeficientes recién agregados)  
**Fecha:** 19 julio 2026

---

## 1. Qué problema resuelven

En el Excel de presupuesto eléctrico hay **dos capas de plata**:

1. **Costo** — lo que realmente cuesta ejecutar (materiales, equipos, mano de obra, GO).
2. **Presentación / venta** — el precio que se ofrece al cliente, más alto, armado con **multiplicadores por familia** (factores ×).

Los factores **no son** gastos generales ni utilidad. Son un markup de **precio de venta por tipo de material/equipo**. La diferencia entre venta y costo del proyecto aparece en el Excel como **«Diferencia por factores»**.

---

## 2. Valores reales en PEL029A2026

### 2.1 Factores de presentación (celda E194–E202)

| Excel FACTORES | Valor en este proyecto | Rol |
|---|---:|---|
| MATERIALES | **1,57×** | Conductores / materiales genéricos |
| EQUIPOS | **1,30×** | Equipos electromecánicos |
| TABLEROS | **1,30×** | Tableros |
| LUMINARIAS | **1,30×** | Iluminación |
| PVC / EPC | **1,57×** | Canalización |
| VARIOS | **1,30×** | Accesorios / varios |
| ARTEFACTOS | **1,30×** | Enchufes / artefactos |
| MOVIMIENTOS | **1,30×** | Movimiento de equipos |
| COMISIONAMIENTO | **1,30×** | Pruebas y ensayos |

> Nota: en otros proyectos (p. ej. PEL019) los tipicos eran 1,50 / 1,44 / 1,10 / 1,17 / 1,12…  
> **Los factores son por proyecto**, no fijos de la app. El botón «Cargar típicos PEL» es solo un punto de partida.

### 2.2 Política comercial (F169–F171)

| Concepto | % | Base de cálculo (fórmula Excel) | Monto CLP |
|---|---:|---|---:|
| Gastos generales (adm. central) | **5 %** | `(Mat + MO + GO) × 5 %` → `H161×F169` | 28.892.349 |
| Utilidades | **8 %** | `(Mat + MO + GO) × 8 %` → `H161×F170` | 46.227.758 |
| Valor agregado sobre equipos | **3 %** | `Equipos × 3 %` → `H162×F171` | 5.037.882 |
| **Total GG + UT + VA** | | | **80.157.989** |

### 2.3 Puente costo → venta (números del archivo)

| Concepto | CLP | UF (aprox.) |
|---|---:|---:|
| Materiales (costo) | 292.927.299 | 7.222 |
| Equipos (costo) | 167.929.393 | 4.140 |
| MO directa + indirecta | ~236.857.500 | ~5.840 |
| Gastos operacionales | 48.062.181 | 1.185 |
| **Costo de ejecución** | **745.776.373** | **18.387** |
| + GG + UT + VA | +80.157.989 | +1.976 |
| **Costo total proyecto** | **825.934.362** | **20.363** |
| **Total venta (desde DETALLE)** | **870.514.944** | **21.462** |
| **Diferencia por factores** | **44.580.582** | **1.099** |
| Margen observado s/ venta | **14,33 %** | `1 − (costo ejecución UF / venta UF)` |

UF de referencia en hoja: **40.560**.

---

## 3. Cómo funciona la lógica en Excel (paso a paso)

```
┌─────────────────────────────────────────────────────────────┐
│  A. COSTOS NETOS                                            │
│     Mat + Equipos + MO + GO  =  Costo de ejecución          │
└───────────────────────────┬─────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────┐
│  B. POLÍTICA SOBRE COSTO                                    │
│     GG  = 5% × (Mat + MO + GO)     ← sin equipos            │
│     UT  = 8% × (Mat + MO + GO)     ← sin equipos            │
│     VA  = 3% × Equipos             ← solo equipos           │
│     Costo proyecto = Ejecución + GG + UT + VA               │
└───────────────────────────┬─────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────┐
│  C. FACTORES DE PRESENTACIÓN (paralelo)                     │
│     En el armado del itemizado / detalle:                   │
│     Precio familia ≈ Costo neto familia × Factor ×          │
│                      (1 − descuentos de esa familia)        │
│     La suma de precios presentados = Venta del presupuesto  │
└───────────────────────────┬─────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────┐
│  D. CIERRE                                                  │
│     Diferencia por factores = Venta − Costo proyecto        │
│     Margen s/ venta = 1 − (Costo ejecución / Venta)         │
│     (el margen aquí es RESULTADO, no un % que fija la venta)│
└─────────────────────────────────────────────────────────────┘
```

### Ideas clave

1. **Los factores viven en la capa de precio unitario de venta**, no en la suma de costos de la hoja COSTOS.
2. **GG y Utilidad NO usan el mismo denominador que VA.** GG/UT excluyen equipos; VA solo mira equipos.
3. **El margen 14,33 % de este archivo no es un input de política** como el 10 % del mockup; se **mide** comparando venta (DETALLE) vs costo de ejecución.
4. Hay un bloque aparte de **descuentos por familia** (celdas, trafo, PVC, conductores…) que reduce el precio antes o junto al factor.

---

## 4. Cómo está hoy en Qubi Cost Electrical

### 4.1 Ya existe (y se reutiliza)

| Pieza Excel | Pieza app | Estado |
|---|---|---|
| Factor × por familia | `session.SetFamilyMarkup` / `GetFamilyMarkup` → `MarkupFactor_{familia}` | Existe |
| Aplicación a partidas | `CubicacionRow.GgUtFactor` · `PrecioUnitClp × GgUtFactor` | Existe |
| UI APU → Coeficientes | Panel de markups en APU | Existe |
| UI Costos (nuevo) | Sección «Coeficientes de sobreventa» | Recién agregado |
| GG % | `ProjectInfo.GastosGeneralesPct` + `MarkupPolicy` | Existe |
| Utilidad / margen | `ProjectInfo.UtilidadPct` + `MarkupMethod` (SobreVenta / SobreCosto) | Existe |
| VA equipos | `ValorAgregadoEquiposPct` (nuevo) | Existe |

### 4.2 Cómo el motor aplica el factor hoy

```
Costo neto línea (CLP)  = Cantidad × (MatClp + MoClp)     // sin factor
Precio venta línea (UF) = PrecioUnitClp × GgUtFactor / UF
Venta agregada          = Σ (precio venta línea)          // si hay factores ≠ 1
```

Cuando **alguna** partida tiene `GgUtFactor ≠ 1`, el motor marca *row-level markups* y la utilidad global deja de forzarse con el % de política: la venta sale de la suma valorizada con factores.

### 4.3 Mapeo Excel → familias Qubi

| Excel | Familia Qubi (markup) | Observación |
|---|---|---|
| MATERIALES | Conductores | A veces hace falta un factor «genérico materiales» si hay más categorías |
| PVC/EPC | Canalización | |
| TABLEROS | Tableros | |
| LUMINARIAS | Iluminación | |
| EQUIPOS | Equipos | |
| VARIOS / MOVIMIENTOS / COMISIONAMIENTO | Accesorios (u otras) | En Excel son 3 filas; en app suelen colapsar a Accesorios |
| ARTEFACTOS | Enchufes y tomacorrientes | |
| — | Protecciones, Puesta a tierra | Existen en app; no tienen fila propia en este Excel |

---

## 5. Brechas: Excel vs app (importante)

### Brecha A — Base de GG y Utilidad

| | Excel PEL029 | App actual |
|---|---|---|
| GG | 5 % de **Mat + MO + GO** (sin equipos) | % de **(costo directo + riesgo)** — **incluye equipos** |
| Utilidad | 8 % de **Mat + MO + GO** (aditiva) | Según método: SobreVenta sobre subtotal, o aditiva; y si hay factores, la venta ya viene “marcada” |
| VA | 3 % solo equipos | 3 % solo equipos ✓ |

**Impacto:** con los mismos %, el GG/UT de la app no replica el Excel si hay muchos equipos.

### Brecha B — Margen vs utilidad

En Excel el **margen s/ venta es un resultado** (14,33 % en este archivo).  
En el mockup y en el botón PEL de la app se usa **UtilidadPct = 10 % en modo SobreVenta** como *objetivo* (`venta ≈ costo / (1 − margen)`).

Son dos filosofías:

- **Excel real:** armo precios con factores → mido margen.
- **Motor SobreVenta:** fijo margen → derivo venta (cuando no hay markups por fila).

Cuando los factores están activos (`GgUtFactor ≠ 1`), la app se acerca más al Excel: **la venta la definen los factores**, no el % de utilidad.

### Brecha C — Descuentos por familia

El Excel tiene columnas de descuentos (celdas, trafo, PVC, conductores…).  
La app **aún no** tiene un espejo 1:1 de esos descuentos en Costos; hoy el factor ya puede absorber un descuento neto (`factor efectivo = factor × (1 − dscto)`).

### Brecha D — «Diferencia por factores» como línea de reporte

En Excel es una fila explícita del resumen.  
En la app conviene mostrar:

`Diferencia por factores ≈ Total venta − Costo proyecto (con GG/UT/VA)`

para que el estimador vea cuánto aportan los ×.

---

## 6. Cómo debe funcionar en la app (lógica objetivo)

### 6.1 Flujo operativo recomendado

1. Estimador carga **costos** (cubicación, APU, MO, GO, EPP).
2. En **Costos → Coeficientes** define factores × por familia (o «Cargar típicos PEL» y ajusta).
3. Al guardar, la app escribe `MarkupFactor_*` y sincroniza `GgUtFactor` en cubicación MAT.
4. Define **GG %, Utilidad/margen %, VA equipos %**.
5. El desglose muestra:
   - Costo directo (sin factores)
   - GG / VA / (utilidad o margen según método)
   - Total venta (con factores en precios de partida)
   - Opcional: **Diferencia por factores**

### 6.2 Reglas de cálculo alineadas a Excel (propuesta de paridad)

Para acercar PEL029:

1. **Factores:** `precio_presentacion_familia = costo_neto × factor` (ya vía `GgUtFactor`).
2. **GG:** aplicar sobre **Mat + MO + GO** (excluir equipos), no sobre todo el directo.
3. **Utilidades (Excel):** % aditivo sobre la misma base Mat+MO+GO.
4. **VA:** % solo sobre equipos (ya implementado).
5. **Venta:** preferir suma de partidas con factor (comportamiento actual con row-level markups).
6. **Margen mostrado:** siempre calcular `margen_real = (venta − costo_ejecución) / venta` como KPI (ya existe `MargenRealPct`).

### 6.3 Qué hacer con el botón «Cargar típicos PEL»

Debe cargar **valores tipicos editables**, no “la verdad” de todos los proyectos:

| Campo | Valor tipico sugerido | En PEL029 real |
|---|---:|---:|
| Materiales / Conductores | 1,50 | **1,57** |
| PVC / Canalización | 1,44 | **1,57** |
| Tableros | 1,10 | **1,30** |
| Luminarias | 1,17 | **1,30** |
| Equipos | 1,12 | **1,30** |
| Varios / Accesorios | 1,14 | **1,30** |
| Artefactos / Enchufes | 1,05 | **1,30** |
| GG | 5 % | 5 % |
| Utilidad aditiva Excel | 8 % | 8 % |
| VA equipos | 3 % | 3 % |
| Margen observado | (resultado) | **14,33 %** |

Ideal: un preset **«Desde este Excel»** no aplica; el usuario pega o ajusta los × del proyecto abierto.

---

## 7. Checklist de implementación (siguiente iteración)

### Ya hecho
- [x] UI de coeficientes en Costos
- [x] Persistencia de factores (misma fuente que APU)
- [x] Sync a `GgUtFactor`
- [x] Campo VA equipos + línea en desglose
- [x] Botones Reset / PEL / Aplicar

### Pendiente para paridad Excel
- [x] **Base GG/UT = Mat + MO + GO** (modo «Paridad Excel COSTOS»)
- [x] **Utilidad aditiva** vs **margen medido s/ venta** (labels en UI)
- [x] Línea de reporte **«Diferencia por factores»**
- [x] **Descuentos por familia** (% en Costos; factor efectivo = × × (1 − dscto))
- [x] **Preset del proyecto** (Guardar / Restaurar en `.qce`)
- [x] Presets **PEL029** y **Típicos**
- [x] Prueba de regresión con números PEL029

### Fuera de alcance (manual)
- [ ] Validación humana proyecto real vs Excel PEL029

---

## 8. Resumen en una frase

> En el Excel, los **factores de presentación multiplican el costo neto por familia para armar el precio de venta**; los **GG 5 % y utilidad 8 % se cargan solo sobre Mat+MO+GO**, el **VA 3 % solo sobre equipos**, y el **margen se mide al final** como diferencia entre la venta del detalle y el costo. En Qubi, los factores ya existen como markups de familia; para que “funcionen como el Excel” falta alinear la **base de GG/UT** y mostrar la **diferencia por factores**, no solo poner los × en pantalla.

---

## Anexo — Fórmulas clave leídas del archivo

```
H161 = H153 + H96 + H38          // GO + MO + materiales
H162 = H68                        // equipos
H169 = H161 * F169                // GG 5%
H170 = H161 * F170                // UT 8%
H171 = H162 * F171                // VA 3% equipos
H173 = SUM(H169:H171)
H181 = costo ejecución + H173     // costo proyecto
C188 = venta CLP desde DETALLE
E239 = C188 - H181                // diferencia por factores
D188 = 1 - (I164 / C187)          // margen s/ venta observado
```
