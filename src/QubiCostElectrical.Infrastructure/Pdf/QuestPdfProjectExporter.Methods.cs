using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QubiCostElectrical;

public sealed partial class QuestPdfProjectExporter : IPdfProjectExporter
{
    public void Export(ExportProjectContext ctx, string path)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta vacía.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        QuestPdfBootstrap.EnsureCommunityLicense();

        var settings = ExcelCostosBridge.EnrichFromProjectData(
            BudgetSettings.FromProject(ctx.Project),
            ctx.Data);
        var cost = budgetCalculation.CalculateBudget(
            ctx.Cubicacion, settings, ctx.Apus, ctx.Itemizado, ctx.OrdenesCambio);

        var project = ctx.Project;
        decimal valorUf = project.ValorUfClp > 0m ? project.ValorUfClp : PriceConversion.DefaultValorUfClp;
        decimal netClp = ctx.OfferNetAmountUf > 0m ? ctx.OfferNetAmountUf
                      : cost.TotalVentaNetoUf > 0m ? cost.TotalVentaNetoUf
                      : cost.TotalVentaUf;
        decimal netUf = PriceConversion.ClpToUf(netClp, valorUf);

        var offer = ctx.OfferSettings;
        var template = ctx.ReportTemplate;
        var date = offer.OfferDate == default ? DateTime.Today : offer.OfferDate;
        var valid = date.AddDays(Math.Max(1, offer.ValidityDays));
        var fechaMaxima = ctx.Materiales.Where(m => m.FechaPrecio != default).Select(m => m.FechaPrecio).DefaultIfEmpty(date).Max();
        string folio = BuildFolio(ctx, date);
        string hash = BuildHash(ctx, cost, netUf, folio);

        var cultureES = CultureInfo.GetCultureInfo("es-CL");
        var monedaDoc = ctx.Currency;
        bool muestraUf = ctx.MuestraUf;
        // Encabezado de la columna principal: "UF"/"USD" en proyectos con UF, "$" en CLP.
        string HdrMoneda = monedaDoc.EncabezadoPrincipal;
        string HdrEquivalente = monedaDoc.EncabezadoEquivalenteClp;
        string UF(decimal v) => v.ToString("#,##0.000", cultureES);
        string N(decimal v) => v.ToString("N0", cultureES);
        // Importe principal desde un monto en pesos (moneda base del motor).
        string MoneyFromClp(decimal clp) => monedaDoc.DesdeClp(clp);
        // Equivalente en pesos; cadena vacía cuando el proyecto está en CLP.
        string EqClp(decimal clp) => monedaDoc.EquivalenteClp(clp);
        string S(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
        string DateLong(DateTime d) => d.ToString("d 'de' MMMM 'de' yyyy", cultureES);

        var offerRows = BuildOfferRows(ctx, cost, netClp);
        var areas = budgetCalculation.GetAreaTotals(ctx.Cubicacion).ToList();

        var theme = ctx.CorporateTheme;
        bool themeEnabled = theme?.Enabled == true;
        string CNavy = themeEnabled && !string.IsNullOrWhiteSpace(theme!.PrimaryHex)
            ? theme.PrimaryHex.Trim()
            : FallbackNavy;
        string CBlue = themeEnabled && !string.IsNullOrWhiteSpace(theme!.AccentHex)
            ? theme.AccentHex.Trim()
            : FallbackBlue;
        string CAccent = CBlue;
        string CHead = themeEnabled && !string.IsNullOrWhiteSpace(theme!.BackgroundHex)
            ? theme.BackgroundHex.Trim()
            : FallbackHead;

        string companyName = !string.IsNullOrWhiteSpace(offer.CompanyName)
            ? offer.CompanyName
            : (themeEnabled && !string.IsNullOrWhiteSpace(theme!.CompanyName)
                ? theme.CompanyName
                : "Qubi Cost Electrical");

        byte[]? logoBytes = null;
        string? logoPath = !string.IsNullOrWhiteSpace(offer.LogoPath)
            ? offer.LogoPath
            : (themeEnabled && !string.IsNullOrWhiteSpace(theme!.LogoPath) ? theme.LogoPath : null);

        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            try { logoBytes = File.ReadAllBytes(logoPath); }
            catch (Exception ex)
            {
                // El archivo existe pero no se pudo leer — bloqueado, corrupto, permisos, etc.
                AppLog.Warning("Pdf", $"No se pudo leer el logo '{logoPath}' para la oferta", ex);
                logoBytes = null;
            }
        }

        byte[]? clientLogoBytes = null;
        string? clientLogoPath = offer.ClientLogoPath;
        if (!string.IsNullOrWhiteSpace(clientLogoPath) && File.Exists(clientLogoPath))
        {
            try { clientLogoBytes = File.ReadAllBytes(clientLogoPath); }
            catch (Exception ex)
            {
                AppLog.Warning("Pdf", $"No se pudo leer el logo del cliente '{clientLogoPath}' para la oferta", ex);
                clientLogoBytes = null;
            }
        }

        byte[]? signatureBytes = null;
        if (!string.IsNullOrWhiteSpace(offer.SignatureImagePath) && File.Exists(offer.SignatureImagePath))
        {
            try { signatureBytes = File.ReadAllBytes(offer.SignatureImagePath); }
            catch (Exception ex)
            {
                AppLog.Warning("Pdf", $"No se pudo leer la firma '{offer.SignatureImagePath}' para la oferta", ex);
                signatureBytes = null;
            }
        }
        // Table cell helpers
        // These helpers decorate an IContainer and add text.

        static void TH(IContainer c, string text, bool right = false)
        {
            var container = right ? c.AlignRight() : c;
            container.Text(t => t.Span(text).FontSize(8).Bold().FontColor(CWhite));
        }

        static void TD(IContainer c, string text, bool right = false, bool bold = false)
        {
            var container = right ? c.AlignRight() : c;
            container.Text(t =>
            {
                var span = t.Span(text).FontSize(8).FontColor(CText);
                if (bold) span.Bold();
            });
        }

        static void TDNavy(IContainer c, string text, bool right = false)
        {
            var container = right ? c.AlignRight() : c;
            container.Text(t => t.Span(text).FontSize(8).Bold().FontColor(CWhite));
        }
        // Page structure helpers

        void SetupPage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.MarginTop(0);
            page.MarginBottom(44);
            page.MarginHorizontal(40);
            page.DefaultTextStyle(s => s.FontSize(9).FontColor(CText));

            if (ctx.IsDraft && template.ShowDraftWatermark)
            {
                page.Foreground().AlignCenter().AlignMiddle().Text(t =>
                    t.Span("BORRADOR").FontSize(72).Bold().FontColor("#1A0C3B5E"));
            }

            page.Header().Height(38).Background(CNavy).Row(row =>
            {
                row.ConstantItem(160).PaddingLeft(40).AlignMiddle().Element(e =>
                {
                    if (logoBytes != null)
                        e.Height(22).Image(logoBytes).FitHeight();
                    else
                        e.Text(t => t.Span(companyName).FontSize(8).FontColor(CWhite));
                });
                row.RelativeItem().PaddingRight(40).AlignMiddle().AlignRight()
                    .Text(t => t.Span($"{ctx.ProjectTitle}  ·  {ctx.ClientName}")
                         .FontSize(7.5f).FontColor("#8AADCC"));
            });

            page.Footer().Height(24).Row(row =>
            {
                row.RelativeItem().AlignMiddle()
                    .Text(t => t.Span($"Folio: {folio}").FontSize(7).FontColor(CMuted));
                row.RelativeItem().AlignMiddle().AlignCenter()
                    .Text(t => t.Span($"Precios vigentes al {fechaMaxima:dd-MM-yyyy}. Vigencia: {offer.ValidityDays} días.").FontSize(6).FontColor(CMuted).Italic());
                row.RelativeItem().AlignMiddle().AlignRight().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(7).FontColor(CMuted);
                    t.Span(" / ").FontSize(7).FontColor(CMuted);
                    t.TotalPages().FontSize(7).FontColor(CMuted);
                });
            });
        }

        void PageTitle(ColumnDescriptor col, string title)
        {
            col.Item().Text(t =>
                t.Span(title.ToUpperInvariant()).FontSize(14).Bold().FontColor(CNavy));
            col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(CNavy);
        }
        // Document generation

        Document.Create(composer =>
        {
            // Cover page
            if (template.ShowCover)
            {
                var coverTitle = string.IsNullOrWhiteSpace(template.CoverTitle)
                    ? offer.OfferTitle
                    : template.CoverTitle.Trim();
                var coverSubtitle = string.IsNullOrWhiteSpace(template.CoverSubtitle)
                    ? template.TemplateName
                    : template.CoverSubtitle.Trim();
                var coverLayout = ReportTemplateCoverLayout.Resolve(template.OrderedFields);
                composer.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    RenderCover(page.Content(), ctx, offer, template, logoBytes, clientLogoBytes, coverTitle, coverSubtitle, netUf, netClp, folio, hash, date, valid, cultureES, signatureBytes, coverLayout, CNavy, CBlue);
                });
            }

            if (offer.IncludePresentationLetter && !string.IsNullOrWhiteSpace(offer.PresentationLetterText))
            {
                var letterTitle = string.IsNullOrWhiteSpace(offer.PresentationLetterTitle)
                    ? "CARTA DE PRESENTACIÓN DE LA OFERTA"
                    : offer.PresentationLetterTitle.Trim();
                var letterText = offer.PresentationLetterText!;

                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, letterTitle);

                        // Membrete formal de la carta
                        col.Item().PaddingTop(12).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t => t.Span($"Fecha: {DateLong(date)}").FontSize(8.5f).FontColor(CMuted));
                                c.Item().PaddingTop(6).Text(t => t.Span("SEÑORES:").FontSize(8).Bold().FontColor(CNavy));
                                c.Item().Text(t => t.Span(ctx.ClientName).FontSize(10.5f).Bold().FontColor(CText));
                                if (!string.IsNullOrWhiteSpace(offer.RecipientCompany) && !string.Equals(offer.RecipientCompany, ctx.ClientName, StringComparison.OrdinalIgnoreCase))
                                {
                                    c.Item().Text(t => t.Span(offer.RecipientCompany).FontSize(8.5f).FontColor(CMuted));
                                }
                                if (!string.IsNullOrWhiteSpace(offer.RecipientName))
                                {
                                    c.Item().Text(t => t.Span($"Atención: {offer.RecipientName}").FontSize(8.5f).FontColor(CText).Italic());
                                }
                                c.Item().PaddingTop(2).Text(t => t.Span($"Proyecto: {ctx.ProjectTitle}").FontSize(8.5f).FontColor(CMuted));
                            });

                            r.ConstantItem(180).AlignRight().Column(c =>
                            {
                                if (!string.IsNullOrWhiteSpace(offer.OfferNumber))
                                    c.Item().Text(t => t.Span($"Oferta N°: {offer.OfferNumber} · {offer.RevisionTag}").FontSize(9).Bold().FontColor(CNavy));
                                if (!string.IsNullOrWhiteSpace(offer.Reference))
                                    c.Item().PaddingTop(2).Text(t => t.Span($"Ref.: {offer.Reference}").FontSize(8).FontColor(CMuted));
                                c.Item().PaddingTop(2).Text(t => t.Span($"Validez: {offer.ValidityDays} días corridos").FontSize(8).FontColor(CMuted));
                            });
                        });

                        col.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(CLine);

                        // Cuerpo de la carta
                        col.Item().PaddingTop(12).Text(t =>
                            t.Span(letterText).FontSize(9.5f).FontColor(CText).LineHeight(1.6f));

                        // Tarjeta ejecutiva de síntesis económica
                        col.Item().PaddingTop(14).Background(CHead).Border(1).BorderColor(CLine).Padding(10).Column(card =>
                        {
                            card.Item().Text(t => t.Span("SÍNTESIS DE LA PROPUESTA ECONÓMICA").FontSize(8.5f).Bold().FontColor(CNavy));
                            card.Item().PaddingTop(6).Row(cr =>
                            {
                                cr.RelativeItem().Column(cc =>
                                {
                                    cc.Item().Text(t => t.Span("Monto Neto Ofertado:").FontSize(7.5f).FontColor(CMuted));
                                    cc.Item().Text(t => t.Span(muestraUf
                                        ? $"UF {netUf.ToString("#,##0.000", cultureES)}  ·  $ {netClp:N0} CLP"
                                        : $"$ {netClp:N0}").FontSize(9.5f).Bold().FontColor(CNavy));
                                    if (offer.IncludeIvaNote)
                                        cc.Item().Text(t => t.Span("Valores netos · agregar 19% IVA").FontSize(7).Italic().FontColor(CMuted));
                                });
                                cr.RelativeItem().Column(cc =>
                                {
                                    cc.Item().Text(t => t.Span("Plazo de Ejecución:").FontSize(7.5f).FontColor(CMuted));
                                    cc.Item().Text(t => t.Span(SafeText(offer.ProjectDuration)).FontSize(9).Bold().FontColor(CText));
                                    cc.Item().PaddingTop(2).Text(t => t.Span("Modalidad de Contrato:").FontSize(7.5f).FontColor(CMuted));
                                    cc.Item().Text(t => t.Span(cost.ClosingPolicy.ToString()).FontSize(8).FontColor(CText));
                                });
                            });
                        });

                        // Cierre formal y bloque de firma
                        col.Item().PaddingTop(14).Text(t =>
                            t.Span("Sin otro particular y a su entera disposición para cualquier aclaración técnica o comercial, saluda atentamente a ustedes,")
                             .FontSize(9).FontColor(CText).Italic());

                        col.Item().PaddingTop(16).Row(fr =>
                        {
                            fr.RelativeItem(3).Column(sc =>
                            {
                                if (signatureBytes != null)
                                {
                                    sc.Item().Height(OfferSignatureSizing.HeightPt(offer)).AlignLeft().Image(signatureBytes).FitHeight();
                                }
                                sc.Item().BorderBottom(1f).BorderColor(CLine).Height(1);
                                sc.Item().PaddingTop(6).Text(t =>
                                    t.Span(S(offer.SignatureName)).FontSize(9.5f).Bold().FontColor(CNavy));
                                if (!string.IsNullOrWhiteSpace(offer.SignatureRole))
                                    sc.Item().Text(t => t.Span(offer.SignatureRole).FontSize(8).FontColor(CMuted));
                                if (!string.IsNullOrWhiteSpace(offer.SignatureRegistroSec) || !string.IsNullOrWhiteSpace(offer.SignatureSecClass))
                                {
                                    string secText = $"Instalador Autorizado SEC {S(offer.SignatureSecClass)}";
                                    if (!string.IsNullOrWhiteSpace(offer.SignatureRegistroSec))
                                        secText += $" · Reg. N° {offer.SignatureRegistroSec}";
                                    sc.Item().Text(t => t.Span(secText).FontSize(8).Bold().FontColor(CBlue));
                                }
                                sc.Item().Text(t => t.Span(S(companyName)).FontSize(8).FontColor(CMuted));
                                var contact = string.Join("  ·  ",
                                    new[] { offer.SignaturePhone, offer.SignatureEmail }
                                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                                if (!string.IsNullOrWhiteSpace(contact))
                                    sc.Item().Text(t => t.Span(contact).FontSize(7.5f).FontColor(CMuted));
                            });
                            fr.RelativeItem(2); // espacio derecho
                        });
                    });
                });
            }
            // Executive summary
            if (template.ShowExecutiveSummary)
            {
                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, "Resumen Ejecutivo");
                        col.Item().PaddingTop(6).Text(t =>
                            t.Span(template.TemplateName).FontSize(8).FontColor(CMuted).Italic());

                        col.Item().PaddingTop(16).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                SummaryField(c, "Proyecto", ctx.ProjectTitle);
                                SummaryField(c, "Cliente", ctx.ClientName);
                                SummaryField(c, "Referencia", S(offer.Reference));
                                SummaryField(c, "N° Oferta", $"{S(offer.OfferNumber)} · {S(offer.RevisionTag)}");
                            });
                            row.ConstantItem(24);
                            row.RelativeItem().Column(c =>
                            {
                                SummaryField(c, "Fecha", DateLong(date));
                                if (template.ShowPriceValidity)
                                    SummaryField(c, "Validez", $"{offer.ValidityDays} días · hasta {valid:dd/MM/yyyy}");
                                SummaryField(c, "Plazo ejec.", S(offer.ProjectDuration));
                                SummaryField(c, "Estado", ctx.BudgetStatus);
                            });
                        });

                        col.Item().PaddingTop(24).Text(t =>
                            t.Span("Desglose económico").FontSize(11).Bold().FontColor(CNavy));

                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                if (muestraUf)
                                    c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Concepto");
                                TH(h.Cell().Background(CNavy).Padding(5), HdrMoneda, right: true);
                                if (muestraUf)
                                    TH(h.Cell().Background(CNavy).Padding(5), HdrEquivalente, right: true);
                            });

                            void CostRow(string label, decimal clp, bool alt, bool bold = false, bool highlight = false)
                            {
                                string bg = highlight ? CLight : (alt ? CRowAlt : CWhite);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), label, bold: bold);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), MoneyFromClp(clp), right: true, bold: bold);
                                if (muestraUf)
                                    TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), EqClp(clp), right: true, bold: bold);
                            }

                            var pol = cost.ClosingPolicy;
                            CostRow("Materiales", cost.MaterialesUf, false);
                            CostRow("Mano de obra", cost.ManoObraUf, true);
                            CostRow("Equipos", cost.EquiposUf, false);
                            CostRow("Subcontratos", cost.SubcontratosUf, true);
                            CostRow("Herramientas/mermas", cost.HerramientasUf + cost.MermasUf, false);
                            CostRow("Costo directo de ejecución", cost.CostoDirectoUf, true, bold: true);

                            switch (pol)
                            {
                                case ClosingPolicyType.CostPlusFee:
                                    decimal feeClp = cost.ClosingPolicyResult?.MargenClp ?? (netClp - cost.CostoDirectoUf);
                                    CostRow("Honorario de administración (Fee)", feeClp, false, bold: true);
                                    CostRow("TOTAL REEMBOLSABLE + FEE (NETO)", netClp, true, bold: true, highlight: true);
                                    break;

                                case ClosingPolicyType.LumpSumEpc:
                                    CostRow("Gastos generales y utilidad", cost.GastosGeneralesUf + cost.UtilidadUf, false);
                                    decimal contingenciaEpc = netClp - (cost.CostoProyectoUf > 0m ? cost.CostoProyectoUf : cost.CostoDirectoUf);
                                    if (contingenciaEpc > 0m)
                                        CostRow("Contingencia e imprevistos Suma Alzada", contingenciaEpc, true);
                                    CostRow("PRECIO CERRADO SUMA ALZADA (NETO)", netClp, false, bold: true, highlight: true);
                                    break;

                                case ClosingPolicyType.TargetPrice:
                                    decimal margenTarget = netClp - cost.CostoDirectoUf;
                                    CostRow("Margen comercial disponible", margenTarget, false);
                                    CostRow("PRECIO OBJETIVO OFERTADO (NETO)", netClp, true, bold: true, highlight: true);
                                    break;

                                case ClosingPolicyType.UnitPricePure:
                                    CostRow("Margen embebido en precios unitarios", cost.GastosGeneralesUf + cost.UtilidadUf, false);
                                    CostRow("TOTAL SERIE PRECIOS UNITARIOS (NETO)", netClp, true, bold: true, highlight: true);
                                    break;

                                case ClosingPolicyType.GainsharePainshare:
                                    CostRow("Gastos generales y utilidad base", cost.GastosGeneralesUf + cost.UtilidadUf, false);
                                    CostRow("TOTAL OFERTA BASE (TARGET COST CONTRACT)", netClp, true, bold: true, highlight: true);
                                    break;

                                default:
                                    CostRow("Gastos generales", cost.GastosGeneralesUf, false);
                                    CostRow("Utilidad", cost.UtilidadUf, true);
                                    CostRow("TOTAL NETO OFERTA", netClp, false, bold: true, highlight: true);
                                    break;
                            }
                        });

                        decimal clpM2 = cost.UfM2;
                        decimal ufM2 = PriceConversion.ClpToUf(clpM2, valorUf);
                        if (clpM2 > 0m)
                            col.Item().PaddingTop(6).Text(t =>
                                t.Span(muestraUf
                                    ? $"UF/m² referencial: {ufM2.ToString("#,##0.000", cultureES)}  ·  $ {clpM2:N0} CLP/m²"
                                    : $"$ {clpM2:N0} CLP/m²")
                                 .FontSize(8).FontColor(CMuted).Italic());

                        string explicacionNota = cost.ClosingPolicy switch
                        {
                            ClosingPolicyType.CostPlusFee => "Modalidad Costo + Fee: los costos directos se reembolsan 1:1 previa comprobación de facturas y planillas. El honorario retribuye la administración y dirección técnica.",
                            ClosingPolicyType.LumpSumEpc => "Modalidad Suma Alzada / EPC: el precio ofertado es cerrado e inamovible, e incluye toda contingencia e imprevisto dentro de las especificaciones técnicas licitadas.",
                            ClosingPolicyType.UnitPricePure => "Modalidad Precios Unitarios: el pago se liquidará mensualmente según las cantidades efectivamente ejecutadas multiplicadas por los P.U. contractuales.",
                            ClosingPolicyType.GainsharePainshare => "Modalidad Costo Objetivo Compartido: se pacta un Costo Objetivo con el mandante. Las desviaciones en la liquidación final se compartirán según el porcentaje acordado.",
                            _ => "Este resumen corresponde a la valorización comercial del alcance indicado. El detalle de cubicación y APU queda como respaldo interno."
                        };

                        col.Item().PaddingTop(16)
                            .Background(CHead).BorderLeft(3).BorderColor(CNavy).Padding(12)
                            .Text(t => t.Span(explicacionNota)
                                 .FontSize(8).FontColor(CMuted).LineHeight(1.4f));

                        if (offer.ShowCostComponentDistribution)
                        {
                            col.Item().PaddingTop(14).Column(costDistCol =>
                            {
                                costDistCol.Item().Row(hdr =>
                                {
                                    hdr.RelativeItem().Text(t => t.Span("DISTRIBUCIÓN ESTRUCTURAL DEL COSTO").FontSize(9.5f).Bold().FontColor(CNavy));
                                    hdr.ConstantItem(180).AlignRight().Text(t => t.Span("Incidencia en Venta Neta").FontSize(8).FontColor(CMuted));
                                });

                                decimal totalVenta = netClp > 0m ? netClp : (cost.TotalVentaNetoUf > 0m ? cost.TotalVentaNetoUf : 1m);
                                decimal matClp = cost.MaterialesUf;
                                decimal moClp = cost.ManoObraUf;
                                decimal eqClp = cost.EquiposUf + cost.HerramientasUf + cost.MermasUf;
                                decimal scClp = cost.SubcontratosUf;
                                decimal indClp = Math.Max(0m, totalVenta - (matClp + moClp + eqClp + scClp));

                                var components = new (string Label, decimal Clp, string HexColor)[]
                                {
                                    ("Materiales e Insumos", matClp, "#1E40AF"),
                                    ("Mano de Obra Especializada", moClp, "#D97706"),
                                    ("Equipos y Herramientas", eqClp, "#7C3AED"),
                                    ("Subcontratos y Especialidades", scClp, "#059669"),
                                    ("Gastos Generales, Riesgo y Margen", indClp, "#2563EB")
                                };

                                costDistCol.Item().PaddingTop(6).Height(9).Row(segBar =>
                                {
                                    foreach (var cmp in components)
                                    {
                                        if (cmp.Clp <= 0m) continue;
                                        float weight = (float)Math.Clamp((cmp.Clp / totalVenta) * 100m, 1m, 100m);
                                        segBar.RelativeItem(weight).Background(cmp.HexColor);
                                    }
                                });

                                costDistCol.Item().PaddingTop(8).Column(detCol =>
                                {
                                    foreach (var cmp in components)
                                    {
                                        decimal cmpPct = Math.Round((cmp.Clp / totalVenta) * 100m, 1);
                                        decimal cmpUf = PriceConversion.ClpToUf(cmp.Clp, valorUf);

                                        detCol.Item().PaddingBottom(4).Row(r =>
                                        {
                                            r.ConstantItem(10).PaddingTop(2).Height(6).Width(6).Background(cmp.HexColor);
                                            r.RelativeItem().PaddingLeft(6).Text(t =>
                                            {
                                                t.Span(cmp.Label).FontSize(8).Bold().FontColor(CText);
                                                t.Span(muestraUf
                                                    ? $"  ·  UF {cmpUf:N3}  ·  $ {cmp.Clp:N0}"
                                                    : $"  ·  $ {cmp.Clp:N0}").FontSize(7.5f).FontColor(CMuted);
                                            });
                                            r.ConstantItem(60).AlignRight().Text(t =>
                                                t.Span($"{cmpPct:N1}%").FontSize(8).Bold().FontColor(CNavy));
                                        });
                                    }
                                });
                            });
                        }
                    });
                });
            }

            // Chapter distribution chart & breakdown
            var chapters = new List<(string Name, decimal Clp, decimal Uf, decimal Pct, int Count)>();
            string curChapName = "1. INSTALACIONES GENERALES";
            decimal curChapClp = 0m;
            int curChapItems = 0;

            foreach (var r in offerRows)
            {
                if (r.EsTitulo)
                {
                    if (curChapItems > 0 || curChapClp > 0m)
                    {
                        decimal cUf = PriceConversion.ClpToUf(curChapClp, valorUf);
                        decimal pct = netClp > 0m ? (curChapClp / netClp) * 100m : 0m;
                        chapters.Add((curChapName, curChapClp, cUf, pct, curChapItems));
                    }
                    curChapName = r.Descripcion;
                    curChapClp = 0m;
                    curChapItems = 0;
                }
                else
                {
                    curChapClp += r.TotalUf;
                    curChapItems++;
                }
            }
            if (curChapItems > 0 || curChapClp > 0m)
            {
                decimal cUf = PriceConversion.ClpToUf(curChapClp, valorUf);
                decimal pct = netClp > 0m ? (curChapClp / netClp) * 100m : 0m;
                chapters.Add((curChapName, curChapClp, cUf, pct, curChapItems));
            }

            if (template.ShowChapterDistributionChart && chapters.Count > 0)
            {
                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, "Distribución y Desglose por Capítulos");
                        col.Item().PaddingTop(4).Text(t =>
                            t.Span("Composición presupuestaria e incidencia porcentual por especialidad técnica.")
                             .FontSize(8.5f).FontColor(CMuted).Italic());

                        // Gráfico de barras horizontales limpias
                        col.Item().PaddingTop(16).Column(barsCol =>
                        {
                            foreach (var ch in chapters)
                            {
                                barsCol.Item().PaddingBottom(10).Column(bc =>
                                {
                                    bc.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text(t =>
                                        {
                                            t.Span(ch.Name).FontSize(9).Bold().FontColor(CNavy);
                                            t.Span($"  ({ch.Count} {(ch.Count == 1 ? "partida" : "partidas")})").FontSize(7.5f).FontColor(CMuted);
                                        });
                                        r.ConstantItem(220).AlignRight().Text(t =>
                                        {
                                            t.Span(muestraUf ? $"UF {ch.Uf:N3}" : $"$ {ch.Clp:N0}").FontSize(8.5f).Bold().FontColor(CNavy);
                                            if (muestraUf)
                                                t.Span($"  ·  $ {ch.Clp:N0}  ·  ").FontSize(8).FontColor(CMuted);
                                            else
                                                t.Span("  ·  ").FontSize(8).FontColor(CMuted);
                                            t.Span($"{ch.Pct:N1}%").FontSize(8.5f).Bold().FontColor(CBlue);
                                        });
                                    });

                                    // Barra proporcional limpia
                                    bc.Item().PaddingTop(3).Height(7).Background("#E2E8F0").Row(br =>
                                    {
                                        float filled = (float)Math.Clamp(ch.Pct, 0.5m, 100m);
                                        br.RelativeItem(filled).Background(CNavy);
                                        if (filled < 99.5f)
                                        {
                                            br.RelativeItem(100f - filled).Background("#E2E8F0");
                                        }
                                    });
                                });
                            }
                        });

                        // Tabla resumen de capítulos
                        col.Item().PaddingTop(12).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);   // Capítulo
                                c.ConstantColumn(42);  // Partidas
                                if (muestraUf)
                                    c.ConstantColumn(70);  // UF
                                c.ConstantColumn(85);  // CLP
                                c.ConstantColumn(55);  // %
                            });
                            tbl.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Capítulo / Especialidad");
                                TH(h.Cell().Background(CNavy).Padding(5), "Partidas", right: true);
                                if (muestraUf)
                                    TH(h.Cell().Background(CNavy).Padding(5), "Subtotal UF", right: true);
                                TH(h.Cell().Background(CNavy).Padding(5), muestraUf ? HdrEquivalente : HdrMoneda, right: true);
                                TH(h.Cell().Background(CNavy).Padding(5), "Incidencia", right: true);
                            });

                            bool alt = false;
                            foreach (var ch in chapters)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                TD(tbl.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), ch.Name);
                                TD(tbl.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), ch.Count.ToString(), right: true);
                                if (muestraUf)
                                    TD(tbl.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), UF(ch.Uf), right: true);
                                TD(tbl.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), MoneyFromClp(ch.Clp), right: true);
                                TD(tbl.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), $"{ch.Pct:N1}%", right: true, bold: true);
                                alt = !alt;
                            }

                            // Total
                            tbl.Cell().ColumnSpan(2).Background(CNavy).PaddingVertical(5).PaddingHorizontal(5)
                                .Text(t => t.Span("TOTAL GENERAL PRESUPUESTO").FontSize(8).Bold().FontColor(CWhite));
                            if (muestraUf)
                                TDNavy(tbl.Cell().Background(CNavy).PaddingHorizontal(5).PaddingVertical(5), $"UF {UF(netUf)}", right: true);
                            TDNavy(tbl.Cell().Background(CNavy).PaddingHorizontal(5).PaddingVertical(5), MoneyFromClp(netClp), right: true);
                            TDNavy(tbl.Cell().Background(CNavy).PaddingHorizontal(5).PaddingVertical(5), "100.0%", right: true);
                        });

                        // Nota de especialidad técnica
                        var maxChap = chapters.OrderByDescending(c => c.Clp).FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(maxChap.Name))
                        {
                            col.Item().PaddingTop(14)
                                .Background(CHead).BorderLeft(3).BorderColor(CBlue).Padding(10)
                                .Text(t =>
                                {
                                    t.Span("Análisis de Incidencia: ").FontSize(8).Bold().FontColor(CNavy);
                                    t.Span($"El capítulo de mayor peso técnico y presupuestario corresponde a '{maxChap.Name}', representando el {maxChap.Pct:N1}% del total neto ofertado.")
                                     .FontSize(8).FontColor(CText);
                                });
                        }
                    });
                });
            }

            // Text pages: Scope, Exclusions, Commercial Terms & SEC Normative Assumptions
            if (template.ShowTermsAndConditions)
            {
                var textSections = new List<(string Title, string? Text, string Icon, bool IsNormative)>();

                if (!string.IsNullOrWhiteSpace(offer.ScopeText))
                    textSections.Add(("Alcance Incluido", offer.ScopeText, "ALCANCE", false));

                if (!string.IsNullOrWhiteSpace(offer.ExclusionsText))
                    textSections.Add(("Exclusiones de la Oferta", offer.ExclusionsText, "EXCLUSIONES", false));

                if (!string.IsNullOrWhiteSpace(offer.CommercialConditionsText))
                    textSections.Add(("Condiciones Comerciales y de Pago", offer.CommercialConditionsText, "COMERCIAL", false));

                if (template.ShowNormativeAssumptions && !string.IsNullOrWhiteSpace(offer.NormativeAssumptionsText))
                    textSections.Add(("Supuestos Normativos y Regulatorios SEC", offer.NormativeAssumptionsText, "NORMATIVA SEC", true));

                if (!string.IsNullOrWhiteSpace(offer.ObservationsText))
                    textSections.Add(("Observaciones y Notas Técnicas", offer.ObservationsText, "OBSERVACIONES", false));

                foreach (var sec in textSections)
                {
                    composer.Page(page =>
                    {
                        SetupPage(page);
                        page.Content().PaddingTop(16).Column(col =>
                        {
                            PageTitle(col, sec.Title);

                            if (sec.IsNormative)
                            {
                                // Encabezado de cumplimiento SEC
                                col.Item().PaddingTop(8)
                                    .Background(CHead).Border(1).BorderColor(CNavy).Padding(10).Column(nc =>
                                    {
                                        nc.Item().Row(nr =>
                                        {
                                            nr.RelativeItem().Column(nl =>
                                            {
                                                nl.Item().Text(t =>
                                                    t.Span("MARCO REGULATORIO ELÉCTRICO NACIONAL · SUPERINTENDENCIA DE ELECTRICIDAD Y COMBUSTIBLES (SEC)")
                                                     .FontSize(8).Bold().FontColor(CNavy));
                                                nl.Item().PaddingTop(2).Text(t =>
                                                    t.Span("Decreto Supremo N° 8/2019 · Pliegos Técnicos Normativos RIC (RIC N° 01 al RIC N° 19)")
                                                     .FontSize(7.5f).FontColor(CMuted));
                                            });
                                            if (!string.IsNullOrWhiteSpace(offer.SignatureSecClass))
                                            {
                                                nr.ConstantItem(120).AlignRight().Background(CNavy).Padding(4).AlignCenter()
                                                    .Text(t => t.Span($"SEC {offer.SignatureSecClass.ToUpperInvariant()}").FontSize(8).Bold().FontColor(CWhite));
                                            }
                                        });
                                    });
                            }
                            else if (sec.Title.Contains("Comerciales", StringComparison.OrdinalIgnoreCase))
                            {
                                // Resumen de parámetros comerciales
                                col.Item().PaddingTop(8)
                                    .Background(CHead).Border(1).BorderColor(CLine).Padding(8).Row(pr =>
                                    {
                                        if (muestraUf)
                                        {
                                            pr.RelativeItem().Column(pc =>
                                            {
                                                pc.Item().Text(t => t.Span("VALOR UF REF:").FontSize(7).Bold().FontColor(CMuted));
                                                pc.Item().Text(t => t.Span($"$ {valorUf:N0} CLP").FontSize(8.5f).Bold().FontColor(CNavy));
                                            });
                                        }
                                        pr.RelativeItem().Column(pc =>
                                        {
                                            pc.Item().Text(t => t.Span("VALIDEZ OFERTA:").FontSize(7).Bold().FontColor(CMuted));
                                            pc.Item().Text(t => t.Span($"{offer.ValidityDays} días corridos").FontSize(8.5f).Bold().FontColor(CText));
                                        });
                                        pr.RelativeItem().Column(pc =>
                                        {
                                            pc.Item().Text(t => t.Span("PLAZO EJECUCIÓN:").FontSize(7).Bold().FontColor(CMuted));
                                            pc.Item().Text(t => t.Span(SafeText(offer.ProjectDuration)).FontSize(8.5f).Bold().FontColor(CText));
                                        });
                                        pr.RelativeItem().Column(pc =>
                                        {
                                            pc.Item().Text(t => t.Span("MONEDA BASE:").FontSize(7).Bold().FontColor(CMuted));
                                            pc.Item().Text(t => t.Span(ctx.CurrencyLabel).FontSize(8.5f).Bold().FontColor(CText));
                                        });
                                    });
                            }

                            // Párrafos y viñetas
                            col.Item().PaddingTop(12).Column(tc =>
                            {
                                var lines = (sec.Text ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var rawLine in lines)
                                {
                                    string line = rawLine.Trim();
                                    if (string.IsNullOrWhiteSpace(line)) continue;

                                    tc.Item().PaddingBottom(6).Row(lr =>
                                    {
                                        lr.ConstantItem(14).Text(t => t.Span("•").FontSize(10).Bold().FontColor(CBlue));
                                        lr.RelativeItem().Text(t =>
                                            t.Span(line).FontSize(9.2f).FontColor(CText).LineHeight(1.5f));
                                    });
                                }
                            });
                        });
                    });
                }
            }
            // Itemized table
            if (template.ShowItemizedDetail)
            {
                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, "Tabla de Partidas");
                        bool showPu = offer.ShowUnitPricesInItemizado;
                        // En CLP no se emite la columna UF: queda una sola columna de moneda.
                        int totalCols = 4 + (showPu ? 1 : 0) + monedaDoc.ColumnasMoneda;
                        int totalSpan = totalCols - monedaDoc.ColumnasMoneda;

                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(36);    // Ítem
                                c.RelativeColumn(showPu ? 4 : 4.6f); // Descripción
                                c.ConstantColumn(26);    // UD
                                c.ConstantColumn(38);    // Cant.
                                if (showPu)
                                    c.ConstantColumn(52); // P.U. UF
                                c.ConstantColumn(58);    // Total UF
                                if (muestraUf)
                                    c.ConstantColumn(72);    // CLP
                            });
                            table.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Ítem");
                                TH(h.Cell().Background(CNavy).Padding(5), "Descripción");
                                TH(h.Cell().Background(CNavy).Padding(5), "UD");
                                TH(h.Cell().Background(CNavy).Padding(5), "Cant.", right: true);
                                if (showPu)
                                    TH(h.Cell().Background(CNavy).Padding(5), $"P.U. {HdrMoneda}", right: true);
                                TH(h.Cell().Background(CNavy).Padding(5), $"Total {HdrMoneda}", right: true);
                                if (muestraUf)
                                    TH(h.Cell().Background(CNavy).Padding(5), HdrEquivalente, right: true);
                            });

                            bool alt = false;
                            foreach (var r in offerRows)
                            {
                                if (r.EsTitulo)
                                {
                                    table.Cell().ColumnSpan((uint)totalCols)
                                        .Background(CHead).BorderLeft(3).BorderColor(CAccent)
                                        .PaddingLeft(8).PaddingRight(5).PaddingVertical(7)
                                        .Text(t => t.Span(r.Descripcion).FontSize(9).Bold().FontColor(CNavy));
                                    alt = false;
                                }
                                else
                                {
                                    string bg = alt ? CRowAlt : CWhite;
                                    TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), r.Item);
                                    var descCell = table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3);
                                    if (string.IsNullOrWhiteSpace(r.Observaciones))
                                    {
                                        descCell.Text(t => t.Span(r.Descripcion).FontSize(8).FontColor(CText));
                                    }
                                    else
                                    {
                                        descCell.Column(dc =>
                                        {
                                            dc.Item().Text(t => t.Span(r.Descripcion).FontSize(8).FontColor(CText));
                                            dc.Item().Text(t => t.Span("Obs: " + r.Observaciones).FontSize(7).Italic().FontColor(CMuted));
                                        });
                                    }
                                    TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), r.Unidad);
                                    TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), N(r.Cantidad), right: true);
                                    decimal rowClp = r.TotalUf;
                                    decimal rowUf = PriceConversion.ClpToUf(rowClp, valorUf);
                                    if (showPu)
                                    {
                                        decimal puUf = r.Cantidad > 0m ? rowUf / r.Cantidad : 0m;
                                        TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3),
                                            r.Cantidad > 0m ? (muestraUf ? UF(puUf) : $"$ {rowClp / r.Cantidad:N0}") : "", right: true);
                                    }
                                    TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3),
                                        rowClp > 0m ? (muestraUf ? UF(rowUf) : $"$ {rowClp:N0}") : "", right: true);
                                    if (muestraUf)
                                        TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3),
                                            rowClp > 0m ? EqClp(rowClp) : "", right: true);
                                    alt = !alt;
                                }
                            }

                            // Fila de total
                            table.Cell().ColumnSpan((uint)totalSpan).Background(CNavy).PaddingVertical(6).PaddingHorizontal(6)
                                .Text(t => t.Span("TOTAL NETO").FontSize(8).Bold().FontColor(CWhite));
                            TDNavy(table.Cell().Background(CNavy).PaddingHorizontal(5).PaddingVertical(6),
                                muestraUf ? $"UF {UF(netUf)}" : $"$ {netClp:N0}", right: true);
                            if (muestraUf)
                                TDNavy(table.Cell().Background(CNavy).PaddingHorizontal(5).PaddingVertical(6), EqClp(netClp), right: true);
                        });

                        if (offer.IncludeIvaNote)
                            col.Item().PaddingTop(6).Text(t =>
                                t.Span("Valores netos · agregar IVA cuando corresponda.")
                                 .FontSize(7.5f).FontColor(CMuted).Italic());
                    });
                });

            }
            // Annex A - areas summary
            if (areas.Count > 0)
            {
                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, "Anexo A · Resumen por Áreas");
                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                if (muestraUf)
                                    c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Área");
                                TH(h.Cell().Background(CNavy).Padding(5), HdrMoneda, right: true);
                                if (muestraUf)
                                    TH(h.Cell().Background(CNavy).Padding(5), HdrEquivalente, right: true);
                            });
                            bool alt = false;
                            foreach (var a in areas)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), a.Area);
                                decimal areaUf = PriceConversion.ClpToUf(a.Total, valorUf);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4),
                                    muestraUf ? UF(areaUf) : $"$ {a.Total:N0}", right: true);
                                if (muestraUf)
                                    TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), EqClp(a.Total), right: true);
                                alt = !alt;
                            }
                        });
                    });
                });
            }
            // Annex B - brands and specs
            if (offer.Productos.Count > 0)
            {
                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, "Anexo B · Marcas y Especificaciones");
                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Categoría");
                                TH(h.Cell().Background(CNavy).Padding(5), "Marca");
                                TH(h.Cell().Background(CNavy).Padding(5), "Especificación");
                            });
                            bool alt = false;
                            foreach (var p in offer.Productos)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), p.Categoria);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), p.Marca);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), p.Especificacion);
                                alt = !alt;
                            }
                        });
                    });
                });
            }
            // Annex C - drawing list
            if (offer.Planos.Count > 0)
            {
                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, "Anexo C · Listado de Planos");
                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(36);
                                c.ConstantColumn(88);
                                c.RelativeColumn();
                                c.ConstantColumn(82);
                            });
                            table.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Rev.");
                                TH(h.Cell().Background(CNavy).Padding(5), "Código");
                                TH(h.Cell().Background(CNavy).Padding(5), "Descripción");
                                TH(h.Cell().Background(CNavy).Padding(5), "Disciplina");
                            });
                            bool alt = false;
                            foreach (var p in offer.Planos)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), p.Revision);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), p.Codigo);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), p.Descripcion);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), p.Disciplina);
                                alt = !alt;
                            }
                        });
                    });
                });
            }
            // Signature and acceptance
            composer.Page(page =>
            {
                SetupPage(page);
                page.Content().PaddingTop(16).Column(col =>
                {
                    PageTitle(col, "Aceptación y Firma");

                    col.Item().PaddingTop(14).Text(t =>
                        t.Span("La presente oferta se emite para revisión, aceptación y archivo del cliente. " +
                               "La ejecución de trabajos adicionales o modificaciones deberá formalizarse " +
                               "mediante orden de cambio aprobada por las partes.")
                         .FontSize(9.5f).FontColor(CText).LineHeight(1.5f));

                    col.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            if (signatureBytes != null)
                                c.Item().Height(OfferSignatureSizing.HeightPt(offer)).AlignLeft()
                                    .Image(signatureBytes).FitHeight();
                            c.Item().BorderBottom(1f).BorderColor(CLine).Height(1);
                            c.Item().PaddingTop(8).Text(t =>
                                t.Span(S(offer.SignatureName)).FontSize(9).Bold().FontColor(CText));
                            if (!string.IsNullOrWhiteSpace(offer.SignatureRut))
                                c.Item().PaddingTop(2).Text(t =>
                                    t.Span($"RUT: {offer.SignatureRut}").FontSize(8).FontColor(CMuted));
                            c.Item().PaddingTop(2).Text(t =>
                                t.Span(S(offer.SignatureRole)).FontSize(8).FontColor(CMuted));
                            if (!string.IsNullOrWhiteSpace(offer.SignatureRegistroSec))
                                c.Item().PaddingTop(2).Text(t =>
                                    t.Span($"N° SEC: {offer.SignatureRegistroSec}").FontSize(8).FontColor(CMuted));
                            c.Item().PaddingTop(2).Text(t =>
                                t.Span(S(companyName)).FontSize(8).FontColor(CMuted));
                            var contact = string.Join("  ·  ",
                                new[] { offer.SignaturePhone, offer.SignatureEmail }
                                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                            if (!string.IsNullOrWhiteSpace(contact))
                                c.Item().PaddingTop(2).Text(t =>
                                    t.Span(contact).FontSize(8).FontColor(CMuted));
                        });
                        row.ConstantItem(48);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().BorderBottom(1f).BorderColor(CLine).Height(1);
                            c.Item().PaddingTop(8).Text(t =>
                                t.Span("Recibido conforme").FontSize(9).Bold().FontColor(CText));
                            c.Item().PaddingTop(10).Text(t =>
                                t.Span("Nombre: ________________________").FontSize(8).FontColor(CMuted));
                            c.Item().PaddingTop(8).Text(t =>
                                t.Span("RUT: ___________________________").FontSize(8).FontColor(CMuted));
                            c.Item().PaddingTop(8).Text(t =>
                                t.Span($"Fecha: {date:dd/MM/yyyy}").FontSize(8).FontColor(CMuted));
                        });
                    });

                    col.Item().PaddingTop(48).BorderTop(1).BorderColor(CLine).PaddingTop(10).Column(c =>
                    {
                        c.Item().Text(t => t.Span($"Folio: {folio}").FontSize(7).FontColor(CMuted));
                        c.Item().Text(t => t.Span($"Hash SHA-256: {hash}").FontSize(7).FontColor(CMuted));
                        c.Item().Text(t => t.Span($"Versión {ctx.VersionSummary}").FontSize(7).FontColor(CMuted));
                    });
                });
            });

        }).GeneratePdf(path);
    }
    // Cover helpers

    private static void CoverField(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingTop(10).Column(c =>
        {
            c.Item().Text(t => t.Span(label).FontSize(7).FontColor(CMuted).Bold());
            c.Item().PaddingTop(3).Text(t => t.Span(value).FontSize(10).FontColor(CText));
        });
    }

    private static void SummaryField(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingTop(6).Row(row =>
        {
            row.ConstantItem(108).Text(t => t.Span(label + ":").FontSize(8).FontColor(CMuted));
            row.RelativeItem().Text(t => t.Span(value).FontSize(8).FontColor(CText));
        });
    }

    private static string SafeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private void RenderCover(
        IContainer content,
        ExportProjectContext ctx,
        OfferPdfSettings offer,
        ExportReportTemplateSettings template,
        byte[]? logoBytes,
        byte[]? clientLogoBytes,
        string coverTitle,
        string coverSubtitle,
        decimal netUf,
        decimal netClp,
        string folio,
        string hash,
        DateTime date,
        DateTime valid,
        CultureInfo cultureES,
        byte[]? signatureBytes,
        ReportTemplateCoverLayout layout,
        string primaryColor,
        string accentColor)
    {
        string compName = !string.IsNullOrWhiteSpace(offer.CompanyName)
            ? offer.CompanyName
            : (ctx.CorporateTheme?.Enabled == true && !string.IsNullOrWhiteSpace(ctx.CorporateTheme.CompanyName)
                ? ctx.CorporateTheme.CompanyName
                : "Qubi Cost Electrical");

        content.Column(col =>
        {
            col.Item().Height(185).Background(primaryColor)
                .PaddingHorizontal(40).PaddingTop(24).PaddingBottom(16).Row(headerRow =>
                {
                    // Lado izquierdo: Contratista / Empresa oferente
                    headerRow.RelativeItem(3).Column(headerLeft =>
                    {
                        if (layout.ShowLogo && logoBytes != null)
                        {
                            headerLeft.Item().Height(46).AlignLeft().Image(logoBytes).FitHeight();
                        }
                        else
                        {
                            headerLeft.Item().Text(t =>
                                t.Span(SafeText(compName)).FontSize(16).Bold().FontColor(CWhite));
                        }

                        headerLeft.Item().PaddingTop(6).Text(t =>
                            t.Span(SafeText(compName)).FontSize(10).Bold().FontColor("#8AADCC"));
                        headerLeft.Item().PaddingTop(4).Text(t =>
                            t.Span($"{SafeText(offer.CompanyRut)}  ·  {SafeText(offer.CompanyAddress)}")
                             .FontSize(8).FontColor("#8AADCC"));
                        headerLeft.Item().PaddingTop(2).Text(t =>
                            t.Span($"{SafeText(offer.CompanyPhone)}  ·  {SafeText(offer.CompanyEmail)}")
                             .FontSize(8).FontColor("#8AADCC"));
                        if (!string.IsNullOrWhiteSpace(offer.CompanyWebsite))
                            headerLeft.Item().PaddingTop(2).Text(t =>
                                t.Span(offer.CompanyWebsite!).FontSize(8).FontColor("#8AADCC"));
                    });

                    // Lado derecho: Mandante / Cliente
                    headerRow.RelativeItem(2).AlignRight().Column(headerRight =>
                    {
                        headerRight.Item().Text(t =>
                            t.Span("PREPARADO PARA:").FontSize(7.5f).Bold().FontColor("#8AADCC"));

                        if (clientLogoBytes != null)
                        {
                            headerRight.Item().PaddingTop(4).Height(42).AlignRight().Image(clientLogoBytes).FitHeight();
                        }

                        headerRight.Item().PaddingTop(4).Text(t =>
                            t.Span(SafeText(ctx.ClientName)).FontSize(11).Bold().FontColor(CWhite));

                        if (!string.IsNullOrWhiteSpace(offer.RecipientCompany) && !string.Equals(offer.RecipientCompany, ctx.ClientName, StringComparison.OrdinalIgnoreCase))
                        {
                            headerRight.Item().PaddingTop(2).Text(t =>
                                t.Span(SafeText(offer.RecipientCompany)).FontSize(8).FontColor("#8AADCC"));
                        }

                        if (!string.IsNullOrWhiteSpace(offer.RecipientName))
                        {
                            headerRight.Item().PaddingTop(2).Text(t =>
                                t.Span($"Att: {SafeText(offer.RecipientName)}").FontSize(8).FontColor("#8AADCC").Italic());
                        }

                        headerRight.Item().PaddingTop(2).Text(t =>
                            t.Span($"Obra: {SafeText(ctx.Location)}").FontSize(8).FontColor("#8AADCC"));
                    });
                });

            col.Item().PaddingHorizontal(40).PaddingTop(36).Column(body =>
            {
                body.Item().Text(t =>
                    t.Span(SafeText(coverTitle).ToUpperInvariant())
                     .FontSize(22).Bold().FontColor(primaryColor));
                if (!string.IsNullOrWhiteSpace(coverSubtitle))
                {
                    body.Item().PaddingTop(4).Text(t =>
                        t.Span(coverSubtitle).FontSize(10).FontColor(CMuted).Italic());
                }
                body.Item().PaddingTop(12).LineHorizontal(1).LineColor(CLine);

                if (ctx.IsDraft && template.ShowCoverTraceability)
                {
                    body.Item().PaddingTop(12).AlignLeft().Background("#F59E0B")
                        .PaddingHorizontal(12).PaddingVertical(6).Text(t =>
                            t.Span("BORRADOR · SUJETO A REVISIÓN")
                             .FontSize(10).Bold().FontColor(CWhite));
                }

                RenderCoverSections(body, ctx, offer, template, netUf, netClp, folio, hash, date, valid, cultureES, signatureBytes, layout, primaryColor, accentColor);
            });
        });
    }

    private void RenderCoverSections(
        ColumnDescriptor col,
        ExportProjectContext ctx,
        OfferPdfSettings offer,
        ExportReportTemplateSettings template,
        decimal netUf,
        decimal netClp,
        string folio,
        string hash,
        DateTime date,
        DateTime valid,
        CultureInfo cultureES,
        byte[]? signatureBytes,
        ReportTemplateCoverLayout layout,
        string primaryColor,
        string accentColor)
    {
        var sections = layout.Sections.Count == 0 ? ReportTemplateCoverLayout.DefaultSections : layout.Sections;
        var index = 0;
        while (index < sections.Count)
        {
            var left = sections[index];
            var right = index + 1 < sections.Count ? sections[index + 1] : (ReportCoverSectionKind?)null;

            col.Item().PaddingTop(index == 0 ? 18 : 14).Row(row =>
            {
                row.RelativeItem().Element(c =>
                    RenderCoverSectionCard(c, left, ctx, offer, template, netUf, netClp, folio, hash, date, valid, cultureES, signatureBytes, primaryColor, accentColor));

                if (right.HasValue)
                {
                    row.ConstantItem(18);
                    row.RelativeItem().Element(c =>
                        RenderCoverSectionCard(c, right.Value, ctx, offer, template, netUf, netClp, folio, hash, date, valid, cultureES, signatureBytes, primaryColor, accentColor));
                }
            });

            index += 2;
        }
    }

    private void RenderCoverSectionCard(
        IContainer container,
        ReportCoverSectionKind section,
        ExportProjectContext ctx,
        OfferPdfSettings offer,
        ExportReportTemplateSettings template,
        decimal netUf,
        decimal netClp,
        string folio,
        string hash,
        DateTime date,
        DateTime valid,
        CultureInfo cultureES,
        byte[]? signatureBytes,
        string primaryColor,
        string accentColor)
    {
        container.Background(CWhite).Border(1).BorderColor(CLine).Padding(14).Column(card =>
        {
            card.Item().Text(t =>
                t.Span(CoverSectionTitle(section, ctx.MuestraUf)).FontSize(10).Bold().FontColor(primaryColor));

            switch (section)
            {
                case ReportCoverSectionKind.Project:
                    CoverField(card, "Proyecto", ctx.ProjectTitle);
                    CoverField(card, "Obra", ctx.Location);
                    CoverField(card, "Oferta", $"{SafeText(offer.OfferNumber)}  ·  {SafeText(offer.RevisionTag)}");
                    CoverField(card, "Fecha", date.ToString("dd/MM/yyyy", cultureES));
                    break;

                case ReportCoverSectionKind.Client:
                    CoverField(card, "Cliente", ctx.ClientName);
                    CoverField(card, "Destinatario", SafeText(offer.RecipientName));
                    string clientCompany = !string.IsNullOrWhiteSpace(offer.RecipientCompany)
                        ? offer.RecipientCompany
                        : ctx.ClientName;
                    CoverField(card, "Empresa", SafeText(clientCompany));
                    CoverField(card, "Referencia", SafeText(offer.Reference));
                    break;

                case ReportCoverSectionKind.Summary:
                    decimal valorUfCover = ctx.Project.ValorUfClp > 0m ? ctx.Project.ValorUfClp : PriceConversion.DefaultValorUfClp;
                    if (ctx.MuestraUf)
                    {
                        card.Item().PaddingTop(6).Text(t =>
                            t.Span($"UF {netUf.ToString("#,##0.000", cultureES)}").FontSize(24).Bold().FontColor(primaryColor));
                        card.Item().PaddingTop(2).Text(t =>
                            t.Span(currencyConversion.FormatMoneyFromUf(netUf, ctx.CurrencyLabel, valorUfCover, ctx.Project.TipoCambioUsdClp))
                             .FontSize(12).FontColor(accentColor));
                    }
                    else
                    {
                        // Proyecto en CLP: se muestra sólo el monto en pesos, sin UF ni tasa.
                        card.Item().PaddingTop(6).Text(t =>
                            t.Span($"$ {netClp:N0}").FontSize(24).Bold().FontColor(primaryColor));
                        card.Item().PaddingTop(2).Text(t =>
                            t.Span($"CLP · neto").FontSize(12).FontColor(accentColor));
                    }
                    if (offer.IncludeIvaNote)
                    {
                        card.Item().PaddingTop(8).Text(t =>
                            t.Span("Valores netos · agregar IVA cuando corresponda.")
                             .FontSize(7.5f).FontColor(CMuted).Italic());
                    }
                    break;

                case ReportCoverSectionKind.Itemized:
                    CoverField(card, "Partidas", ctx.Itemizado.Count(x => !x.EsTitulo).ToString(cultureES));
                    CoverField(card, "Títulos", ctx.Itemizado.Count(x => x.EsTitulo).ToString(cultureES));
                    CoverField(card, "Cubicaciones", ctx.Cubicacion.Count.ToString(cultureES));
                    CoverField(card, "APU", ctx.Apus.Count.ToString(cultureES));
                    break;

                case ReportCoverSectionKind.Conditions:
                    CoverField(card, "Validez", $"{offer.ValidityDays} días · hasta {valid:dd/MM/yyyy}");
                    CoverField(card, "Plazo", SafeText(offer.ProjectDuration));
                    CoverExcerpt(card, "Condiciones", offer.CommercialConditionsText);
                    CoverExcerpt(card, "Exclusiones", offer.ExclusionsText);
                    break;

                case ReportCoverSectionKind.Signature:
                    CoverField(card, "Folio", folio);
                    CoverField(card, "Hash", hash);
                    if (template.ShowCoverTraceability)
                    {
                        CoverField(card, "Versión", ctx.VersionSummary);
                        CoverField(card, "Motor", ctx.CalculationEngineVersion.ToString(cultureES));
                    }

                    if (signatureBytes != null)
                    {
                        card.Item().PaddingTop(10).Height(OfferSignatureSizing.HeightPt(offer)).Image(signatureBytes).FitHeight();
                    }
                    else if (!string.IsNullOrWhiteSpace(offer.SignatureName))
                    {
                        CoverField(card, "Firmante", SafeText(offer.SignatureName));
                        if (!string.IsNullOrWhiteSpace(offer.SignatureRole))
                            CoverField(card, "Cargo", SafeText(offer.SignatureRole));
                    }
                    break;
            }
        });
    }

    private static void CoverExcerpt(ColumnDescriptor col, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        col.Item().PaddingTop(6).Text(t => t.Span(label).FontSize(7).Bold().FontColor(CMuted));
        col.Item().PaddingTop(2).Text(t => t.Span(Excerpt(value, 140)).FontSize(8).FontColor(CText));
    }

    private static string CoverSectionTitle(ReportCoverSectionKind section, bool muestraUf) => section switch
    {
        ReportCoverSectionKind.Project => "Proyecto",
        ReportCoverSectionKind.Client => "Cliente",
        ReportCoverSectionKind.Summary => muestraUf ? "Resumen UF" : "Resumen $",
        ReportCoverSectionKind.Itemized => "Itemizado",
        ReportCoverSectionKind.Conditions => "Condiciones",
        ReportCoverSectionKind.Signature => "Firma",
        _ => "Portada"
    };

    private static string Excerpt(string value, int maxChars)
    {
        string text = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= maxChars ? text : text[..Math.Max(0, maxChars - 1)] + "…";
    }
    // Offer row builder

    private List<OfferRow> BuildOfferRows(ExportProjectContext ctx, CostBreakdown cost, decimal netUf)
    {
        return OfferClienteRowBuilder
            .Build(ctx.Itemizado, cost, netUf, ctx.ProjectTitle)
            .Select(r => new OfferRow
            {
                Item = r.Item,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Cantidad = r.Cantidad,
                TotalUf = r.TotalUf,
                EsTitulo = r.EsTitulo,
                Observaciones = r.Observaciones
            })
            .ToList();
    }

    private static string BuildFolio(ExportProjectContext ctx, DateTime date)
    {
        string source = string.Join("|",
            ctx.ProjectTitle, ctx.ClientName, ctx.OfferSettings.OfferNumber,
            date.ToString("yyyyMMdd"), ProductVersionInfo.ProductVersion);
        string h = ShortHash(source, 8);
        string d = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(ctx.OfferSettings.OfferNumber)
            ? $"PE-{d}-{h}"
            : $"PE-{d}-{SanitizeFolio(ctx.OfferSettings.OfferNumber)}-{h}";
    }

    private static string BuildHash(ExportProjectContext ctx, CostBreakdown cost, decimal netUf, string folio)
    {
        string payload = string.Join("|",
            folio, ctx.ProductVersion, ctx.ProjectFormatVersion, ctx.CalculationEngineVersion,
            ctx.ProjectTitle, ctx.ClientName,
            ctx.ExportedAt.ToString("O", CultureInfo.InvariantCulture),
            netUf.ToString(CultureInfo.InvariantCulture),
            cost.TotalVentaUf.ToString(CultureInfo.InvariantCulture),
            ctx.Itemizado.Count, ctx.Cubicacion.Count, ctx.Apus.Count);
        return ShortHash(payload, 16);
    }

    private static string ShortHash(string payload, int length)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? ""));
        return Convert.ToHexString(bytes)[..Math.Min(length, 64)];
    }

    private static string SanitizeFolio(string value)
    {
        var chars = value.Trim()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            .ToArray();
        string clean = new(chars);
        return string.IsNullOrWhiteSpace(clean) ? "OFERTA" : clean;
    }

    private sealed class OfferRow
    {
        public string Item { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Unidad { get; set; } = "";
        public decimal Cantidad { get; set; }
        public decimal TotalUf { get; set; }
        public bool EsTitulo { get; set; }
        public string Observaciones { get; set; } = "";
    }
}
