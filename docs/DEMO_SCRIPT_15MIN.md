# Demo comercial 15 minutos - Qubi Cost Electrical

Script de venta: proyecto vacío → PDF de oferta. Versión de referencia `1.1.1`.

No improvisar menús. Etiquetas tomadas de la barra del shell.

| Grupo | Entradas |
|-------|----------|
| **Resumen** | Dashboard |
| **Proyecto** | Datos del proyecto, Antecedentes, Monedas e indicadores |
| **Itemizado** | Itemizado |
| **Cubicación** | Cubicación, Cubicación SC |
| **APU** | APU |
| **Precios** | Banco de precios, Cotizaciones, Lista de compra, Solicitudes |
| **Cierre** | Revisión final, Versiones, Órdenes de cambio, Oferta y exportación, Contratos |
| **Más** | Plantillas, Preferencias, … |

Cabecera: **Guardar**, **Revisar**. Login: **Iniciar sesión** + **PIN de acceso**.

Proyecto demo: nombre `Demo Qubi 15`, cliente visible, un `.qce` (no seis Excels).

---

## 00:00 Login + nuevo proyecto

- Abrir `QubiCostElectrical.exe`. **Iniciar sesión** con Admin o Estimador.
- **Resumen** o Ctrl+N: **Nuevo proyecto**.
- Diálogo **Nuevo proyecto**: **Nombre del proyecto** `Demo Qubi 15`, **Cliente**, **Ubicación**, moneda (UF/CLP), **Carpeta destino**. Confirmar.

Cierre de minuto: archivo `.qce` abierto, título de producto visible.

## 01:00 Datos del proyecto

- **Proyecto** → **Datos del proyecto**.
- Bloque **OBRA Y MANDANTE**: **NOMBRE DEL PROYECTO**, **CLIENTE / MANDANTE**, **UBICACIÓN DE LA OBRA**.
- **Guardar**.

## 02:00 Itemizado (2–3 partidas)

- Pestaña **Itemizado**.
- **+ Partida** dos o tres veces. Códigos y descripciones creíbles (ej. alumbrado, circuitos, tablero).
- Opcional: **+ Capítulo** para un título. No importar Excel en esta demo.

## 04:00 Cubicación: 1 recurso + fórmula

- **Cubicación** → **Cubicación**.
- Elegir la primera partida. **Agregar material** (o código con autocompletado).
- Vista **Casillas**: en cantidad escribir `10+2`, Enter. Resultado 12.
- Mencionar columna **CANT** y **Guardar**.

## 06:00 Opcional 90 s: Cubicación SC + Tomar

Si el interlocutor pide servicios comunes; si no, saltar a APU.

- **Cubicación** → **Cubicación SC**. Título **Cubicación de Servicios Comunes (C.SC)**.
- **+ Agregar tablero**. **+ Agregar circuito**. Un circuito alcanza.
- Volver a **Cubicación**. Fila activa. **Alt+L** (**Tomar de SC**).
- Diálogo **Tomar de cubicación SC**. **Tomar seleccionado**. Cantidad en la partida.

## 08:00 APU (glance)

- Pestaña **APU**.
- Mostrar materiales/MO de la partida. No armar APU completo.
- Frase: el APU nace de la cubicación; no se reescribe en otra planilla.

## 09:00 Revisión (bloqueantes)

- **Cierre** → **Revisión final**, o botón **Revisar** del header.
- Panel **Bloqueantes**. Si hay alguno trivial, **Ir a corregir**. No quedarse a limpiar todo.
- CTA de la pantalla: **Generar oferta y exportar →**.

## 10:00 Oferta y exportación → PDF

- **Cierre** → **Oferta y exportación**.
- Wizard: **1. Validar oferta** → **2. Configurar** (preset **Mandante** si hace falta) → **3. Vista previa** → **4. Exportar**.
- Acción **Oferta comercial** / botón **Exportar PDF**. Alternativa: **Exportar documentos seleccionados**.

Si pide Excel: misma pantalla, **Excel interno** / **Exportar Excel**.

## 12:00 Abrir PDF / carpeta

- Abrir el PDF generado (visor o Explorador).
- Mostrar portada con cliente y obra del paso 01:00.
- Toast de éxito «Exportar PDF» con ruta; si hay acción **Abrir carpeta**, usarla.

## 13:00 Roles (una frase)

- **Más** → **Preferencias** → **Usuarios**.
- «El **Visor** consulta el `.qce` y no edita ni exporta. El **Estimador** cubica y guarda. Un **Administrador** crea las cuentas.»
- Un .qce en carpeta de red = 1 editor; el resto consulta. No Google Docs.
- No crear usuarios en vivo salvo que pidan el onboarding de 20 min (`docs/FIRST_RUN_TEAM_ONBOARDING.md`).

## 14:00 Cierre

Frase de cierre, literal:

> Un archivo `.qce`. No seis Excels.

Recordar: Itemizado, cubicación, APU, revisión y oferta viven en el mismo archivo. Guardar con **Guardar**. Cerrar con **Cerrar proyecto** o **Cerrar sesión**.

---

## Si algo falla (no improvisar)

| Síntoma | Qué hacer en demo |
|---------|-------------------|
| Login | PIN Admin de la máquina de demo, preparado antes |
| Export bloqueado | **Revisión final**: leer **Bloqueantes**; no pelear con el wizard |
| Alt+L vacío | Saltar SC; el PDF no depende de C.SC |
| Visor en la máquina | No usar Visor para la demo de edición |

Tiempo duro: 15:00. Mejor PDF a los 12 min que SC incompleto a los 16.
