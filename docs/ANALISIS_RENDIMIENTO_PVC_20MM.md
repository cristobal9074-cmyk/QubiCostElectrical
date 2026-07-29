# Análisis de Rendimiento y Mano de Obra
## Instalación de Tubería PVC Conduit 20mm (3/4")

---

## 1. Descripción del APU

**Actividad:** Instalación de tubería PVC conduit 20mm (3/4") embutida en losa/muro
**Unidad:** ml (metro lineal)
**Cuadrilla base:** 1 Electricista + 1 Ayudante

---

## 2. Desglose de Materiales (por ml)

| Recurso | Código | Unidad | Cantidad | Unidad |
|---|---|---|---|---|
| Tubo PVC conduit 20mm (3/4") | TUBO-PVC-20 | ml | 1.03 | incluye 3% merma |
| Unión PVC 20mm (3/4") | UNION-PVC-20 | UN | 0.33 | 1 unión cada 3m |
| Pegamento PVC | PEG-PVC | UN | 0.01 | 1 tarro ~100 uniones |
| Cinta aislante | CINTA-AISL | UN | 0.02 | 1 rollo ~50 uniones |

---

## 3. Análisis de Rendimiento de Mano de Obra

### 3.1. Datos base del sector (Chile 2025-2026)

| Concepto | Valor | Fuente |
|---|---|---|
| Jornal electricista | $50,000 - $75,000/día | OneEstimate / mercado |
| Jornal ayudante | $30,000 - $40,000/día | OneEstimate / mercado |
| Horas por jornada | 8 h/día | Estándar Chile |
| Días por semana | 5 días | Estándar Chile |

### 3.2. Rendimiento según fuente

| Fuente | Rendimiento | Cuadrilla | Equiv. HH/ml |
|---|---|---|---|
| OneEstimate (blog) | 50-80 m/día | 1 electricista + 1 ayudante | 0.10 - 0.16 HH/ml |
| Tabla Scribd (cableado similar) | 40-60 m/día | Cuadrilla 19 (1 Of. + 1 Ay.) | 0.13 - 0.20 HH/ml |
| ApuTemplateSeeder (existing) | 0.04 HH/ml | Solo electricista | 0.04 HH/ml |

### 3.3. Cálculo de HH Unitarias (propuesto para obra estándar)

**Factor de corrección por condiciones estándar (interior, dificultad normal):**
- `FactorCompuesto = 1.0` (sin recargos)

**Cuadrilla: 1 Electricista + 1 Ayudante**

| Concepto | Cálculo | Valor |
|---|---|---|
| Rendimiento físico | 55 m/día (obra estándar) | |
| Horas cuadrilla/día | 2 personas × 8 h | 16 HH/día |
| HH por metro lineal | 16 HH ÷ 55 m | **0.29 HH/ml** |
| HH electricista | 8 h ÷ 55 m | 0.145 HH/ml |
| HH ayudante | 8 h ÷ 55 m | 0.145 HH/ml |

### 3.4. Costo de Mano de Obra (por ml)

| Recurso | HH/ml | Valor HH (UF)* | Total UF/ml |
|---|---|---|---|
| Electricista | 0.145 | 0.017 UF | 0.0025 UF |
| Ayudante | 0.145 | 0.010 UF | 0.0015 UF |
| **Total MO** | **0.290** | | **0.0040 UF** |

*Valor HH estimado: Electricista ~$55,000/día ÷ 8h = $6,875/h; Ayudante ~$35,000/día ÷ 8h = $4,375/h. UF ≈ $37,000 (referencia).

### 3.5. Comparativa con template existente

| Aspecto | Template actual (ApuTemplateSeeder) | Propuesto |
|---|---|---|
| HH electricista | 0.040 HH/ml | 0.145 HH/ml |
| HH ayudante | - | 0.145 HH/ml |
| Total HH | 0.040 HH/ml | 0.290 HH/ml |
| Cuadrilla | Solo electricista | Electricista + Ayudante |

> **Nota:** El template actual subestima la mano de obra porque considera solo HH del electricista y no incluye ayudante. En obra real, el ayudante es necesario para corte, preparación de mezcla, sujeción de tuberías, etc.

---

## 4. APU Completo (Costo Total por ml)

### 4.1. Materiales

| Recurso | Cantidad | Precio Unit (UF) | Total (UF) |
|---|---|---|---|
| Tubo PVC 20mm | 1.03 ml | 0.0035 | 0.0036 |
| Unión PVC 20mm | 0.33 UN | 0.0008 | 0.0003 |
| Pegamento PVC | 0.01 UN | 0.0020 | 0.0000 |
| Cinta aislante | 0.02 UN | 0.0005 | 0.0000 |
| **Total Materiales** | | | **0.0039 UF** |

### 4.2. Mano de Obra

| Recurso | HH | Valor HH (UF) | Total (UF) |
|---|---|---|---|
| Electricista | 0.145 | 0.017 | 0.0025 |
| Ayudante | 0.145 | 0.010 | 0.0015 |
| **Total MO** | | | **0.0040 UF** |

### 4.3. Resumen

| Componente | Directo (UF) | % |
|---|---|---|
| Materiales | 0.0039 | 39% |
| Mano de Obra | 0.0040 | 40% |
| Herramientas (3%) | 0.0002 | 2% |
| Merma y transporte (5%) | 0.0004 | 4% |
| **Costo Directo** | **0.0085 UF** | **85%** |
| Imprevistos (2%) | 0.0002 | 2% |
| Gastos Generales (15%) | 0.0013 | 13% |
| **Precio Unitario Venta** | **0.0100 UF** | **100%** |

### 4.4. Equivalencia en CLP

| Concepto | UF | CLP ($) |
|---|---|---|
| Costo Directo | 0.0085 | ~$315 |
| Precio Venta | 0.0100 | ~$370 |
| Por 100 ml | 1.00 | ~$37,000 |
| Por 1000 ml | 10.00 | ~$370,000 |

(UF referencial: $37,000 CLP)

---

## 5. Factores de Ajuste (Condiciones Laborales)

La estructura `ApuCondicionesLaboralesRow` permite ajustar el costo MO según:

| Condición | Factor | Ejemplo aplicación |
|---|---|---|
| **Dificultad Normal** | 1.00 | Losa estándar, recorridos rectos |
| **Dificultad Alta** | 1.15 | Muchas curvas, pasadas por vigas |
| **Dificultad Extrema** | 1.30 | Muro existente, canalización retorcida |
| **Altura** (+12%) | 1.12 | Instalación en cielo falso >3m |
| **Subterráneo** (+8%) | 1.08 | Canalización subterránea, zanja |
| **Fachada** (+10%) | 1.10 | Exterior, con andamio |
| **Nocturno** (+25%) | 1.25 | Trabajo fuera de horario laboral |
| **Edificio en operación** (+20%) | 1.20 | Hospital, oficina funcionando |

**Ejemplo: instalación en fachada con dificultad alta**
```
FactorCompuesto = 1.10 (fachada) × 1.15 (alta dificultad) = 1.265
Costo MO ajustado = 0.0040 UF × 1.265 = 0.0051 UF/ml
```

---

## 6. Mapeo al Código Existente

### 6.1. Template actual (ApuTemplateSeeder.cs:49-52)

```csharp
yield return T("apu-tubo-pvc-34", "Tubería PVC 3/4\"", "Tuberías", "ml", now,
    Mat("TUBO-PVC-34", "Tubo PVC conduit 3/4\"", "ml", 1.03m),
    Mat("UNION-PVC-34", "Unión PVC 3/4\"", "UN", 0.20m),
    Mo("Electricista", 0.04m));
```

### 6.2. Template propuesto (con ayudante)

```csharp
yield return T("apu-tubo-pvc-20", "Tubería PVC 20mm (3/4\")", "Tuberías", "ml", now,
    Mat("TUBO-PVC-20", "Tubo PVC conduit 20mm", "ml", 1.03m),
    Mat("UNION-PVC-20", "Unión PVC 20mm", "UN", 0.33m),
    Mat("PEG-PVC", "Pegamento PVC", "UN", 0.01m),
    Mo("Electricista", 0.145m),
    Mo("Ayudante eléctrico", 0.145m));
```

---

## 7. Conclusión

El análisis muestra que:

1. **Rendimiento real estimado:** ~55 m/día para cuadrilla de 2 personas (0.29 HH/ml)
2. **Costo MO real:** ~40% del costo directo total (0.0040 UF/ml)
3. **Template actual insuficiente:** solo considera 0.04 HH/ml sin ayudante (~14% de lo real)
4. **Ajuste por condiciones:** puede variar ±30% según ubicación y dificultad
5. **La app ya soporta todo esto:** `ApuLaborEngine`, `ApuCondicionesLaboralesRow`, `ApuRow.Recursos` con tipo MO

---

*Documento generado con datos de referencia del sector construcción Chile 2025-2026.*
*Los precios son referenciales y deben ajustarse según proveedor y proyecto específico.*
