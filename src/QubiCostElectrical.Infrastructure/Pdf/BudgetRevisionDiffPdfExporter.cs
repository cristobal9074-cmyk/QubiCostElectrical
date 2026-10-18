using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QubiCostElectrical.Infrastructure;

public sealed class BudgetRevisionDiffPdfExporter : IBudgetRevisionDiffPdfExporter
{
    private const string CNavy = "#0B2442";
    private const string CBlue = "#1565C0";
    private const string CBlueSoft = "#EFF6FF";
    private const string CGreen = "#059669";
    private const string CGreenBg = "#ECFDF5";
    private const string CAmber = "#D97706";
    private const string CAmberBg = "#FFFBEB";
    private const string CRed = "#DC2626";
    private const string CRedBg = "#FEF2F2";
    private const string CText = "#1E293B";
    private const string CMuted = "#64748B";
    private const string CLine = "#CBD5E1";
    private const string CRowAlt = "#F8FAFC";
    private const string CWhite = "#FFFFFF";

    public void Export(BudgetVersionComparisonSummary summary, string outputPath)
    {
        if (summary == null) throw new ArgumentNullException(nameof(summary));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Ruta de exportación requerida.", nameof(outputPath));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        QuestPdfBootstrap.EnsureCommunityLicense();

        var esCL = CultureInfo.GetCultureInfo("es-CL");
        string UF(decimal v) => v.ToString("#,##0.000", esCL);
        string CLP(decimal v) => "$ " + v.ToString("#,##0", esCL);
        string Qty(decimal v) => v.ToString("#,##0.##", esCL);
        bool uf = summary.MuestraUf;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f).FontColor(CText));

                // ── CABECERA / HEADER ──
                page.Header().Column(header =>
                {
                    header.Item().Background(CNavy).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("INFORME COMPARATIVO DE REVISIONES Y RECLAMO DE ADICIONALES")
                                .FontSize(13).Bold().FontColor(CWhite);
                            c.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("Variación Contractual y Económica de Obras Eléctricas · ").FontColor("#94A3B8");
                                t.Span($"{summary.BaselineLabel}  vs  {summary.CurrentLabel}").Bold().FontColor(CWhite);
                            });
                        });

                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Fecha: {summary.FechaComparacion:dd/MM/yyyy}").FontColor("#CBD5E1").FontSize(8);
                            if (uf)
                                c.Item().Text($"Valor UF Ref: {CLP(summary.ValorUf)}").Bold().FontColor(CWhite).FontSize(8.5f);
                            c.Item().Text("QubiCost Electrical").FontColor("#94A3B8").FontSize(7.5f);
                        });
                    });

                    // Tarjeta de metadatos de obra y justificación
                    header.Item().BorderBottom(1).BorderColor(CLine).Background("#F1F5F9").Padding(8).Row(row =>
                    {
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("PROYECTO: ").Bold().FontColor(CNavy); t.Span(summary.ProjectTitle); });
                            c.Item().Text(t => { t.Span("MANDANTE / CONSTRUCTORA: ").Bold().FontColor(CNavy); t.Span(summary.ClientName); });
                        });
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("CONTRATISTA ELÉCTRICO: ").Bold().FontColor(CNavy); t.Span(summary.ContractorName); });
                            c.Item().Text(t => { t.Span("PROPÓSITO: ").Bold().FontColor(CNavy); t.Span("Reclamo formal y aprobación de adicionales por modificación de planos."); });
                        });
                    });

                    // Tarjetas Semáforo Financiero
                    header.Item().PaddingTop(6).PaddingBottom(6).Row(kpis =>
                    {
                        // 1. Base
                        kpis.RelativeItem().Border(1).BorderColor(CLine).Background(CWhite).Padding(6).Column(c =>
                        {
                            c.Item().Text("PRESUPUESTO BASE").FontSize(7).Bold().FontColor(CMuted);
                            if (uf)
                            {
                                c.Item().Text($"{UF(summary.BaselineUf)} UF").FontSize(10).Bold().FontColor(CText);
                                c.Item().Text(CLP(summary.BaselineClp)).FontSize(7.5f).FontColor(CMuted);
                            }
                            else
                            {
                                c.Item().Text(CLP(summary.BaselineClp)).FontSize(11).Bold().FontColor(CText);
                            }
                        });

                        kpis.ConstantItem(6);

                        // 2. Modificado
                        kpis.RelativeItem().Border(1).BorderColor(CLine).Background(CWhite).Padding(6).Column(c =>
                        {
                            c.Item().Text("PRESUPUESTO MODIFICADO").FontSize(7).Bold().FontColor(CMuted);
                            if (uf)
                            {
                                c.Item().Text($"{UF(summary.CurrentUf)} UF").FontSize(10).Bold().FontColor(CText);
                                c.Item().Text(CLP(summary.CurrentClp)).FontSize(7.5f).FontColor(CMuted);
                            }
                            else
                            {
                                c.Item().Text(CLP(summary.CurrentClp)).FontSize(11).Bold().FontColor(CText);
                            }
                        });

                        kpis.ConstantItem(6);

                        // 3. Adicionales (+)
                        kpis.RelativeItem().Border(1).BorderColor(CGreen).Background(CGreenBg).Padding(6).Column(c =>
                        {
                            c.Item().Text($"🟢 TOTAL ADICIONALES (+) · {summary.AddedCount} nuevas").FontSize(7).Bold().FontColor(CGreen);
                            if (uf)
                            {
                                c.Item().Text($"+{UF(summary.TotalAdicionalesUf)} UF").FontSize(10).Bold().FontColor(CGreen);
                                c.Item().Text($"+{CLP(summary.TotalAdicionalesClp)}").FontSize(7.5f).Bold().FontColor(CGreen);
                            }
                            else
                            {
                                c.Item().Text($"+{CLP(summary.TotalAdicionalesClp)}").FontSize(11).Bold().FontColor(CGreen);
                            }
                        });

                        kpis.ConstantItem(6);

                        // 4. Deductivos (-)
                        kpis.RelativeItem().Border(1).BorderColor(CRed).Background(CRedBg).Padding(6).Column(c =>
                        {
                            c.Item().Text($"🔴 TOTAL DEDUCTIVOS (-) · {summary.RemovedCount} eliminadas").FontSize(7).Bold().FontColor(CRed);
                            if (uf)
                            {
                                c.Item().Text($"{UF(summary.TotalDeductivosUf)} UF").FontSize(10).Bold().FontColor(CRed);
                                c.Item().Text(CLP(summary.TotalDeductivosClp)).FontSize(7.5f).Bold().FontColor(CRed);
                            }
                            else
                            {
                                c.Item().Text(CLP(summary.TotalDeductivosClp)).FontSize(11).Bold().FontColor(CRed);
                            }
                        });

                        kpis.ConstantItem(6);

                        // 5. Variación Neta (Cobro Adicional)
                        string netBg = summary.NetVariationUf >= 0 ? "#1E293B" : "#F8FAFC";
                        string netFg = summary.NetVariationUf >= 0 ? CWhite : CText;
                        string netAccent = summary.NetVariationUf >= 0 ? "#FCD34D" : CRed;

                        kpis.RelativeItem(1.2f).Border(1).BorderColor(CNavy).Background(netBg).Padding(6).Column(c =>
                        {
                            c.Item().Text("⚡ SALDO NETO RECLAMO ADICIONAL").FontSize(7).Bold().FontColor(netAccent);
                            if (uf)
                            {
                                c.Item().Text($"{(summary.NetVariationUf > 0 ? "+" : "")}{UF(summary.NetVariationUf)} UF").FontSize(11).Bold().FontColor(netFg);
                                c.Item().Text($"{(summary.NetVariationClp > 0 ? "+" : "")}{CLP(summary.NetVariationClp)} ({summary.DeltaPct:+0.00;-0.00}%)").FontSize(8).Bold().FontColor(netAccent);
                            }
                            else
                            {
                                c.Item().Text($"{(summary.NetVariationClp > 0 ? "+" : "")}{CLP(summary.NetVariationClp)} ({summary.DeltaPct:+0.00;-0.00}%)").FontSize(11).Bold().FontColor(netFg);
                            }
                        });
                    });
                });

                // ── CONTENIDO PRINCIPAL: TABLA DE VARIACIONES ──
                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(42);  // Ítem
                            cols.RelativeColumn(3);   // Descripción
                            cols.ConstantColumn(28);  // UD
                            cols.ConstantColumn(46);  // Base Q
                            cols.ConstantColumn(46);  // Actual Q
                            cols.ConstantColumn(46);  // Delta Q
                            cols.ConstantColumn(60);  // P.U. Base
                            cols.ConstantColumn(60);  // P.U. Actual
                            cols.ConstantColumn(62);  // Total Base
                            cols.ConstantColumn(62);  // Total Actual
                            cols.ConstantColumn(uf ? 64 : 72);  // Delta moneda
                            if (uf)
                                cols.ConstantColumn(68);  // Delta CLP
                            cols.ConstantColumn(80);  // Semáforo
                        });

                        // Encabezado de tabla
                        table.Header(h =>
                        {
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("ÍTEM").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).Text("DESCRIPCIÓN DE LA PARTIDA").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("UD").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("Q BASE").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("Q ACT.").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text("Δ Q").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text(uf ? "P.U. BASE (UF)" : "P.U. BASE ($)").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text(uf ? "P.U. ACT. (UF)" : "P.U. ACT. ($)").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text(uf ? "BASE UF" : "BASE $").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text(uf ? "ACT. UF" : "ACT. $").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignRight().Text(uf ? "Δ UF" : "Δ TOTAL $").Bold().FontColor(CWhite).FontSize(7.5f);
                            if (uf)
                                h.Cell().Background(CNavy).Padding(4).AlignRight().Text("Δ CLP").Bold().FontColor(CWhite).FontSize(7.5f);
                            h.Cell().Background(CNavy).Padding(4).AlignCenter().Text("ESTADO").Bold().FontColor(CWhite).FontSize(7.5f);
                        });

                        int rowIndex = 0;
                        foreach (var r in summary.Rows)
                        {
                            rowIndex++;
                            string rowBg = (rowIndex % 2 == 0) ? CRowAlt : CWhite;

                            string badgeBg = CWhite;
                            string badgeFg = CMuted;
                            string deltaFg = CText;

                            if (r.DiffKind == RevisionDiffKind.Adicional)
                            {
                                badgeBg = CGreenBg;
                                badgeFg = CGreen;
                                deltaFg = CGreen;
                            }
                            else if (r.DiffKind == RevisionDiffKind.Deductivo)
                            {
                                badgeBg = CRedBg;
                                badgeFg = CRed;
                                deltaFg = CRed;
                            }
                            else if (r.DiffKind == RevisionDiffKind.Modificado)
                            {
                                badgeBg = CAmberBg;
                                badgeFg = CAmber;
                                deltaFg = r.DeltaUf > 0 ? CGreen : (r.DeltaUf < 0 ? CRed : CText);
                            }

                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignCenter().Text(r.Item).FontSize(7.5f);
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).Column(c =>
                            {
                                c.Item().Text(r.Descripcion).Bold().FontSize(8f);
                                if (!string.IsNullOrWhiteSpace(r.DetalleCambio) && r.DiffKind != RevisionDiffKind.SinCambio)
                                {
                                    c.Item().Text(r.DetalleCambio).FontSize(7f).FontColor(badgeFg);
                                }
                            });
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignCenter().Text(r.Unidad).FontSize(7.5f);
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(Qty(r.CantidadBase)).FontSize(7.5f);
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(Qty(r.CantidadActual)).FontSize(7.5f);
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(r.DeltaCantidad == 0m ? "—" : (r.DeltaCantidad > 0m ? $"+{Qty(r.DeltaCantidad)}" : Qty(r.DeltaCantidad))).FontSize(7.5f).Bold();
                            if (uf)
                            {
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(r.PuUfBase > 0m ? UF(r.PuUfBase) : "—").FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(r.PuUfActual > 0m ? UF(r.PuUfActual) : "—").FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(UF(r.BaselineUf)).FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(UF(r.CurrentUf)).FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(r.DeltaUf == 0m ? "—" : (r.DeltaUf > 0m ? $"+{UF(r.DeltaUf)}" : UF(r.DeltaUf))).FontSize(8f).Bold().FontColor(deltaFg);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(r.DeltaClp == 0m ? "—" : (r.DeltaClp > 0m ? $"+{CLP(r.DeltaClp)}" : CLP(r.DeltaClp))).FontSize(7.5f).FontColor(deltaFg);
                            }
                            else
                            {
                                decimal puBaseClp = r.PuUfBase * summary.ValorUf;
                                decimal puActClp = r.PuUfActual * summary.ValorUf;
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(puBaseClp > 0m ? CLP(puBaseClp) : "—").FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(puActClp > 0m ? CLP(puActClp) : "—").FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(CLP(r.BaselineClp)).FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(CLP(r.CurrentClp)).FontSize(7.5f);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(3).AlignRight().Text(r.DeltaClp == 0m ? "—" : (r.DeltaClp > 0m ? $"+{CLP(r.DeltaClp)}" : CLP(r.DeltaClp))).FontSize(8f).Bold().FontColor(deltaFg);
                            }
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(CLine).Padding(2).AlignCenter().Background(badgeBg).Border(0.5f).BorderColor(badgeFg).PaddingVertical(2).PaddingHorizontal(4).Text(r.DisplayBadge).FontSize(6.5f).Bold().FontColor(badgeFg);
                        }

                        // Fila de Totalización
                        table.Cell().ColumnSpan(8).Background("#E2E8F0").Padding(4).AlignRight().Text("TOTALES COMPARATIVOS:").Bold().FontSize(8.5f);
                        table.Cell().Background("#E2E8F0").Padding(4).AlignRight().Text(uf ? UF(summary.BaselineUf) : CLP(summary.BaselineClp)).Bold().FontSize(8.5f);
                        table.Cell().Background("#E2E8F0").Padding(4).AlignRight().Text(uf ? UF(summary.CurrentUf) : CLP(summary.CurrentClp)).Bold().FontSize(8.5f);
                        table.Cell().Background("#E2E8F0").Padding(4).AlignRight().Text($"{(summary.DeltaUf > 0 ? "+" : "")}{(uf ? UF(summary.DeltaUf) : CLP(summary.DeltaClp))}").Bold().FontSize(9f).FontColor(summary.DeltaUf >= 0 ? CGreen : CRed);
                        if (uf)
                            table.Cell().Background("#E2E8F0").Padding(4).AlignRight().Text($"{(summary.DeltaClp > 0 ? "+" : "")}{CLP(summary.DeltaClp)}").Bold().FontSize(8.5f).FontColor(summary.DeltaClp >= 0 ? CGreen : CRed);
                        table.Cell().Background("#E2E8F0").Padding(4).AlignCenter().Text($"{summary.Rows.Count} partidas").FontSize(7.5f).FontColor(CMuted);
                    });
                });

                // ── PIE DE PÁGINA: BLOQUE FORMAL DE FIRMAS ──
                page.Footer().Column(f =>
                {
                    f.Item().PaddingTop(12).BorderTop(1).BorderColor(CLine).Row(signatures =>
                    {
                        // Firma 1: Contratista
                        signatures.RelativeItem().PaddingHorizontal(16).Column(col =>
                        {
                            col.Item().Height(30);
                            col.Item().BorderTop(1).BorderColor("#94A3B8").PaddingTop(4).AlignCenter().Text(summary.ContractorName).Bold().FontSize(8);
                            col.Item().AlignCenter().Text("Jefe de Proyecto Eléctrico / Registro SEC").FontSize(7).FontColor(CMuted);
                            col.Item().AlignCenter().Text("EMISIÓN Y JUSTIFICACIÓN DE ADICIONALES").FontSize(6.5f).FontColor(CBlue).Bold();
                        });

                        // Firma 2: ITO
                        signatures.RelativeItem().PaddingHorizontal(16).Column(col =>
                        {
                            col.Item().Height(30);
                            col.Item().BorderTop(1).BorderColor("#94A3B8").PaddingTop(4).AlignCenter().Text("Inspección Técnica de Obra (ITO)").Bold().FontSize(8);
                            col.Item().AlignCenter().Text("Conforme Cubicación y Replanteo en Terreno").FontSize(7).FontColor(CMuted);
                            col.Item().AlignCenter().Text("VALIDACIÓN TÉCNICA").FontSize(6.5f).FontColor(CNavy).Bold();
                        });

                        // Firma 3: Constructora
                        signatures.RelativeItem().PaddingHorizontal(16).Column(col =>
                        {
                            col.Item().Height(30);
                            col.Item().BorderTop(1).BorderColor("#94A3B8").PaddingTop(4).AlignCenter().Text(summary.ClientName).Bold().FontSize(8);
                            col.Item().AlignCenter().Text("Administrador de Obra / Gerencia Técnica").FontSize(7).FontColor(CMuted);
                            col.Item().AlignCenter().Text("APROBACIÓN ORDEN DE CAMBIO").FontSize(6.5f).FontColor(CGreen).Bold();
                        });
                    });

                    f.Item().PaddingTop(6).Row(bot =>
                    {
                        bot.RelativeItem().Text("Documento formal de auditoría y respaldo contractual de presupuesto · QubiCost Electrical").FontSize(7).FontColor(CMuted);
                        bot.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Página ");
                            t.CurrentPageNumber();
                            t.Span(" de ");
                            t.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf(outputPath);
    }
}
