using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QubiCostElectrical;

/// <summary>
/// Exporta el estado de pago del mes a PDF con QuestPDF: encabezado sobrio, KPIs,
/// tabla de partidas por capítulo, bloque de totales (anticipo/retención/IVA) y
/// cronograma anual de 12 meses.
/// </summary>
public sealed class EstadoPagoPdfExporter : IEstadoPagoPdfExporter
{
    private const string CNavy = "#0B2442";
    private const string CText = "#1E293B";
    private const string CMuted = "#64748B";
    private const string CLine = "#CBD5E1";
    private const string CWhite = "#FFFFFF";
    private const string CBand = "#F1F5F9";
    private const string CRowAlt = "#F8FAFC";
    private const string CHighlight = "#E2E8F0";

    public void Export(EstadoPagoExportRequest request, string outputPath)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Ruta de exportación requerida.", nameof(outputPath));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        QuestPdfBootstrap.EnsureCommunityLicense();

        var esCL = CultureInfo.GetCultureInfo("es-CL");
        string UF(decimal v) => v.ToString("#,##0.000", esCL);
        string Pct(decimal v) => v.ToString("0.0", esCL) + " %";

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(16);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f).FontColor(CText));

                var mes = request.Mes;
                int partidas = 0;
                foreach (var sec in mes.Secciones)
                    partidas += sec.Filas.Count;

                page.Header().Column(header =>
                {
                    header.Item().Background(CNavy).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ESTADO DE PAGO").FontSize(13).Bold().FontColor(CWhite);
                            c.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span($"{mes.MesNombre} {mes.Anio} · ").FontColor("#94A3B8");
                                t.Span(mes.Folio).Bold().FontColor(CWhite);
                            });
                        });

                        row.ConstantItem(210).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Proyecto: {request.ProjectTitle}").FontColor("#CBD5E1").FontSize(8);
                            c.Item().Text($"Emitido: {mes.FechaEmision:dd/MM/yyyy}").FontColor("#CBD5E1").FontSize(8);
                            c.Item().Text("QubiCost Electrical").FontColor("#94A3B8").FontSize(7.5f);
                        });
                    });

                    header.Item().Background(CBand).BorderBottom(1).BorderColor(CLine).Padding(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("ESTADO: ").Bold().FontColor(CNavy);
                            t.Span(mes.EstadoLabel);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("APROBADO POR: ").Bold().FontColor(CNavy);
                            t.Span(string.IsNullOrWhiteSpace(mes.AprobadoPor) ? "—" : mes.AprobadoPor);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("PARTIDAS: ").Bold().FontColor(CNavy);
                            t.Span(partidas.ToString(esCL));
                        });
                    });
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    var t = mes.Totales;
                    col.Item().Row(kpis =>
                    {
                        WriteKpi(kpis, "TRABAJO DEL MES", UF(t.TrabajoMes) + " UF", CText);
                        kpis.ConstantItem(6);
                        WriteKpi(kpis, "ACUMULADO", $"{UF(t.Acum)} UF · {Pct(t.PctAcum)}", CText);
                        kpis.ConstantItem(6);
                        WriteKpi(kpis, "TOTAL A PAGAR", UF(t.Total) + " UF", CNavy, destacado: true);
                    });

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(48);
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(76);
                            cols.ConstantColumn(58);
                            cols.ConstantColumn(56);
                            cols.ConstantColumn(76);
                            cols.ConstantColumn(56);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("CÓDIGO").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).Text("PARTIDA (ITEMIZADO)").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("MONTO CONTRATADO UF").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("AV. ANTERIOR %").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("AV. MES %").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("MONTO DEL MES UF").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("ACUMULADO %").Bold().FontColor(CWhite).FontSize(7.5f);
                        });

                        int fila = 0;
                        foreach (var sec in mes.Secciones)
                        {
                            if (!string.IsNullOrWhiteSpace(sec.Capitulo))
                                table.Cell().ColumnSpan(7).Background(CBand).Padding(3).Text(sec.Capitulo).Bold().FontSize(7.5f).FontColor(CNavy);

                            foreach (var f in sec.Filas)
                            {
                                fila++;
                                string bg = fila % 2 == 0 ? CRowAlt : CWhite;
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignCenter().Text(f.Item).FontSize(7.5f);
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).Text(f.Descripcion).FontSize(7.5f);
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(UF(f.Monto)).FontSize(7.5f);
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(Pct(f.AvanceAnterior)).FontSize(7.5f);
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(Pct(f.AvanceMes)).FontSize(7.5f);
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(UF(f.MontoMes)).FontSize(7.5f).Bold();
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(Pct(f.AcumPct)).FontSize(7.5f);
                            }
                        }

                        if (fila == 0)
                            table.Cell().ColumnSpan(7).Padding(6).AlignCenter().Text("Sin partidas en el Itemizado.").FontColor(CMuted);
                    });

                    var cfg = request.Config;
                    col.Item().PaddingTop(8).Width(300).AlignRight().Border(1).BorderColor(CLine).Padding(6).Column(totals =>
                    {
                        totals.Item().Text("TOTALES DEL MES").Bold().FontSize(8).FontColor(CNavy);
                        WriteTotalRow(totals, "Trabajo del mes", UF(t.TrabajoMes));
                        WriteTotalRow(totals, "Acumulado a la fecha", UF(t.Acum));
                        WriteTotalRow(totals, $"Recupero de anticipo ({cfg.AnticipoPct:0.##} %)", UF(t.RecuperoMes));
                        WriteTotalRow(totals, $"Retención garantía ({cfg.RetencionPct:0.##} %)", UF(t.RetencionMes));
                        WriteTotalRow(totals, "Neto antes de IVA", UF(t.Neto));
                        WriteTotalRow(totals, $"IVA ({cfg.IvaPct:0.##} %)", UF(t.Iva));
                        totals.Item().PaddingTop(4).Background(CHighlight).Padding(4).Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL A PAGAR").Bold().FontSize(9);
                            r.ConstantItem(110).AlignRight().Text(UF(t.Total) + " UF").Bold().FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(12).Text("CRONOGRAMA ANUAL DE ESTADOS DE PAGO").Bold().FontSize(9).FontColor(CNavy);
                    col.Item().PaddingTop(4).Table(crono =>
                    {
                        crono.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.ConstantColumn(100);
                        });

                        crono.Header(h =>
                        {
                            h.Cell().Background(CNavy).Padding(4).Text("MES").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).Text("FOLIO").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).Text("ESTADO").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("TOTAL UF").Bold().FontColor(CWhite).FontSize(7.5f);
                        });

                        int cm = 0;
                        foreach (var m in request.Cronograma)
                        {
                            cm++;
                            string bg = cm % 2 == 0 ? CRowAlt : CWhite;
                            bool pendiente = m.Total is null;
                            crono.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).Text(m.MesNombre).FontSize(7.5f);
                            crono.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).Text(pendiente ? "—" : m.Folio).FontSize(7.5f);
                            crono.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).Text(m.EstadoLabel).FontSize(7.5f);
                            crono.Cell().Background(bg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight()
                                .Text(pendiente ? "—" : UF(m.Total!.Value)).FontSize(7.5f);
                        }
                    });
                });

                page.Footer().Row(foot =>
                {
                    foot.RelativeItem().Text("Estado de pago · QubiCost Electrical").FontSize(7).FontColor(CMuted);
                    foot.RelativeItem().AlignRight().Text(txt =>
                    {
                        txt.Span("Página ");
                        txt.CurrentPageNumber();
                        txt.Span(" de ");
                        txt.TotalPages();
                    });
                });
            });
        }).GeneratePdf(outputPath);
    }

    private static void WriteKpi(
        RowDescriptor kpis,
        string label,
        string valor,
        string color,
        bool destacado = false)
    {
        kpis.RelativeItem().Border(1).BorderColor(destacado ? CNavy : CLine)
            .Background(destacado ? CNavy : CWhite).Padding(6).Column(c =>
            {
                c.Item().Text(label).FontSize(7).Bold().FontColor(destacado ? "#94A3B8" : CMuted);
                c.Item().Text(valor).FontSize(destacado ? 11 : 10).Bold().FontColor(destacado ? CWhite : color);
            });
    }

    private static void WriteTotalRow(ColumnDescriptor totals, string label, string valor)
    {
        totals.Item().PaddingTop(3).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8);
            r.ConstantItem(110).AlignRight().Text(valor).FontSize(8).Bold();
        });
    }
}
