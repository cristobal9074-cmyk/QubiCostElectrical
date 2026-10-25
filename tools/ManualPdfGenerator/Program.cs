using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical.Infrastructure;

namespace ManualPdfGenerator;

internal static class Program
{
    private const string ColorNavy = "#0B2442";
    private const string ColorPrimaryBlue = "#1E40AF";
    private const string ColorAccent = "#0284C7";
    private const string ColorTextDark = "#0F172A";
    private const string ColorTextBody = "#334155";
    private const string ColorTextMuted = "#64748B";
    private const string ColorBorderLight = "#E2E8F0";
    private const string ColorBgCard = "#F8FAFC";
    private const string ColorInfoBg = "#EFF6FF";
    private const string ColorInfoBorder = "#3B82F6";
    private const string ColorInfoText = "#1E40AF";
    private const string ColorWarnBg = "#FEF3C7";
    private const string ColorWarnBorder = "#F59E0B";
    private const string ColorWarnText = "#92400E";
    private const string ColorSuccessBg = "#DCFCE7";
    private const string ColorSuccessBorder = "#10B981";
    private const string ColorSuccessText = "#065F46";

    private static void Main(string[] args)
    {
        QuestPdfBootstrap.EnsureCommunityLicense();

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")))
        {
            dir = dir.Parent;
        }
        var baseDir = dir?.FullName ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var imagesDir = Path.Combine(baseDir, "docs", "manual", "images");
        var outputPath = Path.Combine(baseDir, "docs", "manual", "Manual_de_Usuario_QubiCost_Electrical.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        Console.WriteLine($"Generando Manual PDF en: {outputPath}");

        var doc = Document.Create(container =>
        {
            // Portada
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

                page.Content().Column(col =>
                {
                    col.Item().Height(280).Background(ColorNavy).Padding(40).Column(head =>
                    {
                        head.Item().Row(r =>
                        {
                            r.AutoItem().Background(ColorAccent).PaddingVertical(4).PaddingHorizontal(12)
                                .Text("DOCUMENTO OFICIAL").FontSize(11).Bold().FontColor(Colors.White);
                            r.RelativeItem().AlignRight().Text("VERSIÓN 1.1.2").FontSize(11).Bold().FontColor(Colors.Grey.Lighten2);
                        });

                        head.Item().PaddingTop(30).Text("Qubi Cost Electrical")
                            .FontSize(38).Bold().FontColor(Colors.White);

                        head.Item().PaddingTop(6).Text("MANUAL DE USUARIO INTEGRAL")
                            .FontSize(20).SemiBold().FontColor(ColorAccent);

                        head.Item().PaddingTop(12).Text("Guía visual completa paso a paso: desde el inicio hasta el cierre y presentación formal del presupuesto eléctrico")
                            .FontSize(12).FontColor(Colors.Grey.Lighten3);
                    });

                    col.Item().Padding(40).Column(body =>
                    {
                        body.Item().Text("ACERCA DE ESTA GUÍA")
                            .FontSize(14).Bold().FontColor(ColorNavy);

                        body.Item().PaddingTop(8).Text(
                            "Este manual ha sido elaborado para que estimadores, ingenieros eléctricos, jefes de propuestas y contratistas " +
                            "puedan operar fluidamente la plataforma Qubi Cost Electrical. A través de capturas reales y explicaciones detalladas, " +
                            "recorrerá el ciclo completo de elaboración de un presupuesto: apertura del archivo .qce, configuración de mandante y monedas, " +
                            "estructuración de capítulos y partidas, cubicación con fórmulas en vivo, cuadros de servicios comunes (C.SC), cálculo de APU, " +
                            "administración de precios, control de consistencia mediante auditoría interna, y exportación de la oferta comercial definitiva en PDF y Excel.")
                            .FontSize(10.5f).LineHeight(1.5f).FontColor(ColorTextBody);

                        body.Item().PaddingTop(25).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(14).Column(c =>
                            {
                                c.Item().Text("ARQUITECTURA DE TRABAJO").FontSize(11).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(4).Text("• Formato único .qce (SQLite)\n• Sin enlaces rotos ni Excels desincronizados\n• Colaboración en red con bloqueo seguro\n• Trazabilidad total de cambios y auditoría")
                                    .FontSize(9.5f).LineHeight(1.4f).FontColor(ColorTextBody);
                            });

                            row.Spacing(16);

                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(14).Column(c =>
                            {
                                c.Item().Text("MÓDULOS DEL FLUJO").FontSize(11).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(4).Text("1. Datos de Proyecto y Moneda\n2. Itemizado y Capítulos\n3. Cubicación y Fórmulas Casilla\n4. Cubicación SC (Tableros y Circuitos)\n5. APU y Condiciones Laborales\n6. Revisión Final y Oferta Comercial")
                                    .FontSize(9.5f).LineHeight(1.4f).FontColor(ColorTextBody);
                            });
                        });

                        body.Item().PaddingTop(35).BorderTop(1).BorderColor(ColorBorderLight).PaddingTop(15).Row(r =>
                        {
                            r.AutoItem().Text("Elaborado por: Departamento de Ingeniería y Presupuestos")
                                .FontSize(9).FontColor(ColorTextMuted);
                            r.RelativeItem().AlignRight().Text("Octubre 2026 — Edición Especial para Producción")
                                .FontSize(9).FontColor(ColorTextMuted);
                        });
                    });
                });
            });

            // Índice y Filosofía de Trabajo
            AddIndexAndPhilosophyPage(container);

            // Paso 1: Inicio y Creación de Proyecto
            AddStepPage(container,
                stepNumber: "01",
                stepTitle: "Inicio y Creación de Proyecto",
                menuLocation: "Menú Principal · Acceso Directo / Ctrl+N",
                imagePath: Path.Combine(imagesDir, "00_inicio.png"),
                imageCaption: "Pantalla de bienvenida y diálogo de inicio de Qubi Cost Electrical.",
                objective: "Comenzar el presupuesto creando un nuevo proyecto en blanco o abriendo un archivo de trabajo existente (.qce), garantizando la exclusividad de edición en carpetas compartidas.",
                instructions: new[]
                {
                    "Al abrir la aplicación, observará la pantalla de inicio con acceso rápido a proyectos recientes y comandos principales.",
                    "Haga clic en 'Nuevo proyecto' o presione el atajo de teclado Ctrl + N.",
                    "En el diálogo emergente, asigne el nombre del presupuesto (ej: 'Edificio Vista Cordillera'), seleccione la carpeta de destino y defina la moneda de trabajo (UF o CLP).",
                    "Si va a retomar una obra anterior, presione 'Abrir proyecto' (Ctrl + O) o elija directamente el archivo en la lista de 'Proyectos recientes'.",
                    "Colaboración en red local (SMB/UNC): Si el archivo ya está abierto por otro usuario del equipo, Qubi Cost Electrical abrirá automáticamente en modo solo lectura para evitar sobrescrituras accidentales."
                },
                tipText: "Trabaje siempre sobre un único archivo .qce. Todos los capítulos, cubicaciones, precios de materiales, APUs y documentos de oferta quedan centralizados en este contenedor seguro.",
                warnText: "Nunca modifique la extensión .qce ni renombre el archivo mientras la aplicación se encuentre abierta para proteger la integridad de la base de datos.");

            // Paso 2: Datos del Proyecto y Parámetros Comerciales
            AddStepPage(container,
                stepNumber: "02",
                stepTitle: "Datos del Proyecto y Parámetros Comerciales",
                menuLocation: "Navegación: Proyecto → Datos del proyecto",
                imagePath: Path.Combine(imagesDir, "02_datos_proyecto.png"),
                imageCaption: "Ficha de datos generales de obra, mandante, ubicación, valor UF y porcentajes comerciales.",
                objective: "Definir los antecedentes contractuales que alimentarán automáticamente las portadas, cartas de oferta y los cálculos de gastos generales y márgenes de utilidad.",
                instructions: new[]
                {
                    "En la barra de navegación superior, seleccione el grupo 'Proyecto' y luego 'Datos del proyecto'.",
                    "Complete la sección 'OBRA Y MANDANTE': Nombre de la obra, Razón Social del Cliente, RUT, Ubicación geográfica y Metros Cuadrados construidos.",
                    "En 'MONEDAS E INDICADORES': Verifique el valor de la UF del día (la app puede sincronizar automáticamente o permitir ingreso manual) y seleccione la moneda de presentación.",
                    "En 'PARÁMETROS COMERCIALES': Configure el porcentaje de Gastos Generales (% GG), Utilidad deseada (% Margen), Descuento comercial si aplica, y porcentaje de Imprevistos.",
                    "Configure la política de IVA: Puede optar por incluir IVA en los totales del presupuesto o presentar la oferta en valores netos más IVA según las bases de licitación."
                },
                tipText: "Los datos de Mandante, Obra y Dirección ingresados aquí se propagan automáticamente a las cartas de oferta, resúmenes ejecutivos y exportaciones Excel sin tener que reescribirlos.",
                warnText: "Si modifica el valor de la UF con el presupuesto avanzado, los precios unitarios en pesos (CLP) se mantendrán y el motor recalculará los equivalentes en UF en tiempo real.");

            // Paso 3: Estructuración del Itemizado
            AddStepPage(container,
                stepNumber: "03",
                stepTitle: "Estructura del Itemizado y Partidas",
                menuLocation: "Navegación: Presupuesto → Itemizado",
                imagePath: Path.Combine(imagesDir, "03_itemizado.png"),
                imageCaption: "Módulo de Itemizado con árbol jerárquico de capítulos, partidas, unidades y cantidades oficiales.",
                objective: "Organizar la estructura jerárquica de la propuesta eléctrica, definiendo capítulos principales y partidas de obra con sus códigos WBS y unidades contractuales.",
                instructions: new[]
                {
                    "Haga clic en la pestaña 'Itemizado' en la barra superior.",
                    "Para crear un capítulo o sección principal (ej: '2. ALIMENTADORES Y FUERZA'), presione el botón '+ Capítulo'. Marque la casilla de título si no lleva cantidad directa.",
                    "Para crear una partida cubicable (ej: '2.1 Alimentador principal EVA 3x50mm2'), presione '+ Partida' dentro del capítulo correspondiente.",
                    "Defina el código WBS (1.1, 1.2, 2.1...), la descripción técnica detallada, la unidad de medida (GL, ML, UN, M2, PTO...) y la cantidad contractual del mandante.",
                    "También puede importar un itemizado completo desde planillas Excel oficiales mediante el botón 'Importar Excel'.",
                    "Identifique el estado visual de cada partida mediante los indicadores de color: pendiente (gris), en cubicación (amarillo) y cubicada/completa (verde)."
                },
                tipText: "Al importar itemizados de licitaciones oficiales, el sistema respeta exactamente los códigos WBS y nombres de partidas del mandante para evitar observaciones en la entrega.",
                warnText: "No elimine partidas que ya contengan líneas de cubicación o APUs asociados sin verificar previamente si sus recursos deben ser reasignados a otro ítem.");

            // Paso 4: Cubicación Técnica y Fórmulas
            AddStepPage(container,
                stepNumber: "04",
                stepTitle: "Cubicación Técnica y Fórmulas en Casillas",
                menuLocation: "Navegación: Cubicación → Cubicación",
                imagePath: Path.Combine(imagesDir, "04_cubicacion.png"),
                imageCaption: "Pantalla de Cubicación con asignación de materiales, desglose de tramos y casillas de cálculo aritmético.",
                objective: "Desglosar los recursos físicos (cables, ductos, accesorios, artefactos) necesarios para ejecutar cada partida, aprovechando el potente motor de fórmulas en casillas.",
                instructions: new[]
                {
                    "Navegue a 'Cubicación' → 'Cubicación'. En el panel lateral o selector superior, elija la partida que desea cubicar (ej: '2.1 Alimentador principal').",
                    "Presione '+ Agregar material' o escriba directamente el código en la fila para autocompletar desde el Banco de Precios.",
                    "Fórmulas en Casillas de Cantidad: En lugar de usar una calculadora externa, haga clic en las celdas de tramos y escriba directamente operaciones como '120+40', '32*2', '(15*4)+10'.",
                    "Presione Enter: La aplicación evalúa la expresión inmediatamente, calcula el subtotal y almacena la fórmula original para que siempre sea auditable.",
                    "Aplique factores de pérdida (% Merma) para considerar despuntes de conductores o roturas en obra.",
                    "Configure redondeos automáticos si requiere comprar materiales en múltiplos comerciales (ej: tiras de 3 metros de tubería o rollos de 100 metros de cable)."
                },
                tipText: "El uso de fórmulas en las casillas permite que cualquier revisor comprenda exactamente de dónde salió cada número (ej: tramo subterráneo + subida vertical + holgura en tablero).",
                warnText: "Asegúrese de que la unidad de medida del material en el banco coincida con la unidad en la que está cubicando (ej: metros lineales de conductor vs bobinas completas).");

            // Paso 5: Cubicación de Servicios Comunes (Cubicación SC)
            AddStepPage(container,
                stepNumber: "05",
                stepTitle: "Cubicación de Servicios Comunes (C.SC)",
                menuLocation: "Navegación: Cubicación → Cubicación SC / Atajo Alt+L",
                imagePath: Path.Combine(imagesDir, "05_cubicacion_sc.png"),
                imageCaption: "Módulo C.SC para modelar tableros, alimentadores, circuitos de distribución y tramos por piso.",
                objective: "Cuantificar con precisión de ingeniería todos los tableros, circuitos de alumbrado, enchufes y fuerza de servicios comunes, evitando el cálculo manual repetitivo.",
                instructions: new[]
                {
                    "Haga clic en 'Cubicación SC'. Esta herramienta especializada permite modelar la distribución eléctrica de edificios e instalaciones complejas.",
                    "Presione '+ Agregar tablero' para crear el cuadro de carga (ej: 'TDA-SC: Tablero Alumbrado Servicios Comunes').",
                    "Dentro del tablero, presione '+ Agregar circuito'. Asigne el número de circuito, descripción y tipo de carga (Alumbrado 'A', Enchufe 'E', Clima 'CL', Fuerza 'F').",
                    "Defina las características técnicas: Longitud por bandeja, longitud por tubería, calibre de derivación y sección de distribución (ej: H07Z1-K 2.5 mm²).",
                    "Si el circuito abarca múltiples pisos (ej: piso -1 al 12), utilice el desglose de tramos por piso para asignar metrajes específicos a cada nivel.",
                    "Conexión con el presupuesto: Vuelva a la pantalla 'Cubicación', sitúese en la partida deseada y presione el atajo Alt + L ('Tomar de SC'). Seleccione el circuito y los metrajes pasarán automáticamente."
                },
                tipText: "El comando Alt+L ('Tomar de SC') es una de las funciones más productivas del sistema: conecta el modelo técnico de circuitos con el presupuesto económico sin digitar ni un solo metraje dos veces.",
                warnText: "Verifique que el número de conductores por fase (monofásico 3x, trifásico 4x o 5x) esté correctamente configurado antes de tomar las cantidades a la partida.");

            // Paso 6: Análisis de Precios Unitarios (APU)
            AddStepPage(container,
                stepNumber: "06",
                stepTitle: "Análisis de Precios Unitarios (APU)",
                menuLocation: "Navegación: Presupuesto → Herramienta APU",
                imagePath: Path.Combine(imagesDir, "06_apu.png"),
                imageCaption: "Desglose de costo unitario por partida: materiales, mano de obra (HH), herramientas menores y condiciones laborales.",
                objective: "Analizar la estructura de costos directos de cada partida, calculando el rendimiento de cuadrillas, horas-hombre (HH), leyes sociales y recargos por altura y entorno de trabajo.",
                instructions: new[]
                {
                    "Acceda a 'Presupuesto' → 'Herramienta APU'. Seleccione la partida a analizar en el selector superior.",
                    "El sistema desglosa los costos en cuatro componentes clave: Materiales (MAT), Mano de Obra (MO), Equipos y Maquinarias (EQ) y Subcontratos (SC).",
                    "En Mano de Obra, configure el rendimiento en Horas-Hombre (HH) para cada categoría de cuadrilla: Maestro Electricista SEC Clase B, Ayudante Eléctrico o Liniero.",
                    "Asigne el porcentaje de Herramientas Menores (estándar 3% a 5% sobre el costo de la mano de obra).",
                    "Condiciones Laborales y Entorno: Ajuste el recargo por 'Factor Altura' (para trabajos sobre 1.8 metros con andamios o plataformas elevadoras) y el nivel de dificultad del recinto.",
                    "Recargo por Edificio en Operación: Si la faena se realiza con público o áreas funcionando en horario nocturno, active el recargo correspondiente."
                },
                tipText: "Los materiales asignados en Cubicación se sincronizan automáticamente con el APU de la partida, garantizando coherencia absoluta entre cantidades físicas y precios unitarios.",
                warnText: "No aplique factores de pérdida en dos niveles a la vez: si un material ya declara su merma propia en la fila, no le sume nuevamente la merma global del APU.");

            // Paso 7: Banco de Precios y Cotizaciones
            AddStepPage(container,
                stepNumber: "07",
                stepTitle: "Banco de Precios, Cotizaciones y Compras",
                menuLocation: "Navegación: Precios → Banco de precios / Cotizaciones",
                imagePath: Path.Combine(imagesDir, "07_banco_precios.png"),
                imageCaption: "Catálogo central de materiales con precios unitarios en UF/CLP, proveedores y estados de vigencia.",
                objective: "Mantener un catálogo actualizado y confiable de insumos eléctricos, solicitar cotizaciones a distribuidores y generar la lista de compras consolidada del proyecto.",
                instructions: new[]
                {
                    "Ingrese a 'Precios' → 'Banco de precios'. Aquí reside la biblioteca de materiales, protecciones, canalizaciones y artefactos.",
                    "Consulte o filtre por categoría técnica: Conductores, Tuberías y Bandejas, Tableros y Gabinetes, Protecciones Modulares, Artefactos e Iluminación.",
                    "Cada registro cuenta con: Código interno, descripción normalizada, unidad comercial, precio en CLP, precio equivalente en UF, proveedor habitual y fecha de cotización.",
                    "Actualización de precios: Al ingresar un nuevo precio de proveedor, el sistema puede actualizarlo únicamente en el proyecto actual o sincronizarlo con el banco maestro compartido.",
                    "Módulo Cotizaciones y Lista de Compra: Con un clic en 'Lista de compra', obtenga el metraje consolidado de todos los insumos del presupuesto para cotizar por volumen con distribuidores."
                },
                tipText: "Puede importar listas de precios masivas desde archivos Excel o CSV de sus proveedores habituales (Rexel, Rhona, Dartel, Ticino, etc.) usando la función 'Importar catálogo'.",
                warnText: "Preste atención a la fecha de vigencia de los precios, especialmente en conductores de cobre y canalizaciones metálicas sujetos a variaciones del mercado internacional.");

            // Paso 8: Revisión Final y Control de Calidad
            AddStepPage(container,
                stepNumber: "08",
                stepTitle: "Revisión Final y Auditoría de Consistencia",
                menuLocation: "Navegación: Cierre → Revisión final / Botón 'Revisar'",
                imagePath: Path.Combine(imagesDir, "08_revision_final.png"),
                imageCaption: "Panel de revisión con diagnóstico automático de partidas incompletas, recursos sin valorizar e inconsistencias.",
                objective: "Realizar una auditoría integral y automatizada del presupuesto antes de enviarlo al mandante, identificando cualquier omisión o error que pudiera comprometer la oferta.",
                instructions: new[]
                {
                    "Haga clic en 'Cierre' → 'Revisión final' o presione el botón 'Revisar' ubicado en la cabecera del sistema.",
                    "La pantalla ejecuta de inmediato una batería de comprobaciones lógicas sobre todo el proyecto.",
                    "Alertas Bloqueantes (rojas): Errores críticos que impiden la exportación segura de la oferta (ej: partidas con cantidad cero, materiales sin precio asignado o APUs vacíos).",
                    "Botón 'Ir a corregir': Al hacer clic en este botón junto a cualquier alerta, la aplicación lo transporta de forma automática y precisa a la celda exacta que requiere solución.",
                    "Alertas de Advertencia (amarillas): Sugerencias de buenas prácticas (ej: partidas sin observaciones, mermas inusualmente altas o rendimientos laborales atípicos).",
                    "Una vez que el panel 'Bloqueantes' indique 0 observaciones pendientes, el botón principal 'Generar oferta y exportar →' se habilitará para continuar."
                },
                tipText: "El botón 'Ir a corregir' le ahorrará minutos valiosos al navegar directamente al problema en presupuestos de cientos de partidas sin tener que buscar manualmente.",
                warnText: "Nunca presente una oferta comercial al mandante si existen partidas bloqueantes sin resolver; esto previene pérdidas económicas severas por omisión de costos en licitaciones.");

            // Paso 9: Cierre, Wizard de Oferta y Exportación
            AddStepPage(container,
                stepNumber: "09",
                stepTitle: "Oferta Comercial y Exportación Oficial",
                menuLocation: "Navegación: Cierre → Oferta y exportación",
                imagePath: Path.Combine(imagesDir, "09_oferta_exportacion.png"),
                imageCaption: "Asistente (Wizard) de exportación: validación, personalización de portada, vista previa interactiva y generación de PDF/Excel.",
                objective: "Consolidar el presupuesto y generar la documentación formal para el cliente: Carta de Oferta, Resumen Ejecutivo, Presupuesto Detallado y Planillas Excel auditables.",
                instructions: new[]
                {
                    "Acceda a 'Cierre' → 'Oferta y exportación'. Se desplegará el Asistente de Cierre en cuatro etapas intuitivas:",
                    "Paso 1 - Validar oferta: Comprobación final de que los montos totales, gastos generales y márgenes comerciales cuadran a la perfección.",
                    "Paso 2 - Configurar: Seleccione los documentos a generar (Carta Mandante, Resumen Ejecutivo de Presupuesto, Detalle por Capítulos) e incorpore el logotipo corporativo.",
                    "Paso 3 - Vista previa: Visualice el PDF renderizado en alta definición antes de emitirlo. Compruebe la distribución de páginas, membretes y totales.",
                    "Paso 4 - Exportar: Presione 'Exportar PDF' o 'Exportar Excel'. El sistema creará los archivos en la carpeta de la obra y desplegará un aviso con opción de abrir la carpeta contenedora.",
                    "Libro Excel Oficial: Si el mandante solicita respaldo en planilla, la exportación genera un libro con fórmulas vivas, respetando la estructura formal requerida en licitaciones públicas y privadas."
                },
                tipText: "El PDF exportado incluye metadatos de seguridad, fecha y hora de emisión, y la firma del responsable técnico del estudio, ofreciendo un estándar corporativo impecable.",
                warnText: "Antes de enviar los archivos, abra siempre la carpeta de salida y verifique que los documentos generados correspondan a la última versión cerrada del presupuesto.");

            // Paso 10: Resumen Ejecutivo / Dashboard y Cierre del Ciclo
            AddDashboardAndShortcutsPage(container, imagesDir);
        });

        doc.GeneratePdf(outputPath);
        Console.WriteLine("Manual PDF generado exitosamente.");
    }

    private static void AddIndexAndPhilosophyPage(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(30);
            page.MarginHorizontal(35);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "ÍNDICE GENERAL Y FILOSOFÍA DE TRABAJO"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(15).Column(col =>
            {
                col.Item().Text("Ruta Operativa del Presupuesto Eléctrico")
                    .FontSize(18).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text("El proceso de presupuestación en Qubi Cost Electrical sigue un flujo lógico y riguroso diseñado para garantizar rapidez, precisión en el costo y trazabilidad total:")
                    .FontSize(10.5f).LineHeight(1.4f).FontColor(ColorTextBody);

                col.Item().PaddingTop(18).Column(steps =>
                {
                    var items = new (string Num, string Title, string Desc)[]
                    {
                        ("Paso 01", "Inicio y Creación del Proyecto", "Crear o abrir el archivo .qce, configuración de seguridad y trabajo colaborativo."),
                        ("Paso 02", "Datos de Obra y Parámetros Comerciales", "Ficha del mandante, valor UF, porcentajes de GG, utilidad, mermas e IVA."),
                        ("Paso 03", "Estructura del Itemizado", "Jerarquía de capítulos, partidas de obra, códigos WBS y cantidades oficiales."),
                        ("Paso 04", "Cubicación Técnica y Fórmulas", "Asignación de materiales, casillas con operaciones aritméticas y redondeos comerciales."),
                        ("Paso 05", "Cubicación de Servicios Comunes (C.SC)", "Modelamiento de tableros, alimentadores y circuitos con traspaso rápido (Alt+L)."),
                        ("Paso 06", "Análisis de Precios Unitarios (APU)", "Costo directo, rendimientos de mano de obra (HH), leyes sociales y factores de altura."),
                        ("Paso 07", "Banco de Precios y Cotizaciones", "Catálogo maestro, actualización de precios en UF/CLP y lista consolidada de compra."),
                        ("Paso 08", "Revisión Final y Auditoría", "Detección automática de inconsistencias, alertas bloqueantes y botón 'Ir a corregir'."),
                        ("Paso 09", "Oferta Comercial y Exportación", "Asistente de cierre, generación de cartas PDF corporativas y libros Excel con fórmulas.")
                    };

                    foreach (var it in items)
                    {
                        steps.Item().PaddingBottom(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Row(r =>
                        {
                            r.AutoItem().Background(ColorNavy).PaddingVertical(4).PaddingHorizontal(8)
                                .Text(it.Num).FontSize(9).Bold().FontColor(Colors.White);
                            r.RelativeItem().PaddingLeft(12).Column(c =>
                            {
                                c.Item().Text(it.Title).FontSize(10.5f).Bold().FontColor(ColorNavy);
                                c.Item().Text(it.Desc).FontSize(9).FontColor(ColorTextBody);
                            });
                        });
                    }
                });

                col.Item().PaddingTop(15).Border(1).BorderColor(ColorInfoBorder).Background(ColorInfoBg).Padding(12).Column(c =>
                {
                    c.Item().Text("¿POR QUÉ UN ÚNICO ARCHIVO .QCE?").FontSize(11).Bold().FontColor(ColorInfoText);
                    c.Item().PaddingTop(3).Text(
                        "Tradicionalmente, un presupuesto eléctrico requería entre cuatro y seis planillas Excel descoordinadas (itemizado, cubicación de alimentadores, cuadros de carga, APUs, cotizaciones y resumen). " +
                        "Qubi Cost Electrical elimina este problema integrando todo en una base de datos local SQLite (.qce). " +
                        "Cualquier cambio en un conductor o tablero actualiza de forma instantánea la cubicación, el APU y el valor total de la oferta, sin riesgo de fórmulas rotas.")
                        .FontSize(9).LineHeight(1.35f).FontColor(ColorTextBody);
                });
            });
        });
    }

    private static void AddStepPage(
        IDocumentContainer container,
        string stepNumber,
        string stepTitle,
        string menuLocation,
        string imagePath,
        string imageCaption,
        string objective,
        string[] instructions,
        string tipText,
        string warnText)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, $"PASO {stepNumber}: {stepTitle.ToUpperInvariant()}"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                // Título y ubicación
                col.Item().Row(r =>
                {
                    r.AutoItem().Background(ColorNavy).PaddingVertical(3).PaddingHorizontal(8)
                        .Text($"PASO {stepNumber}").FontSize(11).Bold().FontColor(Colors.White);

                    r.RelativeItem().PaddingLeft(10).Column(c =>
                    {
                        c.Item().Text(stepTitle).FontSize(16).Bold().FontColor(ColorNavy);
                        c.Item().Text(menuLocation).FontSize(9).SemiBold().FontColor(ColorAccent);
                    });
                });

                // Objetivo
                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Row(r =>
                {
                    r.AutoItem().Text("OBJETIVO: ").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                    r.RelativeItem().Text(objective).FontSize(9.5f).FontColor(ColorTextBody);
                });

                // Imagen de pantalla
                if (File.Exists(imagePath))
                {
                    col.Item().PaddingTop(10).AlignCenter().Column(imgCol =>
                    {
                        imgCol.Item().Border(1).BorderColor(ColorBorderLight).Padding(2).MaxHeight(245).Image(imagePath);
                        imgCol.Item().PaddingTop(3).AlignCenter().Text($"Figura {stepNumber}: {imageCaption}")
                            .FontSize(8).Italic().FontColor(ColorTextMuted);
                    });
                }

                // Instrucciones paso a paso
                col.Item().PaddingTop(10).Text("Instrucciones Paso a Paso").FontSize(11.5f).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Column(instCol =>
                {
                    for (int i = 0; i < instructions.Length; i++)
                    {
                        int num = i + 1;
                        var text = instructions[i];

                        instCol.Item().PaddingBottom(4).Row(row =>
                        {
                            row.AutoItem().Width(18).Height(18).Background(ColorPrimaryBlue)
                                .AlignCenter().AlignMiddle()
                                .Text(num.ToString()).FontSize(8.5f).Bold().FontColor(Colors.White);

                            row.RelativeItem().PaddingLeft(8)
                                .Text(text).FontSize(9).LineHeight(1.35f).FontColor(ColorTextBody);
                        });
                    }
                });

                // Tips y Advertencias
                col.Item().PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().Border(1).BorderColor(ColorSuccessBorder).Background(ColorSuccessBg).Padding(7).Column(c =>
                    {
                        c.Item().Text("CONSEJO PRÁCTICO").FontSize(8.5f).Bold().FontColor(ColorSuccessText);
                        c.Item().PaddingTop(2).Text(tipText).FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    });

                    r.Spacing(10);

                    r.RelativeItem().Border(1).BorderColor(ColorWarnBorder).Background(ColorWarnBg).Padding(7).Column(c =>
                    {
                        c.Item().Text("IMPORTANTE / PRECAUCIÓN").FontSize(8.5f).Bold().FontColor(ColorWarnText);
                        c.Item().PaddingTop(2).Text(warnText).FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    });
                });
            });
        });
    }

    private static void AddDashboardAndShortcutsPage(IDocumentContainer container, string imagesDir)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "RESUMEN EJECUTIVO Y ATAJOS DE TECLADO"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("Dashboard: Control y Seguimiento del Presupuesto")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(3).Text("El módulo Resumen (Dashboard) consolida todos los indicadores clave en una sola vista de control:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                var dashImg = Path.Combine(imagesDir, "01_dashboard.png");
                if (File.Exists(dashImg))
                {
                    col.Item().PaddingTop(8).AlignCenter().Column(c =>
                    {
                        c.Item().Border(1).BorderColor(ColorBorderLight).Padding(2).MaxHeight(220).Image(dashImg);
                        c.Item().PaddingTop(3).AlignCenter().Text("Figura 10: Tablero de control general con montos netos, costo/m², avance y desglose de partidas.")
                            .FontSize(8).Italic().FontColor(ColorTextMuted);
                    });
                }

                col.Item().PaddingTop(12).Text("Atajos de Teclado del Estimador")
                    .FontSize(12).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(110);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(ColorNavy).Padding(6).Text("ATAJO").FontSize(9).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(6).Text("ACCIÓN").FontSize(9).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(6).Text("UTILIDAD EN EL FLUJO").FontSize(9).Bold().FontColor(Colors.White);
                    });

                    var shortcuts = new[]
                    {
                        ("Ctrl + N", "Nuevo proyecto", "Inicia una propuesta en blanco con diálogo de configuración."),
                        ("Ctrl + O", "Abrir proyecto", "Selecciona y carga un archivo .qce local o de red."),
                        ("Ctrl + S", "Guardar proyecto", "Guarda cambios de inmediato y actualiza el snapshot."),
                        ("Ctrl + P", "Command Palette", "Buscador global rápido de partidas, materiales y pantallas."),
                        ("Alt + L", "Tomar de SC", "En Cubicación, transfiere metrajes de circuitos directamente."),
                        ("Ctrl + F", "Búsqueda en grilla", "Filtra partidas o materiales en la vista activa."),
                        ("F5 / Ctrl + R", "Recalcular y Revisar", "Fuerza la actualización de costos y ejecuta el auditor.")
                    };

                    bool alt = false;
                    foreach (var s in shortcuts)
                    {
                        var bg = alt ? ColorBgCard : "#FFFFFF";
                        table.Cell().Background(bg).Padding(5).Text(s.Item1).FontSize(8.5f).Bold().FontColor(ColorPrimaryBlue);
                        table.Cell().Background(bg).Padding(5).Text(s.Item2).FontSize(8.5f).SemiBold().FontColor(ColorTextDark);
                        table.Cell().Background(bg).Padding(5).Text(s.Item3).FontSize(8.5f).FontColor(ColorTextBody);
                        alt = !alt;
                    }
                });

                col.Item().PaddingTop(14).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                {
                    c.Item().Text("REGLA DE ORO DEL ESTIMADOR PROFESIONAL").FontSize(10).Bold().FontColor(ColorNavy);
                    c.Item().PaddingTop(3).Text(
                        "“Un archivo .qce, no seis Excels dispersos.” Conserve la disciplina de mantener los datos del cliente, cubicaciones técnicas, " +
                        "precios de materiales y cartas de oferta en el mismo archivo. Antes de cualquier entrega, revise que no existan alertas rojas " +
                        "en el módulo de Revisión y exporte con el Wizard oficial para garantizar presupuestos 100% confiables y ganadores.")
                        .FontSize(9).Italic().LineHeight(1.35f).FontColor(ColorTextBody);
                });
            });
        });
    }

    private static void RenderHeader(IContainer container, string title)
    {
        container.Row(row =>
        {
            row.AutoItem().Text("Qubi Cost Electrical — Manual Oficial").FontSize(8.5f).SemiBold().FontColor(ColorNavy);
            row.RelativeItem().AlignRight().Text(title).FontSize(8.5f).FontColor(ColorTextMuted);
        });
    }

    private static void RenderFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(ColorBorderLight).PaddingTop(4).Row(row =>
        {
            row.AutoItem().Text("Documentación confidencial para uso interno y de estimación")
                .FontSize(8).FontColor(ColorTextMuted);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.Span("Página ").FontSize(8).FontColor(ColorTextMuted);
                x.CurrentPageNumber().FontSize(8).Bold().FontColor(ColorNavy);
                x.Span(" de ").FontSize(8).FontColor(ColorTextMuted);
                x.TotalPages().FontSize(8).Bold().FontColor(ColorNavy);
            });
        });
    }
}
