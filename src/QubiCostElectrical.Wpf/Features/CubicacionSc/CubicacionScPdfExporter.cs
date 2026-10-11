using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public static class CubicacionScPdfExporter
{
    public static void Export(
        ProjectInfoRow projectInfo,
        IReadOnlyList<CuadroCargaSCRow> cuadros,
        IReadOnlyList<ZonaProyecto> zonas,
        CubicacionScConfig config,
        string path)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var clientName = projectInfo.Cliente ?? "S/I";
        var projName = projectInfo.Nombre ?? "S/I";
        var estimador = projectInfo.UsuarioNombre ?? "S/I";
        var dateStr = DateTime.Now.ToString("dd/MM/yyyy");

        // Perform aggregations using Domain engine
        var totalMateriales = CubicacionSCEngine.AggregateTotal(cuadros, config);
        var porTablero = CubicacionSCEngine.AggregatePorTablero(cuadros, config);
        var porPiso = CubicacionSCEngine.AggregateMaterialesPorPiso(cuadros, config, zonas);
        var artefactosPorPiso = CubicacionSCEngine.AggregateArtefactosPorZona(cuadros, zonas);

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
                            titleCol.Item().Text("CUBICACIÓN DE SERVICIOS COMUNES (C.SC)").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("INFORME DE MATERIALES").FontSize(8).Bold().FontColor("#0B2447");
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
                                t.Span("Tableros: ").Bold();
                                t.Span(cuadros.Count.ToString());
                            });
                        });
                    });

                    column.Item().PaddingTop(14);

                    // 1. Resumen Total General de Materiales
                    column.Item().Text("1. RESUMEN GENERAL DE MATERIALES").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(7f); // Material
                            columns.RelativeColumn(2f); // Cantidad
                            columns.RelativeColumn(2f); // Unidad
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Descripción de Material").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Cantidad").Bold().FontColor(Colors.White).FontSize(8.5f).AlignRight();
                            header.Cell().Background("#0B2447").Padding(4).Text("Unidad").Bold().FontColor(Colors.White).FontSize(8.5f).AlignCenter();
                        });

                        int rowIndex = 0;
                        foreach (var mat in totalMateriales.Materiales)
                        {
                            var bg = (rowIndex % 2 == 0) ? "#FFFFFF" : "#F9FAFB";
                            rowIndex++;

                            table.Cell().Background(bg).Padding(4).Text(mat.Nombre).FontSize(8.5f);
                            table.Cell().Background(bg).Padding(4).Text(mat.Cantidad.ToString("N2")).FontSize(8.5f).AlignRight();
                            table.Cell().Background(bg).Padding(4).Text(mat.Unidad).FontSize(8.5f).AlignCenter();
                        }
                    });

                    // Page break for Tableros breakdown
                    column.Item().PageBreak();

                    // 2. Desglose de Materiales por Tablero
                    column.Item().Text("2. DESGLOSE DE MATERIALES POR TABLERO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4);

                    foreach (var tabGroup in porTablero)
                    {
                        if (tabGroup.Materiales.Count == 0) continue;

                        column.Item().PaddingTop(8).Text(tabGroup.Nombre.ToUpper()).FontSize(9.5f).Bold().FontColor("#19376D");
                        column.Item().Text($"Detalle: {tabGroup.Detalle} (Canalización: {tabGroup.TuberiaMl:N1} m, Conductor: {tabGroup.ConductorMl:N1} m)").FontSize(8).Italic().FontColor(Colors.Grey.Darken2);

                        column.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(7f);
                                columns.RelativeColumn(2f);
                                columns.RelativeColumn(2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#19376D").Padding(3).Text("Descripción").Bold().FontColor(Colors.White).FontSize(8f);
                                header.Cell().Background("#19376D").Padding(3).Text("Cantidad").Bold().FontColor(Colors.White).FontSize(8f).AlignRight();
                                header.Cell().Background("#19376D").Padding(3).Text("Unidad").Bold().FontColor(Colors.White).FontSize(8f).AlignCenter();
                            });

                            int tabRowIndex = 0;
                            foreach (var mat in tabGroup.Materiales)
                            {
                                var bg = (tabRowIndex % 2 == 0) ? "#FFFFFF" : "#F9FAFB";
                                tabRowIndex++;

                                table.Cell().Background(bg).Padding(3).Text(mat.Nombre).FontSize(8f);
                                table.Cell().Background(bg).Padding(3).Text(mat.Cantidad.ToString("N2")).FontSize(8f).AlignRight();
                                table.Cell().Background(bg).Padding(3).Text(mat.Unidad).FontSize(8f).AlignCenter();
                            }
                        });
                        column.Item().PaddingBottom(8);
                    }

                    // Page break for Floors & Zones
                    column.Item().PageBreak();

                    // 3. Desglose de Materiales por Piso
                    column.Item().Text("3. DESGLOSE DE MATERIALES POR PISO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4);

                    foreach (var floorGroup in porPiso)
                    {
                        if (floorGroup.Materiales.Count == 0) continue;

                        column.Item().PaddingTop(8).Text(floorGroup.Nombre.ToUpper()).FontSize(9.5f).Bold().FontColor("#19376D");
                        column.Item().Text($"Detalle: {floorGroup.Detalle} (Canalización: {floorGroup.TuberiaMl:N1} m, Conductor: {floorGroup.ConductorMl:N1} m)").FontSize(8).Italic().FontColor(Colors.Grey.Darken2);

                        column.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(7f);
                                columns.RelativeColumn(2f);
                                columns.RelativeColumn(2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#475569").Padding(3).Text("Descripción").Bold().FontColor(Colors.White).FontSize(8f);
                                header.Cell().Background("#475569").Padding(3).Text("Cantidad").Bold().FontColor(Colors.White).FontSize(8f).AlignRight();
                                header.Cell().Background("#475569").Padding(3).Text("Unidad").Bold().FontColor(Colors.White).FontSize(8f).AlignCenter();
                            });

                            int floorRowIndex = 0;
                            foreach (var mat in floorGroup.Materiales)
                            {
                                var bg = (floorRowIndex % 2 == 0) ? "#FFFFFF" : "#F9FAFB";
                                floorRowIndex++;

                                table.Cell().Background(bg).Padding(3).Text(mat.Nombre).FontSize(8f);
                                table.Cell().Background(bg).Padding(3).Text(mat.Cantidad.ToString("N2")).FontSize(8f).AlignRight();
                                table.Cell().Background(bg).Padding(3).Text(mat.Unidad).FontSize(8f).AlignCenter();
                            }
                        });
                        column.Item().PaddingBottom(8);
                    }

                    // Page break for Artefacts
                    column.Item().PageBreak();

                    // 4. Artefactos por Zona
                    column.Item().Text("4. REGISTRO DE ARTEFACTOS POR ZONA DE PROYECTO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5f); // Zona
                            columns.RelativeColumn(4f); // Artefacto
                            columns.RelativeColumn(2f); // Cantidad
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0B2447").Padding(4).Text("Zona / Recinto").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Artefacto").Bold().FontColor(Colors.White).FontSize(8.5f);
                            header.Cell().Background("#0B2447").Padding(4).Text("Cantidad").Bold().FontColor(Colors.White).FontSize(8.5f).AlignRight();
                        });

                        int artRowIndex = 0;
                        foreach (var zoneGroup in artefactosPorPiso)
                        {
                            if (zoneGroup.Artefactos.Count == 0) continue;

                            bool first = true;
                            foreach (var art in zoneGroup.Artefactos)
                            {
                                var bg = (artRowIndex % 2 == 0) ? "#FFFFFF" : "#F9FAFB";
                                artRowIndex++;

                                if (first)
                                {
                                    table.Cell().Background(bg).Padding(4).Text(zoneGroup.ZonaNombre).Bold().FontSize(8.5f);
                                    first = false;
                                }
                                else
                                {
                                    table.Cell().Background(bg).Padding(4).Text("").FontSize(8.5f);
                                }

                                table.Cell().Background(bg).Padding(4).Text(art.Nombre).FontSize(8.5f);
                                table.Cell().Background(bg).Padding(4).Text(art.Cantidad.ToString("N0")).FontSize(8.5f).AlignRight();
                            }
                        }
                    });
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Cubicaciones Servicios Comunes").FontSize(8).FontColor(Colors.Grey.Medium);
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
