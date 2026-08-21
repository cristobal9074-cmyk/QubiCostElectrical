using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PlanPdfGenerator;

class Program
{
    static void Main(string[] args)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // Modos: "matriz", "sub", "guia", "ceo", "proyecto"
        string mode = "ceo"; // default to ceo
        string pdfPath = "";

        if (args.Length > 0)
        {
            if (args[0].Equals("matriz", StringComparison.OrdinalIgnoreCase))
            {
                mode = "matriz";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Matriz_Roles_y_Pestanas_QubiCost.pdf");
            }
            else if (args[0].Equals("sub", StringComparison.OrdinalIgnoreCase))
            {
                mode = "sub";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Plan_Suscripcion_Mensual.pdf");
            }
            else if (args[0].Equals("guia", StringComparison.OrdinalIgnoreCase))
            {
                mode = "guia";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Guia_Ciclo_Vida_QubiCost.pdf");
            }
            else if (args[0].Equals("ceo", StringComparison.OrdinalIgnoreCase))
            {
                mode = "ceo";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Plan_CEO_Auditoria_y_Restriccion_Triple.pdf");
            }
            else if (args[0].Equals("proyecto", StringComparison.OrdinalIgnoreCase) || args[0].Equals("datos", StringComparison.OrdinalIgnoreCase))
            {
                mode = "proyecto";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Plan_Rediseno_Datos_Proyecto.pdf");
            }
            else if (args[0].Equals("itemizado", StringComparison.OrdinalIgnoreCase))
            {
                mode = "itemizado";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Plan_Rediseno_Itemizado.pdf");
            }
            else if (args[0].Equals("antecedentes", StringComparison.OrdinalIgnoreCase))
            {
                mode = "antecedentes";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Plan_Rediseno_Antecedentes.pdf");
            }
            else if (args[0].Equals("cubicacionsc", StringComparison.OrdinalIgnoreCase) || args[0].Equals("csc", StringComparison.OrdinalIgnoreCase))
            {
                mode = "cubicacionsc";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Plan_Rediseno_CubicacionSc.pdf");
            }
            else if (args[0].Equals("manodeobra", StringComparison.OrdinalIgnoreCase) || args[0].Equals("mo", StringComparison.OrdinalIgnoreCase))
            {
                mode = "manodeobra";
                pdfPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "Plan_Rediseno_ManoDeObra.pdf");
            }
            else
            {
                pdfPath = args[0];
                if (pdfPath.Contains("Matriz", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "matriz";
                }
                else if (pdfPath.Contains("Antecedentes", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "antecedentes";
                }
                else if (pdfPath.Contains("CubicacionSc", StringComparison.OrdinalIgnoreCase) || pdfPath.Contains("CSC", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "cubicacionsc";
                }
                else if (pdfPath.Contains("ManoDeObra", StringComparison.OrdinalIgnoreCase) || pdfPath.Contains("MO", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "manodeobra";
                }
                else if (pdfPath.Contains("Suscripcion", StringComparison.OrdinalIgnoreCase) || pdfPath.Contains("Sub", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "sub";
                }
                else if (pdfPath.Contains("CEO", StringComparison.OrdinalIgnoreCase) || pdfPath.Contains("Restriccion", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "ceo";
                }
                else if (pdfPath.Contains("Proyecto", StringComparison.OrdinalIgnoreCase) || pdfPath.Contains("Datos", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "proyecto";
                }
                else if (pdfPath.Contains("Itemizado", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "itemizado";
                }
                else
                {
                    mode = "guia";
                }
            }
        }
        else
        {
            pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "Plan_CEO_Auditoria_y_Restriccion_Triple.pdf");
        }

        try
        {
            if (mode == "matriz")
            {
                GenerateMatrixDocument(pdfPath);
            }
            else if (mode == "sub")
            {
                GenerateSubscriptionDocument(pdfPath);
            }
            else if (mode == "ceo")
            {
                GenerateCeoPlanDocument(pdfPath);
            }
            else if (mode == "proyecto")
            {
                GenerateProjectDataPlanDocument(pdfPath);
            }
            else if (mode == "itemizado")
            {
                GenerateItemizedPlanDocument(pdfPath);
            }
            else if (mode == "antecedentes")
            {
                GenerateAntecedentesPlanDocument(pdfPath);
            }
            else if (mode == "cubicacionsc")
            {
                GenerateCubicacionScPlanDocument(pdfPath);
            }
            else if (mode == "manodeobra")
            {
                GenerateManoDeObraPlanDocument(pdfPath);
            }
            else
            {
                GenerateUserGuideDocument(pdfPath);
            }
            Console.WriteLine($"PDF generado con éxito ({mode}) en: {pdfPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al generar el PDF: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    private static void GenerateMatrixDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape()); // Formato horizontal para la matriz de 6 columnas
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("MATRIZ DE PERMISOS, ROLES Y PESTAÑAS ACTIVAS").FontSize(10).Medium().FontColor("#19376D");
                        });

                        row.ConstantItem(120).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("CONFIGURACIÓN DE SEGURIDAD").FontSize(7).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    column.Item().Text("1. MATRIZ GENERAL DE ACCESOS POR ROL").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La siguiente tabla define la visibilidad de las pestañas principales del sistema según la responsabilidad de cada perfil:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Pestaña
                            columns.RelativeColumn(1); // Gerentes
                            columns.RelativeColumn(1.2f); // Jefe Adq.
                            columns.RelativeColumn(1); // Adq.
                            columns.RelativeColumn(1.2f); // Enc. Presup.
                            columns.RelativeColumn(1); // Superv.
                            columns.RelativeColumn(1.2f); // Of. Técnica
                        });

                        // Cabeceras
                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Pestaña / Módulo").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).AlignCenter().Text("Gerentes").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).AlignCenter().Text("Jefe Adq.").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).AlignCenter().Text("Adquisic.").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).AlignCenter().Text("Enc. Presup.").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).AlignCenter().Text("Supervis.").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).AlignCenter().Text("Of. Técnica").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        // Filas de Datos (Pestañas)
                        AddMatrixRow(table, "Resumen (Dashboard)", "✔", "✔", "✗", "✔", "✗", "✔", true);
                        AddMatrixRow(table, "Datos del Proyecto", "✔", "✗", "✗", "✔", "✔", "✔", false);
                        AddMatrixRow(table, "Antecedentes (Docs)", "✔", "✗", "✗", "✔", "✔", "✔", true);
                        AddMatrixRow(table, "Itemizado", "✗", "✗", "✗", "✔", "✗", "✔", false);
                        AddMatrixRow(table, "Cubicación (Takeoff)", "✗", "✗", "✗", "✔", "✔", "✔", true);
                        AddMatrixRow(table, "Cubicación SC (Comunes)", "✗", "✗", "✗", "✔", "✔", "✔", false);
                        AddMatrixRow(table, "APU", "✗", "✗", "✗", "✔", "✗", "✗", true);
                        AddMatrixRow(table, "Plantillas", "✗", "✗", "✗", "✔", "✗", "✗", false);
                        AddMatrixRow(table, "Resumen x Piso", "✔", "✗", "✗", "✔", "✗", "✔", true);
                        AddMatrixRow(table, "Solicitudes (Material/EPP)", "✔ (Lect.)", "✔ (Aprob.)", "✔ (OC)", "✗", "✔ (Crea)", "✔ (Rev.)", false);
                        AddMatrixRow(table, "Banco de Precios", "✗", "✔", "✔", "✔", "✗", "✗", true);
                        AddMatrixRow(table, "Cotizaciones", "✗", "✔", "✔", "✔", "✗", "✗", false);
                        AddMatrixRow(table, "Lista de Compra", "✗", "✔", "✔", "✗", "✗", "✗", true);
                        AddMatrixRow(table, "Revisión Final", "✔", "✗", "✗", "✔", "✗", "✔", false);
                        AddMatrixRow(table, "Versiones", "✔", "✗", "✗", "✔", "✗", "✔", true);
                        AddMatrixRow(table, "Órdenes de Cambio", "✔", "✗", "✗", "✔", "✗", "✔", false);
                        AddMatrixRow(table, "Oferta y Exportación", "✔", "✗", "✗", "✔", "✗", "✗", true);
                    });

                    column.Item().PageBreak(); // Salto de página para el detalle explicativo

                    // 2. Detalle de Permisos por Rol
                    column.Item().Text("2. DETALLE OPERATIVO POR PERFIL").FontSize(11).Bold().FontColor("#0B2447");

                    column.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("A. Gerentes (Directores de Proyecto)").Bold().FontSize(9.5f).FontColor("#19376D");
                            col.Item().Text("Foco: Supervisión financiera, aprobación macro y cierre comercial.\n• Pestañas activas: Resumen (Dashboard), Datos del Proyecto, Antecedentes, Resumen x Piso, Solicitudes (modo consulta), Revisión Final, Versiones, Órdenes de Cambio y Oferta y Exportación.").FontSize(8.5f);
                            
                            col.Item().PaddingTop(10).Text("B. Jefe de Adquisiciones").Bold().FontSize(9.5f).FontColor("#19376D");
                            col.Item().Text("Foco: Aprobación de presupuestos de compras, negociación y contratos.\n• Pestañas activas: Resumen (Dashboard), Solicitudes (módulo de aprobación definitiva), Banco de Precios, Cotizaciones y Lista de Compra.").FontSize(8.5f);
                            
                            col.Item().PaddingTop(10).Text("C. Adquisiciones").Bold().FontSize(9.5f).FontColor("#19376D");
                            col.Item().Text("Foco: Gestión diaria de compras, abastecimiento y despacho a obra.\n• Pestañas activas: Solicitudes (importación de pedidos aprobados), Banco de Precios (consulta), Cotizaciones y Lista de Compra.").FontSize(8.5f);
                        });

                        row.ConstantItem(20); // Separador

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("D. Encargado de Presupuestos (Estimador)").Bold().FontSize(9.5f).FontColor("#19376D");
                            col.Item().Text("Foco: Estudio de propuestas, valorización y cálculo de costos técnicos.\n• Pestañas activas: Resumen, Datos del Proyecto, Antecedentes, Itemizado, Cubicación, Cubicación SC, APU, Plantillas, Resumen x Piso, Banco de Precios, Cotizaciones, Revisión Final, Versiones, Órdenes de Cambio y Oferta y Exportación.").FontSize(8.5f);
                            
                            col.Item().PaddingTop(10).Text("E. Supervisores (En Terreno)").Bold().FontSize(9.5f).FontColor("#19376D");
                            col.Item().Text("Foco: Reporte de avances, cubicaciones físicas y pedidos de materiales.\n• Pestañas activas: Datos del Proyecto (local), Antecedentes (planos), Cubicación (avances), Cubicación SC (servicios comunes de obra) y Solicitudes (generación de borradores y exportación de paquetes .qcreq).").FontSize(8.5f);
                            
                            col.Item().PaddingTop(10).Text("F. Oficina Técnica (Control de Proyecto)").Bold().FontSize(9.5f).FontColor("#19376D");
                            col.Item().Text("Foco: Planificación, seguimiento de versiones y control de órdenes de cambio.\n• Pestañas activas: Resumen, Datos del Proyecto, Antecedentes, Itemizado, Cubicación, Cubicación SC, Resumen x Piso, Solicitudes (revisión técnica de pedidos), Revisión Final, Versiones y Órdenes de Cambio.").FontSize(8.5f);
                        });
                    });
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Matriz de Permisos Confidencial").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void GenerateSubscriptionDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter); // Formato vertical (Retrato) para el informe de estrategia
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header común
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("PLAN DE SUSCRIPCIÓN MENSUAL Y CONTROL DE IMPAGOS").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("ESTRATEGIA AGILE & SMART").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Introducción
                    column.Item().Text("RESUMEN EJECUTIVO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Este plan estratégico detalla el modelo de suscripción recurrente mensual (SaaS) para la aplicación de escritorio Qubi Cost Electrical. El objetivo es estructurar planes comerciales competitivos y definir un sistema inteligente y robusto (SMART) para evitar la elusión de pagos mediante un esquema offline-first compatible con el uso del software en terreno sin conectividad."
                    ).FontSize(8.5f);

                    // 1. Matriz de Planes y Suscripciones (Pricing)
                    column.Item().PaddingTop(12).Text("1. ESTRUCTURA DE PLANES COMERCIALES (TIERS)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Se definen cuatro niveles de suscripción mensual basados en los roles de usuario y las necesidades de colaboración:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.3f); // Plan
                            columns.RelativeColumn(1.1f); // Precio
                            columns.RelativeColumn(2.6f); // Funciones Clave
                            columns.RelativeColumn(2.0f); // Público Objetivo
                        });

                        // Cabeceras
                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(5).Text("Plan").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(5).AlignCenter().Text("Precio Mensual").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(5).Text("Funciones Clave").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(5).Text("Mercado Objetivo").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        // Fila 1 - Estimador Lite
                        table.Cell().Background("#F9FAFB").Padding(5).Text("Estimador Lite").Bold().FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(5).AlignCenter().Text("$15 USD/usuario").FontSize(8).FontColor("#19376D").Bold();
                        table.Cell().Background("#F9FAFB").Padding(5).Text("Apertura .qce, cubicación de avance local, visor de APUs/precios y exportación de reportes. Sin edición de costos.").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(5).Text("Supervisores en obra, inspectores técnicos (ITO) y clientes finales.").FontSize(8);

                        // Fila 2 - Estimador Pro
                        table.Cell().Background("#FFFFFF").Padding(5).Text("Estimador Pro").Bold().FontSize(8);
                        table.Cell().Background("#FFFFFF").Padding(5).AlignCenter().Text("$59 USD/usuario").FontSize(8).FontColor("#10AC84").Bold();
                        table.Cell().Background("#FFFFFF").Padding(5).Text("Edición completa de itemizado, APUs ilimitados, base de precios local, plantillas y exportación comercial oficial.").FontSize(8);
                        table.Cell().Background("#FFFFFF").Padding(5).Text("Estimadores y proyectistas eléctricos independientes.").FontSize(8);

                        // Fila 3 - Estimador Team
                        table.Cell().Background("#F9FAFB").Padding(5).Text("Estimador Team").Bold().FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(5).AlignCenter().Text("$179 USD/mes\n(base 3 estimadores)").FontSize(8).FontColor("#19376D").Bold();
                        table.Cell().Background("#F9FAFB").Padding(5).Text("Bloqueo de archivos por red SMB/UNC (.qce.lock), auditoría local SQLite persistente, 3 licencias Pro ($49/usuario extra).").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(5).Text("Oficinas técnicas y Pymes de ingeniería/construcción (3–8 estimadores).").FontSize(8);

                        // Fila 4 - Enterprise
                        table.Cell().Background("#FFFFFF").Padding(5).Text("Enterprise").Bold().FontSize(8);
                        table.Cell().Background("#FFFFFF").Padding(5).AlignCenter().Text("Desde $499 USD/mes").FontSize(8).FontColor("#19376D").Bold();
                        table.Cell().Background("#FFFFFF").Padding(5).Text("Inicio de sesión único (SSO Azure AD/Google), base de datos de precios centralizada, soporte prioritario 24/7 y auditoría en nube.").FontSize(8);
                        table.Cell().Background("#FFFFFF").Padding(5).Text("Grandes constructoras y corporaciones con equipos descentralizados.").FontSize(8);
                    });

                    // 2. Control de Impagos y Gestión de Mora
                    column.Item().PaddingTop(12).Text("2. CONTROL DE ACCESOS E IMPAGOS (OFFLINE-FIRST ENFORCEMENT)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Para evitar piratería y eludir el pago mensual sin obstaculizar el trabajo del estimador en terreno sin conectividad, la aplicación implementa el siguiente flujo:"
                    ).FontSize(8.5f);

                    // Pasos visuales
                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Row(row =>
                    {
                        row.ConstantItem(45).AlignCenter().Text("JWT").Bold().FontSize(12).FontColor("#19376D");
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("A. Validación Híbrida mediante JWT (Seguridad Local)").Bold().FontSize(9).FontColor("#19376D");
                            c.Item().Text("Al iniciar sesión con conexión, el cliente descarga un JSON Web Token (JWT) firmado con clave privada RS256 en la API. El token se guarda localmente cifrado mediante DPAPI de Windows (cifrado a nivel de OS) en %LOCALAPPDATA%\\QubiCostElectrical\\license.key para evitar su manipulación o clonación.").FontSize(8);
                        });
                    });

                    column.Item().PaddingTop(5).Background("#F0F3FF").Padding(8).Row(row =>
                    {
                        row.ConstantItem(45).AlignCenter().Text("7 Días").Bold().FontSize(10).FontColor("#19376D");
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("B. Periodo de Gracia Offline (Grace Period)").Bold().FontSize(9).FontColor("#19376D");
                            c.Item().Text("La app permite trabajar sin internet hasta un máximo de 7 días continuos desde la última validación en línea exitosa. Al día 8 sin conectarse, el SessionManager de la app WPF revoca los permisos de escritura y cambia el rol a Visor (solo lectura) hasta que vuelva a detectar conexión y valide contra la API.").FontSize(8);
                        });
                    });

                    column.Item().PaddingTop(5).Background("#FFF3CD").Padding(8).Row(row =>
                    {
                        row.ConstantItem(45).AlignCenter().Text("9 Días").Bold().FontSize(10).FontColor("#FF9F43");
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("C. Ciclo de Cobro Fallido y Reintentos (Dunning)").Bold().FontSize(9).FontColor("#FF9F43");
                            c.Item().Text("Ante cobros fallidos en Stripe, se dispara una estrategia de alertas y reintentos automáticos durante 9 días corridos:\n• Día 1 (Fallo 1): Banner amarillo superior en la app advirtiendo sobre el fallo. Quedan 9 días.\n• Día 4 (Fallo 2): Banner naranja. Advertencia persistente de mora. Quedan 6 días.\n• Día 8 (Fallo 3): Banner rojo vibrante. Último aviso de suspensión. Quedan 24 horas.").FontSize(8);
                        });
                    });

                    column.Item().PaddingTop(5).Background("#FFE5E5").Padding(8).Row(row =>
                    {
                        row.ConstantItem(45).AlignCenter().Text("Mora").Bold().FontSize(10).FontColor("#EE5253");
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("D. Modo Solo Lectura ante Cancelación Definitiva (Día 10)").Bold().FontSize(9).FontColor("#EE5253");
                            c.Item().Text("Si al día 10 el pago no ha sido regularizado, Stripe cancela la suscripción y el backend de licencias expira el JWT. La app WPF deshabilita la edición, APUs y guardado de cambios. Para evitar fricciones destructivas, los archivos .qce locales NO se bloquean: el usuario puede seguir abriéndolos y leyéndolos como Visor.").FontSize(8);
                        });
                    });

                    column.Item().PageBreak(); // Salto de página para el detalle metodológico e implementación

                    // 3. Metodología SMART
                    column.Item().Text("3. METODOLOGÍA SMART APLICADA AL CONTROL DE LICENCIAS").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Los objetivos y criterios de control de pago e impagos cumplen con la metodología SMART:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.3f); // Criterio
                            columns.RelativeColumn(4.7f); // Detalle del Objetivo SMART
                        });

                        // Cabeceras
                        table.Header(header =>
                        {
                            header.Cell().Background("#19376D").Padding(4).Text("Criterio").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#19376D").Padding(4).Text("Objetivo Operativo de Qubi Cost").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        // Specific
                        table.Cell().Background("#F9FAFB").Padding(4).Text("S (Specific)").Bold().FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Restringir dinámicamente las capacidades de edición y exportación en el cliente de escritorio WPF mediante la inyección del rol 'Visor' en SessionManager al detectar la invalidez del token JWT o ante la recepción del webhook de suspensión de Stripe.").FontSize(8);

                        // Measurable
                        table.Cell().Background("#FFFFFF").Padding(4).Text("M (Measurable)").Bold().FontSize(8);
                        table.Cell().Background("#FFFFFF").Padding(4).Text("Medir de forma exacta los plazos operativos: 7 días de límite offline continuo (medido localmente mediante reloj criptográfico) y 9 días exactos de periodo de dunning de cobro antes de la desactivación definitiva.").FontSize(8);

                        // Achievable
                        table.Cell().Background("#F9FAFB").Padding(4).Text("A (Achievable)").Bold().FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Es alcanzable a través del uso de Stripe Billing (pasarela SaaS líder), microservicio REST de validación de licencias en la nube (AWS/Azure) y almacenamiento cifrado local a nivel de OS (DPAPI de Windows) en .NET 10.").FontSize(8);

                        // Relevant
                        table.Cell().Background("#FFFFFF").Padding(4).Text("R (Relevant)").Bold().FontSize(8);
                        table.Cell().Background("#FFFFFF").Padding(4).Text("Protege los ingresos de la empresa frente a elusores y crackers de licencias offline, pero mantiene una buena experiencia de usuario (UX) al permitir lectura de los proyectos históricos en modo Visor sin destruir los datos locales.").FontSize(8);

                        // Time-bound
                        table.Cell().Background("#F9FAFB").Padding(4).Text("T (Time-bound)").Bold().FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Plazos acotados e inamovibles de suspensión en el día 10 tras el primer fallo de cobro, y reactivación instantánea (latencia < 5 segundos) una vez que Stripe confirma el pago regularizado.").FontSize(8);
                    });

                    // 4. Plan de Trabajo Agile
                    column.Item().PaddingTop(12).Text("4. PLAN DE TRABAJO AGILE (3 SPRINTS DE 7 DÍAS)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Plan estructurado en sprints ágiles para desplegar de forma iterativa y segura el control de licencias:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Row(row =>
                    {
                        // Sprint 1
                        row.RelativeItem().Background("#F0F3FF").Padding(8).Column(c =>
                        {
                            c.Item().Background("#19376D").Padding(3).AlignCenter().Text("Sprint 1 (Días 1-7)").Bold().FontColor(Colors.White).FontSize(8.5f);
                            c.Item().PaddingTop(4).Text("Backend & Stripe Billing").Bold().FontSize(8.5f).FontColor("#19376D");
                            c.Item().PaddingTop(4).Text("• Configuración de productos, precios y políticas en Stripe Billing.").FontSize(8);
                            c.Item().Text("• Desarrollo del endpoint REST de validación y generación de licencias (/api/licenses/validate).").FontSize(8);
                            c.Item().Text("• Generación de tokens JWT firmados digitalmente (RS256) en el backend.").FontSize(8);
                            c.Item().Text("• Integración de Webhooks de Stripe para detectar fallas de pago en tiempo real.").FontSize(8);
                        });

                        row.ConstantItem(10); // Separador

                        // Sprint 2
                        row.RelativeItem().Background("#F0F3FF").Padding(8).Column(c =>
                        {
                            c.Item().Background("#19376D").Padding(3).AlignCenter().Text("Sprint 2 (Días 8-14)").Bold().FontColor(Colors.White).FontSize(8.5f);
                            c.Item().PaddingTop(4).Text("Cliente WPF & Lógica Offline").Bold().FontSize(8.5f).FontColor("#19376D");
                            c.Item().PaddingTop(4).Text("• Integración del validador de JWT local en el cliente de escritorio .NET 10.").FontSize(8);
                            c.Item().Text("• Cifrado local seguro del token usando Windows DPAPI (cifrado a nivel de OS).").FontSize(8);
                            c.Item().Text("• Lógica de reloj offline (temporizador local de 7 días continuos sin red).").FontSize(8);
                            c.Item().Text("• Bloqueo en SessionManager: forzar rol de Visor ante JWT inválido o expirado.").FontSize(8);
                        });

                        row.ConstantItem(10); // Separador

                        // Sprint 3
                        row.RelativeItem().Background("#F0F3FF").Padding(8).Column(c =>
                        {
                            c.Item().Background("#19376D").Padding(3).AlignCenter().Text("Sprint 3 (Días 15-21)").Bold().FontColor(Colors.White).FontSize(8.5f);
                            c.Item().PaddingTop(4).Text("UI/UX & Smoke Testing").Bold().FontSize(8.5f).FontColor("#19376D");
                            c.Item().PaddingTop(4).Text("• Diseño de banners superiores en WPF para indicar periodos de mora (amarillo/naranja/rojo).").FontSize(8);
                            c.Item().Text("• Integración del botón de autogestión de pago mediante Stripe Customer Portal.").FontSize(8);
                            c.Item().Text("• Pruebas de integración simulando cortes de red e impagos continuos.").FontSize(8);
                            c.Item().Text("• Despliegue de actualización v1.1.0 automatizada por canal estable con Velopack.").FontSize(8);
                        });
                    });
                });

                // Footer común
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Propuesta de Estrategia de Suscripción").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void GenerateUserGuideDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter); // Formato vertical (Retrato) para guías
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header común
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("GUÍA DEL CICLO DE VIDA COMPLETO DEL PROYECTO").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("MANUAL DE OPERACIÓN v1.0").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Introducción
                    column.Item().Text("INTRODUCCIÓN Y FLUJO GENERAL").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Esta guía detalla el flujo operativo extremo a extremo para la realización de presupuestos eléctricos profesionales utilizando Qubi Cost Electrical. El ciclo de vida sigue la secuencia lógica: Proyecto → Itemizado → Cubicación (General y SC) → APU → Banco de Precios → Solicitudes/Cotizaciones → Control de Cambios/Versiones → Revisión Final y Oferta/Exportación."
                    ).FontSize(8.5f);

                    // 1. Datos del Proyecto e Inicio
                    column.Item().PaddingTop(10).Text("FASE 1: APERTURA Y CONFIGURACIÓN INICIAL DEL PROYECTO").FontSize(10).Bold().FontColor("#19376D");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("1.1 Creación del Archivo Local (.qce)").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Inicie Qubi Cost Electrical y haga clic en 'Nuevo Proyecto'. El software creará un archivo único con extensión .qce (una base de datos SQLite integrada). Esta base de datos almacena todas las partidas, cubicaciones, APUs e historial en un solo archivo portable.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("1.2 Metadatos del Proyecto").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("En la pestaña 'Datos del Proyecto', complete el Cliente, Obra, Fecha y Moneda principal (UF o CLP) junto al tipo de cambio. Configure también el IVA por defecto (normalmente activo por defecto) y los márgenes de sobreventa comercial.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("1.3 Seguridad y Colaboración en Red (Bloqueos de Archivo)").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Para evitar que dos estimadores sobrescriban el mismo proyecto en una carpeta compartida de red (SMB/UNC), el servicio ProjectCollaborationLockService bloquea el archivo original y crea un metadato temporal .qce.lock. El primer usuario obtiene el permiso de edición exclusivo; los demás abren en Solo Lectura de forma automática.").FontSize(8);
                    });

                    // 2. Estructuración del Itemizado (WBS)
                    column.Item().PaddingTop(10).Text("FASE 2: ESTRUCTURACIÓN DEL ITEMIZADO (WBS - ARBOL DE PARTIDAS)").FontSize(10).Bold().FontColor("#19376D");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("2.1 Creación del WBS").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Defina la jerarquía de Capítulos (ej: 1. Canalizaciones), Subcapítulos (ej: 1.1 Embutidas) y Partidas (ej: 1.1.1 Tubo PVC 20mm). El árbol WBS proporciona la estructura para el cálculo de costos y la asignación posterior de recursos.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("2.2 Importación y Pegado Inteligente (Ctrl+V) desde Excel").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Puede estructurar el itemizado manualmente o cargarlo masivamente:\n• Importar Excel: Cargue archivos .xlsx directos. Un cuadro resumen le indicará filas leídas, procesadas y errores o advertencias.\n• Pegado Inteligente: Copie filas de Excel y use Ctrl+V en la grilla. Una ventana de vista previa validará códigos WBS duplicados o estructuras inconsistentes antes de confirmar el guardado.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("2.3 Configuración Especial: Exclusión de Cubicación").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Si una partida corresponde a un cobro directo o servicio que no requiere desglose detallado de materiales (ej. Derechos Municipales), marque la columna 'Excl. cubic.' en el Itemizado. Esto deshabilitará la edición de recursos para esa partida en el módulo de Cubicación por seguridad.").FontSize(8);
                    });

                    column.Item().PageBreak(); // Salto de página a la fase técnica (Cubicación y Costos)

                    // 3. Cubicación y Takeoff
                    column.Item().Text("FASE 3: CUBICACIÓN DE RECURSOS (TAKEOFF) Y SERVICIOS COMUNES (SC)").FontSize(10).Bold().FontColor("#19376D");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("3.1 Asignación de Recursos a Partidas").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("En la pestaña 'Cubicación', seleccione la partida en el panel izquierdo. En la grilla de la derecha, añada los recursos necesarios (Materiales, Mano de Obra, Equipos y Subcontratos). Cada recurso queda anclado a la partida mediante su ItemId único.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("3.2 Ingreso Dinámico con Barra de Fórmulas").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("En lugar de valores estáticos, utilice la Barra de Fórmulas superior para detallar desgloses matemáticos (ej: (10*2.4)+12.5). Al presionar Enter, el software guarda la fórmula textual para auditoría y calcula de forma automática el resultado final de cantidad en la grilla. Use atajos como Alt+1..9 para navegación veloz.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("3.3 Cubicación de Servicios Comunes (Cubicación SC)").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Para canalizaciones troncales, subestaciones o tableros de servicios comunes que alimentan a múltiples áreas, utilice el módulo 'Cubicación SC'. Esta sección distribuye el costo total de manera proporcional entre las partidas seleccionadas del presupuesto principal, evitando duplicidades.").FontSize(8);
                    });

                    // 4. APU y Banco de Precios
                    column.Item().PaddingTop(10).Text("FASE 4: ANÁLISIS DE PRECIOS UNITARIOS (APU) Y BANCO DE PRECIOS").FontSize(10).Bold().FontColor("#19376D");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("4.1 Estructura del APU").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("El APU (Análisis de Precio Unitario) permite desglosar el costo unitario de una partida por unidad de medida (ej: $/metro). Puede heredar automáticamente las cantidades y precios de la cubicación detallada o sobreescribirse con tarifas fijas según la política del proyecto.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("4.2 Consistencia del Banco de Precios").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("El Banco de Precios de la empresa centraliza las tarifas oficiales. Al cubicar, la app sugiere autocompletado y valida si el precio del recurso está actualizado. Si un estimador agrega un material a mano, este aparecerá como 'Sin precio' en la grilla hasta que sea homologado con el catálogo oficial.").FontSize(8);
                    });

                    // 5. Cotizaciones y Gestión de Compras (Solicitudes)
                    column.Item().PaddingTop(10).Text("FASE 5: GESTIÓN DE COMPRAS EN TERRENO Y COTIZACIONES").FontSize(10).Bold().FontColor("#19376D");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("5.1 Creación de Solicitudes en Terreno (.qcreq)").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Los supervisores en obra pueden usar la app localmente para generar solicitudes de materiales y EPPs. Estas se exportan como un archivo de paquete liviano de requerimientos (.qcreq) que se envía a la oficina técnica por correo o red interna.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("5.2 Flujo de Validación y Aprobación de Compras").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("El sistema sigue un flujo estructurado de permisos:\n• Supervisor: Genera el borrador del requerimiento.\n• Oficina Técnica: Revisa la solicitud contra el presupuesto original. La app advierte automáticamente si las cantidades pedidas superan lo cubicado en el proyecto original (control de sobrecostos).\n• Jefe de Adquisiciones: Aprueba la solicitud definitiva para su emisión.\n• Adquisiciones: Consolida las solicitudes en la Lista de Compra y genera la Orden de Compra (OC).").FontSize(8);
                    });

                    column.Item().PageBreak(); // Salto de página a la fase de control y cierre

                    // 6. Snapshots, Versiones y Cambios
                    column.Item().Text("FASE 6: CONTROL DE VERSIONES Y ÓRDENES DE CAMBIO").FontSize(10).Bold().FontColor("#19376D");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("6.1 Snapshots Comparativos del Presupuesto").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Durante la negociación, el presupuesto sufre cambios. En la pestaña 'Versiones', guarde snapshots fijos del proyecto (V1 Oferta, V2 Ajuste Técnico). El módulo permite una comparación Delta A/B visual de todas las partidas. Al hacer doble clic en una partida modificada, la app le redirige ('Ir a partida') directamente a su edición.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("6.2 Gestión de Órdenes de Cambio").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Si durante la ejecución se aprueban adicionales u obras extraordinarias, regístrelas como 'Órdenes de Cambio' asociadas al proyecto. Esto permite mantener el presupuesto de línea base (contrato original) intacto y controlar los márgenes de los adicionales por separado.").FontSize(8);
                    });

                    // 7. Cierre, Revisión Final y Exportación
                    column.Item().PaddingTop(10).Text("FASE 7: REVISIÓN FINAL Y CIERRE COMERCIAL (WIZARD UNIFICADO)").FontSize(10).Bold().FontColor("#19376D");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("7.1 Módulo de Revisión y Validación").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Antes de entregar el presupuesto, navegue a la pestaña 'Revisión'. La app ejecutará un diagnóstico automático para encontrar inconsistencias: partidas sin recursos asignados, recursos sin precios (tarifa $0), o fórmulas sintácticamente incorrectas. Los errores graves bloquearán la exportación definitiva hasta ser resueltos.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("7.2 Cierre Único: Configuración de Markup e IVA").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Haga clic en 'Oferta y Exportación' en el menú Cierre. Configure el factor final de sobreventa comercial (Markup) y el IVA del proyecto. El motor de cálculo recalculó el total de manera centralizada en la capa de Domain para evitar diferencias de redondeo.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("7.3 Vista Previa en WPF y Exportación de Entregables").Bold().FontSize(8.5f).FontColor("#0B2447");
                        c.Item().Text("Revise el presupuesto formateado directamente en el panel de vista previa de PDF integrado en WPF (PdfPageRenderer). Si es conforme, exporte los entregables definitivos:\n• Oferta PDF Comercial: Incluye la portada formal del presupuesto y la memoria detallada de cubicación para el cliente.\n• Archivo Excel Golden: Hoja de cálculo limpia y ordenada con fórmulas de suma para su entrega comercial oficial.").FontSize(8);
                    });

                    // Cuadro de Resumen de Atajos de Teclado
                    column.Item().PaddingTop(12).Text("ATAJOS DE TECLADO RÁPIDOS").FontSize(10).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.5f); // Atajo
                            columns.RelativeColumn(4.5f); // Acción
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(3).Text("Atajo").Bold().FontColor(Colors.White).FontSize(8);
                            header.Cell().Background("#0B2447").Padding(3).Text("Función en la Aplicación").Bold().FontColor(Colors.White).FontSize(8);
                        });

                        table.Cell().Background("#F9FAFB").Padding(3).Text("Alt + 1..9").Bold().FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(3).Text("Navega de forma instantánea entre los módulos principales (Proyecto, Itemizado, Cubicación, etc.).").FontSize(7.5f);

                        table.Cell().Background("#FFFFFF").Padding(3).Text("Ctrl + V").Bold().FontSize(7.5f);
                        table.Cell().Background("#FFFFFF").Padding(3).Text("Pegado inteligente en Itemizado y Cubicación con pre-visualización y detección de WBS.").FontSize(7.5f);

                        table.Cell().Background("#F9FAFB").Padding(3).Text("Alt + ↑ / ↓").Bold().FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(3).Text("Mueve la fila de partida o recurso seleccionada arriba o abajo en la jerarquía.").FontSize(7.5f);

                        table.Cell().Background("#FFFFFF").Padding(3).Text("Supr / Del").Bold().FontSize(7.5f);
                        table.Cell().Background("#FFFFFF").Padding(3).Text("Elimina la partida seleccionada del itemizado o el recurso de la cubicación.").FontSize(7.5f);
                    });
                });

                // Footer común
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Manual Completo del Ciclo de Vida del Presupuesto").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void GenerateCeoPlanDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("AUDITORÍA DE PRODUCTIZACIÓN Y RESTRICCIÓN TRIPLE").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(160).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("MODELO AGILE & SMART").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Resumen Ejecutivo
                    column.Item().Text("RESUMEN EJECUTIVO (VISIÓN DEL CEO)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Para que Qubi Cost Electrical se convierta en una solución de software comercial altamente valorada en el mercado corporativo, esta auditoría establece la hoja de ruta estratégica. Se definen las acciones críticas de limpieza (eliminar deuda técnica) y optimizaciones premium (mejorar valor), estructuradas bajo el marco metodológico de la Restricción Triple del proyecto."
                    ).FontSize(8.5f);

                    // 1. Auditoría de Limpieza (Eliminar)
                    column.Item().PaddingTop(12).Text("1. AUDITORÍA DE ELIMINACIÓN (REDUCCIÓN DE DEUDA TÉCNICA)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(6).Column(c =>
                    {
                        c.Item().Text("• Código Residual de WinForms:").Bold().FontSize(8.5f).FontColor("#19376D");
                        c.Item().Text("  Eliminar cualquier directiva de importación heredada y referencias muertas en el ensamblado WPF para asegurar un empaquetado ultra-limpio.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("• Mockups Temporales fuera del Build:").Bold().FontSize(8.5f).FontColor("#19376D");
                        c.Item().Text("  Mover los prototipos HTML y scripts de análisis fuera del árbol 'src/' a un directorio '.gemini' o 'docs/dev' para no sobrecargar el compilador.").FontSize(8);
                        
                        c.Item().PaddingTop(4).Text("• Consolidación de Migraciones SQLite:").Bold().FontSize(8.5f).FontColor("#19376D");
                        c.Item().Text("  Simplificar el migrador de base de datos consolidando las versiones históricas (v1-v5) en una estructura inicial única, optimizando el tiempo de arranque de la aplicación.").FontSize(8);
                    });

                    // 2. Auditoría de Mejoras Premium (Mejorar)
                    column.Item().PaddingTop(12).Text("2. AUDITORÍA DE MEJORAS DE ALTO VALOR (PRODUCTIZACIÓN)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Background("#F0F3FF").Padding(6).Column(c =>
                    {
                        c.Item().Text("• Markups por 9 Familias de Lista de Precios:").Bold().FontSize(8.5f).FontColor("#19376D");
                        c.Item().Text("  Integrar en el motor de costos los multiplicadores editables para cada familia de la base de datos (Conductores, Canalizaciones, Tableros, etc.) para un cuadre exacto de licitaciones.").FontSize(8);

                        c.Item().PaddingTop(4).Text("• Seguridad & Licenciamiento Offline-First:").Bold().FontSize(8.5f).FontColor("#19376D");
                        c.Item().Text("  Desplegar validación local con firma RSA-2048 para archivos '.qcelic' con 7 días de gracia sin internet y dunning de mora automático de 9 días antes de pasar a solo lectura.").FontSize(8);

                        c.Item().PaddingTop(4).Text("• Colaboración Segura sobre Carpetas Compartidas (SMB):").Bold().FontSize(8.5f).FontColor("#19376D");
                        c.Item().Text("  Garantizar exclusión a nivel de sistema de archivos usando FileShare.None e implementar un heartbeat activo de 15 segundos para proteger los datos.").FontSize(8);

                        c.Item().PaddingTop(4).Text("• Virtualización en WPF para Cargas Masivas:").Bold().FontSize(8.5f).FontColor("#19376D");
                        c.Item().Text("  Forzar virtualización de filas y reciclaje de contenedores en las grillas de cubicación para mantener un rendimiento suave en presupuestos de más de 3.000 filas.").FontSize(8);
                    });

                    column.Item().PageBreak();

                    // 3. El Modelo de la Restricción Triple
                    column.Item().Text("3. EL MODELO DE LA RESTRICCIÓN TRIPLE").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Cualquier proyecto de ingeniería y desarrollo comercial de software está gobernado por tres límites interconectados. El balance SMART para Qubi Cost Electrical se define a continuación:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f); // Eje
                            columns.RelativeColumn(4.8f); // Criterios y Limites
                        });

                        // Cabeceras
                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(5).Text("Eje de Control").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(5).Text("Límites del Proyecto SMART").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        // Alcance
                        table.Cell().Background("#F9FAFB").Padding(5).Text("ALCANCE\n(Scope)").Bold().FontSize(8).FontColor("#19376D");
                        table.Cell().Background("#F9FAFB").Padding(5).Text("• Estabilización de la arquitectura local (WPF + SQLite) y migración de precios CLP.\n• Sistema de licenciamiento offline-first basado en archivos '.qcelic'.\n• Exclusiones: No desarrollar base de datos en nube en esta fase para evitar incrementos de alcance y mantener la independencia offline.").FontSize(8);

                        // Tiempo
                        table.Cell().Background("#FFFFFF").Padding(5).Text("TIEMPO\n(Time)").Bold().FontSize(8).FontColor("#10AC84");
                        table.Cell().Background("#FFFFFF").Padding(5).Text("• Plan de despliegue ágil en 3 Sprints semanales (21 días totales):\n  - Sprint 1 (Días 1-7): Desarrollo del backend de Stripe y firma de licencias.\n  - Sprint 2 (Días 8-14): Lógica offline y DPAPI de Windows en la app WPF.\n  - Sprint 3 (Días 15-21): UI/UX de banners de mora y pruebas de humo.").FontSize(8);

                        // Costo
                        table.Cell().Background("#F9FAFB").Padding(5).Text("COSTO\n(Cost)").Bold().FontSize(8).FontColor("#EE5253");
                        table.Cell().Background("#F9FAFB").Padding(5).Text("• Cero costo de infraestructura en nube gracias al motor local SQLite integrado.\n• Costo fijo de desarrollo (HH de programadores locales).\n• Reducción de costos de soporte y postventa utilizando el portal de autoservicio para clientes de Stripe (Stripe Customer Portal).").FontSize(8);
                    });

                    // 4. Conclusión
                    column.Item().PaddingTop(15).Text("4. CONCLUSIÓN E IMPACTO COMERCIAL").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Al empaquetar y resguardar la app mediante licenciamiento local y markups específicos por familias, Qubi Cost Electrical se convierte en un activo de software de alto valor, listo para ser monetizado bajo el modelo SaaS (Suscripción Mensual) de forma profesional y escalable."
                    ).FontSize(8.5f);
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Auditoría del CEO y Restricción Triple").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void AddMatrixRow(TableDescriptor table, string label, string c1, string c2, string c3, string c4, string c5, string c6, bool alternate)
    {
        var bg = alternate ? "#F9FAFB" : "#FFFFFF";

        table.Cell().Background(bg).Padding(4).Text(label).FontSize(8);
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c1).FontSize(8).FontColor(c1 == "✗" ? Colors.Grey.Lighten1 : (c1.Contains("✔") ? Colors.Green.Darken2 : Colors.Blue.Darken2));
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c2).FontSize(8).FontColor(c2 == "✗" ? Colors.Grey.Lighten1 : Colors.Green.Darken2);
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c3).FontSize(8).FontColor(c3 == "✗" ? Colors.Grey.Lighten1 : Colors.Green.Darken2);
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c4).FontSize(8).FontColor(c4 == "✗" ? Colors.Grey.Lighten1 : Colors.Green.Darken2);
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c5).FontSize(8).FontColor(c5 == "✗" ? Colors.Grey.Lighten1 : Colors.Green.Darken2);
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c6).FontSize(8).FontColor(c6 == "✗" ? Colors.Grey.Lighten1 : Colors.Green.Darken2);
    }

    private static void GenerateProjectDataPlanDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("PLAN DE REDISEÑO: DATOS DEL PROYECTO ($20B DESIGN)").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("DISEÑO Y ARQUITECTURA").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Resumen Ejecutivo
                    column.Item().Text("RESUMEN EJECUTIVO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Para maximizar la eficiencia y profesionalismo del módulo de Datos del Proyecto en Qubi Cost Electrical, se implementa una interfaz unificada en dos columnas principales. Este enfoque elimina el uso de múltiples sub-pestañas secundarias, permitiendo un acceso rápido y una edición a la vista de todos los parámetros clave sin generar fatiga cognitiva."
                    ).FontSize(8.5f);

                    // 1. Estructura de la Interfaz
                    column.Item().PaddingTop(12).Text("1. DISTRIBUCIÓN DEL ESPACIO VISUAL (DOUBLE-COLUMN)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La pantalla activa se divide en dos columnas balanceadas para agrupar campos según su naturaleza operativa:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Sección
                            columns.RelativeColumn(4.5f); // Campos
                            columns.RelativeColumn(3.5f); // UX Key
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Sección").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Campos Incluidos").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text(" UX Key Feature").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Identificación").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Nombre, Cliente, Ubicación, Tipo, Estado del Presupuesto.").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Selector de Estado tipo pastillas con colores semánticos.").FontSize(8);

                        table.Cell().Padding(4).Text("Parámetros").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Moneda base, Valor UF, Tipo de Cambio USD, Metros Cuadrados, GG%, Utilidad%, IVA%, Validez.").FontSize(8);
                        table.Cell().Padding(4).Text("Panel HUD superior reactivo que muestra el resumen GG/Utilidad/IVA.").FontSize(8);

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Markups").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Conductores, Canalización, Tableros, Protecciones, Iluminación, Equipos, Enchufes, Puesta a Tierra, Accesorios.").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Grilla compacta 3x3 con luz piloto de severidad por factor.").FontSize(8);

                        table.Cell().Padding(4).Text("Responsable & Scope").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Nombre de Estimador, Rol/Cargo, memorias de Alcance y Exclusiones.").FontSize(8);
                        table.Cell().Padding(4).Text("Textareas de párrafo de alta densidad para reportes comerciales.").FontSize(8);
                    });

                    column.Item().PageBreak();

                    // 2. Control de Cambios y Flujo de Datos
                    column.Item().Text("2. COMPORTAMIENTO REACTIVO Y CONTROL DE CAMBIOS").FontSize(11).Bold().FontColor("#0B2447");
                    
                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("A. HUD de Parámetros Comerciales").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("En la parte superior de la tarjeta de condiciones comerciales se ubica un visualizador HUD. Al modificar las celdas numéricas de Gastos Generales, Utilidad o IVA, estos indicadores superiores se actualizan de forma inmediata (tras perder el foco la celda), facilitando la lectura ejecutiva de los recargos globales.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F9FAFB").Padding(8).Column(c =>
                    {
                        c.Item().Text("B. Barra Contextual Flotante (Dirty State)").Bold().FontSize(9).FontColor("#0B2447");
                        c.Item().Text("Se eliminan los botones estáticos de la cabecera. La barra de acciones (Aplicar / Descartar) flota en la parte inferior del formulario con un fondo translúcido y bordes iluminados, mostrándose únicamente cuando el modelo detecta cambios pendientes de guardado en la sesión activa.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("C. Historial de Auditoría Colapsable").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("Para no sobrecargar visualmente el formulario principal, el DataGrid del historial de auditorías SQLite se encapsula al fondo dentro de un expansor colapsable. El usuario puede abrirlo en cualquier momento para revisar la bitácora de quién, cuándo y qué se modificó.").FontSize(8);
                    });

                    column.Item().PaddingTop(12).Text("3. VERIFICACIÓN Y SMOKE TESTING").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "El aseguramiento de calidad técnico contempla compilar en Release, ejecutar la suite completa de 1.713 pruebas unitarias sin regresiones, y validar la consistencia en el guardado de los markups de las 9 familias en la base SQLite (.qce)."
                    ).FontSize(8.5f);
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Plan de Rediseño de Datos del Proyecto").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void GenerateItemizedPlanDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("PLAN DE REDISEÑO: ITEMIZADO VALORADO ($20B DESIGN)").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("DISEÑO Y ARQUITECTURA").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Resumen Ejecutivo
                    column.Item().Text("RESUMEN EJECUTIVO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Con el fin de consolidar a Qubi Cost Electrical como una herramienta líder en presupuestación, se rediseña la pestaña del Itemizado Valorado. Reorganizamos la interfaz reduciendo la carga cognitiva mediante la reubicación de métricas críticas a un HUD superior y simplificando las operaciones en una única botonera unificada."
                    ).FontSize(8.5f);

                    // 1. Estructura de la Interfaz
                    column.Item().PaddingTop(12).Text("1. ESTRUCTURA Y ELEMENTOS CLAVE DEL DISEÑO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La propuesta reorganiza la información en tres capas horizontales altamente legibles:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Capa
                            columns.RelativeColumn(7); // Propósito y Comportamiento UX
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Capa de Interfaz").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Propósito y Comportamiento UX").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        table.Cell().Background("#F9FAFB").Padding(4).Text("HUD Superior (Métricas)").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("4 tarjetas ejecutivas: Monto Total (UF/CLP), Partidas Totales, Porcentaje de Avance de APUs y Conteo de partidas sin cotizar.").FontSize(8);

                        table.Cell().Padding(4).Text("Toolbar de Control").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Agrupa el buscador reactivo con los botones encapsulados de '+ Partida', '+ Título', 'Importar Excel', 'Pegar celdas' y el menú desplegable secundario.").FontSize(8);

                        table.Cell().Background("#F9FAFB").Padding(4).Text("DataGrid de Partidas").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Grilla con alineación numérica monospace en P.U. y Totales, formato de capítulo visualmente destacado con acento azul y badges de estado semánticos.").FontSize(8);
                    });

                    column.Item().PageBreak();

                    // 2. Comportamiento y Funcionalidades Premium
                    column.Item().Text("2. COMPORTAMIENTO Y COMPONENTES EXCLUSIVOS").FontSize(11).Bold().FontColor("#0B2447");
                    
                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("A. Alineación Monospace y Legibilidad").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("Para evitar que los valores numéricos bailen o se desalineen al cambiar de fila, las columnas de Precio Unitario y Total se configuran con la fuente JetBrains Mono o Consolas, garantizando una comparación vertical limpia y profesional, típica de hojas de cálculo de grado financiero.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F9FAFB").Padding(8).Column(c =>
                    {
                        c.Item().Text("B. Jerarquía de Títulos y Capítulos").Bold().FontSize(9).FontColor("#0B2447");
                        c.Item().Text("Las filas marcadas como títulos/capítulos se pintan con un fondo azul claro muy suave y un borde sólido azul a la izquierda de la celda. Esto permite romper visualmente la tabla en secciones lógicas, facilitando el escaneo rápido del presupuesto.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("C. Reporte de Cierre e Imprimible PDF").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("Se implementa un generador nativo de PDF que compila el itemizado valorado con membretes, resúmenes e identificación formal del presupuesto. Este se puede invocar directamente desde la cabecera del módulo.").FontSize(8);
                    });

                    column.Item().PaddingTop(12).Text("3. VERIFICACIÓN Y ASEGURAMIENTO DE CALIDAD").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La verificación técnica validará que la importación/pegado masivo de filas mantenga la consistencia de las llaves foráneas en SQLite, los cálculos automáticos de sumatoria de subtotales en UF, y la correcta generación formal del reporte PDF."
                    ).FontSize(8.5f);
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Plan de Rediseño de Itemizado Valorado").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void GenerateAntecedentesPlanDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("PLAN DE REDISEÑO: ANTECEDENTES Y PLANOS ($20B DESIGN)").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("DISEÑO Y ARQUITECTURA").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Resumen Ejecutivo
                    column.Item().Text("RESUMEN EJECUTIVO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Con el fin de consolidar a Qubi Cost Electrical como una plataforma líder, se propone el rediseño completo de la pestaña Antecedentes y Planos (Módulo de Documentos). La propuesta optimiza la densidad de información a través de un panel dividido (Split Layout) que separa la lista general del inspector de detalles, integrando controles de fecha de corte y adjuntos en un flujo intuitivo de alta gama."
                    ).FontSize(8.5f);

                    // 1. Estructura de la Interfaz
                    column.Item().PaddingTop(12).Text("1. ARQUITECTURA VISUAL Y COMPONENTES CLAVE").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "El rediseño reestructura el módulo de documentos en cuatro componentes visuales de alto impacto:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Componente
                            columns.RelativeColumn(7); // Propósito y Comportamiento UX
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Componente UI").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Propósito y Comportamiento UX").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Tarjetas KPI Superiores").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("4 tarjetas ejecutivas: Planos de Proyecto, Especificaciones Técnicas, Addendas/Aclaraciones y Consultas Abiertas, con colores temáticos distintivos.").FontSize(8);

                        table.Cell().Padding(4).Text("Toolbar de Filtros y Corte").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Buscador reactivo + filtro por tipo + selector de Fecha de Corte integrado (con checkbox 'Sin fecha de corte') + botones de acción encapsulados.").FontSize(8);

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Grilla de Documentos (Izquierda)").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Muestra el listado de documentos con pills coloreadas según tipo, código monospace, fecha formal, estado semántico y un indicador compacto de archivo adjunto.").FontSize(8);

                        table.Cell().Padding(4).Text("Inspector de Detalles (Derecha)").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Panel colapsable que centraliza los metadatos complejos: disciplina, responsable, notas detalladas, adjuntos (con acción directa para Abrir/Reemplazar) y checkboxes explicativos de rol de la oferta.").FontSize(8);
                    });

                    column.Item().PageBreak();

                    // 2. Comportamiento y Funcionalidades Premium
                    column.Item().Text("2. FUNCIONALIDADES PREMIUM DE GESTIÓN TÉCNICA").FontSize(11).Bold().FontColor("#0B2447");
                    
                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("A. Control de Fecha de Corte y Vigencia").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("La Fecha de Corte de Antecedentes es crítica para congelar el estado del proyecto técnico. El nuevo DatePicker integrado en la barra de herramientas permite configurar este parámetro global de forma inmediata sin necesidad de ingresar al menú de preferencias del proyecto.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F9FAFB").Padding(8).Column(c =>
                    {
                        c.Item().Text("B. Roles del Documento en la Oferta").Bold().FontSize(9).FontColor("#0B2447");
                        c.Item().Text("Se definen roles booleanos claros para cada antecedente: Pendiente del mandante (para control de consultas), Exclusión de la oferta (para autocompletar la cláusula comercial de exclusiones), Supuesto de la oferta (para respaldar consideraciones especiales) e Incluido en alcance. El panel de la derecha explica de forma descriptiva cada rol para guiar al usuario.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("C. Integración Directa con la Oferta Comercial (Menú Más)").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("Se añaden accesos directos en el menú para 'Sincronizar planos a oferta' (que copia el listado de planos vigentes a las tablas de la oferta) e 'Incorporar exclusiones' (que concatena las notas de exclusión al cuerpo del documento final).").FontSize(8);
                    });

                    column.Item().PaddingTop(12).Text("3. VERIFICACIÓN Y ASEGURAMIENTO DE CALIDAD").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La validación técnica garantizará que la fecha de corte se guarde correctamente en el esquema de la base de datos SQLite (.qce), que los archivos adjuntos se copien a la carpeta local del proyecto, y que las exclusiones/planos se sincronicen de forma consistente con las tablas de configuración de la oferta comercial."
                    ).FontSize(8.5f);
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Plan de Rediseño de Antecedentes y Planos").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void GenerateCubicacionScPlanDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("PLAN DE REDISEÑO: CUBICACIÓN DE SERVICIOS COMUNES (C.SC)").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("CUBICACIÓN Y MEDICIONES").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Resumen Ejecutivo
                    column.Item().Text("RESUMEN EJECUTIVO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La pestaña de Cubicación de Servicios Comunes (C.SC) centraliza el cálculo de canalizaciones, conductores, cajas de derivación y terminales flexibles asociados a circuitos y tableros de uso común del presupuesto. Se propone un rediseño de alta gama que reorganiza el espacio a través de un panel lateral (Sidebar) para los tableros y una planilla estilo Excel optimizada, con soporte para resúmenes de materiales detallados en PDF."
                    ).FontSize(8.5f);

                    // 1. Estructura de la Interfaz
                    column.Item().PaddingTop(12).Text("1. COMPONENTES VISUALES Y ARQUITECTURA").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La interfaz se estructura en tres zonas clave:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Componente
                            columns.RelativeColumn(7); // Propósito y Comportamiento UX
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Componente UI").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Propósito y Comportamiento UX").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        table.Cell().Background("#F9FAFB").Padding(4).Text("HUD Estadístico").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("4 tarjetas ejecutivas: Longitud Total de Canalizaciones (m), Tableros SC creados, Circuitos Activos y Cajas/Terminales acumulados en el cálculo.").FontSize(8);

                        table.Cell().Padding(4).Text("Banda de Categorías Superior").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Cabecera de dos filas que agrupa visualmente las columnas de la planilla por categoría técnica (Tramo Bandeja, Tramo PVC, Cajas, Terminales) con subrayados finos de color para guiar la vista.").FontSize(8);

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Planilla Excel-Like").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Grilla con selección por celda única. Columnas de cantidad, longitud y diámetro estilizadas en fuentes monospace para alineación numérica estricta.").FontSize(8);
                    });

                    column.Item().PageBreak();

                    // 2. Comportamiento y Funcionalidades Premium
                    column.Item().Text("2. OPERATIVA Y RESUMEN DE MATERIALES DE ALTA FIDELIDAD").FontSize(11).Bold().FontColor("#0B2447");
                    
                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("A. Planilla Excel de Edición Fluida").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("La planilla permite cargar circuitos a gran velocidad utilizando únicamente el teclado. El sistema normaliza entradas de tipo (ej: escribir 'e' + Tab asigna 'Enchufes'), heredando la especificación del ducto y conductor del circuito activo.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F9FAFB").Padding(8).Column(c =>
                    {
                        c.Item().Text("B. Reporte de Cubicación SC (PDF)").Bold().FontSize(9).FontColor("#0B2447");
                        c.Item().Text("Se incorpora un botón 'Exportar PDF' en el módulo que genera un informe completo detallando: el listado de circuitos de servicios comunes por tablero, el resumen consolidado de bandejas, tubos PVC y cajas de derivación requeridos, y el desglose de artefactos por zona de proyecto.").FontSize(8);
                    });

                    column.Item().PaddingTop(12).Text("3. PLAN DE TRABAJO Y VERIFICACIÓN").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Se validará la correcta acumulación de materiales por tablero y piso en el motor de cálculos, la consistencia de las relaciones de circuitos con las zonas del presupuesto, y la legibilidad visual de la planilla multi-columna en resoluciones estándar sin pérdida de foco."
                    ).FontSize(8.5f);
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Plan de Rediseño de Cubicación SC (C.SC)").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static void GenerateManoDeObraPlanDocument(string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("PLAN DE REDISEÑO: PLANIFICACIÓN DE MANO DE OBRA (MO)").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("MANO DE OBRA Y TARIFAS").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Resumen Ejecutivo
                    column.Item().Text("RESUMEN EJECUTIVO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "El módulo de Mano de Obra proyecta y calcula el costo mensual del personal directo (maestros, ayudantes) e indirecto (supervisores, administración) del proyecto. Se propone un rediseño de alta gama ($20B Design) que introduce un Histograma de Personal interactivo con curva de costo acumulado (Curva S), parametrización mediante plantillas de curva, edición directa de celdas con marcas de sobreescritura manual, y un panel tarifario interactivo para cargos y sueldos base."
                    ).FontSize(8.5f);

                    // 1. Componentes Visuales y Arquitectura
                    column.Item().PaddingTop(12).Text("1. COMPONENTES VISUALES Y ARQUITECTURA").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "La nueva interfaz está diseñada para maximizar el control visual y la velocidad de carga:"
                    ).FontSize(8.5f);

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Componente
                            columns.RelativeColumn(7); // Propósito y Comportamiento UX
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Componente UI").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Propósito y Comportamiento UX").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        table.Cell().Background("#F9FAFB").Padding(4).Text("HUD Estadístico").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Muestra el Costo Total MO (CLP y UF), Sueldos Base, Leyes Sociales acumuladas (35%), y el desglose de costo de MO Directa e Indirecta.").FontSize(8);

                        table.Cell().Padding(4).Text("Histograma de Personal").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Gráfico interactivo de barras apiladas que representa la dotación mensual. Incluye una línea superpuesta que grafica la Curva S del costo acumulado del proyecto.").FontSize(8);

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Planilla de Dotación").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Grilla mensual (M1 a Mn) donde el usuario puede editar directamente la cantidad de personal. Las celdas modificadas se destacan con un indicador naranja de sobreescritura.").FontSize(8);

                        table.Cell().Padding(4).Text("Panel Tarifario (Right Panel)").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Panel colapsable lateral que lista el catálogo de cargos activos, permitiendo editar sueldos base y agregar nuevos roles de forma paramétrica.").FontSize(8);
                    });

                    column.Item().PageBreak();

                    // 2. Funcionalidades Premium
                    column.Item().Text("2. FUNCIONALIDADES PREMIUM Y OPERATIVA").FontSize(11).Bold().FontColor("#0B2447");
                    
                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("A. Modelador Paramétrico de Curvas").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("El usuario puede seleccionar presets de curvas (Campana/Curva S, Lineal, Fija) para generar la distribución teórica de dotación de personal de acuerdo al plazo total. Al modificar la dotación pico, el sistema escala toda la curva automáticamente.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F9FAFB").Padding(8).Column(c =>
                    {
                        c.Item().Text("B. Sobreescritura Manual e Inteligente").Bold().FontSize(9).FontColor("#0B2447");
                        c.Item().Text("El estimador conserva control absoluto sobre los meses críticos. Si modifica manualmente la dotación de un cargo en el Mes 5, este valor se congela e independiza de la curva general. Un botón 'Restablecer' permite volver a la curva paramétrica.").FontSize(8);
                    });

                    column.Item().PaddingTop(6).Background("#F0F3FF").Padding(8).Column(c =>
                    {
                        c.Item().Text("C. Conmutación CLP / UF y Exportabilidad").Bold().FontSize(9).FontColor("#19376D");
                        c.Item().Text("El software permite alternar instantáneamente la visualización de todos los costos (KPIs, sueldos y totales de planilla) entre Pesos Chilenos (CLP) y Unidades de Fomento (UF) para alinearse con los informes ejecutivos de cierre.").FontSize(8);
                    });

                    column.Item().PaddingTop(12).Text("3. PLAN DE TRABAJO Y VERIFICACIÓN").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(2).Text(
                        "Se comprobará la consistencia matemática entre la dotación manual ingresada, el porcentaje de leyes sociales y la sumatoria de costos mensuales por cargo. Adicionalmente, se verificará que la representación gráfica del histograma SVG coincida exactamente con las filas calculadas en la planilla."
                    ).FontSize(8.5f);
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Plan de Rediseño de Mano de Obra (MO)").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }).GeneratePdf(path);
    }
}
