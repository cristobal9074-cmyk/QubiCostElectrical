using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public static class ItemizadoPdfExporter
{
    public static void Export(ProjectInfoRow projectInfo, IReadOnlyList<ItemizadoRowVm> rows, string path)
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
                            titleCol.Item().Text("ESTRUCTURA DEL ITEMIZADO").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("INFORME DE ESTRUCTURA").FontSize(8).Bold().FontColor("#0B2447");
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
                                t.Span("Unidades de Medida").Bold(); 
                            });
                        });
                    });

                    column.Item().PaddingTop(12);

                    // Budget Table
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f); // Item
                            columns.RelativeColumn(8.0f); // Descripcion
                            columns.RelativeColumn(0.8f); // Ud
                        });

                        // Headers
                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Ítem").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Descripción").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Ud").Bold().FontColor(Colors.White).FontSize(8.5f).AlignCenter();
                        });

                        int rowIndex = 0;

                        foreach (var row in rows)
                        {
                            var bg = (rowIndex % 2 == 0) ? "#FFFFFF" : "#F9FAFB";
                            rowIndex++;

                            if (row.EsTitulo)
                            {
                                table.Cell().Background("#F0F3FF").Padding(4).Text(row.Item).Bold().FontColor("#19376D").FontSize(8.5f);
                                table.Cell().Background("#F0F3FF").Padding(4).Text(row.Descripcion).Bold().FontColor("#19376D").FontSize(8.5f);
                                table.Cell().Background("#F0F3FF").Padding(4).Text("").FontSize(8.5f);
                            }
                            else
                            {
                                table.Cell().Background(bg).Padding(4).Text(row.Item).FontSize(8.5f);
                                table.Cell().Background(bg).Padding(4).Text(row.Descripcion).FontSize(8.5f);
                                table.Cell().Background(bg).Padding(4).Text(row.Unidad).AlignCenter().FontSize(8.5f);
                            }
                        }
                    });
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Informe de Estructura").FontSize(8).FontColor(Colors.Grey.Medium);
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
