using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Project;

public static class ProjectFichaExporter
{
    public static void Export(ProjectEditSnapshot snapshot, string path, string creationDate, string modDate)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9.5f).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("Qubi Cost Electrical").FontSize(16).Bold().FontColor("#0B2447");
                            titleCol.Item().Text("FICHA TÉCNICA DE PROYECTO Y CONDICIONES").FontSize(9).Bold().FontColor("#19376D");
                        });

                        row.ConstantItem(150).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("ESTADO: " + snapshot.EstadoPresupuesto.ToUpper()).FontSize(9).Bold().FontColor(
                                snapshot.EstadoPresupuesto == "Aprobado" ? "#10AC84" : (snapshot.EstadoPresupuesto == "En revisión" ? "#19376D" : "#FF9F43")
                            );
                            metaCol.Item().Text("Fecha: " + DateTime.Now.ToString("dd/MM/yyyy")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    column.Item().PaddingTop(6).PaddingBottom(12).LineHorizontal(1).LineColor("#19376D");
                });

                // Content
                page.Content().Column(column =>
                {
                    // 1. Identificación del Proyecto
                    column.Item().Text("1. IDENTIFICACIÓN DEL PROYECTO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(8).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t => { t.Span("Nombre: ").Bold(); t.Span(snapshot.Nombre); });
                        });
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem(1.5f).Text(t => { t.Span("Cliente: ").Bold(); t.Span(snapshot.Cliente); });
                            r.RelativeItem(1.0f).Text(t => { t.Span("Tipo: ").Bold(); t.Span(snapshot.TipoProyecto); });
                        });
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text(t => { t.Span("Ubicación: ").Bold(); t.Span(snapshot.Ubicacion); });
                        });
                    });

                    // 2. Responsable
                    column.Item().PaddingTop(12).Text("2. RESPONSABLE DEL PRESUPUESTO").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Background("#F9FAFB").Padding(8).Row(r =>
                    {
                        r.RelativeItem().Text(t => { t.Span("Estimador: ").Bold(); t.Span(snapshot.UsuarioNombre); });
                        r.RelativeItem().Text(t => { t.Span("Rol / Cargo: ").Bold(); t.Span(snapshot.UsuarioRol); });
                    });

                    // 3. Parámetros y Condiciones Comerciales
                    column.Item().PaddingTop(12).Text("3. CONDICIONES COMERCIALES Y CONVERSIÓN").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Background("#0B2447").Padding(4).Text("Parámetro").Bold().FontColor(Colors.White).FontSize(8.5f);
                        table.Cell().Background("#0B2447").Padding(4).Text("Valor").Bold().FontColor(Colors.White).FontSize(8.5f);
                        table.Cell().Background("#0B2447").Padding(4).Text("Parámetro").Bold().FontColor(Colors.White).FontSize(8.5f);
                        table.Cell().Background("#0B2447").Padding(4).Text("Valor").Bold().FontColor(Colors.White).FontSize(8.5f);

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Moneda Base").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text(snapshot.MonedaPresupuesto).FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Valor UF (CLP)").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text(snapshot.ValorUfClp.ToString("N0")).FontSize(8).Bold();

                        table.Cell().Padding(4).Text("Gastos Generales (GG)").FontSize(8);
                        table.Cell().Padding(4).Text(snapshot.GastosGeneralesPct.ToString("F1") + "%").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Margen Utilidad").FontSize(8);
                        table.Cell().Padding(4).Text(snapshot.UtilidadPct.ToString("F1") + "%").FontSize(8).Bold();

                        table.Cell().Background("#F9FAFB").Padding(4).Text("Impuesto IVA").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text(snapshot.IvaPct.ToString("F1") + "%").FontSize(8).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Validez de Oferta").FontSize(8);
                        table.Cell().Background("#F9FAFB").Padding(4).Text(snapshot.ValidityDays + " días").FontSize(8).Bold();

                        table.Cell().Padding(4).Text("Superficie Obra").FontSize(8);
                        table.Cell().Padding(4).Text(snapshot.MetrosCuadrados.ToString("N2") + " m²").FontSize(8).Bold();
                        table.Cell().Padding(4).Text("Cambio USD (CLP)").FontSize(8);
                        table.Cell().Padding(4).Text(snapshot.TipoCambioUsdClp.ToString("N0")).FontSize(8).Bold();
                    });

                    // 4. Coeficientes de Sobreventa (Markups)
                    column.Item().PaddingTop(12).Text("4. COEFICIENTES DE SOBREVENTA POR FAMILIA (MARKUPS)").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn(0.6f);
                            columns.RelativeColumn();
                            columns.RelativeColumn(0.6f);
                            columns.RelativeColumn();
                            columns.RelativeColumn(0.6f);
                        });

                        table.Cell().Background("#19376D").Padding(4).Text("Familia").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Background("#19376D").Padding(4).AlignCenter().Text("Factor").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Background("#19376D").Padding(4).Text("Familia").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Background("#19376D").Padding(4).AlignCenter().Text("Factor").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Background("#19376D").Padding(4).Text("Familia").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Background("#19376D").Padding(4).AlignCenter().Text("Factor").Bold().FontColor(Colors.White).FontSize(8);

                        // Row 1
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Conductores").FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(4).AlignCenter().Text(snapshot.MarkupConductores.ToString("F2") + "x").FontSize(7.5f).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Canalización").FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(4).AlignCenter().Text(snapshot.MarkupCanalizacion.ToString("F2") + "x").FontSize(7.5f).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Tableros").FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(4).AlignCenter().Text(snapshot.MarkupTableros.ToString("F2") + "x").FontSize(7.5f).Bold();

                        // Row 2
                        table.Cell().Padding(4).Text("Protecciones").FontSize(7.5f);
                        table.Cell().Padding(4).AlignCenter().Text(snapshot.MarkupProtecciones.ToString("F2") + "x").FontSize(7.5f).Bold();
                        table.Cell().Padding(4).Text("Iluminación").FontSize(7.5f);
                        table.Cell().Padding(4).AlignCenter().Text(snapshot.MarkupIluminacion.ToString("F2") + "x").FontSize(7.5f).Bold();
                        table.Cell().Padding(4).Text("Equipos").FontSize(7.5f);
                        table.Cell().Padding(4).AlignCenter().Text(snapshot.MarkupEquipos.ToString("F2") + "x").FontSize(7.5f).Bold();

                        // Row 3
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Enchufes y T.").FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(4).AlignCenter().Text(snapshot.MarkupEnchufes.ToString("F2") + "x").FontSize(7.5f).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Puesta a Tierra").FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(4).AlignCenter().Text(snapshot.MarkupPuestaTierra.ToString("F2") + "x").FontSize(7.5f).Bold();
                        table.Cell().Background("#F9FAFB").Padding(4).Text("Accesorios").FontSize(7.5f);
                        table.Cell().Background("#F9FAFB").Padding(4).AlignCenter().Text(snapshot.MarkupAccesorios.ToString("F2") + "x").FontSize(7.5f).Bold();
                    });

                    // 5. Alcance & Exclusiones
                    column.Item().PaddingTop(12).Text("5. ALCANCE Y EXCLUSIONES").FontSize(11).Bold().FontColor("#0B2447");
                    column.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ALCANCE DEL PROYECTO").Bold().FontSize(8.5f).FontColor("#19376D");
                            c.Item().PaddingTop(2).Background("#F9FAFB").Padding(6).Text(string.IsNullOrWhiteSpace(snapshot.ScopeText) ? "No especificado." : snapshot.ScopeText).FontSize(8);
                        });
                        row.ConstantItem(15);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("EXCLUSIONES DE LA OFERTA").Bold().FontSize(8.5f).FontColor("#19376D");
                            c.Item().PaddingTop(2).Background("#F9FAFB").Padding(6).Text(string.IsNullOrWhiteSpace(snapshot.ExclusionsText) ? "Ninguna declarada." : snapshot.ExclusionsText).FontSize(8);
                        });
                    });

                    // Metadatos al pie
                    column.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    column.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Creado: " + creationDate).FontSize(7.5f).FontColor(Colors.Grey.Medium);
                        r.RelativeItem().AlignRight().Text("Modificado: " + modDate).FontSize(7.5f).FontColor(Colors.Grey.Medium);
                    });
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Qubi Cost Electrical - Ficha Técnica Oficial").FontSize(8).FontColor(Colors.Grey.Medium);
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
