using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public static class ChangeOrderPdfExporter
{
    private const string CNavy = "#0B2447";
    private const string CBlue = "#0A59A1";
    private const string CHeaderBg = "#F1F5F9";
    private const string CRowAlt = "#F8FAFC";
    private const string CText = "#1E293B";
    private const string CMuted = "#64748B";
    private const string CLine = "#E2E8F0";

    public static void Export(ChangeOrderRow oc, ProjectInfoRow project, bool muestraUf, decimal ufRate, string filePath)
    {
        QuestPdfBootstrap.EnsureCommunityLicense();

        string FormatCurrency(decimal ufAmount)
        {
            if (muestraUf)
                return $"{ufAmount:N3} UF";
            decimal clp = Math.Round(ufAmount * ufRate, 0, MidpointRounding.AwayFromZero);
            return $"$ {clp:N0}";
        }

        string FormatDelta(decimal ufAmount)
        {
            if (muestraUf)
                return $"{ufAmount:+0.000;-0.000;0.000} UF";
            decimal clp = Math.Round(ufAmount * ufRate, 0, MidpointRounding.AwayFromZero);
            return clp > 0 ? $"+$ {clp:N0}" : clp < 0 ? $"- $ {Math.Abs(clp):N0}" : "$ 0";
        }

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(CText));

                // ── HEADER FORMAL ──
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("QUBI COST ELECTRICAL").FontSize(14).Bold().FontColor(CNavy).LetterSpacing(0.04f);
                            titleCol.Item().Text("DOCUMENTO FORMAL DE ADICIONAL DE OBRA / ORDEN DE CAMBIO").FontSize(8.5f).Bold().FontColor(CBlue);
                        });

                        row.ConstantItem(210).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text($"N° ADICIONAL: {oc.Numero}").FontSize(11).Bold().FontColor(CNavy);
                            metaCol.Item().Text($"Fecha: {oc.FechaCreacion:dd/MM/yyyy}").FontSize(8.5f).FontColor(CMuted);
                            metaCol.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("ESTADO: ").FontSize(8).Bold().FontColor(CMuted);
                                t.Span(oc.Estado.ToUpperInvariant()).FontSize(8.5f).Bold().FontColor(oc.Estado == "Aprobado" ? "#15803D" : oc.Estado == "Rechazado" ? "#B91C1C" : oc.Estado == "En revisión" ? "#B45309" : "#475569");
                            });
                        });
                    });

                    header.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1.5f).LineColor(CNavy);
                });

                // ── CONTENIDO PRINCIPAL ──
                page.Content().Column(col =>
                {
                    // 1. Ficha del Proyecto y Adicional
                    col.Item().Border(1f).BorderColor(CLine).Background("#F8FAFC").Padding(10).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem(1.4f).Text(t =>
                            {
                                t.Span("OBRA / PROYECTO: ").Bold().FontSize(8.5f).FontColor(CMuted);
                                t.Span(project.Nombre ?? "Sin nombre").Bold().FontSize(9.5f).FontColor(CNavy);
                            });
                            r.RelativeItem(1.0f).Text(t =>
                            {
                                t.Span("TIPO OBRA: ").Bold().FontSize(8.5f).FontColor(CMuted);
                                t.Span(project.TipoProyecto ?? "Instalación Eléctrica").Bold().FontSize(9.5f).FontColor(CText);
                            });
                        });

                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem(1.4f).Text(t =>
                            {
                                t.Span("CLIENTE / MANDANTE: ").Bold().FontSize(8.5f).FontColor(CMuted);
                                t.Span(project.Cliente ?? "Sin mandante").FontSize(9f).FontColor(CText);
                            });
                            r.RelativeItem(1.0f).Text(t =>
                            {
                                t.Span("UBICACIÓN: ").Bold().FontSize(8.5f).FontColor(CMuted);
                                t.Span(project.Ubicacion ?? "Sin ubicación").FontSize(9f).FontColor(CText);
                            });
                        });

                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem(1.4f).Text(t =>
                            {
                                t.Span("TÍTULO ADICIONAL: ").Bold().FontSize(8.5f).FontColor(CMuted);
                                t.Span(oc.Titulo).Bold().FontSize(9.5f).FontColor(CBlue);
                            });
                            r.RelativeItem(1.0f).Text(t =>
                            {
                                t.Span("ELABORADO POR: ").Bold().FontSize(8.5f).FontColor(CMuted);
                                t.Span(string.IsNullOrWhiteSpace(oc.CreadoPor) ? (project.UsuarioNombre ?? "Estimador") : oc.CreadoPor).FontSize(9f).FontColor(CText);
                            });
                        });

                        if (!string.IsNullOrWhiteSpace(oc.Nota))
                        {
                            c.Item().PaddingTop(4).Text(t =>
                            {
                                t.Span("JUSTIFICACIÓN / NOTA: ").Bold().FontSize(8.5f).FontColor(CMuted);
                                t.Span(oc.Nota).FontSize(8.5f).Italic().FontColor(CText);
                            });
                        }
                    });

                    col.Item().PaddingTop(12);

                    // 2. Tarjetas de Resumen Financiero Ejecutivo
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1f).BorderColor(CLine).Background("#F1F5F9").Padding(8).Column(box =>
                        {
                            box.Item().Text("COSTO DIRECTO NETO").FontSize(7.5f).Bold().FontColor(CMuted);
                            box.Item().PaddingTop(2).Text(FormatDelta(oc.DeltaCostoUf)).FontSize(11).Bold().FontColor(CNavy);
                        });

                        r.ConstantItem(6);

                        r.RelativeItem().Border(1f).BorderColor(CLine).Background("#F1F5F9").Padding(8).Column(box =>
                        {
                            box.Item().Text($"GASTOS GENERALES ({oc.GastosGeneralesPct:N1}%)").FontSize(7.5f).Bold().FontColor(CMuted);
                            box.Item().PaddingTop(2).Text(FormatDelta(oc.DeltaGastosGeneralesUf)).FontSize(11).Bold().FontColor(CNavy);
                        });

                        r.ConstantItem(6);

                        r.RelativeItem().Border(1f).BorderColor(CLine).Background("#F1F5F9").Padding(8).Column(box =>
                        {
                            box.Item().Text($"UTILIDAD ({oc.UtilidadPct:N1}%)").FontSize(7.5f).Bold().FontColor(CMuted);
                            box.Item().PaddingTop(2).Text(FormatDelta(oc.DeltaUtilidadUf)).FontSize(11).Bold().FontColor(CNavy);
                        });

                        r.ConstantItem(6);

                        r.RelativeItem(1.2f).Border(1.5f).BorderColor("#15803D").Background("#F0FDF4").Padding(8).Column(box =>
                        {
                            box.Item().Text("TOTAL VENTA NETO ADICIONAL").FontSize(8f).Bold().FontColor("#15803D");
                            box.Item().PaddingTop(2).Text(FormatDelta(oc.DeltaVentaUf)).FontSize(12).Bold().FontColor("#15803D");
                        });
                    });

                    col.Item().PaddingTop(14);

                    // 3. Tabla Formal de Partidas
                    col.Item().Text("1. DESGLOSE DE PARTIDAS Y MEDICIÓN").FontSize(10.5f).Bold().FontColor(CNavy);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(42);       // Ítem
                            cols.RelativeColumn(3.8f);     // Descripción
                            cols.ConstantColumn(80);       // Tipo
                            cols.ConstantColumn(32);       // UD
                            cols.ConstantColumn(48);       // Cant Base
                            cols.ConstantColumn(48);       // Δ Cant
                            cols.ConstantColumn(48);       // Cant Nueva
                            cols.ConstantColumn(74);       // P.U. Venta
                            cols.ConstantColumn(84);       // Subtotal Venta
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("ÍTEM").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).Text("DESCRIPCIÓN DE LA PARTIDA").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("TIPO").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("UD").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("BASE").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("Δ CANT").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("NUEVA").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("P.U. VENTA").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("SUBTOTAL").Bold().FontSize(8f).FontColor(Colors.White);
                        });

                        int idx = 0;
                        foreach (var linea in oc.Lineas)
                        {
                            if (linea.EsTitulo)
                            {
                                table.Cell().ColumnSpan(9).Background("#E2E8F0").Padding(4).Text(t =>
                                {
                                    t.Span(linea.Item).Bold().FontSize(8.5f).FontColor(CNavy);
                                    t.Span("  ");
                                    t.Span(linea.DescripcionPartida).Bold().FontSize(8.5f).FontColor(CNavy);
                                });
                                continue;
                            }

                            string bg = idx % 2 == 0 ? Colors.White : CRowAlt;
                            idx++;

                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(linea.Item).FontSize(8.5f);
                            table.Cell().Background(bg).Padding(3).Text(linea.DescripcionPartida).FontSize(8.5f);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(linea.EsContractual ? "Contractual" : "Extraordinaria").FontSize(7.5f).FontColor(linea.EsContractual ? "#B45309" : CBlue);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(linea.Unidad).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignRight().Text(linea.CantidadBase.ToString("N1")).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignRight().Text(linea.CantidadAdicional > 0 ? $"+{linea.CantidadAdicional:N1}" : linea.CantidadAdicional.ToString("N1")).FontSize(8f).Bold();
                            table.Cell().Background(bg).Padding(3).AlignRight().Text(linea.CantidadNueva.ToString("N1")).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignRight().Text(FormatCurrency(linea.PrecioUnitarioContratoUf)).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignRight().Text(FormatDelta(linea.SubtotalUf)).FontSize(8.5f).Bold();
                        }

                        // Filas de Totales al pie de la tabla
                        table.Cell().ColumnSpan(7).Background("#F1F5F9").Padding(4).AlignRight().Text("SUBTOTAL COSTO DIRECTO:").Bold().FontSize(8.5f).FontColor(CNavy);
                        table.Cell().ColumnSpan(2).Background("#F1F5F9").Padding(4).AlignRight().Text(FormatDelta(oc.DeltaCostoUf)).Bold().FontSize(9f).FontColor(CNavy);

                        table.Cell().ColumnSpan(7).Background("#F1F5F9").Padding(3).AlignRight().Text($"GASTOS GENERALES ({oc.GastosGeneralesPct:N1}%):").FontSize(8f).FontColor(CMuted);
                        table.Cell().ColumnSpan(2).Background("#F1F5F9").Padding(3).AlignRight().Text(FormatDelta(oc.DeltaGastosGeneralesUf)).FontSize(8.5f).FontColor(CText);

                        table.Cell().ColumnSpan(7).Background("#F1F5F9").Padding(3).AlignRight().Text($"UTILIDAD ({oc.UtilidadPct:N1}%):").FontSize(8f).FontColor(CMuted);
                        table.Cell().ColumnSpan(2).Background("#F1F5F9").Padding(3).AlignRight().Text(FormatDelta(oc.DeltaUtilidadUf)).FontSize(8.5f).FontColor(CText);

                        table.Cell().ColumnSpan(7).Background("#DCFCE7").Padding(5).AlignRight().Text("TOTAL VENTA NETO ADICIONAL:").Bold().FontSize(9f).FontColor("#15803D");
                        table.Cell().ColumnSpan(2).Background("#DCFCE7").Padding(5).AlignRight().Text(FormatDelta(oc.DeltaVentaUf)).Bold().FontSize(10.5f).FontColor("#15803D");
                    });

                    col.Item().PaddingTop(24);

                    // 4. Bloque Formal de Firmas
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1f).BorderColor(CLine).Padding(10).Column(signBox =>
                        {
                            signBox.Item().AlignCenter().Text("POR EMPRESA CONTRATISTA").Bold().FontSize(8.5f).FontColor(CNavy);
                            signBox.Item().PaddingTop(32).LineHorizontal(1f).LineColor(CLine);
                            signBox.Item().PaddingTop(4).Text("Nombre / Cargo:").FontSize(8f).FontColor(CMuted);
                            signBox.Item().PaddingTop(2).Text("Firma y Timbre:").FontSize(8f).FontColor(CMuted);
                            signBox.Item().PaddingTop(2).Text("Fecha: _____ / _____ / 20___").FontSize(8f).FontColor(CMuted);
                        });

                        r.ConstantItem(18);

                        r.RelativeItem().Border(1f).BorderColor(CLine).Padding(10).Column(signBox =>
                        {
                            signBox.Item().AlignCenter().Text("POR INSPECCIÓN TÉCNICA / MANDANTE").Bold().FontSize(8.5f).FontColor(CNavy);
                            signBox.Item().PaddingTop(32).LineHorizontal(1f).LineColor(CLine);
                            signBox.Item().PaddingTop(4).Text("Nombre / Cargo:").FontSize(8f).FontColor(CMuted);
                            signBox.Item().PaddingTop(2).Text("Firma de Aprobación:").FontSize(8f).FontColor(CMuted);
                            signBox.Item().PaddingTop(2).Text("Fecha: _____ / _____ / 20___").FontSize(8f).FontColor(CMuted);
                        });
                    });

                    // 5. ANEXO TÉCNICO: CUBICACIÓN Y MATERIALES POR PARTIDA (Si existen materiales)
                    var lineasConMateriales = oc.Lineas.Where(l => !l.EsTitulo && l.Materiales != null && l.Materiales.Count > 0).ToList();
                    if (lineasConMateriales.Count > 0)
                    {
                        col.Item().PageBreak();

                        col.Item().Text("2. ANEXO TÉCNICO: DESGLOSE DE CUBICACIÓN Y MATERIALES").FontSize(12).Bold().FontColor(CNavy);
                        col.Item().Text("Detalle de los recursos, insumos y cubicaciones unitarias que componen cada partida del adicional.").FontSize(8.5f).FontColor(CMuted);
                        col.Item().PaddingTop(8);

                        foreach (var linea in lineasConMateriales)
                        {
                            col.Item().PaddingTop(10).Border(1f).BorderColor(CLine).Background("#F8FAFC").Padding(6).Row(pr =>
                            {
                                pr.RelativeItem().Text(t =>
                                {
                                    t.Span("Partida ").Bold().FontColor(CMuted);
                                    t.Span(linea.Item).Bold().FontColor(CNavy);
                                    t.Span(": ");
                                    t.Span(linea.DescripcionPartida).Bold().FontColor(CNavy);
                                });
                                pr.ConstantItem(160).AlignRight().Text(t =>
                                {
                                    t.Span("Δ Medición: ").FontColor(CMuted);
                                    t.Span($"{linea.CantidadAdicional:N2} {linea.Unidad}").Bold().FontColor(CBlue);
                                });
                            });

                            col.Item().Table(matTable =>
                            {
                                matTable.ColumnsDefinition(mcols =>
                                {
                                    mcols.ConstantColumn(85);      // Código
                                    mcols.RelativeColumn(3.5f);    // Descripción
                                    mcols.ConstantColumn(35);      // UD
                                    mcols.ConstantColumn(60);      // Cantidad
                                    mcols.ConstantColumn(80);      // P.U. Costo
                                    mcols.ConstantColumn(90);      // Total Costo
                                });

                                matTable.Header(mh =>
                                {
                                    mh.Cell().Background(CBlue).Padding(3).Text("CÓDIGO").Bold().FontSize(7.5f).FontColor(Colors.White);
                                    mh.Cell().Background(CBlue).Padding(3).Text("DESCRIPCIÓN RECURSO / MATERIAL").Bold().FontSize(7.5f).FontColor(Colors.White);
                                    mh.Cell().Background(CBlue).Padding(3).AlignCenter().Text("UD").Bold().FontSize(7.5f).FontColor(Colors.White);
                                    mh.Cell().Background(CBlue).Padding(3).AlignRight().Text("CANTIDAD").Bold().FontSize(7.5f).FontColor(Colors.White);
                                    mh.Cell().Background(CBlue).Padding(3).AlignRight().Text("P.U. COSTO").Bold().FontSize(7.5f).FontColor(Colors.White);
                                    mh.Cell().Background(CBlue).Padding(3).AlignRight().Text("TOTAL COSTO").Bold().FontSize(7.5f).FontColor(Colors.White);
                                });

                                int mIdx = 0;
                                foreach (var mat in linea.Materiales)
                                {
                                    string mbg = mIdx % 2 == 0 ? Colors.White : CRowAlt;
                                    mIdx++;

                                    matTable.Cell().Background(mbg).Padding(2.5f).Text(mat.Codigo).FontSize(7.5f);
                                    matTable.Cell().Background(mbg).Padding(2.5f).Text(mat.Descripcion).FontSize(7.5f);
                                    matTable.Cell().Background(mbg).Padding(2.5f).AlignCenter().Text(mat.Unidad).FontSize(7.5f);
                                    matTable.Cell().Background(mbg).Padding(2.5f).AlignRight().Text(mat.Cantidad.ToString("N2")).FontSize(7.5f);
                                    matTable.Cell().Background(mbg).Padding(2.5f).AlignRight().Text(FormatCurrency(mat.UfUnit)).FontSize(7.5f);
                                    matTable.Cell().Background(mbg).Padding(2.5f).AlignRight().Text(FormatCurrency(mat.UfTotal)).FontSize(7.5f).Bold();
                                }

                                decimal subtotalMat = linea.Materiales.Sum(m => m.UfTotal);
                                matTable.Cell().ColumnSpan(5).Background("#F1F5F9").Padding(3).AlignRight().Text("SUBTOTAL MATERIALES PARTIDA:").Bold().FontSize(7.5f).FontColor(CNavy);
                                matTable.Cell().Background("#F1F5F9").Padding(3).AlignRight().Text(FormatCurrency(subtotalMat)).Bold().FontSize(8f).FontColor(CNavy);
                            });

                            col.Item().PaddingTop(6);
                        }
                    }
                });

                // ── FOOTER DE PÁGINA ──
                page.Footer().Height(20).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Qubi Cost Electrical · Sistema de Presupuestos & Control de Obras").FontSize(7.5f).FontColor(CMuted);
                    });
                    r.ConstantItem(120).AlignRight().Text(x =>
                    {
                        x.CurrentPageNumber().FontSize(8f).FontColor(CMuted);
                        x.Span(" / ").FontSize(8f).FontColor(CMuted);
                        x.TotalPages().FontSize(8f).FontColor(CMuted);
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    public static void ExportList(IReadOnlyList<ChangeOrderRow> orders, ProjectInfoRow project, decimal originalContractUf, bool muestraUf, decimal ufRate, string filePath)
    {
        QuestPdfBootstrap.EnsureCommunityLicense();

        string FormatCurrency(decimal ufAmount)
        {
            if (muestraUf)
                return $"{ufAmount:N3} UF";
            decimal clp = Math.Round(ufAmount * ufRate, 0, MidpointRounding.AwayFromZero);
            return $"$ {clp:N0}";
        }

        string FormatDelta(decimal ufAmount)
        {
            if (muestraUf)
                return $"{ufAmount:+0.000;-0.000;0.000} UF";
            decimal clp = Math.Round(ufAmount * ufRate, 0, MidpointRounding.AwayFromZero);
            return clp > 0 ? $"+$ {clp:N0}" : clp < 0 ? $"- $ {Math.Abs(clp):N0}" : "$ 0";
        }

        decimal totalAprobadoUf = orders.Where(x => x.Estado == "Aprobado").Sum(x => x.DeltaVentaUf);
        decimal totalTramiteUf = orders.Where(x => x.Estado is "Borrador" or "En revisión").Sum(x => x.DeltaVentaUf);
        decimal totalProyectadoUf = originalContractUf + totalAprobadoUf;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(CText));

                // Header
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("QUBI COST ELECTRICAL").FontSize(14).Bold().FontColor(CNavy);
                            titleCol.Item().Text("PLANILLA DE CONTROL GENERAL DE ADICIONALES Y AUMENTOS DE CONTRATO").FontSize(8.5f).Bold().FontColor(CBlue);
                        });

                        row.ConstantItem(220).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text($"Proyecto: {project.Nombre ?? "Sin nombre"}").FontSize(9.5f).Bold().FontColor(CNavy);
                            metaCol.Item().Text($"Fecha emisión: {DateTime.Now:dd/MM/yyyy}").FontSize(8.5f).FontColor(CMuted);
                            metaCol.Item().Text($"Total Adicionales: {orders.Count}").FontSize(8.5f).FontColor(CText);
                        });
                    });

                    header.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1.5f).LineColor(CNavy);
                });

                // Content
                page.Content().Column(col =>
                {
                    // KPI Cards
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1f).BorderColor(CLine).Background("#F1F5F9").Padding(8).Column(box =>
                        {
                            box.Item().Text("CONTRATO ORIGINAL").FontSize(7.5f).Bold().FontColor(CMuted);
                            box.Item().PaddingTop(2).Text(FormatCurrency(originalContractUf)).FontSize(11).Bold().FontColor(CNavy);
                        });

                        r.ConstantItem(8);

                        r.RelativeItem().Border(1f).BorderColor("#15803D").Background("#F0FDF4").Padding(8).Column(box =>
                        {
                            box.Item().Text("APROBADOS MANDANTE").FontSize(7.5f).Bold().FontColor("#15803D");
                            box.Item().PaddingTop(2).Text(FormatDelta(totalAprobadoUf)).FontSize(11).Bold().FontColor("#15803D");
                        });

                        r.ConstantItem(8);

                        r.RelativeItem().Border(1f).BorderColor("#B45309").Background("#FEF3C7").Padding(8).Column(box =>
                        {
                            box.Item().Text("EN TRÁMITE / ITO").FontSize(7.5f).Bold().FontColor("#B45309");
                            box.Item().PaddingTop(2).Text(FormatDelta(totalTramiteUf)).FontSize(11).Bold().FontColor("#B45309");
                        });

                        r.ConstantItem(8);

                        r.RelativeItem().Border(1.5f).BorderColor(CNavy).Background("#F8FAFC").Padding(8).Column(box =>
                        {
                            box.Item().Text("TOTAL OBRA PROYECTADO").FontSize(7.5f).Bold().FontColor(CNavy);
                            box.Item().PaddingTop(2).Text(FormatCurrency(totalProyectadoUf)).FontSize(11.5f).Bold().FontColor(CNavy);
                        });
                    });

                    col.Item().PaddingTop(14);

                    // Master Table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);       // #
                            cols.ConstantColumn(65);       // N° OC
                            cols.RelativeColumn(3.5f);     // Título
                            cols.ConstantColumn(80);       // Estado
                            cols.ConstantColumn(60);       // Partidas
                            cols.ConstantColumn(75);       // Fecha
                            cols.ConstantColumn(90);       // Creada por
                            cols.ConstantColumn(90);       // Costo Directo
                            cols.ConstantColumn(95);       // Venta Neta
                            cols.RelativeColumn(2.0f);     // Nota
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("#").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("N° OC").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).Text("TÍTULO DEL ADICIONAL").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("ESTADO").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("PARTIDAS").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("FECHA").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).Text("CREADO POR").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("COSTO DIRECTO").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("VENTA NETA").Bold().FontSize(8f).FontColor(Colors.White);
                            h.Cell().Background(CNavy).Padding(4).Text("NOTA").Bold().FontSize(8f).FontColor(Colors.White);
                        });

                        int seq = 1;
                        foreach (var oc in orders)
                        {
                            string bg = seq % 2 == 0 ? Colors.White : CRowAlt;

                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(seq.ToString()).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(oc.Numero).FontSize(8.5f).Bold();
                            table.Cell().Background(bg).Padding(3).Text(oc.Titulo).FontSize(8.5f);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(oc.Estado).FontSize(7.5f).Bold().FontColor(oc.Estado == "Aprobado" ? "#15803D" : oc.Estado == "Rechazado" ? "#B91C1C" : oc.Estado == "En revisión" ? "#B45309" : "#475569");
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text($"{oc.Lineas.Count} part.").FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(oc.FechaCreacion.ToString("dd/MM/yyyy")).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).Text(oc.CreadoPor).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignRight().Text(FormatDelta(oc.DeltaCostoUf)).FontSize(8f);
                            table.Cell().Background(bg).Padding(3).AlignRight().Text(FormatDelta(oc.DeltaVentaUf)).FontSize(8.5f).Bold();
                            table.Cell().Background(bg).Padding(3).Text(oc.Nota).FontSize(7.5f).FontColor(CMuted);

                            seq++;
                        }

                        // Totals Row
                        decimal sumCosto = orders.Sum(x => x.DeltaCostoUf);
                        decimal sumVenta = orders.Sum(x => x.DeltaVentaUf);

                        table.Cell().ColumnSpan(7).Background("#F1F5F9").Padding(5).AlignRight().Text("TOTALES ACUMULADOS DE ADICIONALES:").Bold().FontSize(8.5f).FontColor(CNavy);
                        table.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text(FormatDelta(sumCosto)).Bold().FontSize(9f).FontColor(CNavy);
                        table.Cell().Background("#DCFCE7").Padding(5).AlignRight().Text(FormatDelta(sumVenta)).Bold().FontSize(9.5f).FontColor("#15803D");
                        table.Cell().Background("#F1F5F9").Padding(5).Text("");
                    });
                });

                // Footer
                page.Footer().Height(20).Row(r =>
                {
                    r.RelativeItem().Text("Qubi Cost Electrical · Planilla General de Adicionales").FontSize(7.5f).FontColor(CMuted);
                    r.ConstantItem(120).AlignRight().Text(x =>
                    {
                        x.CurrentPageNumber().FontSize(8f).FontColor(CMuted);
                        x.Span(" / ").FontSize(8f).FontColor(CMuted);
                        x.TotalPages().FontSize(8f).FontColor(CMuted);
                    });
                });
            });
        }).GeneratePdf(filePath);
    }
}
