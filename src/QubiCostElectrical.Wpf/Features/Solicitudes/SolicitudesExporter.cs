using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public static class SolicitudesPdfExporter
{
    private const string CNavy = "#0B2442";
    private const string CBlue = "#0A59A1";
    private const string CHeader = "#F1F5F9";
    private const string CRowAlt = "#F8FAFC";
    private const string CText = "#1E293B";
    private const string CMuted = "#64748B";
    private const string CLine = "#E2E8F0";
    private const string CWhite = "#FFFFFF";

    public static void Export(string path, string correlativo, string obra, string solicitadoPor, string fecha, IReadOnlyList<SolicitudItemRowVm> materiales, IReadOnlyList<SolicitudItemRowVm> epps)
    {
        QuestPdfBootstrap.EnsureCommunityLicense();

        Document.Create(composer =>
        {
            composer.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9.5f).FontColor(CText));

                // 1. Header (Full Width Dark Navy Bar)
                page.Header().Height(42).Background(CNavy).AlignMiddle().Row(row =>
                {
                    row.RelativeItem().PaddingLeft(36).Text(t => t.Span("QUBI COST ELECTRICAL").FontSize(11).Bold().FontColor(Colors.White).LetterSpacing(0.05f));
                    row.RelativeItem().PaddingRight(36).AlignRight().Text(t => t.Span($"SOLICITUD N° {correlativo}").FontSize(9).FontColor("#94A3B8").Bold());
                });

                // 2. Content (With Margins)
                page.Content().PaddingHorizontal(36).PaddingVertical(24).Column(col =>
                {
                    // Title
                    col.Item().Text(t => t.Span("SOLICITUD DE MATERIALES Y EPP").FontSize(16).Bold().FontColor(CNavy));
                    col.Item().PaddingTop(2).Text(t => t.Span("Documento interno de control, despacho y provisión de materiales de obra.").FontSize(9).FontColor(CMuted));
                    col.Item().PaddingTop(12);

                    // Requisition Info Box (Bordered Grid)
                    col.Item().Border(1f).BorderColor(CLine).Background("#F8FAFC").Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(stack =>
                        {
                            stack.Item().Text(t => { t.Span("CÓDIGO SOLICITUD: ").Bold().FontSize(8.5f).FontColor(CMuted); t.Span(correlativo).Bold().FontSize(9.5f).FontColor(CBlue); });
                            stack.Item().PaddingTop(6).Text(t => { t.Span("OBRA / PROYECTO:  ").Bold().FontSize(8.5f).FontColor(CMuted); t.Span(obra).Bold().FontSize(9.5f).FontColor(CNavy); });
                        });
                        row.RelativeItem().Column(stack =>
                        {
                            stack.Item().Text(t => { t.Span("FECHA SOLICITUD:  ").Bold().FontSize(8.5f).FontColor(CMuted); t.Span(fecha).FontSize(9.5f).FontColor(CText); });
                            stack.Item().PaddingTop(6).Text(t => { t.Span("SOLICITADO POR:   ").Bold().FontSize(8.5f).FontColor(CMuted); t.Span(solicitadoPor).FontSize(9.5f).FontColor(CText); });
                        });
                    });
                    col.Item().PaddingTop(20);

                    // Materials Table
                    if (materiales.Any())
                    {
                        col.Item().Text(t => t.Span("1. DETALLE DE MATERIALES E INSUMOS").FontSize(11).Bold().FontColor(CNavy));
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80f);  // Codigo
                                columns.RelativeColumn(3f);   // Descripcion
                                columns.ConstantColumn(40f);  // U.M.
                                columns.ConstantColumn(70f);  // Cantidad
                                columns.RelativeColumn(2f);   // Observaciones
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).Text(t => t.Span("Código").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).Text(t => t.Span("Descripción Material / Insumo").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).AlignCenter().Text(t => t.Span("U.M.").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).AlignRight().Text(t => t.Span("Cantidad").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).Text(t => t.Span("Observaciones").FontColor(CNavy).Bold().FontSize(8.5f));
                            });

                            bool alt = false;
                            foreach (var item in materiales)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).Text(t => t.Span(item.Codigo ?? "").FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).Text(t => t.Span(item.Descripcion ?? "").FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).AlignCenter().Text(t => t.Span(item.Unidad ?? "").FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).AlignRight().Text(t => t.Span(item.Cantidad.ToString("N0")).FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).Text(t => t.Span(item.Observaciones ?? "").FontColor(CText).FontSize(8.5f));
                                alt = !alt;
                            }
                        });
                        col.Item().PaddingTop(24);
                    }

                    // EPP Table
                    if (epps.Any())
                    {
                        col.Item().Text(t => t.Span("2. ELEMENTOS DE PROTECCIÓN PERSONAL (EPP)").FontSize(11).Bold().FontColor(CNavy));
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80f);  // Codigo
                                columns.RelativeColumn(3f);   // Descripcion
                                columns.ConstantColumn(40f);  // U.M.
                                columns.ConstantColumn(70f);  // Cantidad
                                columns.RelativeColumn(2f);   // Observaciones
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).Text(t => t.Span("Código").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).Text(t => t.Span("Descripción Elemento EPP").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).AlignCenter().Text(t => t.Span("U.M.").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).AlignRight().Text(t => t.Span("Cantidad").FontColor(CNavy).Bold().FontSize(8.5f));
                                header.Cell().Background(CHeader).BorderBottom(1.5f).BorderColor(CNavy).PaddingHorizontal(8).PaddingVertical(6).Text(t => t.Span("Observaciones").FontColor(CNavy).Bold().FontSize(8.5f));
                            });

                            bool alt = false;
                            foreach (var item in epps)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).Text(t => t.Span(item.Codigo ?? "").FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).Text(t => t.Span(item.Descripcion ?? "").FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).AlignCenter().Text(t => t.Span(item.Unidad ?? "").FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).AlignRight().Text(t => t.Span(item.Cantidad.ToString("N0")).FontColor(CText).FontSize(8.5f));
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).PaddingHorizontal(8).PaddingVertical(5).Text(t => t.Span(item.Observaciones ?? "").FontColor(CText).FontSize(8.5f));
                                alt = !alt;
                            }
                        });
                        col.Item().PaddingTop(24);
                    }

                    // Signatures Box
                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Column(stack =>
                        {
                            stack.Item().BorderBottom(0.5f).BorderColor(CMuted).PaddingTop(40).Width(180);
                            stack.Item().PaddingTop(4).Text(t => t.Span("Firma Solicitante").FontSize(8).FontColor(CMuted));
                            stack.Item().Text(t => t.Span(solicitadoPor).Bold().FontSize(9).FontColor(CText));
                        });
                        row.ConstantItem(60);
                        row.RelativeItem().Column(stack =>
                        {
                            stack.Item().BorderBottom(0.5f).BorderColor(CMuted).PaddingTop(40).Width(180);
                            stack.Item().PaddingTop(4).Text(t => t.Span("Firma Aprobación Bodega").FontSize(8).FontColor(CMuted));
                            stack.Item().Text(t => t.Span("Nombre: ______________________").FontSize(8).FontColor(CMuted));
                        });
                    });
                });

                // 3. Footer (Bottom Margined)
                page.Footer().Height(30).PaddingHorizontal(36).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text(t => t.Span("Qubi Cost Electrical  •  Planilla de Pedido Interno").FontSize(7.5f).FontColor(CMuted));
                    row.RelativeItem().AlignMiddle().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(7.5f).FontColor(CMuted);
                        x.CurrentPageNumber().FontSize(7.5f).FontColor(CMuted);
                        x.Span(" de ").FontSize(7.5f).FontColor(CMuted);
                        x.TotalPages().FontSize(7.5f).FontColor(CMuted);
                    });
                });
            });
        }).GeneratePdf(path);
    }
}

public static class SolicitudesExcelExporter
{
    public static void Export(string path, string correlativo, string obra, string solicitadoPor, string fecha, IReadOnlyList<SolicitudItemRowVm> materiales, IReadOnlyList<SolicitudItemRowVm> epps)
    {
        if (File.Exists(path))
            File.Delete(path);

        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Segoe UI";
        wb.Style.Font.FontSize = 9;

        // Sheet 1: Materiales e Insumos
        var wsMat = wb.Worksheets.Add("Materiales e Insumos");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(wsMat);
        ClosedXmlExportStyler.AddSheetTitle(wsMat, $"PEDIDO INTERNO - MATERIALES Y HERRAMIENTAS ({correlativo})", 6);
        
        wsMat.Cell(2, 1).Value = "Código Solicitud";
        wsMat.Cell(2, 2).Value = correlativo;
        wsMat.Cell(2, 4).Value = "Obra / Proyecto";
        wsMat.Cell(2, 5).Value = obra;
        
        wsMat.Cell(3, 1).Value = "Solicitado por";
        wsMat.Cell(3, 2).Value = solicitadoPor;
        wsMat.Cell(3, 4).Value = "Fecha de Solicitud";
        wsMat.Cell(3, 5).Value = fecha;

        wsMat.Range(2, 1, 3, 5).Style.Font.Bold = true;

        int headerRow = 5;
        ClosedXmlExportStyler.SetHeaderRow(wsMat, headerRow, new[]
        {
            "CÓDIGO", "DESCRIPCIÓN MATERIAL / INSUMO", "U.M.", "CANTIDAD", "ENLACE / LINK", "OBSERVACIONES"
        });

        int r = headerRow + 1;
        foreach (var item in materiales)
        {
            wsMat.Cell(r, 1).Value = item.Codigo;
            wsMat.Cell(r, 2).Value = item.Descripcion;
            wsMat.Cell(r, 3).Value = item.Unidad;
            wsMat.Cell(r, 4).SetValue(ClosedXmlExportStyler.ToDouble(item.Cantidad));
            wsMat.Cell(r, 5).Value = item.Link;
            wsMat.Cell(r, 6).Value = item.Observaciones;
            r++;
        }
        int lastRowMat = Math.Max(r - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(wsMat, headerRow, 1, lastRowMat, 6, "tbl_materiales");
        wsMat.Range(headerRow + 1, 4, lastRowMat, 4).Style.NumberFormat.Format = "#,##0";
        wsMat.Column(2).Width = 48;
        wsMat.Column(5).Width = 24;
        wsMat.Column(6).Width = 30;
        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(wsMat);

        // Sheet 2: Elementos de Protección Personal
        var wsEpp = wb.Worksheets.Add("EPP");
        ClosedXmlExportStyler.PrepareClosedXmlSheet(wsEpp);
        ClosedXmlExportStyler.AddSheetTitle(wsEpp, $"PEDIDO INTERNO - ELEMENTOS DE PROTECCIÓN PERSONAL ({correlativo})", 6);
        
        wsEpp.Cell(2, 1).Value = "Código Solicitud";
        wsEpp.Cell(2, 2).Value = correlativo;
        wsEpp.Cell(2, 4).Value = "Obra / Proyecto";
        wsEpp.Cell(2, 5).Value = obra;
        
        wsEpp.Cell(3, 1).Value = "Solicitado por";
        wsEpp.Cell(3, 2).Value = solicitadoPor;
        wsEpp.Cell(3, 4).Value = "Fecha de Solicitud";
        wsEpp.Cell(3, 5).Value = fecha;

        wsEpp.Range(2, 1, 3, 5).Style.Font.Bold = true;

        ClosedXmlExportStyler.SetHeaderRow(wsEpp, headerRow, new[]
        {
            "CÓDIGO", "DESCRIPCIÓN ELEMENTO EPP", "U.M.", "CANTIDAD", "ENLACE / LINK", "OBSERVACIONES"
        });

        r = headerRow + 1;
        foreach (var item in epps)
        {
            wsEpp.Cell(r, 1).Value = item.Codigo;
            wsEpp.Cell(r, 2).Value = item.Descripcion;
            wsEpp.Cell(r, 3).Value = item.Unidad;
            wsEpp.Cell(r, 4).SetValue(ClosedXmlExportStyler.ToDouble(item.Cantidad));
            wsEpp.Cell(r, 5).Value = item.Link;
            wsEpp.Cell(r, 6).Value = item.Observaciones;
            r++;
        }
        int lastRowEpp = Math.Max(r - 1, headerRow);
        ClosedXmlExportStyler.StyleClosedXmlTable(wsEpp, headerRow, 1, lastRowEpp, 6, "tbl_epps");
        wsEpp.Range(headerRow + 1, 4, lastRowEpp, 4).Style.NumberFormat.Format = "#,##0";
        wsEpp.Column(2).Width = 48;
        wsEpp.Column(5).Width = 24;
        wsEpp.Column(6).Width = 30;
        ClosedXmlExportStyler.FinalizeClosedXmlWorksheet(wsEpp);

        wb.SaveAs(path);
    }
}
