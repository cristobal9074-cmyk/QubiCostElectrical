using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Documents;

public static class DocumentsPdfExporter
{
    public static void Export(ProjectInfoRow projectInfo, IReadOnlyList<AntecedenteRowVm> rows, string path)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var clientName = projectInfo.Cliente ?? "S/I";
        var projName = projectInfo.Nombre ?? "S/I";
        var estimador = projectInfo.UsuarioNombre ?? "S/I";
        var dateStr = DateTime.Now.ToString("dd/MM/yyyy");

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
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(15).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("REGISTRO DE ANTECEDENTES Y PLANOS").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("DOCUMENTACIÓN TÉCNICA").FontSize(8).Bold().FontColor("#0B2447");
                            metaCol.Item().Text($"Fecha: {dateStr}").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // Project metadata block
                    column.Item().Background("#F9FAFB").Padding(10).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem(1.2f).Text(t => 
                            { 
                                t.DefaultTextStyle(x => x.FontSize(8.5f)); 
                                t.Span("Proyecto: ").Bold(); 
                                t.Span(projName); 
                            });
                            r.RelativeItem(0.8f).Text(t => 
                            { 
                                t.DefaultTextStyle(x => x.FontSize(8.5f)); 
                                t.Span("Cliente: ").Bold(); 
                                t.Span(clientName); 
                            });
                        });
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem(1.2f).Text(t => 
                            { 
                                t.DefaultTextStyle(x => x.FontSize(8.5f)); 
                                t.Span("Estimador: ").Bold(); 
                                t.Span(estimador); 
                            });
                            r.RelativeItem(0.8f).Text(t => 
                            { 
                                t.DefaultTextStyle(x => x.FontSize(8.5f)); 
                                t.Span("Registros totales: ").Bold(); 
                                t.Span(rows.Count.ToString()); 
                            });
                        });
                    });

                    column.Item().PaddingTop(12);

                    // Documents Table
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f); // Tipo
                            columns.RelativeColumn(1.4f); // Codigo
                            columns.RelativeColumn(0.5f); // Rev.
                            columns.RelativeColumn(4.7f); // Descripcion
                            columns.RelativeColumn(1.2f); // Disciplina
                            columns.RelativeColumn(1.0f); // Estado
                        });

                        // Headers
                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Tipo").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Código").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Rev.").Bold().FontColor(Colors.White).FontSize(8.5f).AlignCenter();
                            header.Cell().Background("#0B2447").Padding(4).Text("Documento / Descripción").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Disciplina").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Estado").Bold().FontColor(Colors.White).FontSize(8.5f);
                        });

                        int rowIndex = 0;

                        foreach (var row in rows)
                        {
                            var bg = (rowIndex % 2 == 0) ? "#FFFFFF" : "#F9FAFB";
                            rowIndex++;

                            table.Cell().Background(bg).Padding(4).Text(row.Tipo).FontSize(8.5f);
                            table.Cell().Background(bg).Padding(4).Text(row.Codigo).FontSize(8.5f);
                            table.Cell().Background(bg).Padding(4).Text(row.Revision).AlignCenter().FontSize(8.5f);
                            table.Cell().Background(bg).Padding(4).Text(row.Descripcion).FontSize(8.5f);
                            table.Cell().Background(bg).Padding(4).Text(row.Disciplina ?? "—").FontSize(8.5f);
                            
                            var statusText = row.EstadoLabel;
                            var statusColor = "#1E293B";
                            if (statusText.Equals("Vigente", StringComparison.OrdinalIgnoreCase))
                                statusColor = "#059669";
                            else if (statusText.Equals("Pendiente mandante", StringComparison.OrdinalIgnoreCase))
                                statusColor = "#D97706";
                            else if (statusText.Equals("Histórico", StringComparison.OrdinalIgnoreCase))
                                statusColor = "#DC2626";

                            table.Cell().Background(bg).Padding(4).Text(statusText).FontColor(statusColor).Bold().FontSize(8.5f);
                        }
                    });
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Listado de Antecedentes").FontSize(8).FontColor(Colors.Grey.Medium);
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
