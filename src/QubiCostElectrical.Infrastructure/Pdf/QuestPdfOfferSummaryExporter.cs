using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Genera el Resumen Ejecutivo de Presupuesto para el Mandante en PDF,
/// con la misma estructura y formalidad que la hoja resumen histórica de Ingelsam.
/// </summary>
public sealed class QuestPdfOfferSummaryExporter : IOfferSummaryPdfExporter
{
    private readonly IBudgetCalculationService budgetCalculation;
    private readonly ICurrencyConversionService currencyConversion;

    public QuestPdfOfferSummaryExporter()
        : this(new BudgetCalculationService(), new CurrencyConversionService())
    {
    }

    public QuestPdfOfferSummaryExporter(
        IBudgetCalculationService budgetCalculation,
        ICurrencyConversionService currencyConversion)
    {
        this.budgetCalculation = budgetCalculation ?? throw new ArgumentNullException(nameof(budgetCalculation));
        this.currencyConversion = currencyConversion ?? throw new ArgumentNullException(nameof(currencyConversion));
    }

    public void Export(ExportProjectContext ctx, string path)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta de exportación vacía.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        QuestPdfBootstrap.EnsureCommunityLicense();

        var offer = ctx.OfferSettings;
        var theme = ctx.CorporateTheme;
        var project = ctx.Project;
        var culture = CultureInfo.GetCultureInfo("es-CL");

        bool hasTheme = theme?.Enabled == true;
        string cNavy = hasTheme && !string.IsNullOrWhiteSpace(theme!.PrimaryHex) ? theme.PrimaryHex.Trim() : "#0B2442";
        string cHead = "#EBF2FB";
        string cText = "#1A2332";
        string cMuted = "#5A6472";
        string cLine = "#CBD5E1";

        string? logoPath = !string.IsNullOrWhiteSpace(offer.LogoPath) && File.Exists(offer.LogoPath) ? offer.LogoPath
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.LogoPath) && File.Exists(theme.LogoPath) ? theme.LogoPath : null);
        byte[]? logoBytes = logoPath != null ? File.ReadAllBytes(logoPath) : null;

        string companyName = !string.IsNullOrWhiteSpace(offer.CompanyName) ? offer.CompanyName
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.CompanyName) ? theme.CompanyName : "EMPRESA CONTRATISTA");
        string companyRut = !string.IsNullOrWhiteSpace(offer.CompanyRut) ? offer.CompanyRut.Trim() : "";
        string companyAddress = !string.IsNullOrWhiteSpace(offer.CompanyAddress) ? offer.CompanyAddress.Trim() : "";
        string companyPhone = !string.IsNullOrWhiteSpace(offer.CompanyPhone) ? offer.CompanyPhone.Trim() : "";
        string companyEmail = !string.IsNullOrWhiteSpace(offer.CompanyEmail) ? offer.CompanyEmail.Trim() : "";

        var date = offer.OfferDate == default ? DateTime.Today : offer.OfferDate;
        string folio = !string.IsNullOrWhiteSpace(offer.OfferNumber) ? offer.OfferNumber.Trim() : $"OFT-{date:yyyyMMdd}-01";

        decimal valorUf = project.ValorUfClp > 0m ? project.ValorUfClp : PriceConversion.DefaultValorUfClp;
        bool muestraUf = ctx.MuestraUf;
        string currencyLabel = muestraUf ? "UF" : "$";

        var cost = ctx.BudgetSettings != null
            ? budgetCalculation.CalculateBudget(ctx.Cubicacion, ctx.BudgetSettings, ctx.Apus, ctx.Itemizado, ctx.OrdenesCambio)
            : new CostBreakdown();

        bool ocultarGgUt = offer.OcultarGgYUtilidadEnResumen;
        decimal targetAmount = ocultarGgUt
            ? (ctx.OfferNetAmountUf > 0m ? ctx.OfferNetAmountUf : cost.TotalVentaNetoUf)
            : (cost.CostoDirectoUf > 0m ? cost.CostoDirectoUf : cost.TotalVentaNetoUf);

        var offerRows = OfferClienteRowBuilder.Build(ctx.Itemizado, cost, targetAmount, ctx.ProjectTitle);

        decimal subtotalMonto = 0m;
        foreach (var r in offerRows.Where(x => !x.EsTitulo))
        {
            subtotalMonto += muestraUf ? PriceConversion.ClpToUf(r.TotalUf, valorUf) : r.TotalUf;
        }

        decimal ggMonto = muestraUf
            ? (cost.GastosGeneralesUf > 0 ? PriceConversion.ClpToUf(cost.GastosGeneralesUf, valorUf) : 0m)
            : cost.GastosGeneralesUf;

        decimal utMonto = muestraUf
            ? (cost.UtilidadUf > 0 ? PriceConversion.ClpToUf(cost.UtilidadUf, valorUf) : 0m)
            : cost.UtilidadUf;

        decimal netoMonto = ocultarGgUt ? subtotalMonto : (subtotalMonto + ggMonto + utMonto);
        decimal ivaMonto = netoMonto * 0.19m;
        decimal totalFinalMonto = netoMonto + ivaMonto;

        string FmtMoney(decimal val) => muestraUf ? $"{val:N3} UF" : $"$ {Money.Round(val):N0}";
        string FmtQty(decimal qty) => qty % 1 == 0 ? qty.ToString("N0", culture) : qty.ToString("#,##0.##", culture);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(14, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f).FontColor(cText));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        // Izquierda: Logo y datos de empresa
                        r.RelativeItem(3).Column(c =>
                        {
                            if (logoBytes != null)
                                c.Item().Height(40).Width(150).Image(logoBytes).FitArea();
                            else
                                c.Item().Text(companyName).Bold().FontSize(13).FontColor(cNavy);

                            c.Item().PaddingTop(2).Text(companyName).Bold().FontSize(8.5f);
                            c.Item().Text(companyAddress).FontSize(8).FontColor(cMuted);
                            c.Item().Text($"FONO: {companyPhone}  |  {companyEmail}").FontSize(8).FontColor(cMuted);
                        });

                        // Derecha: Cuadro DATOS CLIENTES
                        r.RelativeItem(2).Border(0.8f).BorderColor(cLine).Column(c =>
                        {
                            c.Item().Background("#E2E8F0").PaddingVertical(2).AlignCenter().Text("DATOS CLIENTES").Bold().FontSize(8.5f).FontColor(cNavy);
                            c.Item().PaddingHorizontal(6).PaddingVertical(3).Column(det =>
                            {
                                det.Item().Text(t => { t.Span("Mandante: ").Bold().FontSize(8); t.Span(ctx.ClientName).FontSize(8); });
                                det.Item().Text(t => { t.Span("Nombre: ").Bold().FontSize(8); t.Span(!string.IsNullOrWhiteSpace(offer.RecipientName) ? offer.RecipientName : ctx.ClientName).FontSize(8); });
                                det.Item().Text(t => { t.Span("Carta de Oferta: ").Bold().FontSize(8); t.Span(folio).Bold().FontSize(8).FontColor(cNavy); });
                                det.Item().Text(t => { t.Span("Fecha: ").Bold().FontSize(8); t.Span(date.ToString("dd/MM/yyyy")).FontSize(8); });
                            });
                        });
                    });

                    col.Item().PaddingTop(8).Height(2).Background(cNavy);
                    col.Item().PaddingTop(4);
                });

                page.Content().PaddingTop(4).Column(col =>
                {
                    // Tabla de partidas (Itemizado)
                    col.Item().Border(0.6f).BorderColor(cLine).Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(40);  // ITEM
                            c.RelativeColumn(5);   // DESCRIPCIÓN
                            c.ConstantColumn(36);  // UNIDAD
                            c.ConstantColumn(46);  // CANTIDAD
                            c.ConstantColumn(64);  // P.U.
                            c.ConstantColumn(72);  // TOTAL
                            c.RelativeColumn(2);   // COMENTARIOS
                        });

                        // Encabezados
                        tbl.Header(h =>
                        {
                            void HCell(IContainer cell, string title, bool right = false)
                            {
                                var c = cell.Background(cNavy).PaddingVertical(3).PaddingHorizontal(4);
                                if (right) c.AlignRight().Text(title).Bold().FontSize(8).FontColor(Colors.White);
                                else if (title == "ITEM" || title == "UNIDAD") c.AlignCenter().Text(title).Bold().FontSize(8).FontColor(Colors.White);
                                else c.AlignLeft().Text(title).Bold().FontSize(8).FontColor(Colors.White);
                            }

                            HCell(h.Cell(), "ITEM");
                            HCell(h.Cell(), "DESCRIPCIÓN");
                            HCell(h.Cell(), "UNIDAD");
                            HCell(h.Cell(), "CANT.", true);
                            HCell(h.Cell(), $"UNIT {currencyLabel}", true);
                            HCell(h.Cell(), $"TOTAL {currencyLabel}", true);
                            HCell(h.Cell(), "COMENTARIOS");
                        });

                        foreach (var it in offerRows)
                        {
                            if (it.EsTitulo)
                            {
                                tbl.Cell().Background(cHead).BorderBottom(0.4f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).AlignCenter().Text(it.Item).Bold().FontSize(8).FontColor(cNavy);
                                tbl.Cell().Background(cHead).BorderBottom(0.4f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).Text(it.Descripcion).Bold().FontSize(8).FontColor(cNavy);
                                tbl.Cell().Background(cHead).BorderBottom(0.4f).BorderColor(cLine).Text("");
                                tbl.Cell().Background(cHead).BorderBottom(0.4f).BorderColor(cLine).Text("");
                                tbl.Cell().Background(cHead).BorderBottom(0.4f).BorderColor(cLine).Text("");
                                tbl.Cell().Background(cHead).BorderBottom(0.4f).BorderColor(cLine).Text("");
                                tbl.Cell().Background(cHead).BorderBottom(0.4f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).Text("Subtotal Capítulo").Italic().FontSize(7.5f).FontColor(cMuted);
                            }
                            else
                            {
                                decimal unitPrice = muestraUf
                                    ? (it.Cantidad > 0m ? PriceConversion.ClpToUf(it.TotalUf, valorUf) / it.Cantidad : 0m)
                                    : (it.Cantidad > 0m ? it.TotalUf / it.Cantidad : 0m);

                                decimal lineTotal = muestraUf ? PriceConversion.ClpToUf(it.TotalUf, valorUf) : it.TotalUf;

                                tbl.Cell().BorderBottom(0.3f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).AlignCenter().Text(it.Item).FontSize(7.5f);
                                tbl.Cell().BorderBottom(0.3f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).Text(it.Descripcion).FontSize(7.5f);
                                tbl.Cell().BorderBottom(0.3f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).AlignCenter().Text(it.Unidad).FontSize(7.5f);
                                tbl.Cell().BorderBottom(0.3f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).AlignRight().Text(FmtQty(it.Cantidad)).FontSize(7.5f);
                                tbl.Cell().BorderBottom(0.3f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).AlignRight().Text(muestraUf ? unitPrice.ToString("N3", culture) : Money.Round(unitPrice).ToString("N0", culture)).FontSize(7.5f);
                                tbl.Cell().BorderBottom(0.3f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).AlignRight().Text(muestraUf ? lineTotal.ToString("N3", culture) : Money.Round(lineTotal).ToString("N0", culture)).FontSize(7.5f);
                                tbl.Cell().BorderBottom(0.3f).BorderColor(cLine).PaddingVertical(2).PaddingHorizontal(4).Text(it.Observaciones).FontSize(7.5f);
                            }
                        }
                    });

                    // Bloque de Totales
                    col.Item().PaddingTop(6).AlignRight().Width(280).Border(0.6f).BorderColor(cLine).Table(tot =>
                    {
                        tot.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        void TotRow(string label, decimal amount, bool bold = false, string? bg = null, string? textColor = null, float fontSize = 8f)
                        {
                            IContainer c1 = tot.Cell();
                            if (bg != null) c1 = c1.Background(bg);
                            var p1 = c1.PaddingVertical(2.5f).PaddingHorizontal(6);

                            IContainer c2 = tot.Cell();
                            if (bg != null) c2 = c2.Background(bg);
                            var p2 = c2.PaddingVertical(2.5f).PaddingHorizontal(6).AlignRight();

                            var t1 = p1.Text(label).FontSize(fontSize);
                            var t2 = p2.Text(FmtMoney(amount)).FontSize(fontSize);
                            if (bold) { t1.Bold(); t2.Bold(); }
                            if (textColor != null) { t1.FontColor(textColor); t2.FontColor(textColor); }
                        }

                        if (!ocultarGgUt)
                        {
                            TotRow("Subtotal", subtotalMonto, bold: true);
                            if (ggMonto > 0m) TotRow("Gastos Generales", ggMonto);
                            if (utMonto > 0m) TotRow("Utilidad", utMonto);
                        }
                        TotRow($"TOTAL NETO {currencyLabel}", netoMonto, bold: true, bg: cHead, textColor: cNavy, fontSize: 8.5f);
                        TotRow("IVA (19%)", ivaMonto);
                        TotRow($"TOTAL {currencyLabel}", totalFinalMonto, bold: true, bg: cNavy, textColor: Colors.White, fontSize: 9.5f);
                    });

                    // Bloque de Firmas
                    col.Item().PaddingTop(24).Row(r =>
                    {
                        string signName = !string.IsNullOrWhiteSpace(offer.SignatureName) ? offer.SignatureName
                            : (!string.IsNullOrWhiteSpace(project.UsuarioNombre) ? project.UsuarioNombre : "RESPONSABLE TÉCNICO");
                        string signRole = !string.IsNullOrWhiteSpace(offer.SignatureRole) ? offer.SignatureRole : "JEFE DE PROYECTOS";

                        // Firma Emisor
                        r.RelativeItem().PaddingRight(20).Column(s =>
                        {
                            s.Item().BorderBottom(0.8f).BorderColor(Colors.Black).Height(25);
                            s.Item().PaddingTop(3).AlignCenter().Text(signName).Bold().FontSize(9);
                            s.Item().AlignCenter().Text(signRole).FontSize(8).FontColor(cMuted);
                            s.Item().AlignCenter().Text(companyName).FontSize(8).FontColor(cMuted);
                        });

                        // Firma Mandante
                        r.RelativeItem().PaddingLeft(20).Column(s =>
                        {
                            s.Item().BorderBottom(0.8f).BorderColor(Colors.Black).Height(25);
                            s.Item().PaddingTop(3).AlignCenter().Text("ACEPTO PRESUPUESTO").Bold().FontSize(9);
                            s.Item().AlignCenter().Text("Nombre, Apellidos y Firma del Mandante").FontSize(8).FontColor(cMuted);
                            s.Item().AlignCenter().Text("RUT y Fecha:").FontSize(8).FontColor(cMuted);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ").FontSize(7.5f).FontColor(cMuted);
                    x.CurrentPageNumber().FontSize(7.5f).FontColor(cMuted);
                    x.Span(" de ").FontSize(7.5f).FontColor(cMuted);
                    x.TotalPages().FontSize(7.5f).FontColor(cMuted);
                });
            });
        }).GeneratePdf(path);
    }
}
