import os
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch

pdf_path = r"C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1\analisis_factores_presentacion_presupuestos.pdf"

doc = SimpleDocTemplate(
    pdf_path,
    pagesize=letter,
    rightMargin=54,
    leftMargin=54,
    topMargin=54,
    bottomMargin=54
)

styles = getSampleStyleSheet()

# Colors
primary_color = colors.HexColor('#0F172A')
accent_blue = colors.HexColor('#1D4ED8')
text_dark = colors.HexColor('#334155')
bg_light = colors.HexColor('#F8FAFC')
border_gray = colors.HexColor('#E2E8F0')

# Text Styles
title_style = ParagraphStyle(
    'TitleStyle',
    parent=styles['Normal'],
    fontName='Helvetica-Bold',
    fontSize=22,
    leading=26,
    textColor=primary_color,
    spaceAfter=6
)

subtitle_style = ParagraphStyle(
    'SubtitleStyle',
    parent=styles['Normal'],
    fontName='Helvetica',
    fontSize=11,
    leading=15,
    textColor=colors.HexColor('#64748B'),
    spaceAfter=25
)

h1_style = ParagraphStyle(
    'H1Style',
    parent=styles['Normal'],
    fontName='Helvetica-Bold',
    fontSize=14,
    leading=18,
    textColor=accent_blue,
    spaceBefore=15,
    spaceAfter=8,
    keepWithNext=True
)

h2_style = ParagraphStyle(
    'H2Style',
    parent=styles['Normal'],
    fontName='Helvetica-Bold',
    fontSize=11,
    leading=15,
    textColor=primary_color,
    spaceBefore=10,
    spaceAfter=6,
    keepWithNext=True
)

body_style = ParagraphStyle(
    'BodyStyle',
    parent=styles['Normal'],
    fontName='Helvetica',
    fontSize=9.5,
    leading=13.5,
    textColor=text_dark,
    spaceAfter=10
)

bullet_style = ParagraphStyle(
    'BulletStyle',
    parent=body_style,
    leftIndent=15,
    firstLineIndent=-10,
    spaceAfter=6
)

box_style = ParagraphStyle(
    'BoxStyle',
    parent=styles['Normal'],
    fontName='Helvetica-Oblique',
    fontSize=9,
    leading=13,
    textColor=colors.HexColor('#1E293B'),
    backColor=colors.HexColor('#F0FDF4'),
    borderColor=colors.HexColor('#DCFCE7'),
    borderWidth=1,
    borderPadding=10,
    spaceAfter=12
)

table_hdr = ParagraphStyle('TblHdr', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8.5, leading=11, textColor=colors.white)
table_cell = ParagraphStyle('TblCell', parent=styles['Normal'], fontName='Helvetica', fontSize=8.5, leading=11, textColor=text_dark)

story = []

# Title & Metadata
story.append(Paragraph("Qubi Cost Electrical — Informe de Ingeniería de Costos", subtitle_style))
story.append(Paragraph("Análisis de Factores de Presentación y Cierre en Presupuestos", title_style))
story.append(Paragraph("Estudio sobre la distribución de markups, desbalanceo de ofertas y prácticas de la industria", subtitle_style))
story.append(Spacer(1, 10))

# 1. ¿Qué son y por qué se usan?
story.append(Paragraph("1. ¿Qué son los Factores de Presentación y por qué se usan?", h1_style))
story.append(Paragraph(
    "En los presupuestos de construcción y montajes industriales, el <b>Costo Directo Base</b> representa el valor neto "
    "de adquisición de materiales y el pago nominal de la mano de obra. Sin embargo, para presentar la oferta comercial final al cliente "
    "(el <b>Precio de Venta</b>), el estimador debe cargar los gastos indirectos, utilidades, riesgos y costos de financiamiento.",
    body_style
))
story.append(Paragraph(
    "En lugar de aplicar un único factor global o un recargo visible al final de la cotización, se utilizan los "
    "<b>Factores de Presentación</b>. Estos son multiplicadores específicos aplicados directamente a nivel de recursos, "
    "partidas o familias en los Análisis de Precios Unitarios (APU). Se utilizan principalmente por tres razones críticas:",
    body_style
))

story.append(Paragraph(
    "• <b>Ocultamiento de Márgenes Comerciales (Hidden Markups):</b> Los mandantes suelen auditar los APU línea por línea. "
    "Si un presupuesto muestra abiertamente una utilidad del 15% o Gastos Generales del 20% al final, el cliente ejercerá fuerte presión "
    "para reducirlos. Al prorratear estos indirectos dentro de los precios unitarios (ej. multiplicando el costo base de conductores por 1.54), "
    "el margen queda enmascarado como parte del costo directo del material, dificultando su negociación a la baja.",
    bullet_style
))
story.append(Paragraph(
    "• <b>Desbalanceo de Ofertas (Unbalanced Bidding):</b> Permite cargar mayor margen en partidas que el contratista sabe "
    "(por experiencia u omisiones en planos) que van a aumentar de volumen durante la obra (ej. canalizaciones o excavaciones), "
    "y cargar menor margen en partidas fijas o de alto costo (como los transformadores o generadores). Esto maximiza la utilidad "
    "mediante órdenes de cambio futuras y mejora el flujo de caja temprano (Front-Loading).",
    bullet_style
))
story.append(Paragraph(
    "• <b>Asignación Diferenciada de Riesgo:</b> Ciertos materiales o trabajos conllevan mayor riesgo de merma, volatilidad "
    "de precios, complejidad de montaje o demoras de importación. Por ello, el estimador aplica un factor de presentación más alto "
    "(ej. 1.54 para PVC/Canalizaciones por mermas y accesorios omitidos) que para equipos pre-cotizados estables (ej. 1.12 para Equipos o 1.10 para Tableros).",
    bullet_style
))

# Highlight Box
story.append(Paragraph(
    "<b>Regla General del Estimador:</b> 'El precio final presentado al cliente no debe revelar el costo real de adquisición de la empresa, "
    "sino reflejar el valor de mercado estratégico que la empresa decide ofertar para ganar la licitación resguardando su rentabilidad.'",
    box_style
))
story.append(Spacer(1, 10))

# 2. Análisis Comparativo en la Industria y Software Especializado
story.append(Paragraph("2. Prácticas en la Industria y Software de Presupuestos", h1_style))
story.append(Paragraph(
    "La práctica de redistribuir gastos generales y utilidades en los APU no es exclusiva de hojas Excel; es una funcionalidad "
    "core en los sistemas líderes de gestión de estimaciones a nivel mundial:",
    body_style
))

# Table comparing software
soft_data = [
    [Paragraph("Software / Metodología", table_hdr), Paragraph("Nombre del Concepto", table_hdr), Paragraph("Funcionamiento / Implementación", table_hdr)],
    [Paragraph("<b>Candy CCS</b><br/>(Líder en Minería e Infraestructura)", table_cell), Paragraph("Markup Allocation / Spread", table_cell), Paragraph("Permite definir los indirectos y 'esparcirlos' de forma ponderada o manual sobre recursos específicos, manteniendo el costo real oculto para reportes del cliente.", table_cell)],
    [Paragraph("<b>WinEst (Trimble)</b> / <b>Sage Estimating</b>", table_cell), Paragraph("Indirect Cost WBS Allocation", table_cell), Paragraph("Permite aplicar markups por categorías de base de datos (Material, Mano de Obra, Equipos) y distribuirlos proporcionalmente en los ítems del APU.", table_cell)],
    [Paragraph("<b>Presto</b><br/>(Líder en España y Latam)", table_cell), Paragraph("Coeficientes de Paso", table_cell), Paragraph("Aplica factores multiplicadores a los precios de costo para generar el 'Presupuesto de Venta' que se entrega formalmente, manteniendo la base de costo limpia.", table_cell)],
    [Paragraph("<b>Estimación Pública (MOP / Serviu)</b>", table_cell), Paragraph("Factor de Prorrateo de Indirectos", table_cell), Paragraph("Exige desglosar los APU estrictamente bajo costos directos. Los estimadores aplican los factores de presentación internamente antes de ingresar los datos al portal del mandante.", table_cell)]
]

t1 = Table(soft_data, colWidths=[2.2*inch, 1.8*inch, 3.0*inch])
t1.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), accent_blue),
    ('ALIGN', (0,0), (-1,-1), 'LEFT'),
    ('BOTTOMPADDING', (0,0), (-1,0), 6),
    ('GRID', (0,0), (-1,-1), 0.5, border_gray),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, bg_light]),
    ('VALIGN', (0,0), (-1,-1), 'TOP'),
    ('TOPPADDING', (0,0), (-1,-1), 6),
    ('BOTTOMPADDING', (0,0), (-1,-1), 6),
]))
story.append(t1)

story.append(PageBreak())

# Section 3: How it functions in Qubi Cost (Excel Analysis)
story.append(Paragraph("3. Funcionamiento de Factores en Qubi Cost Electrical", h1_style))
story.append(Paragraph(
    "En la plantilla del proyecto <b>PEL019G2026</b>, la combinación de factores de presentación y la regla de exclusión "
    "de equipos opera como un sistema de doble filtro comercial:",
    body_style
))

story.append(Paragraph("A. El Rol del Multiplicador de Mano de Obra (Factor M/O = 10.58)", h2_style))
story.append(Paragraph(
    "En la lista de precios y APU, la Mano de Obra se cubica a partir del costo diario nominal de una pareja de montaje. "
    "Sin embargo, el precio que se presenta al cliente se multiplica por un factor de <b>10.58</b> (o hasta <b>19.82</b> según la zona y leyes sociales). "
    "Este factor no es solo utilidad, sino que integra los siguientes costos indirectos:",
    body_style
))
story.append(Paragraph(
    "1. <b>Leyes Sociales e Imposiciones:</b> Representa entre un 30% y un 65% adicional sobre el salario base líquido.<br/>"
    "2. <b>Rendimiento y Tiempos Muertos:</b> Pérdidas por traslados, charlas de seguridad, condiciones climáticas o coordinaciones de obra.<br/>"
    "3. <b>Supervisión y Herramientas:</b> Carga la cuota de capataces, herramientas de mano (3-5%) y elementos de protección personal (EPP).",
    bullet_style
))

story.append(Paragraph("B. Regla de Exclusión de Equipos Mayores en Gastos Generales (5%) y Utilidad (8%)", h2_style))
story.append(Paragraph(
    "El presupuesto distingue claramente entre partidas de instalación (Materiales y M/O) y el suministro de Equipos mayores. "
    "La regla financiera implementada en la celda <code>H122</code> y <code>H123</code> calcula el margen sobre:",
    body_style
))
story.append(Paragraph(
    "$$\\text{Base Imponible} = \\text{Costo Directo Total} - \\text{Costo Equipos Mayor}$$",
    body_style
))
story.append(Paragraph(
    "• Los <b>Gastos Generales (5%)</b> y la <b>Utilidad (8%)</b> se aplican exclusivamente sobre esta Base Imponible.<br/>"
    "• Para los <b>Equipos</b>, se aplica un factor reducido del <b>3% (Valor Agregado s/ Equipos)</b>. Esto se hace por dos razones comerciales:",
    body_style
))
story.append(Paragraph(
    "1. <b>Competitividad:</b> Si se aplicara el 13% combinado (5% GG + 8% U) sobre equipos de alto valor (como transformadores o celdas de media tensión que suman $431 millones), el precio del proyecto se dispararía fuera de mercado.<br/>"
    "2. <b>Riesgo Financiero:</b> Los equipos mayores suelen comprarse bajo cotizaciones fijas y se despachan directamente de fábrica, requiriendo menor administración y riesgo de instalación por parte del contratista principal en comparación con las redes de conductores y canalización.",
    bullet_style
))

story.append(Spacer(1, 15))
story.append(Paragraph("Conclusión", h2_style))
story.append(Paragraph(
    "La metodología de factores de presentación observada en el Excel representa una práctica madura y sofisticada de ingeniería de costos. "
    "Al implementarla en Qubi Cost Electrical, la aplicación permite a los estimadores estructurar ofertas comerciales competitivas, "
    "resguardando los márgenes de operación reales bajo un esquema de APU limpio y profesional auditable por cualquier cliente.",
    body_style
))

# Build document
doc.build(story)
print("Factors analysis PDF generated successfully.")
