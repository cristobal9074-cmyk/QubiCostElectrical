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
    private const string ColorBorderLight = "#CBD5E1";
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
    private const string ColorDangerBg = "#FEE2E2";
    private const string ColorDangerBorder = "#EF4444";
    private const string ColorDangerText = "#991B1B";

    private static void Main(string[] args)
    {
        QuestPdfBootstrap.EnsureCommunityLicense();
        QuestPDF.Settings.EnableDebugging = true;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")))
        {
            dir = dir.Parent;
        }
        var baseDir = dir?.FullName ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var imagesDir = Path.Combine(baseDir, "docs", "manual", "images");
        Directory.CreateDirectory(Path.Combine(baseDir, "docs", "manual"));

        // 1. Generar Manual de Usuario de la Aplicación
        GenerateUserManualPdf(baseDir, imagesDir);

        // 2. Generar Guía Financiera y Comercial (Mano de Obra, FA, Factores y Tablero)
        GenerateFinancialGuidePdf(baseDir, imagesDir);

        // 3. Generar Dossier Integral para Desarrollo de Web Promocional
        GenerateWebPromoBriefPdf(baseDir, imagesDir);
    }

    // =========================================================================
    // DOCUMENTO 1: MANUAL DE USUARIO PASO A PASO
    // =========================================================================
    private static void GenerateUserManualPdf(string baseDir, string imagesDir)
    {
        var outputPath = Path.Combine(baseDir, "docs", "manual", "Manual_de_Usuario_QubiCost_Electrical.pdf");
        Console.WriteLine($"Generando Manual de Usuario en: {outputPath}");

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

            // Pasos 1 a 9
            AddStepPage(container, "01", "Inicio y Creación de Proyecto", "Menú Principal · Acceso Directo / Ctrl+N",
                Path.Combine(imagesDir, "00_inicio.png"), "Pantalla de bienvenida y diálogo de inicio de Qubi Cost Electrical.",
                "Comenzar el presupuesto creando un nuevo proyecto en blanco o abriendo un archivo de trabajo existente (.qce), garantizando la exclusividad de edición en carpetas compartidas.",
                new[]
                {
                    "Al abrir la aplicación, observará la pantalla de inicio con acceso rápido a proyectos recientes y comandos principales.",
                    "Haga clic en 'Nuevo proyecto' o presione el atajo de teclado Ctrl + N.",
                    "En el diálogo emergente, asigne el nombre del presupuesto, seleccione la carpeta de destino y defina la moneda de trabajo (UF o CLP).",
                    "Si va a retomar una obra anterior, presione 'Abrir proyecto' (Ctrl + O) o elija directamente el archivo en la lista de recientes.",
                    "Colaboración en red local (SMB/UNC): Si el archivo ya está abierto por otro usuario del equipo, Qubi Cost Electrical abrirá automáticamente en modo solo lectura."
                },
                "Trabaje siempre sobre un único archivo .qce. Todos los capítulos, cubicaciones, precios de materiales, APUs y documentos de oferta quedan centralizados en este contenedor seguro.",
                "Nunca modifique la extensión .qce ni renombre el archivo mientras la aplicación se encuentre abierta para proteger la integridad de la base de datos.");

            AddStepPage(container, "02", "Datos del Proyecto y Parámetros Comerciales", "Navegación: Proyecto → Datos del proyecto",
                Path.Combine(imagesDir, "02_datos_proyecto.png"), "Ficha de datos generales de obra, mandante, ubicación, valor UF y porcentajes comerciales.",
                "Definir los antecedentes contractuales que alimentarán automáticamente las portadas, cartas de oferta y los cálculos de gastos generales y márgenes de utilidad.",
                new[]
                {
                    "En la barra de navegación superior, seleccione el grupo 'Proyecto' y luego 'Datos del proyecto'.",
                    "Complete la sección 'OBRA Y MANDANTE': Nombre de la obra, Razón Social del Cliente, RUT, Ubicación geográfica y Metros Cuadrados construidos.",
                    "En 'MONEDAS E INDICADORES': Verifique el valor de la UF del día y seleccione la moneda de presentación de la oferta.",
                    "En 'PARÁMETROS COMERCIALES': Configure el porcentaje de Gastos Generales (% GG), Utilidad deseada (% Margen), Descuento comercial y porcentaje de Imprevistos.",
                    "Configure la política de IVA: Puede optar por incluir IVA en los totales del presupuesto o presentar la oferta en valores netos más IVA."
                },
                "Los datos de Mandante, Obra y Dirección ingresados aquí se propagan automáticamente a las cartas de oferta, resúmenes ejecutivos y exportaciones Excel sin tener que reescribirlos.",
                "Si modifica el valor de la UF con el presupuesto avanzado, los precios unitarios en pesos (CLP) se mantendrán y el motor recalculará los equivalentes en UF en tiempo real.");

            AddStepPage(container, "03", "Estructura del Itemizado y Partidas", "Navegación: Presupuesto → Itemizado",
                Path.Combine(imagesDir, "03_itemizado.png"), "Módulo de Itemizado con árbol jerárquico de capítulos, partidas, unidades y cantidades oficiales.",
                "Organizar la estructura jerárquica de la propuesta eléctrica, definiendo capítulos principales y partidas de obra con sus códigos WBS y unidades contractuales.",
                new[]
                {
                    "Haga clic en la pestaña 'Itemizado' en la barra superior.",
                    "Para crear un capítulo principal (ej: '2. ALIMENTADORES Y FUERZA'), presione el botón '+ Capítulo'. Marque la casilla de título si no lleva cantidad directa.",
                    "Para crear una partida cubicable (ej: '2.1 Alimentador principal EVA 3x50mm2'), presione '+ Partida' dentro del capítulo correspondiente.",
                    "Defina el código WBS (1.1, 1.2, 2.1...), la descripción técnica detallada, la unidad de medida (GL, ML, UN, M2, PTO...) y la cantidad contractual del mandante.",
                    "También puede importar un itemizado completo desde planillas Excel oficiales mediante el botón 'Importar Excel'.",
                    "Identifique el estado visual de cada partida mediante los indicadores de color: pendiente (gris), en cubicación (amarillo) y cubicada/completa (verde)."
                },
                "Al importar itemizados de licitaciones oficiales, el sistema respeta exactamente los códigos WBS y nombres de partidas del mandante para evitar observaciones en la entrega.",
                "No elimine partidas que ya contengan líneas de cubicación o APUs asociados sin verificar previamente si sus recursos deben ser reasignados a otro ítem.");

            AddStepPage(container, "04", "Cubicación Técnica y Fórmulas en Casillas", "Navegación: Cubicación → Cubicación",
                Path.Combine(imagesDir, "04_cubicacion.png"), "Pantalla de Cubicación con asignación de materiales, desglose de tramos y casillas de cálculo aritmético.",
                "Desglosar los recursos físicos (cables, ductos, accesorios, artefactos) necesarios para ejecutar cada partida, aprovechando el potente motor de fórmulas en casillas.",
                new[]
                {
                    "Navegue a 'Cubicación' → 'Cubicación'. En el panel lateral o selector superior, elija la partida que desea cubicar.",
                    "Presione '+ Agregar material' o escriba directamente el código en la fila para autocompletar desde el Banco de Precios.",
                    "Fórmulas en Casillas de Cantidad: En lugar de usar una calculadora externa, haga clic en las celdas de tramos y escriba directamente operaciones como '120+40', '32*2', '(15*4)+10'.",
                    "Presione Enter: La aplicación evalúa la expresión inmediatamente, calcula el subtotal y almacena la fórmula original para que siempre sea auditable.",
                    "Aplique factores de pérdida (% Merma) para considerar despuntes de conductores o roturas en obra.",
                    "Configure redondeos automáticos si requiere comprar materiales en múltiplos comerciales (ej: tiras de 3 metros de tubería o rollos de 100 metros de cable)."
                },
                "El uso de fórmulas en las casillas permite que cualquier revisor comprenda exactamente de dónde salió cada número (ej: tramo subterráneo + subida vertical + holgura en tablero).",
                "Asegúrese de que la unidad de medida del material en el banco coincida con la unidad en la que está cubicando (ej: metros lineales de conductor vs bobinas completas).");

            AddStepPage(container, "05", "Cubicación de Servicios Comunes (C.SC)", "Navegación: Cubicación → Cubicación SC / Atajo Alt+L",
                Path.Combine(imagesDir, "05_cubicacion_sc.png"), "Módulo C.SC para modelar tableros, alimentadores, circuitos de distribución y tramos por piso.",
                "Cuantificar con precisión de ingeniería todos los tableros, circuitos de alumbrado, enchufes y fuerza de servicios comunes, evitando el cálculo manual repetitivo.",
                new[]
                {
                    "Haga clic en 'Cubicación SC'. Esta herramienta especializada permite modelar la distribución eléctrica de edificios e instalaciones complejas.",
                    "Presione '+ Agregar tablero' para crear el cuadro de carga (ej: 'TDA-SC: Tablero Alumbrado Servicios Comunes').",
                    "Dentro del tablero, presione '+ Agregar circuito'. Asigne el número de circuito, descripción y tipo de carga (Alumbrado 'A', Enchufe 'E', Clima 'CL', Fuerza 'F').",
                    "Defina las características técnicas: Longitud por bandeja, longitud por tubería, calibre de derivación y sección de distribución (ej: H07Z1-K 2.5 mm²).",
                    "Si el circuito abarca múltiples pisos, utilice el desglose de tramos por piso para asignar metrajes específicos a cada nivel.",
                    "Conexión con el presupuesto: Vuelva a la pantalla 'Cubicación', sitúese en la partida deseada y presione el atajo Alt + L ('Tomar de SC'). Seleccione el circuito y los metrajes pasarán automáticamente."
                },
                "El comando Alt+L ('Tomar de SC') es una de las funciones más productivas del sistema: conecta el modelo técnico de circuitos con el presupuesto económico sin digitar ni un solo metraje dos veces.",
                "Verifique que el número de conductores por fase (monofásico 3x, trifásico 4x o 5x) esté correctamente configurado antes de tomar las cantidades a la partida.");

            AddStepPage(container, "06", "Análisis de Precios Unitarios (APU)", "Navegación: Presupuesto → Herramienta APU",
                Path.Combine(imagesDir, "06_apu.png"), "Desglose de costo unitario por partida: materiales, mano de obra (HH), herramientas menores y condiciones laborales.",
                "Analizar la estructura de costos directos de cada partida, calculando el rendimiento de cuadrillas, horas-hombre (HH), leyes sociales y recargos por altura y entorno de trabajo.",
                new[]
                {
                    "Acceda a 'Presupuesto' → 'Herramienta APU'. Seleccione la partida a analizar en el selector superior.",
                    "El sistema desglosa los costos en cuatro componentes clave: Materiales (MAT), Mano de Obra (MO), Equipos y Maquinarias (EQ) y Subcontratos (SC).",
                    "En Mano de Obra, configure el rendimiento en Horas-Hombre (HH) para cada categoría de cuadrilla: Maestro Electricista SEC Clase B, Ayudante Eléctrico o Liniero.",
                    "Asigne el porcentaje de Herramientas Menores (estándar 3% a 5% sobre el costo de la mano de obra).",
                    "Condiciones Laborales y Entorno: Ajuste el recargo por 'Factor Altura' y el nivel de dificultad del recinto.",
                    "Recargo por Edificio en Operación: Si la faena se realiza con público o áreas funcionando en horario nocturno, active el recargo correspondiente."
                },
                "Los materiales asignados en Cubicación se sincronizan automáticamente con el APU de la partida, garantizando coherencia absoluta entre cantidades físicas y precios unitarios.",
                "No aplique factores de pérdida en dos niveles a la vez: si un material ya declara su merma propia en la fila, no le sume nuevamente la merma global del APU.");

            AddStepPage(container, "07", "Banco de Precios, Cotizaciones y Compras", "Navegación: Precios → Banco de precios / Cotizaciones",
                Path.Combine(imagesDir, "07_banco_precios.png"), "Catálogo central de materiales con precios unitarios en UF/CLP, proveedores y estados de vigencia.",
                "Mantener un catálogo actualizado y confiable de insumos eléctricos, solicitar cotizaciones a distribuidores y generar la lista de compras consolidada del proyecto.",
                new[]
                {
                    "Ingrese a 'Precios' → 'Banco de precios'. Aquí reside la biblioteca de materiales, protecciones, canalizaciones y artefactos.",
                    "Consulte o filtre por categoría técnica: Conductores, Tuberías y Bandejas, Tableros y Gabinetes, Protecciones Modulares, Artefactos e Iluminación.",
                    "Cada registro cuenta con: Código interno, descripción normalizada, unidad comercial, precio en CLP, precio equivalente en UF, proveedor habitual y fecha de cotización.",
                    "Actualización de precios: Al ingresar un nuevo precio de proveedor, el sistema puede actualizarlo únicamente en el proyecto actual o sincronizarlo con el banco maestro compartido.",
                    "Módulo Cotizaciones y Lista de Compra: Con un clic en 'Lista de compra', obtenga el metraje consolidado de todos los insumos del presupuesto para cotizar por volumen con distribuidores."
                },
                "Puede importar listas de precios masivas desde archivos Excel o CSV de sus proveedores habituales usando la función 'Importar catálogo'.",
                "Preste atención a la fecha de vigencia de los precios, especialmente en conductores de cobre y canalizaciones metálicas sujetos a variaciones del mercado internacional.");

            AddStepPage(container, "08", "Revisión Final y Auditoría de Consistencia", "Navegación: Cierre → Revisión final / Botón 'Revisar'",
                Path.Combine(imagesDir, "08_revision_final.png"), "Panel de revisión con diagnóstico automático de partidas incompletas, recursos sin valorizar e inconsistencias.",
                "Realizar una auditoría integral y automatizada del presupuesto antes de enviarlo al mandante, identificando cualquier omisión o error que pudiera comprometer la oferta.",
                new[]
                {
                    "Haga clic en 'Cierre' → 'Revisión final' o presione el botón 'Revisar' ubicado en la cabecera del sistema.",
                    "La pantalla ejecuta de inmediato una batería de comprobaciones lógicas sobre todo el proyecto.",
                    "Alertas Bloqueantes (rojas): Errores críticos que impiden la exportación segura de la oferta (ej: partidas con cantidad cero, materiales sin precio asignado o APUs vacíos).",
                    "Botón 'Ir a corregir': Al hacer clic en este botón junto a cualquier alerta, la aplicación lo transporta de forma automática y precisa a la celda exacta que requiere solución.",
                    "Alertas de Advertencia (amarillas): Sugerencias de buenas prácticas (ej: partidas sin observaciones, mermas inusualmente altas o rendimientos laborales atípicos).",
                    "Una vez que el panel 'Bloqueantes' indique 0 observaciones pendientes, el botón principal 'Generar oferta y exportar →' se habilitará para continuar."
                },
                "El botón 'Ir a corregir' le ahorrará minutos valiosos al navegar directamente al problema en presupuestos de cientos de partidas sin tener que buscar manualmente.",
                "Nunca presente una oferta comercial al mandante si existen partidas bloqueantes sin resolver; esto previene pérdidas económicas severas por omisión de costos en licitaciones.");

            AddStepPage(container, "09", "Oferta Comercial y Exportación Oficial", "Navegación: Cierre → Oferta y exportación",
                Path.Combine(imagesDir, "09_oferta_exportacion.png"), "Asistente (Wizard) de exportación: validación, personalización de portada, vista previa interactiva y generación de PDF/Excel.",
                "Consolidar el presupuesto y generar la documentación formal para el cliente: Carta de Oferta, Resumen Ejecutivo, Presupuesto Detallado y Planillas Excel auditables.",
                new[]
                {
                    "Acceda a 'Cierre' → 'Oferta y exportación'. Se desplegará el Asistente de Cierre en cuatro etapas intuitivas:",
                    "Paso 1 - Validar oferta: Comprobación final de que los montos totales, gastos generales y márgenes comerciales cuadran a la perfección.",
                    "Paso 2 - Configurar: Seleccione los documentos a generar e incorpore el logotipo corporativo.",
                    "Paso 3 - Vista previa: Visualice el PDF renderizado en alta definición antes de emitirlo.",
                    "Paso 4 - Exportar: Presione 'Exportar PDF' o 'Exportar Excel'. El sistema creará los archivos en la carpeta de la obra.",
                    "Libro Excel Oficial: Si el mandante solicita respaldo en planilla, la exportación genera un libro con fórmulas vivas, respetando la estructura formal requerida."
                },
                "El PDF exportado incluye metadatos de seguridad, fecha y hora de emisión, y la firma del responsable técnico del estudio, ofreciendo un estándar corporativo impecable.",
                "Antes de enviar los archivos, abra siempre la carpeta de salida y verifique que los documentos generados correspondan a la última versión cerrada del presupuesto.");

            AddDashboardAndShortcutsPage(container, imagesDir);
        });

        doc.GeneratePdf(outputPath);
        Console.WriteLine("Manual de Usuario generado exitosamente.");
    }

    // =========================================================================
    // DOCUMENTO 2: GUÍA FINANCIERA Y COMERCIAL COMPLETA
    // =========================================================================
    private static void GenerateFinancialGuidePdf(string baseDir, string imagesDir)
    {
        var outputPath = Path.Combine(baseDir, "docs", "manual", "Guia_Financiera_ManoDeObra_y_CierreComercial.pdf");
        Console.WriteLine($"Generando Guía Financiera y Comercial en: {outputPath}");

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
                                .Text("INGENIERÍA ECONÓMICA & COSTOS").FontSize(11).Bold().FontColor(Colors.White);
                            r.RelativeItem().AlignRight().Text("EDICIÓN PRÁCTICA 2026").FontSize(11).Bold().FontColor(Colors.Grey.Lighten2);
                        });

                        head.Item().PaddingTop(25).Text("MANO DE OBRA, FACTOR M/O Y CIERRE COMERCIAL")
                            .FontSize(30).Bold().FontColor(Colors.White).LineHeight(1.15f);

                        head.Item().PaddingTop(8).Text("GUÍA DEFINITIVA DE CONTROL FINANCIERO EN INSTALACIONES ELÉCTRICAS")
                            .FontSize(15).SemiBold().FontColor(ColorAccent);

                        head.Item().PaddingTop(12).Text("Cómo se reparte la mano de obra, el significado del 0,025 × FA, el markup por familias y el control de las 6 tarjetas de costos explicado desde cero.")
                            .FontSize(11).FontColor(Colors.Grey.Lighten3);
                    });

                    col.Item().Padding(35).Column(body =>
                    {
                        body.Item().Text("PROPÓSITO DE ESTE DOCUMENTO")
                            .FontSize(13).Bold().FontColor(ColorNavy);

                        body.Item().PaddingTop(6).Text(
                            "En el mundo real de las obras eléctricas, el mayor riesgo de quiebra no proviene de cotizar mal un cable o un transformador, " +
                            "sino de desalinear el costo real de la nómina de faena con los precios unitarios presentados al mandante. " +
                            "Este documento explica en lenguaje claro y sin tecnicismos oscuros la lógica matemática y estratégica de Qubi Cost Electrical: " +
                            "por qué la mano de obra se proyecta en meses, cómo el Factor de Ajuste (FA) calibra el presupuesto, por qué los factores de venta " +
                            "se mueven entre familias de materiales, y cómo interpretar cada cifra del tablero de costos.")
                            .FontSize(9.5f).LineHeight(1.4f).FontColor(ColorTextBody);

                        body.Item().PaddingTop(18).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                            {
                                c.Item().Text("1. REPARTO DE MANO DE OBRA").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(3).Text("• Método A (por tornillo y cable)\n• Método B (por nómina en meses)\n• Leyes sociales (30%-35%)\n• Curva de dotación en campana")
                                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                            });

                            row.Spacing(10);

                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                            {
                                c.Item().Text("2. FÓRMULA 0,025 × FA").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(3).Text("• Qué es el 2,5% de base\n• El FA como sintonizador global\n• Calibración contra nómina real\n• Por qué cambian los materiales")
                                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                            });
                        });

                        body.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                            {
                                c.Item().Text("3. FACTORES DE PRESENTACIÓN").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(3).Text("• Comparabilidad de precios\n• Cables 1,72× vs Equipos 1,43×\n• Mermas y roturas en losa\n• Protección ante recortes de obra")
                                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                            });

                            row.Spacing(10);

                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                            {
                                c.Item().Text("4. TABLERO DE 6 KPIS").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(3).Text("• Costo de Ejecución ($342M)\n• Costo Total Proyecto ($387M)\n• Sobreganancia Delta ($7,2M)\n• Venta Neta y Margen Real 13,1%")
                                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                            });
                        });

                        body.Item().PaddingTop(25).BorderTop(1).BorderColor(ColorBorderLight).PaddingTop(12).Row(r =>
                        {
                            r.AutoItem().Text("Qubi Cost Electrical — Guía de Estrategia Financiera y Cierre")
                                .FontSize(8.5f).FontColor(ColorTextMuted);
                            r.RelativeItem().AlignRight().Text("Octubre 2026 — Documento de Formación Interna")
                                .FontSize(8.5f).FontColor(ColorTextMuted);
                        });
                    });
                });
            });

            // Página 2: El Gran Dilema de la Mano de Obra
            AddPageCapitulo1ManoDeObra(container);

            // Página 3: La Lista de Precios y el Enigma de 0,025 x FA
            AddPageCapitulo2FormulaFa(container);

            // Página 4: ¿Cómo Cierran las Empresas Grandes? (Comparativa de Mercado)
            AddPageCapitulo3ModelosEmpresasGrandes(container);

            // Página 5: Factores de Presentación por Familia (Análisis de Imagen 1)
            AddPageCapitulo4FactoresFamilias(container, imagesDir);

            // Página 6: El Tablero Financiero y los 6 KPIs (Análisis de Imagen 2)
            AddPageCapitulo5TableroKpis(container, imagesDir);

            // Página 7: Resumen Ejecutivo y Matriz de Decisiones
            AddPageCapitulo6MatrizDecisiones(container);
        });

        doc.GeneratePdf(outputPath);
        Console.WriteLine("Guía Financiera y Comercial generada exitosamente.");
    }

    // =========================================================================
    // SECCIONES DE LA GUÍA FINANCIERA
    // =========================================================================

    private static void AddPageCapitulo1ManoDeObra(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "CAPÍTULO 1: EL REPARTO DE LA MANO DE OBRA"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("El Gran Dilema: ¿Cómo se Reparte la Mano de Obra?")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Cuando preparas el presupuesto de una instalación eléctrica (por ejemplo, un edificio de 10 pisos que durará 8 meses), " +
                    "te enfrentas de inmediato a dos formas completamente opuestas de calcular el costo del personal:")
                    .FontSize(9.5f).LineHeight(1.35f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Row(r =>
                {
                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                    {
                        c.Item().Text("MÉTODO A: \"POR TORNILLO Y CABLE\" (TEÓRICO)").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                        c.Item().PaddingTop(4).Text(
                            "Abres la lista de materiales y sumas la mano de obra unitaria:\n" +
                            "• Instalar 1 m de tubería = $500\n" +
                            "• Instalar 1 enchufe = $2.000\n" +
                            "• Cablear 1 metro = $150\n\n" +
                            "Multiplicas por todas las cantidades de la obra y la suma teórica te da: $50.000.000 en mano de obra.")
                            .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                    });

                    r.Spacing(12);

                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                    {
                        c.Item().Text("MÉTODO B: \"POR PERSONAS EN EL TIEMPO\" (REAL)").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                        c.Item().PaddingTop(4).Text(
                            "En la realidad tú no contratas electricistas por 5 minutos. Contratas un equipo humano por meses de faena:\n" +
                            "• 1 Capataz permanente los 8 meses\n" +
                            "• 6 Maestros y 6 Ayudantes en los meses pico\n" +
                            "• Sueldos base + Leyes Sociales (32%)\n\n" +
                            "Calculas la nómina real de los 8 meses y el costo real es: $80.000.000.")
                            .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                    });
                });

                col.Item().PaddingTop(12).Border(1).BorderColor(ColorDangerBorder).Background(ColorDangerBg).Padding(10).Column(c =>
                {
                    c.Item().Text("EL RIESGO MORTAL DEL CONTRATISTA ELÉCTRICO").FontSize(9.5f).Bold().FontColor(ColorDangerText);
                    c.Item().PaddingTop(2).Text(
                        "Si cobras al cliente según la suma teórica del Método A ($50 millones), pero a fin de mes la nómina de sueldos de tus trabajadores " +
                        "te exige pagar $80 millones (Método B)... ¡Perdiste $30 millones de tu propio bolsillo y la empresa quiebra!\n" +
                        "Y al revés: si inflas las partidas a ciegas sin base matemática, quedas fuera de mercado en la licitación.")
                        .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                });

                col.Item().PaddingTop(12).Text("La Curva de Dotación en el Tiempo (Curva de Campana)").FontSize(12).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "En una obra, la mano de obra directa no es plana; sigue tres etapas biológicas bien definidas que el módulo de Mano de Obra modela:")
                    .FontSize(9).FontColor(ColorTextBody);

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(90);
                        columns.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(ColorNavy).Padding(5).Text("FASE").FontSize(8.5f).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(5).Text("DURACIÓN").FontSize(8.5f).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(5).Text("LABOR EN TERRENO").FontSize(8.5f).Bold().FontColor(Colors.White);
                    });

                    table.Cell().Background(ColorBgCard).Padding(5).Text("1. Ramp-up (Subida)").FontSize(8).Bold().FontColor(ColorNavy);
                    table.Cell().Background(ColorBgCard).Padding(5).Text("Primeros 2 meses").FontSize(8).FontColor(ColorTextBody);
                    table.Cell().Background(ColorBgCard).Padding(5).Text("Empalme provisorio, loseros, trazado de losas de hormigón y canalización subterránea. Dotación baja (3 a 5 personas).").FontSize(8).FontColor(ColorTextBody);

                    table.Cell().Background(Colors.White).Padding(5).Text("2. Peak (Pico de obra)").FontSize(8).Bold().FontColor(ColorPrimaryBlue);
                    table.Cell().Background(Colors.White).Padding(5).Text("Meses 3 al 6").FontSize(8).FontColor(ColorTextBody);
                    table.Cell().Background(Colors.White).Padding(5).Text("Fase intensa: enlauche, cableado masivo de alimentadores, montaje de bandejas y tableros. Máxima dotación (12 a 18 personas).").FontSize(8).FontColor(ColorTextBody);

                    table.Cell().Background(ColorBgCard).Padding(5).Text("3. Ramp-down (Bajada)").FontSize(8).Bold().FontColor(ColorNavy);
                    table.Cell().Background(ColorBgCard).Padding(5).Text("Últimos 2 meses").FontSize(8).FontColor(ColorTextBody);
                    table.Cell().Background(ColorBgCard).Padding(5).Text("Instalación de artefactos finos, placas, pruebas de aislamiento, comisionamiento y trámites TE1 ante la SEC (4 personas).").FontSize(8).FontColor(ColorTextBody);
                });

                col.Item().PaddingTop(10).Border(1).BorderColor(ColorInfoBorder).Background(ColorInfoBg).Padding(8).Column(c =>
                {
                    c.Item().Text("¿QUÉ SON LAS LEYES SOCIALES (30% - 35%)?").FontSize(9).Bold().FontColor(ColorInfoText);
                    c.Item().PaddingTop(2).Text(
                        "En Chile, pagar un sueldo líquido de $800.000 a un maestro le cuesta a la empresa cerca de $1.080.000. " +
                        "Las leyes sociales cubren: imposiciones previsionales AFP y Fonasa/Isapre, Seguro de Invalidez y Sobrevivencia (SIS), seguro de accidentes del trabajo (Mutual CChC / ACHS / IST), seguro de cesantía (AFC), más la provisión obligatoria de feriado legal (vacaciones) y finiquitos de término de faena.")
                        .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                });
            });
        });
    }

    private static void AddPageCapitulo2FormulaFa(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "CAPÍTULO 2: LA FÓRMULA 0,025 × FA Y SU FUNCIÓN"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("La Lista de Precios y el Misterio de `0,025 × FA`")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "En la biblioteca de precios de la empresa, cada material tiene su costo de compra (MatClp) y su costo de instalación (MoClp). " +
                    "Sin embargo, existen materiales con mano de obra fija y otros que usan la fórmula del porcentaje:")
                    .FontSize(9.5f).LineHeight(1.35f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Row(r =>
                {
                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                    {
                        c.Item().Text("MATERIALES CON MO FIJA TABULADA").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                        c.Item().PaddingTop(4).Text(
                            "Son elementos grandes o de labor estandarizada:\n" +
                            "• Tablero general TDF = $50.000 fija de MO\n" +
                            "• Centro de alumbrado = $4.500 fija de MO\n" +
                            "• Enchufe doble = $2.500 fija de MO\n\n" +
                            "Su mano de obra se conoce de antemano y no cambia con el precio del material.")
                            .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                    });

                    r.Spacing(12);

                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                    {
                        c.Item().Text("MATERIALES CON FÓRMULA: Material × 0,025 × FA").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                        c.Item().PaddingTop(4).Text(
                            "Son miles de piezas accesorias y fijaciones:\n" +
                            "• Abrazaderas caddy, pernos, rieles unistrut\n" +
                            "• Coplas, curvas, terminales, cajas de paso\n" +
                            "• Mallas a tierra, barras copperweld\n\n" +
                            "Nadie mide minutos por perno. Su mano de obra se calcula como un porcentaje de su valor por el FA.")
                            .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                    });
                });

                col.Item().PaddingTop(12).Text("¿Qué significa exactamente el 0,025?").FontSize(12).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(3).Text(
                    "El número 0,025 representa el 2,5% del valor de compra del material. " +
                    "Nació en las planillas maestras de presupuestos de instaladores eléctricos como una regla empírica: en piezas chicas y ferretería, " +
                    "el esfuerzo físico de montaje es proporcional al costo del insumo (un accesorio caro requiere más cuidado y fijación que uno barato). " +
                    "Pero el 2,5% por sí solo da un valor muy pequeño (ej: si una abrazadera vale $1.000, el 2,5% son apenas $25 pesos). " +
                    "Por eso requiere un multiplicador global de escala: el Factor de Ajuste (FA).")
                    .FontSize(9).LineHeight(1.35f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Border(1).BorderColor(ColorSuccessBorder).Background(ColorSuccessBg).Padding(10).Column(c =>
                {
                    c.Item().Text("¿QUÉ ES EL FACTOR M/O (FA) Y CÓMO SE CALCULA?").FontSize(9.5f).Bold().FontColor(ColorSuccessText);
                    c.Item().PaddingTop(2).Text(
                        "El FA es el \"sintonizador de radio\" que conecta la nómina real con el presupuesto. " +
                        "La fórmula matemática exacta que ejecuta el sistema es:\n\n" +
                        "FA = (Mano de Obra Real Proyectada − MO Fija ya cubierta) ÷ Σ (Material × 2,5% × Cantidad)\n\n" +
                        "Ejemplo con números reales:\n" +
                        "1. Tu nómina real de sueldos exige: $80.000.000\n" +
                        "2. La mano de obra fija de tableros y centros cubre: $30.000.000\n" +
                        "3. Remanente que falta por cobrar al cliente: $50.000.000\n" +
                        "4. La suma de todas las bases al 2,5% de tus materiales suma: $796.178\n" +
                        "5. El FA resultante es: $50.000.000 ÷ $796.178 = 62,80\n\n" +
                        "Al aplicar FA = 62,80 a los materiales, ¡la mano de obra total del presupuesto da EXACTO los $80 millones que necesitas para pagar los sueldos!")
                        .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                });

                col.Item().PaddingTop(10).Text("¿Por qué cambia el precio de los materiales al usar el factor?").FontSize(11.5f).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(2).Text(
                    "Si el cliente te avisa que la obra se retrasará y durará 10 meses en vez de 8, vas al módulo de Mano de Obra y aumentas los meses. " +
                    "Tu nómina sube de $80M a $100M. Al recalcular el FA, este sube (por ejemplo de 62,80 a 78,50). " +
                    "De inmediato, la columna de mano de obra de los materiales en la cubicación sube de precio para absorber los $20 millones adicionales. " +
                    "Así tu presupuesto queda blindado y jamás trabajas a pérdida por sobrecostos de personal.")
                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
            });
        });
    }

    private static void AddPageCapitulo3ModelosEmpresasGrandes(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "CAPÍTULO 3: COMPARATIVA CON EMPRESAS GRANDES"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("¿Este Cierre Comercial es Usado en Empresas Reales?")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Sí. En el mercado de la construcción y montaje existen tres grandes formas de cerrar comercialmente un presupuesto. " +
                    "El modelo de Qubi Cost Electrical (Factores por Familia + FA) es el estándar indiscutido entre contratistas especialistas:")
                    .FontSize(9.5f).LineHeight(1.35f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(120);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.6f);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(ColorNavy).Padding(6).Text("MODELO").FontSize(8.5f).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(6).Text("DÓNDE SE USA").FontSize(8.5f).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(6).Text("MECÁNICA COMERCIAL Y VENTAJAS").FontSize(8.5f).Bold().FontColor(Colors.White);
                    });

                    // Fila 1
                    table.Cell().Background(ColorBgCard).Padding(6).Column(c =>
                    {
                        c.Item().Text("1. Factores + FA").FontSize(8.5f).Bold().FontColor(ColorPrimaryBlue);
                        c.Item().Text("(Markup por Familia)").FontSize(7.5f).Italic().FontColor(ColorTextMuted);
                    });
                    table.Cell().Background(ColorBgCard).Padding(6).Text(
                        "Subcontratistas e instaladores eléctricos medianos y grandes en edificación habitacional, oficinas, centros comerciales, clínicas e industria ligera (ej: Ingelsam, Fleischmann, Schréder, contratistas CChC).")
                        .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    table.Cell().Background(ColorBgCard).Padding(6).Text(
                        "• Oculta la estructura interna de costos (el cliente no ve tus sueldos ni tu margen real).\n" +
                        "• Defiende tu utilidad ante recortes de partidas del mandante.\n" +
                        "• Carga más margen en ítems masivos (cables) y menos en equipos comparables.")
                        .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);

                    // Fila 2
                    table.Cell().Background(Colors.White).Padding(6).Column(c =>
                    {
                        c.Item().Text("2. APU Puro Clásico").FontSize(8.5f).Bold().FontColor(ColorNavy);
                        c.Item().Text("(Precios Unitarios Abiertos)").FontSize(7.5f).Italic().FontColor(ColorTextMuted);
                    });
                    table.Cell().Background(Colors.White).Padding(6).Text(
                        "Grandes licitaciones de infraestructura pública (MOP, Serviu, Metro de Santiago), minería (Codelco, BHP, Antofagasta Minerals) y megaproyectos viales.")
                        .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    table.Cell().Background(Colors.White).Padding(6).Text(
                        "• Las bases exigen un libro de APU 100% desglosado: Horas-Hombre (HH) directas de cuadrilla × tarifa horaria oficial.\n" +
                        "• Los Gastos Generales (12%) y la Utilidad (8%) van abiertos en una carátula única.\n" +
                        "• Los fiscalizadores auditan rendimientos y no permiten factores ocultos.")
                        .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);

                    // Fila 3
                    table.Cell().Background(ColorBgCard).Padding(6).Column(c =>
                    {
                        c.Item().Text("3. Open Book / Cost-Plus").FontSize(8.5f).Bold().FontColor(ColorNavy);
                        c.Item().Text("(Libros Abiertos y Target Price)").FontSize(7.5f).Italic().FontColor(ColorTextMuted);
                    });
                    table.Cell().Background(ColorBgCard).Padding(6).Text(
                        "Megaproyectos industriales, celulosas, centrales de energía y consorcios multinacionales de ingeniería (Bechtel, Fluor, Wood, Techint).")
                        .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    table.Cell().Background(ColorBgCard).Padding(6).Text(
                        "• Los materiales se compran al costo de factura auditada por el mandante.\n" +
                        "• La mano de obra se liquida contra marcación de huella biométrica en el torniquete de faena.\n" +
                        "• El contratista cobra un honorario de gerenciamiento acordado (Management Fee de 6% a 10%).")
                        .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                });

                col.Item().PaddingTop(12).Text("¿Por qué las Empresas Prefieren el Modelo de Factores en Edificación?").FontSize(11.5f).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(3).Text(
                    "En edificación privada (inmobiliarias, constructoras, retail), si entregas un presupuesto con APU abierto mostrando tus sueldos y un 15% de utilidad neta, " +
                    "el gerente de compras de la constructora te llamará a su oficina para negociar y te dirá: \"Bájame la utilidad del 15% al 6% o se lo doy a otro\".\n\n" +
                    "Con el modelo de Factores por Familia, el cliente solo ve precios unitarios de mercado competitivos en cada partida (el enchufe a $18.500, el metro de cable a $4.200). " +
                    "Tu margen y tu mano de obra están perfectamente distribuidos y blindados dentro de los materiales, permitiéndote negociar sin que nadie toque la salud financiera de tu empresa.")
                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Border(1).BorderColor(ColorWarnBorder).Background(ColorWarnBg).Padding(8).Row(r =>
                {
                    r.AutoItem().Text("NOTA CLAVE: ").FontSize(8.5f).Bold().FontColor(ColorWarnText);
                    r.RelativeItem().Text("Qubi Cost Electrical soporta ambos mundos: puedes cerrar usando la estrategia de Factores Comerciales (PEL) para obras privadas, o usar el motor de APU con Horas-Hombre cuando una licitación formal lo exija.")
                        .FontSize(8).FontColor(ColorTextBody);
                });
            });
        });
    }

    private static void AddPageCapitulo4FactoresFamilias(IDocumentContainer container, string imagesDir)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "CAPÍTULO 4: FACTORES DE PRESENTACIÓN POR FAMILIA"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("Estrategia Comercial: Factores de Presentación")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Esta es la tabla de Factores de Presentación donde se define cómo se transforma el costo de costo directo en precio de venta:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                var imgPath = Path.Combine(imagesDir, "factor_familias_tabla.png");
                if (File.Exists(imgPath))
                {
                    col.Item().PaddingTop(6).AlignCenter().Column(c =>
                    {
                        c.Item().Border(1).BorderColor(ColorBorderLight).Padding(2).Width(515).Image(imgPath);
                        c.Item().PaddingTop(2).AlignCenter().Text("Figura: Tabla de Factores de Presentación por Familia comercial (Costo UF, Factor, Dcto%, Efectivo y Venta UF).")
                            .FontSize(7.5f).Italic().FontColor(ColorTextMuted);
                    });
                }

                col.Item().PaddingTop(8).Text("Explicación de las Columnas").FontSize(11).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(3).Column(list =>
                {
                    list.Item().PaddingBottom(2).Text(t =>
                    {
                        t.Span("• COSTO UF: ").Bold().FontColor(ColorPrimaryBlue).FontSize(8.5f);
                        t.Span("Es lo que a ti te cuesta comprar e instalar físicamente ese grupo de materiales sin ningún recargo. (Ej: en Materiales cuesta 963,946 UF).").FontSize(8.5f).FontColor(ColorTextBody);
                    });
                    list.Item().PaddingBottom(2).Text(t =>
                    {
                        t.Span("• FACTOR: ").Bold().FontColor(ColorPrimaryBlue).FontSize(8.5f);
                        t.Span("El multiplicador de venta para esa familia. Un factor de 1,72 significa que cobras el costo (1,0) más un 72% adicional (0,72) de margen bruto.").FontSize(8.5f).FontColor(ColorTextBody);
                    });
                    list.Item().PaddingBottom(2).Text(t =>
                    {
                        t.Span("• DCTO %: ").Bold().FontColor(ColorPrimaryBlue).FontSize(8.5f);
                        t.Span("Descuentos especiales obtenidos de proveedores o trasladados al cliente (en la imagen está en 0%).").FontSize(8.5f).FontColor(ColorTextBody);
                    });
                    list.Item().PaddingBottom(2).Text(t =>
                    {
                        t.Span("• VENTA UF: ").Bold().FontColor(ColorPrimaryBlue).FontSize(8.5f);
                        t.Span("El precio final que ve el cliente: Costo UF × Factor Efectivo (963,946 × 1,72 = 1.657,987 UF).").FontSize(8.5f).FontColor(ColorTextBody);
                    });
                });

                col.Item().PaddingTop(8).Text("¿Por qué el Factor \"se Mueve\" y es Distinto para Cada Familia?").FontSize(11).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(3).Text(
                    "Fíjate que Materiales y PVC tienen 1,72×, Tableros tiene 1,44×, y Equipos tiene 1,43×. El factor se mueve por 3 razones estratégicas:")
                    .FontSize(8.5f).FontColor(ColorTextBody);

                col.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(7).Column(c =>
                    {
                        c.Item().Text("1. COMPARABILIDAD COMERCIAL").FontSize(8.5f).Bold().FontColor(ColorNavy);
                        c.Item().PaddingTop(2).Text(
                            "Un Generador o Transformador (Equipos 1,43) lo cotiza el cliente en 10 minutos con el fabricante. Si le pones 1,72 te descarta por caro.\n" +
                            "En cambio, nadie puede auditar 25.000 m de cables en el plano (1,72). Ahí colocas tu margen fuerte.")
                            .FontSize(7.5f).LineHeight(1.25f).FontColor(ColorTextBody);
                    });

                    r.Spacing(8);

                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(7).Column(c =>
                    {
                        c.Item().Text("2. RIESGO DE ROTURAS Y MERMA").FontSize(8.5f).Bold().FontColor(ColorNavy);
                        c.Item().PaddingTop(2).Text(
                            "Las tuberías y cables (1,72) sufren despuntes, cortes y roturas al hormigonar losas en faena.\n" +
                            "Un tablero (1,44) viene armado de fábrica y tiene casi cero merma física.")
                            .FontSize(7.5f).LineHeight(1.25f).FontColor(ColorTextBody);
                    });

                    r.Spacing(8);

                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(7).Column(c =>
                    {
                        c.Item().Text("3. DEFENSA ANTE RECORTES").FontSize(8.5f).Bold().FontColor(ColorNavy);
                        c.Item().PaddingTop(2).Text(
                            "Si el cliente decide comprar el grupo electrógeno por su cuenta para ahorrar, no pierdes tu utilidad de la obra.\n" +
                            "Tu ganancia está distribuida y protegida en los ductos y cables que sí o sí tienes que montar tú.")
                            .FontSize(7.5f).LineHeight(1.25f).FontColor(ColorTextBody);
                    });
                });
            });
        });
    }

    private static void AddPageCapitulo5TableroKpis(IDocumentContainer container, string imagesDir)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "CAPÍTULO 5: EL TABLERO DE CONTROL Y LAS 6 TARJETAS"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("El Tablero de Control Financiero (Hero KPIs)")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Esta barra superior resume la salud financiera completa de la propuesta eléctrica antes de emitir la oferta formal:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                var imgPath = Path.Combine(imagesDir, "desglose_costos_kpis.png");
                if (File.Exists(imgPath))
                {
                    col.Item().PaddingTop(6).AlignCenter().Column(c =>
                    {
                        c.Item().Border(1).BorderColor(ColorBorderLight).Padding(2).Width(515).Image(imgPath);
                        c.Item().PaddingTop(2).AlignCenter().Text("Figura: Las 6 tarjetas financieras de Desglose de Costos (Ejecución, Costo Total, Diferencia, Margen, Venta Neta y Unitaria).")
                            .FontSize(7.5f).Italic().FontColor(ColorTextMuted);
                    });
                }

                col.Item().PaddingTop(6).Text("Explicación de las 6 Tarjetas con Manzanitas").FontSize(11).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(125);
                        columns.ConstantColumn(85);
                        columns.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(ColorNavy).Padding(5).Text("TARJETA").FontSize(8).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(5).Text("VALOR EJEMPLO").FontSize(8).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(5).Text("SIGNIFICADO PRÁCTICO PARA EL ESTIMADOR").FontSize(8).Bold().FontColor(Colors.White);
                    });

                    // 1
                    table.Cell().Background(ColorBgCard).Padding(4).Text("1. COSTO DE EJECUCIÓN").FontSize(8).Bold().FontColor(ColorDangerText);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("$ 342.518.572").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("Tu costo directo puro de faena: compra de materiales + sueldos con leyes sociales + arriendo de andamios y EPPs. Si vendes bajo este monto, pagas tú de tu bolsillo.").FontSize(7.5f).FontColor(ColorTextBody);

                    // 2
                    table.Cell().Background(Colors.White).Padding(4).Text("2. COSTO TOTAL PROYECTO").FontSize(8).Bold().FontColor(ColorNavy);
                    table.Cell().Background(Colors.White).Padding(4).Text("$ 387.045.986").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(Colors.White).Padding(4).Text("Costo de obra + Gastos Generales corporativos de oficina central (arriendos, contador, gerencia) + la Utilidad base presupuestada. Es lo que deberías cobrar técnicamente.").FontSize(7.5f).FontColor(ColorTextBody);

                    // 3
                    table.Cell().Background(ColorBgCard).Padding(4).Text("3. DIFERENCIA C/ PROYECTO").FontSize(8).Bold().FontColor(ColorSuccessText);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("+ $ 7.284.677").FontSize(8).Bold().FontColor(ColorSuccessText);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("Tu sobreganancia comercial: Venta Neta ($394M) − Costo Total ($387M). Como tus factores en cables son 1,72, ganas $7,2 millones POR ENCIMA de tu meta. (Si fuera negativo, faltaría margen).").FontSize(7.5f).FontColor(ColorTextBody);

                    // 4
                    table.Cell().Background(Colors.White).Padding(4).Text("4. MARGEN REAL").FontSize(8).Bold().FontColor(ColorPrimaryBlue);
                    table.Cell().Background(Colors.White).Padding(4).Text("13,1 %").FontSize(8).Bold().FontColor(ColorPrimaryBlue);
                    table.Cell().Background(Colors.White).Padding(4).Text("La tajada de dinero limpio sobre la venta: (Venta $394M − Ejecución $342M) ÷ Venta $394M. De cada $100 que el cliente te paga, $13,1 quedan líquidos en tu empresa.").FontSize(7.5f).FontColor(ColorTextBody);

                    // 5
                    table.Cell().Background(ColorBgCard).Padding(4).Text("5. VENTA NETA").FontSize(8).Bold().FontColor(ColorNavy);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("$ 394.330.663").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("El precio contractual final sin IVA que se imprime en la Carta de Oferta formal para el mandante. Sobre este monto se factura con el 19% de IVA.").FontSize(7.5f).FontColor(ColorTextBody);

                    // 6
                    table.Cell().Background(Colors.White).Padding(4).Text("6. VENTA UNITARIA").FontSize(8).Bold().FontColor(ColorTextMuted);
                    table.Cell().Background(Colors.White).Padding(4).Text("$ 0 / m² (Sin m²)").FontSize(8).Bold().FontColor(ColorTextMuted);
                    table.Cell().Background(Colors.White).Padding(4).Text("Ratio de comparación: Venta ÷ m² construidos. Sale $0 porque falta ingresar los metros cuadrados en 'Datos del proyecto'. Permite saber al tiro si estás en rango de mercado.").FontSize(7.5f).FontColor(ColorTextBody);
                });

                col.Item().PaddingTop(8).Text("Los Botones de Acción Superior").FontSize(10.5f).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(2).Text(
                    "• Restaurar: Deshace cambios de factores. • Guardar preset: Guarda tu combinación ganadora. " +
                    "• Cargar de otro proyecto: Copia factores de un presupuesto exitoso. • Fijar factores actuales: Congela los números. " +
                    "• Aplicar al presupuesto (Botón Azul): Actualiza todo el proyecto con estos precios de venta.")
                    .FontSize(8).FontColor(ColorTextBody);
            });
        });
    }

    private static void AddPageCapitulo6MatrizDecisiones(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderHeader(h, "RESUMEN Y REGLAS DE ORO DEL ESTIMADOR"));
            page.Footer().Element(RenderFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("Reglas de Oro del Cierre Comercial Eléctrico")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Memoriza estas cinco reglas fundamentales para asegurar que tus propuestas sean competitivas y rentables:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Column(rules =>
                {
                    var items = new (string Num, string Title, string Desc)[]
                    {
                        ("Regla 1", "El Costo de Ejecución es tu Límite Sagrado", "Jamás aceptes una adjudicación o descuento que deje la Venta Neta por debajo del Costo de Ejecución. Vender por debajo significa pagar tú mismo por trabajar."),
                        ("Regla 2", "La Diferencia con Proyecto Siempre Debe Ser Positiva", "Verifica siempre que la Tarjeta 3 sea mayor a $0 (+). Si está en rojo con signo menos, significa que tus factores comerciales no alcanzan a pagar los costos fijos de tu oficina."),
                        ("Regla 3", "Vuelve a Calibrar el FA si el Plazo de Obra Cambia", "Si el cliente pasa la obra de 6 a 9 meses, tu nómina de sueldos sube un 50%. Ve al módulo de Mano de Obra, actualiza la duración y recalcula el FA para que los materiales absorban el costo."),
                        ("Regla 4", "Carga el Margen Fuerte en Materiales Masivos, No en Máquinas", "Aplica factores altos (1,70 - 1,75) en cables y ductos donde nadie puede auditar metrajes, y factores ajustados (1,35 - 1,43) en generadores, celdas y transformadores fácilmente comparables."),
                        ("Regla 5", "Completa Siempre los Metros Cuadrados en Datos del Proyecto", "Al llenar la superficie en m², la Tarjeta 6 te dirá al instante tu costo/m² ($/m² y UF/m²), permitiéndote chequear en 5 segundos si estás dentro del rango de mercado antes de enviar la carta.")
                    };

                    foreach (var r in items)
                    {
                        rules.Item().PaddingBottom(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Row(row =>
                        {
                            row.AutoItem().Background(ColorNavy).PaddingVertical(3).PaddingHorizontal(7)
                                .Text(r.Num).FontSize(8.5f).Bold().FontColor(Colors.White);
                            row.RelativeItem().PaddingLeft(10).Column(c =>
                            {
                                c.Item().Text(r.Title).FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(1).Text(r.Desc).FontSize(8).FontColor(ColorTextBody);
                            });
                        });
                    }
                });

                col.Item().PaddingTop(12).Border(1).BorderColor(ColorInfoBorder).Background(ColorInfoBg).Padding(10).Column(c =>
                {
                    c.Item().Text("GLOSARIO RÁPIDO PARA EL ESTIMADOR").FontSize(9.5f).Bold().FontColor(ColorInfoText);
                    c.Item().PaddingTop(3).Text(
                        "• Costo Directo: Lo que se queda pegado en la pared de la obra (materiales y mano de obra de montaje).\n" +
                        "• Gastos Generales (GG): Costo de mantener la empresa funcionando (oficina, vehículos, gerencia, seguros).\n" +
                        "• FA (Factor de Ajuste): Índice que escala la mano de obra en los materiales para igualar la nómina real.\n" +
                        "• 0,025: Porcentaje base del 2,5% del valor de materiales menores usado para estimar su esfuerzo de montaje.\n" +
                        "• Markup / Factor: Multiplicador comercial (ej. 1,44 = costo + 44% de margen) para fijar el precio de venta.")
                        .FontSize(8).LineHeight(1.35f).FontColor(ColorTextBody);
                });
            });
        });
    }

    // =========================================================================
    // MÉTODOS AUXILIARES COMUNES
    // =========================================================================

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

                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Row(r =>
                {
                    r.AutoItem().Text("OBJETIVO: ").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                    r.RelativeItem().Text(objective).FontSize(9.5f).FontColor(ColorTextBody);
                });

                if (File.Exists(imagePath))
                {
                    col.Item().PaddingTop(10).AlignCenter().Column(imgCol =>
                    {
                        imgCol.Item().Border(1).BorderColor(ColorBorderLight).Padding(2).MaxHeight(245).Image(imagePath);
                        imgCol.Item().PaddingTop(3).AlignCenter().Text($"Figura {stepNumber}: {imageCaption}")
                            .FontSize(8).Italic().FontColor(ColorTextMuted);
                    });
                }

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

    // =========================================================================
    // DOCUMENTO 3: DOSSIER INTEGRAL PARA DESARROLLO DE WEB PROMOCIONAL
    // =========================================================================
    private static void GenerateWebPromoBriefPdf(string baseDir, string imagesDir)
    {
        var outputPath = Path.Combine(baseDir, "docs", "manual", "Dossier_Promocional_Web_QubiCost_Electrical.pdf");
        Console.WriteLine($"Generando Dossier Web Promocional en: {outputPath}");

        var doc = Document.Create(container =>
        {
            // PÁGINA 1: PORTADA OFICIAL (MARKETING & PRODUCT SPEC)
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
                                .Text("PRODUCT SPEC & MARKETING BRIEF").FontSize(10.5f).Bold().FontColor(Colors.White);
                            r.RelativeItem().AlignRight().Text("EDICIÓN WEB 2026").FontSize(11).Bold().FontColor(Colors.Grey.Lighten2);
                        });

                        head.Item().PaddingTop(25).Text("QUBI COST ELECTRICAL")
                            .FontSize(36).Bold().FontColor(Colors.White).LineHeight(1.15f);

                        head.Item().PaddingTop(6).Text("DOSSIER OFICIAL PARA DESARROLLO DE SITIO WEB PROMOCIONAL")
                            .FontSize(15).SemiBold().FontColor(ColorAccent);

                        head.Item().PaddingTop(12).Text("Guía maestra de producto, propuesta de valor, arquitectura de funciones, capturas oficiales y estructura de conversión para el equipo de desarrollo web.")
                            .FontSize(11).FontColor(Colors.Grey.Lighten3);
                    });

                    col.Item().Padding(35).Column(body =>
                    {
                        body.Item().Text("OBJETIVO DE ESTE DOCUMENTO PARA EL EQUIPO WEB")
                            .FontSize(13).Bold().FontColor(ColorNavy);

                        body.Item().PaddingTop(6).Text(
                            "Este dossier condensa todo lo que la aplicación hace, cómo resuelve los dolores más críticos del sector eléctrico " +
                            "y cómo debe presentarse cada funcionalidad en el nuevo sitio web promocional para lograr la máxima conversión de clientes. " +
                            "Incluye titulares sugeridos, estructura de secciones (wireframe), capturas reales de la interfaz, tabla comparativa contra Excel, " +
                            "paleta de colores oficial y especificaciones de marca para que el sitio web sea técnicamente impecable y comercialmente irresistible.")
                            .FontSize(9.5f).LineHeight(1.4f).FontColor(ColorTextBody);

                        body.Item().PaddingTop(18).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                            {
                                c.Item().Text("FICHA TÉCNICA DE PRODUCTO").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(3).Text("• Nombre: Qubi Cost Electrical (v1.1.2)\n• Tipo: Desktop App nativa (WPF / .NET 10)\n• Formato: Base de datos SQLite (.qce)\n• Plataforma: Windows 64-bit exclusivo\n• Licenciamiento: Por usuario / red local")
                                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                            });

                            row.Spacing(10);

                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                            {
                                c.Item().Text("PÚBLICO OBJETIVO (TARGET)").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(3).Text("• Empresas de Montaje Eléctrico\n• Subcontratistas en Edificación e Industria\n• Estimadores y Jefes de Propuestas\n• Contratistas Eléctricos Independientes\n• Oficinas Técnicas y Consultores SEC")
                                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                            });
                        });

                        body.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(10).Column(c =>
                            {
                                c.Item().Text("EL DIFERENCIADOR RADICAL").FontSize(9.5f).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().PaddingTop(3).Text("No es otro software genérico de construcción: fue diseñado por y para ingenieros eléctricos, uniendo la cubicación de conductores, tableros y cuadros C.SC con la sincronización de nómina real en el tiempo.")
                                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);
                            });
                        });

                        body.Item().PaddingTop(25).BorderTop(1).BorderColor(ColorBorderLight).PaddingTop(12).Row(r =>
                        {
                            r.AutoItem().Text("Qubi Cost Electrical — Brief de Producto y Especificación Web")
                                .FontSize(8.5f).FontColor(ColorTextMuted);
                            r.RelativeItem().AlignRight().Text("Octubre 2026 — Documento de Marketing y UX")
                                .FontSize(8.5f).FontColor(ColorTextMuted);
                        });
                    });
                });
            });

            // PÁGINA 2: PROPUESTA DE VALOR Y EL DOLOR DEL CLIENTE
            AddPageWebCapitulo1PropuestaValor(container);

            // PÁGINA 3: SHOWCASE DE INGENIERÍA ELÉCTRICA (ITEMIZADO, CUBICACIÓN, C.SC)
            AddPageWebCapitulo2ShowcaseIngenieria(container, imagesDir);

            // PÁGINA 4: SHOWCASE DE INTELIGENCIA FINANCIERA (MANO DE OBRA, FACTORES, KPIS)
            AddPageWebCapitulo3ShowcaseFinanciero(container, imagesDir);

            // PÁGINA 5: SHOWCASE DE CONFIANZA, CALIDAD Y CIERRE (AUDITORÍA, EXPORTACIÓN, RED)
            AddPageWebCapitulo4ShowcaseCalidad(container, imagesDir);

            // PÁGINA 6: WIREFRAME Y ARQUITECTURA DE LA LANDING PAGE
            AddPageWebCapitulo5WireframeLanding(container);

            // PÁGINA 7: GUÍA DE MARCA, UI/UX, BUYER PERSONAS Y KIT DE ACTIVOS
            AddPageWebCapitulo6BrandKitActivos(container);
        });

        doc.GeneratePdf(outputPath);
        Console.WriteLine("Dossier Web Promocional generado exitosamente.");
    }

    private static void AddPageWebCapitulo1PropuestaValor(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderWebHeader(h, "1. PROPUESTA DE VALOR & COPYWRITING CENTRAL"));
            page.Footer().Element(RenderWebFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("El Mensaje Central de la Web: Titulares y Promesa de Marca")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Para capturar la atención en los primeros 5 segundos de visita en el sitio web, el mensaje debe atacar directamente la frustración diaria del instalador:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                col.Item().PaddingTop(8).Border(1).BorderColor(ColorInfoBorder).Background(ColorInfoBg).Padding(12).Column(c =>
                {
                    c.Item().Text("GRAN TITULAR SUGERIDO PARA EL HERO DE LA WEB:").FontSize(9).Bold().FontColor(ColorInfoText);
                    c.Item().PaddingTop(3).Text("“Deja de perder millones en planillas Excel rotas. Presupuesta tus obras eléctricas con precisión de ingeniería, sincronización de nómina real y blindaje de margen comercial.”")
                        .FontSize(12).Bold().FontColor(ColorNavy).LineHeight(1.25f);
                    c.Item().PaddingTop(4).Text("Subtítulo web: Qubi Cost Electrical integra Itemizado, Cubicación por tramos, Tableros C.SC, APUs y Cierre financiero en un único archivo .qce seguro. Sin fórmulas rotas. Sin sorpresas a fin de mes.")
                        .FontSize(9).FontColor(ColorTextBody);
                });

                col.Item().PaddingTop(12).Text("Los 3 Dolores Críticos del Cliente (Pain Points a destacar en la web)").FontSize(12).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                    {
                        c.Item().Text("1. EL LABERINTO DE 6 EXCELS").FontSize(9).Bold().FontColor(ColorDangerText);
                        c.Item().PaddingTop(2).Text("Tener 6 libros sueltos (alimentadores, cuadros, APUs, cotizaciones, resumen). Cambiar el precio de un cable exige actualizar 6 archivos a mano. Si una celda se rompe, el error pasa inadvertido.")
                            .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    });

                    r.Spacing(8);

                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                    {
                        c.Item().Text("2. LA TRAMPA DE LA MANO DE OBRA").FontSize(9).Bold().FontColor(ColorDangerText);
                        c.Item().PaddingTop(2).Text("Presupuestar por tornillo y cable unitario sin considerar la nómina real de sueldos en el tiempo. A mitad de obra descubres que los sueldos costaron 40% más y te comiste toda la utilidad.")
                            .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    });

                    r.Spacing(8);

                    r.RelativeItem().Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                    {
                        c.Item().Text("3. LA TIJERA DEL MANDANTE").FontSize(9).Bold().FontColor(ColorDangerText);
                        c.Item().PaddingTop(2).Text("Poner todo el margen en el generador o transformador. Cuando el cliente dice 'yo compro la máquina por mi cuenta', te quita el equipo y te deja trabajando a costo puro en faena.")
                            .FontSize(8).LineHeight(1.3f).FontColor(ColorTextBody);
                    });
                });

                col.Item().PaddingTop(12).Text("Tabla Comparativa Lista para la Web: Excel vs Qubi Cost").FontSize(11.5f).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.8f);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(ColorNavy).Padding(5).Text("CAPACIDAD").FontSize(8).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(5).Text("EXCEL TRADICIONAL").FontSize(8).Bold().FontColor(Colors.White);
                        h.Cell().Background(ColorNavy).Padding(5).Text("QUBI COST ELECTRICAL").FontSize(8).Bold().FontColor(Colors.White);
                    });

                    table.Cell().Background(ColorBgCard).Padding(4).Text("Contenedor de datos").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("6 a 8 archivos .xlsx sueltos y frágiles").FontSize(7.5f).FontColor(ColorDangerText);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("1 único archivo SQLite .qce ultrarrápido y seguro").FontSize(7.5f).Bold().FontColor(ColorSuccessText);

                    table.Cell().Background(Colors.White).Padding(4).Text("Cálculo de Mano de Obra").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(Colors.White).Padding(4).Text("Desconectado de la nómina real de faena").FontSize(7.5f).FontColor(ColorDangerText);
                    table.Cell().Background(Colors.White).Padding(4).Text("Sincronización perfecta con dotación mensual y FA").FontSize(7.5f).Bold().FontColor(ColorSuccessText);

                    table.Cell().Background(ColorBgCard).Padding(4).Text("Circuitos y Tableros").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("Copiar y pegar metrajes celda por celda").FontSize(7.5f).FontColor(ColorDangerText);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("Módulo C.SC con traspaso automático en un clic (Alt+L)").FontSize(7.5f).Bold().FontColor(ColorSuccessText);

                    table.Cell().Background(Colors.White).Padding(4).Text("Control de Errores").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(Colors.White).Padding(4).Text("Manual; fácil omitir ítems sin precio").FontSize(7.5f).FontColor(ColorDangerText);
                    table.Cell().Background(Colors.White).Padding(4).Text("Auditor inteligente con botón 'Ir a corregir'").FontSize(7.5f).Bold().FontColor(ColorSuccessText);

                    table.Cell().Background(ColorBgCard).Padding(4).Text("Exportación").FontSize(8).Bold().FontColor(ColorTextDark);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("Imprimir a PDF manual con descuadres de página").FontSize(7.5f).FontColor(ColorDangerText);
                    table.Cell().Background(ColorBgCard).Padding(4).Text("Wizard oficial de 4 pasos (PDF corporativo y Excel)").FontSize(7.5f).Bold().FontColor(ColorSuccessText);
                });
            });
        });
    }

    private static void AddPageWebCapitulo2ShowcaseIngenieria(IDocumentContainer container, string imagesDir)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderWebHeader(h, "2. SHOWCASE DE FUNCIONES: INGENIERÍA ELÉCTRICA"));
            page.Footer().Element(RenderWebFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("Módulos Técnicos Estrella: Lo que la Web Debe Mostrar")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Esta sección del sitio web debe cautivar a los estimadores e ingenieros mostrando la velocidad y fluidez de cálculo:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                // Función 1: Cubicación con Fórmulas
                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.AutoItem().Background(ColorNavy).PaddingVertical(2).PaddingHorizontal(6).Text("FUNCIÓN 1").FontSize(8).Bold().FontColor(Colors.White);
                        r.RelativeItem().PaddingLeft(8).Text("Cubicación Técnica con Fórmulas en Casillas (Sin calculadora externa)").FontSize(10).Bold().FontColor(ColorNavy);
                    });
                    c.Item().PaddingTop(3).Text("En lugar de multiplicar en una calculadora y pegar el resultado, escribes operaciones directas en las celdas: '120+40', '32*2', '(15*4)+10'. Enter evalúa la fórmula, guarda el desglose auditable y aplica mermas y redondeos comerciales automáticos.")
                        .FontSize(8).LineHeight(1.25f).FontColor(ColorTextBody);

                    var img = Path.Combine(imagesDir, "04_cubicacion.png");
                    if (File.Exists(img))
                    {
                        c.Item().PaddingTop(4).AlignCenter().Border(1).BorderColor(ColorBorderLight).Width(480).Image(img);
                    }
                });

                // Función 2: Cubicación SC y Alt+L
                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.AutoItem().Background(ColorPrimaryBlue).PaddingVertical(2).PaddingHorizontal(6).Text("FUNCIÓN 2").FontSize(8).Bold().FontColor(Colors.White);
                        r.RelativeItem().PaddingLeft(8).Text("Cubicación de Servicios Comunes (C.SC) y Atajo 'Tomar de SC' (Alt+L)").FontSize(10).Bold().FontColor(ColorNavy);
                    });
                    c.Item().PaddingTop(3).Text("Modela tableros de distribución, circuitos de alumbrado/enchufes/fuerza y tramos de tubería/bandeja por piso. Con presionar Alt+L en el presupuesto, los metrajes calculados se transfieren en 1 segundo.")
                        .FontSize(8).LineHeight(1.25f).FontColor(ColorTextBody);

                    var img = Path.Combine(imagesDir, "05_cubicacion_sc.png");
                    if (File.Exists(img))
                    {
                        c.Item().PaddingTop(4).AlignCenter().Border(1).BorderColor(ColorBorderLight).Width(480).Image(img);
                    }
                });
            });
        });
    }

    private static void AddPageWebCapitulo3ShowcaseFinanciero(IDocumentContainer container, string imagesDir)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderWebHeader(h, "3. SHOWCASE FINANCIERO: LO QUE NINGUNA OTRA APP HACE"));
            page.Footer().Element(RenderWebFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("El Gran Gancho Comercial: Inteligencia Económica")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Esta sección de la web es la que convence a los dueños y gerentes de empresa: demuestra cómo el software protege el dinero y la rentabilidad:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                // Función 3: Factores por Familia
                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.AutoItem().Background(ColorNavy).PaddingVertical(2).PaddingHorizontal(6).Text("ESTRATEGIA").FontSize(8).Bold().FontColor(Colors.White);
                        r.RelativeItem().PaddingLeft(8).Text("Factores de Presentación: Margen Inteligente por Familia").FontSize(10).Bold().FontColor(ColorNavy);
                    });
                    c.Item().PaddingTop(3).Text("Permite fijar multiplicadores diferenciados: competitivo en Equipos (1,43×) donde el cliente cotiza fácil, y margen fuerte en Cables y Canalizaciones (1,72×) para absorber mermas y blindar la utilidad ante recortes de obra.")
                        .FontSize(8).LineHeight(1.25f).FontColor(ColorTextBody);

                    var img = Path.Combine(imagesDir, "factor_familias_tabla.png");
                    if (File.Exists(img))
                    {
                        c.Item().PaddingTop(4).AlignCenter().Border(1).BorderColor(ColorBorderLight).Width(500).Image(img);
                    }
                });

                // Función 4: Tablero Hero KPIs
                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.AutoItem().Background(ColorSuccessBorder).PaddingVertical(2).PaddingHorizontal(6).Text("CONTROL").FontSize(8).Bold().FontColor(Colors.White);
                        r.RelativeItem().PaddingLeft(8).Text("El Tablero de 6 KPIs Financieros en Tiempo Real").FontSize(10).Bold().FontColor(ColorNavy);
                    });
                    c.Item().PaddingTop(3).Text("Visualiza en vivo el Costo de Ejecución ($342M piso de faena), Costo Total Proyecto ($387M con oficina y utilidad), Sobreganancia Delta ($+7,2M), Margen Real (13,1%) y Ratio Venta $/m².")
                        .FontSize(8).LineHeight(1.25f).FontColor(ColorTextBody);

                    var img = Path.Combine(imagesDir, "desglose_costos_kpis.png");
                    if (File.Exists(img))
                    {
                        c.Item().PaddingTop(4).AlignCenter().Border(1).BorderColor(ColorBorderLight).Width(500).Image(img);
                    }
                });

                col.Item().PaddingTop(8).Border(1).BorderColor(ColorInfoBorder).Background(ColorInfoBg).Padding(6).Row(r =>
                {
                    r.AutoItem().Text("PUNTO FUERTE PARA LA WEB: ").FontSize(8.5f).Bold().FontColor(ColorInfoText);
                    r.RelativeItem().Text("El Factor de Ajuste de Mano de Obra (FA) calcula la nómina real de trabajadores por meses con leyes sociales (32%) y la sintoniza con las partidas unitarias sin que sobre ni falte un solo peso.")
                        .FontSize(8).FontColor(ColorTextBody);
                });
            });
        });
    }

    private static void AddPageWebCapitulo4ShowcaseCalidad(IDocumentContainer container, string imagesDir)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderWebHeader(h, "4. SHOWCASE DE CONFIANZA, CALIDAD Y CIERRE"));
            page.Footer().Element(RenderWebFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("Confianza y Cierre Profesional: Presentación Impecable")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "En una propuesta comercial, la presentación define si ganas o pierdes la licitación. Estos módulos dan tranquilidad total:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                // Función 5: Auditoría y Bloqueantes
                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.AutoItem().Background(ColorDangerBorder).PaddingVertical(2).PaddingHorizontal(6).Text("SEGURIDAD").FontSize(8).Bold().FontColor(Colors.White);
                        r.RelativeItem().PaddingLeft(8).Text("Auditoría Automática Previa al Cierre (Botón 'Ir a corregir')").FontSize(10).Bold().FontColor(ColorNavy);
                    });
                    c.Item().PaddingTop(3).Text("Detecta en segundos partidas con cantidad cero, materiales sin precio o APUs vacíos. El botón 'Ir a corregir' transporta al usuario exactamente a la celda problemática, impidiendo cotizar con pérdidas.")
                        .FontSize(8).LineHeight(1.25f).FontColor(ColorTextBody);

                    var img = Path.Combine(imagesDir, "08_revision_final.png");
                    if (File.Exists(img))
                    {
                        c.Item().PaddingTop(4).AlignCenter().Border(1).BorderColor(ColorBorderLight).Width(480).Image(img);
                    }
                });

                // Función 6: Wizard de Exportación
                col.Item().PaddingTop(8).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(8).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.AutoItem().Background(ColorPrimaryBlue).PaddingVertical(2).PaddingHorizontal(6).Text("EXPORTACIÓN").FontSize(8).Bold().FontColor(Colors.White);
                        r.RelativeItem().PaddingLeft(8).Text("Wizard Oficial de 4 Pasos: Cartas de Oferta PDF y Libros Excel").FontSize(10).Bold().FontColor(ColorNavy);
                    });
                    c.Item().PaddingTop(3).Text("Genera documentos formales en PDF corporativo (Carta Mandante, Resumen Ejecutivo, Desglose por Capítulos) y libros Excel con fórmulas vivas auditables listas para entregar en licitaciones públicas o privadas.")
                        .FontSize(8).LineHeight(1.25f).FontColor(ColorTextBody);

                    var img = Path.Combine(imagesDir, "09_oferta_exportacion.png");
                    if (File.Exists(img))
                    {
                        c.Item().PaddingTop(4).AlignCenter().Border(1).BorderColor(ColorBorderLight).Width(480).Image(img);
                    }
                });
            });
        });
    }

    private static void AddPageWebCapitulo5WireframeLanding(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderWebHeader(h, "5. ESTRUCTURA Y WIREFRAME DE LA LANDING PAGE"));
            page.Footer().Element(RenderWebFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("Wireframe de Conversión: Cómo Diseñar la Página Web")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Para el desarrollador y diseñador web: esta es la secuencia exacta de 8 bloques que maximiza el registro y la solicitud de licencias:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                col.Item().PaddingTop(8).Column(blocks =>
                {
                    var sections = new (string Tag, string Title, string Description)[]
                    {
                        ("BLOQUE 1", "Hero Section de Alto Impacto", "Titular contundente, subtítulo de dolor, botón CTA 'Solicitar Demo / Cotizar', y mockup interactivo en perspectiva de la app funcionando."),
                        ("BLOQUE 2", "La Pesadilla de Excel vs La Solución .qce", "Contraste visual lado a lado: 'Las 6 planillas rotas' vs 'Un solo archivo SQLite'. Destacar la pérdida de dinero por errores humanos."),
                        ("BLOQUE 3", "Grid Interactivo de 6 Funciones Clave", "Tarjetas con íconos, micro-videos o capturas al pasar el mouse: Cubicación con fórmulas, C.SC con Alt+L, APU, Factor FA, Tablero y Exportador."),
                        ("BLOQUE 4", "La Prueba Financiera (Calculadora de Ahorro)", "Widget interactivo donde el usuario ingresa cuántas obras hace al año y ve cuánto dinero y horas ahorra al eliminar la desalineación de mano de obra."),
                        ("BLOQUE 5", "Tabla Comparativa de Mercado", "Qubi Cost Electrical vs Excel vs Software Genérico de Construcción (mostrar los 'Check' verdes en cubicación eléctrica y factor FA)."),
                        ("BLOQUE 6", "Seguridad y Trabajo en Equipo (Red Local)", "Mencionar que funciona en red local compartida (SMB) con bloqueo seguro (.qce.lock), 100% offline y sin riesgo de que la nube se caiga."),
                        ("BLOQUE 7", "Preguntas Frecuentes (FAQ)", "Respuestas a: ¿Necesito internet? ¿Cómo paso mis presupuestos antiguos de Excel? ¿Qué computadores soporta? ¿Cómo es el soporte?"),
                        ("BLOQUE 8", "Footer & CTA Final Irresistible", "Llamado a la acción: 'Empieza a licitar con precisión hoy mismo'. Formulario de contacto directo, teléfono comercial y WhatsApp de ventas.")
                    };

                    foreach (var s in sections)
                    {
                        blocks.Item().PaddingBottom(5).Border(1).BorderColor(ColorBorderLight).Background(ColorBgCard).Padding(6).Row(r =>
                        {
                            r.AutoItem().Background(ColorNavy).PaddingVertical(3).PaddingHorizontal(6)
                                .Text(s.Tag).FontSize(8).Bold().FontColor(Colors.White);
                            r.RelativeItem().PaddingLeft(8).Column(c =>
                            {
                                c.Item().Text(s.Title).FontSize(9).Bold().FontColor(ColorPrimaryBlue);
                                c.Item().Text(s.Description).FontSize(7.5f).FontColor(ColorTextBody);
                            });
                        });
                    }
                });

                col.Item().PaddingTop(8).Border(1).BorderColor(ColorSuccessBorder).Background(ColorSuccessBg).Padding(6).Row(r =>
                {
                    r.AutoItem().Text("CONSEJO DE CONVERSIÓN: ").FontSize(8.5f).Bold().FontColor(ColorSuccessText);
                    r.RelativeItem().Text("Coloca un botón flotante de WhatsApp en la esquina inferior derecha del sitio web. En el sector eléctrico de contratistas, más del 60% de los cierres comerciales de licencias se inician por WhatsApp directo.")
                        .FontSize(8).FontColor(ColorTextBody);
                });
            });
        });
    }

    private static void AddPageWebCapitulo6BrandKitActivos(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginVertical(26);
            page.MarginHorizontal(32);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI"));

            page.Header().Element(h => RenderWebHeader(h, "6. BRAND KIT, UI/UX & KIT DE ACTIVOS DIGITALES"));
            page.Footer().Element(RenderWebFooter);

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Text("Especificaciones de Marca y Diseño UI para la Web")
                    .FontSize(16).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Text(
                    "Paleta oficial de colores, fuentes recomendadas y catálogo de capturas disponibles en el proyecto:")
                    .FontSize(9.5f).FontColor(ColorTextBody);

                // Paleta de colores
                col.Item().PaddingTop(8).Text("Paleta Oficial de Colores (Códigos HEX)").FontSize(11).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Background("#0B2442").Padding(8).Column(c =>
                    {
                        c.Item().Text("Qubi Navy").FontSize(8.5f).Bold().FontColor(Colors.White);
                        c.Item().Text("#0B2442").FontSize(8).FontColor(Colors.Grey.Lighten2);
                        c.Item().Text("Fondos oscuros, títulos").FontSize(7).FontColor(Colors.Grey.Lighten3);
                    });
                    r.Spacing(6);
                    r.RelativeItem().Background("#2563EB").Padding(8).Column(c =>
                    {
                        c.Item().Text("Electric Blue").FontSize(8.5f).Bold().FontColor(Colors.White);
                        c.Item().Text("#2563EB").FontSize(8).FontColor(Colors.White);
                        c.Item().Text("Botones CTA primarios").FontSize(7).FontColor(Colors.White);
                    });
                    r.Spacing(6);
                    r.RelativeItem().Background("#0284C7").Padding(8).Column(c =>
                    {
                        c.Item().Text("Accent Cyan").FontSize(8.5f).Bold().FontColor(Colors.White);
                        c.Item().Text("#0284C7").FontSize(8).FontColor(Colors.White);
                        c.Item().Text("Badges y etiquetas").FontSize(7).FontColor(Colors.White);
                    });
                    r.Spacing(6);
                    r.RelativeItem().Background("#10B981").Padding(8).Column(c =>
                    {
                        c.Item().Text("Success Green").FontSize(8.5f).Bold().FontColor(Colors.White);
                        c.Item().Text("#10B981").FontSize(8).FontColor(Colors.White);
                        c.Item().Text("Rentabilidad, checks").FontSize(7).FontColor(Colors.White);
                    });
                    r.Spacing(6);
                    r.RelativeItem().Background("#F8FAFC").Border(1).BorderColor(ColorBorderLight).Padding(8).Column(c =>
                    {
                        c.Item().Text("Canvas Light").FontSize(8.5f).Bold().FontColor(ColorTextDark);
                        c.Item().Text("#F8FAFC").FontSize(8).FontColor(ColorTextMuted);
                        c.Item().Text("Fondo general web").FontSize(7).FontColor(ColorTextBody);
                    });
                });

                col.Item().PaddingTop(12).Text("Tipografías Recomendadas para la Web").FontSize(11).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(3).Text(
                    "• Títulos y Textos Generales: Inter o Segoe UI (gran legibilidad en pantallas de alta densidad).\n" +
                    "• Cifras y Fórmulas Numéricas: Cascadia Code o Roboto Mono (alineación tabular perfecta para precios y porcentajes).")
                    .FontSize(8.5f).LineHeight(1.35f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Text("Catálogo de Capturas de Pantalla Disponibles en el Repositorio").FontSize(11).Bold().FontColor(ColorNavy);

                col.Item().PaddingTop(3).Text(
                    "El equipo web dispone de las siguientes 12 imágenes originales en alta definición dentro de docs/manual/images/:\n" +
                    "1. 00_inicio.png (Bienvenida y proyectos) · 2. 01_dashboard.png (Resumen ejecutivo) · 3. 02_datos_proyecto.png (Mandante/UF)\n" +
                    "4. 03_itemizado.png (Árbol de partidas) · 5. 04_cubicacion.png (Fórmulas en casillas) · 6. 05_cubicacion_sc.png (Tableros C.SC)\n" +
                    "7. 06_apu.png (Análisis de costos y HH) · 8. 07_banco_precios.png (Catálogo de insumos) · 9. 08_revision_final.png (Auditor bloqueantes)\n" +
                    "10. 09_oferta_exportacion.png (Wizard exportador) · 11. factor_familias_tabla.png (Markups) · 12. desglose_costos_kpis.png (6 Tarjetas)")
                    .FontSize(8).LineHeight(1.35f).FontColor(ColorTextBody);

                col.Item().PaddingTop(10).Border(1).BorderColor(ColorNavy).Background(ColorBgCard).Padding(8).Column(c =>
                {
                    c.Item().Text("CONCLUSIÓN PARA EL EQUIPO DE DESARROLLO WEB:").FontSize(9).Bold().FontColor(ColorNavy);
                    c.Item().PaddingTop(2).Text(
                        "Construyan una web que transmita solidez de ingeniería, velocidad y control financiero absoluto. " +
                        "No vendan 'un software de construcción más': vendan el estándar definitivo que evita que las empresas eléctricas pierdan millones por errores de cálculo.")
                        .FontSize(8).Italic().FontColor(ColorTextBody);
                });
            });
        });
    }

    private static void RenderWebHeader(IContainer container, string title)
    {
        container.Row(row =>
        {
            row.AutoItem().Text("QUBI COST ELECTRICAL — DOSSIER WEB PROMOCIONAL").FontSize(8.5f).SemiBold().FontColor(ColorNavy);
            row.RelativeItem().AlignRight().Text(title).FontSize(8.5f).FontColor(ColorTextMuted);
        });
    }

    private static void RenderWebFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(ColorBorderLight).PaddingTop(4).Row(row =>
        {
            row.AutoItem().Text("Dossier de Producto y Especificación para Desarrollo Web")
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

