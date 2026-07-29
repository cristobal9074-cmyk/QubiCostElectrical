from pathlib import Path
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.lib.colors import HexColor, white
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY

out = Path(__file__).with_name("analisis-factores-presentacion-PEL029A2026.pdf")
doc = SimpleDocTemplate(
    str(out),
    pagesize=A4,
    leftMargin=1.8 * cm,
    rightMargin=1.8 * cm,
    topMargin=1.6 * cm,
    bottomMargin=1.8 * cm,
    title="Factores de presentacion PEL029A2026",
    author="Qubi Cost Electrical",
)

navy = HexColor("#101C30")
blue = HexColor("#2563B0")
ink = HexColor("#101A26")
muted = HexColor("#5C6A7C")
line_color = HexColor("#E2E8F0")
soft = HexColor("#EDF3FA")
amber = HexColor("#FBF1DD")
green = HexColor("#EAF8EF")

styles = getSampleStyleSheet()
styles.add(ParagraphStyle(
    name="CoverTitle", fontName="Helvetica-Bold", fontSize=22, leading=26,
    textColor=white, alignment=TA_CENTER, spaceAfter=10,
))
styles.add(ParagraphStyle(
    name="CoverSub", fontName="Helvetica", fontSize=11, leading=15,
    textColor=HexColor("#C7D7F0"), alignment=TA_CENTER, spaceAfter=6,
))
styles.add(ParagraphStyle(
    name="H1c", fontName="Helvetica-Bold", fontSize=14, leading=18,
    textColor=navy, spaceBefore=14, spaceAfter=8,
))
styles.add(ParagraphStyle(
    name="H2c", fontName="Helvetica-Bold", fontSize=11.5, leading=15,
    textColor=blue, spaceBefore=10, spaceAfter=5,
))
styles.add(ParagraphStyle(
    name="Bodyc", fontName="Helvetica", fontSize=9.5, leading=13,
    textColor=ink, alignment=TA_JUSTIFY, spaceAfter=6,
))
styles.add(ParagraphStyle(
    name="Bulletc", fontName="Helvetica", fontSize=9.5, leading=13,
    textColor=ink, leftIndent=10, spaceAfter=3,
))
styles.add(ParagraphStyle(
    name="Notec", fontName="Helvetica", fontSize=9, leading=12,
    textColor=HexColor("#1e40af"), spaceAfter=6,
))
styles.add(ParagraphStyle(
    name="Codec", fontName="Courier", fontSize=8, leading=11,
    textColor=ink, spaceAfter=4,
))
styles.add(ParagraphStyle(
    name="Small", fontName="Helvetica", fontSize=8.5, leading=11, textColor=muted,
))
styles.add(ParagraphStyle(name="Th", fontName="Helvetica-Bold", fontSize=8, leading=10, textColor=navy))
styles.add(ParagraphStyle(name="Td", fontName="Helvetica", fontSize=8, leading=10, textColor=ink))

story = []

cover = Table([
    [Paragraph("QUBI COST ELECTRICAL", styles["CoverSub"])],
    [Paragraph("Factores de presentacion", styles["CoverTitle"])],
    [Paragraph(
        "Analisis de logica Excel PEL029A2026<br/>y como debe funcionar en la aplicacion",
        styles["CoverSub"],
    )],
    [Spacer(1, 8)],
    [Paragraph(
        "Fuente: PEL029A2026 ABIERTO (1).xlsx · hoja -=COSTOS=-<br/>19 julio 2026",
        styles["CoverSub"],
    )],
], colWidths=[16 * cm])
cover.setStyle(TableStyle([
    ("BACKGROUND", (0, 0), (-1, -1), navy),
    ("TOPPADDING", (0, 0), (-1, -1), 10),
    ("BOTTOMPADDING", (0, 0), (-1, -1), 10),
    ("LEFTPADDING", (0, 0), (-1, -1), 16),
    ("RIGHTPADDING", (0, 0), (-1, -1), 16),
    ("ALIGN", (0, 0), (-1, -1), "CENTER"),
]))
story.append(cover)
story.append(Spacer(1, 14))

story.append(Paragraph("1. Que problema resuelven", styles["H1c"]))
story.append(Paragraph(
    "En el Excel de presupuesto electrico hay <b>dos capas</b>: (1) <b>Costo</b> — lo que cuesta "
    "ejecutar (materiales, equipos, mano de obra, GO); (2) <b>Presentacion / venta</b> — el precio "
    "al cliente, armado con <b>multiplicadores por familia</b> (factores x). Los factores <b>no son</b> "
    "gastos generales ni utilidad: son markup de precio de venta. La diferencia aparece en Excel "
    "como <b>«Diferencia por factores»</b>.",
    styles["Bodyc"],
))

story.append(Paragraph("2. Valores reales en PEL029A2026", styles["H1c"]))
story.append(Paragraph("2.1 Factores de presentacion (E194–E202)", styles["H2c"]))


def tbl(headers, rows, widths):
    data = [[Paragraph(h, styles["Th"]) for h in headers]]
    for r in rows:
        data.append([Paragraph(str(c), styles["Td"]) for c in r])
    t = Table(data, colWidths=widths, repeatRows=1)
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), soft),
        ("GRID", (0, 0), (-1, -1), 0.4, line_color),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [white, HexColor("#F8FAFC")]),
    ]))
    return t


story.append(tbl(
    ["Excel FACTORES", "Valor PEL029", "Familia Qubi"],
    [
        ["MATERIALES", "1,57x", "Conductores"],
        ["EQUIPOS", "1,30x", "Equipos"],
        ["TABLEROS", "1,30x", "Tableros"],
        ["LUMINARIAS", "1,30x", "Iluminacion"],
        ["PVC / EPC", "1,57x", "Canalizacion"],
        ["VARIOS", "1,30x", "Accesorios"],
        ["ARTEFACTOS", "1,30x", "Enchufes"],
        ["MOVIMIENTOS", "1,30x", "Accesorios / movimientos"],
        ["COMISIONAMIENTO", "1,30x", "Accesorios / ensayos"],
    ],
    [5.2 * cm, 3.2 * cm, 7.6 * cm],
))
story.append(Spacer(1, 6))
story.append(Paragraph(
    "<b>Nota:</b> en otros proyectos (p. ej. PEL019) los tipicos eran 1,50 / 1,44 / 1,10 / 1,17 / 1,12… "
    "Los factores son <b>por proyecto</b>. El boton «Cargar tipicos PEL» es solo punto de partida.",
    styles["Notec"],
))

story.append(Paragraph("2.2 Politica comercial (F169–F171)", styles["H2c"]))
story.append(tbl(
    ["Concepto", "%", "Base Excel", "Monto CLP"],
    [
        ["Gastos generales (adm. central)", "5 %", "(Mat + MO + GO) x 5%", "28.892.349"],
        ["Utilidades", "8 %", "(Mat + MO + GO) x 8%", "46.227.758"],
        ["VA sobre equipos", "3 %", "Equipos x 3%", "5.037.882"],
        ["Total GG + UT + VA", "", "", "80.157.989"],
    ],
    [5.5 * cm, 1.8 * cm, 4.5 * cm, 4.2 * cm],
))

story.append(Paragraph("2.3 Puente costo → venta", styles["H2c"]))
story.append(tbl(
    ["Concepto", "CLP", "UF"],
    [
        ["Materiales (costo)", "292.927.299", "7.222"],
        ["Equipos (costo)", "167.929.393", "4.140"],
        ["MO directa + indirecta", "~236.857.500", "~5.840"],
        ["Gastos operacionales", "48.062.181", "1.185"],
        ["Costo de ejecucion", "745.776.373", "18.387"],
        ["+ GG + UT + VA", "+80.157.989", "+1.976"],
        ["Costo total proyecto", "825.934.362", "20.363"],
        ["Total venta (DETALLE)", "870.514.944", "21.462"],
        ["Diferencia por factores", "44.580.582", "1.099"],
        ["Margen observado s/ venta", "14,33 %", "resultado"],
    ],
    [7 * cm, 5 * cm, 4 * cm],
))
story.append(Paragraph("UF de referencia en hoja: <b>40.560</b>.", styles["Small"]))

story.append(Paragraph("3. Como funciona la logica en Excel", styles["H1c"]))
story.append(Paragraph(
    "<b>A. Costos netos</b> — Mat + Equipos + MO + GO = Costo de ejecucion.",
    styles["Bulletc"],
))
story.append(Paragraph(
    "<b>B. Politica sobre costo</b> — GG 5% y UT 8% sobre <b>Mat+MO+GO (sin equipos)</b>; "
    "VA 3% <b>solo sobre equipos</b>. Costo proyecto = Ejecucion + GG + UT + VA.",
    styles["Bulletc"],
))
story.append(Paragraph(
    "<b>C. Factores de presentacion (en paralelo)</b> — al armar el itemizado/detalle: "
    "Precio familia ≈ Costo neto x Factor x (1 − descuentos). La suma = Venta.",
    styles["Bulletc"],
))
story.append(Paragraph(
    "<b>D. Cierre</b> — Diferencia por factores = Venta − Costo proyecto. "
    "Margen s/ venta = 1 − (Costo ejecucion / Venta). En Excel el margen es <b>RESULTADO</b>, "
    "no un % que fija la venta.",
    styles["Bulletc"],
))
story.append(Spacer(1, 4))
story.append(Paragraph(
    "Los factores viven en la capa de <b>precio unitario de venta</b>, no en la suma de costos "
    "de la hoja COSTOS. GG/UT no usan el mismo denominador que VA.",
    styles["Bodyc"],
))

story.append(Paragraph("4. Como esta hoy en Qubi Cost", styles["H1c"]))
story.append(Paragraph("4.1 Piezas que ya existen", styles["H2c"]))
story.append(tbl(
    ["Excel", "App", "Estado"],
    [
        ["Factor x familia", "SetFamilyMarkup / MarkupFactor_*", "Existe"],
        ["Aplicacion a partidas", "CubicacionRow.GgUtFactor", "Existe"],
        ["UI APU coeficientes", "Panel markups APU", "Existe"],
        ["UI Costos", "Seccion coeficientes (nueva)", "Hecho"],
        ["GG %", "GastosGeneralesPct + MarkupPolicy", "Existe"],
        ["Utilidad / margen", "UtilidadPct + MarkupMethod", "Existe"],
        ["VA equipos", "ValorAgregadoEquiposPct", "Hecho"],
    ],
    [4.5 * cm, 7 * cm, 4.5 * cm],
))

story.append(Paragraph("4.2 Motor actual del factor", styles["H2c"]))
story.append(Paragraph(
    "Costo neto linea = Cantidad x (MatClp + MoClp) <b>sin factor</b>. "
    "Precio venta linea = PrecioUnitClp x GgUtFactor / UF. "
    "Si alguna partida tiene factor ≠ 1, el motor marca row-level markups y la venta agregada "
    "sale de esa suma valorizada (la utilidad global % deja de forzar la venta).",
    styles["Bodyc"],
))

story.append(Paragraph("5. Brechas Excel vs app", styles["H1c"]))
gap = Table([
    [Paragraph(
        "<b>Brecha A — Base GG/UT</b><br/>Excel: 5%/8% sobre Mat+MO+GO (sin equipos).<br/>"
        "App: % sobre (costo directo + riesgo), que <b>incluye equipos</b>.",
        styles["Td"],
    )],
    [Paragraph(
        "<b>Brecha B — Margen vs utilidad</b><br/>Excel: margen 14,33% es medido. "
        "App SobreVenta: % como objetivo venta=costo/(1−m). Con factores activos, "
        "la app se acerca al Excel (venta la definen los x).",
        styles["Td"],
    )],
    [Paragraph(
        "<b>Brecha C — Descuentos por familia</b><br/>Excel tiene dsctos (celdas, trafo, PVC…). "
        "App aun no; se pueden absorber en el factor efectivo.",
        styles["Td"],
    )],
    [Paragraph(
        "<b>Brecha D — Linea «Diferencia por factores»</b><br/>Excel la muestra en resumen. "
        "App deberia reportar: Venta − Costo proyecto (con GG/UT/VA).",
        styles["Td"],
    )],
], colWidths=[16 * cm])
gap.setStyle(TableStyle([
    ("BACKGROUND", (0, 0), (-1, 0), amber),
    ("BACKGROUND", (0, 1), (-1, 1), soft),
    ("BACKGROUND", (0, 2), (-1, 2), amber),
    ("BACKGROUND", (0, 3), (-1, 3), soft),
    ("BOX", (0, 0), (-1, -1), 0.6, line_color),
    ("LINEBELOW", (0, 0), (-1, -2), 0.4, line_color),
    ("TOPPADDING", (0, 0), (-1, -1), 7),
    ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
    ("LEFTPADDING", (0, 0), (-1, -1), 8),
    ("RIGHTPADDING", (0, 0), (-1, -1), 8),
]))
story.append(gap)

story.append(Paragraph("6. Como debe funcionar en la app (objetivo)", styles["H1c"]))
story.append(Paragraph(
    "1) Cargar costos (cubicacion, APU, MO, GO, EPP). "
    "2) En Costos → Coeficientes definir factores x (o tipicos PEL y ajustar). "
    "3) Al guardar: MarkupFactor_* + sync GgUtFactor en cubicacion MAT. "
    "4) Definir GG %, utilidad/margen %, VA equipos %. "
    "5) Desglose: costo directo (sin x), GG/VA/utilidad, total venta (con x), "
    "y opcionalmente <b>Diferencia por factores</b>.",
    styles["Bodyc"],
))

story.append(Paragraph("Reglas de paridad Excel propuestas", styles["H2c"]))
story.append(Paragraph("1. precio_presentacion = costo_neto x factor (via GgUtFactor).", styles["Bulletc"]))
story.append(Paragraph("2. GG sobre Mat + MO + GO (excluir equipos) — modo «Paridad Excel».", styles["Bulletc"]))
story.append(Paragraph("3. Utilidad aditiva Excel sobre la misma base Mat+MO+GO.", styles["Bulletc"]))
story.append(Paragraph("4. VA % solo sobre equipos (ya implementado).", styles["Bulletc"]))
story.append(Paragraph("5. Venta = suma de partidas con factor (row-level).", styles["Bulletc"]))
story.append(Paragraph("6. KPI margen_real = (venta − costo_ejecucion) / venta (ya existe).", styles["Bulletc"]))

story.append(Paragraph("7. Checklist", styles["H1c"]))
story.append(Paragraph(
    "<b>Hecho:</b> UI Costos, persistencia factores, sync GgUtFactor, VA equipos, botones Reset/PEL/Aplicar.",
    styles["Bulletc"],
))
story.append(Paragraph(
    "<b>Pendiente:</b> base GG/UT = Mat+MO+GO; separar utilidad aditiva vs margen medido; "
    "linea «Diferencia por factores»; descuentos o documentar absorcion en x; "
    "preset editable por proyecto; prueba regresion PEL029.",
    styles["Bulletc"],
))

story.append(Paragraph("8. Resumen", styles["H1c"]))
box = Table([[Paragraph(
    "En el Excel, los <b>factores de presentacion multiplican el costo neto por familia para armar "
    "el precio de venta</b>; los <b>GG 5% y utilidad 8% se cargan solo sobre Mat+MO+GO</b>; el "
    "<b>VA 3% solo sobre equipos</b>; y el <b>margen se mide al final</b>. En Qubi los factores ya "
    "existen como markups de familia; para que funcionen como el Excel falta alinear la "
    "<b>base de GG/UT</b> y mostrar la <b>diferencia por factores</b>, no solo poner los x en pantalla.",
    styles["Bodyc"],
)]], colWidths=[16 * cm])
box.setStyle(TableStyle([
    ("BACKGROUND", (0, 0), (-1, -1), green),
    ("BOX", (0, 0), (-1, -1), 1, HexColor("#15803D")),
    ("TOPPADDING", (0, 0), (-1, -1), 10),
    ("BOTTOMPADDING", (0, 0), (-1, -1), 10),
    ("LEFTPADDING", (0, 0), (-1, -1), 10),
    ("RIGHTPADDING", (0, 0), (-1, -1), 10),
]))
story.append(box)

story.append(Spacer(1, 10))
story.append(Paragraph("Anexo — Formulas clave del archivo", styles["H1c"]))
for formula in [
    "H161 = H153 + H96 + H38     // GO + MO + materiales",
    "H162 = H68                  // equipos",
    "H169 = H161 * F169          // GG 5%",
    "H170 = H161 * F170          // UT 8%",
    "H171 = H162 * F171          // VA 3% equipos",
    "H173 = SUM(H169:H171)",
    "H181 = costo ejecucion + H173",
    "C188 = venta CLP desde DETALLE",
    "E239 = C188 - H181          // diferencia por factores",
    "D188 = 1 - (I164 / C187)    // margen s/ venta observado",
]:
    story.append(Paragraph(formula.replace(" ", "&nbsp;"), styles["Codec"]))


def footer(canvas, doc_):
    canvas.saveState()
    canvas.setStrokeColor(line_color)
    canvas.setLineWidth(0.5)
    canvas.line(1.8 * cm, 1.2 * cm, A4[0] - 1.8 * cm, 1.2 * cm)
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(muted)
    canvas.drawString(1.8 * cm, 0.7 * cm, "Qubi Cost Electrical · Analisis factores PEL029A2026 · CONFIDENTIAL")
    canvas.drawRightString(A4[0] - 1.8 * cm, 0.7 * cm, f"Pag. {doc_.page}")
    canvas.restoreState()


doc.build(story, onFirstPage=footer, onLaterPages=footer)
print(out)
print("size", out.stat().st_size)
