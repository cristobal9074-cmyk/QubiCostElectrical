using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QubiCostElectrical.Infrastructure;

internal static class QuestPdfCoverComposer
{
    private const string CWhite = "#FFFFFF";
    private const string CLine = "#C8D8E8";
    private const string CMuted = "#5A6472";
    private const string CText = "#1A2332";

    public static string BuildFolio(ExportProjectContext ctx, DateTime date)
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

    public static string BuildHash(ExportProjectContext ctx, CostBreakdown cost, decimal netUf, string folio)
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

    public static string ShortHash(string payload, int length)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? ""));
        return Convert.ToHexString(bytes)[..Math.Min(length, 64)];
    }

    public static string SanitizeFolio(string value)
    {
        var chars = value.Trim()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            .ToArray();
        string clean = new(chars);
        return string.IsNullOrWhiteSpace(clean) ? "OFERTA" : clean;
    }

    public static void RenderCover(
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
        string accentColor,
        ICurrencyConversionService currencyConversion)
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

                RenderCoverSections(body, ctx, offer, template, netUf, netClp, folio, hash, date, valid, cultureES, signatureBytes, layout, primaryColor, accentColor, currencyConversion);
            });
        });
    }

    private static void RenderCoverSections(
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
        string accentColor,
        ICurrencyConversionService currencyConversion)
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
                    RenderCoverSectionCard(c, left, ctx, offer, template, netUf, netClp, folio, hash, date, valid, cultureES, signatureBytes, primaryColor, accentColor, currencyConversion));

                if (right.HasValue)
                {
                    row.ConstantItem(18);
                    row.RelativeItem().Element(c =>
                        RenderCoverSectionCard(c, right.Value, ctx, offer, template, netUf, netClp, folio, hash, date, valid, cultureES, signatureBytes, primaryColor, accentColor, currencyConversion));
                }
            });

            index += 2;
        }
    }

    private static void RenderCoverSectionCard(
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
        string accentColor,
        ICurrencyConversionService currencyConversion)
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

    private static void CoverField(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingTop(10).Column(c =>
        {
            c.Item().Text(t => t.Span(label).FontSize(7).FontColor(CMuted).Bold());
            c.Item().PaddingTop(3).Text(t => t.Span(value).FontSize(10).FontColor(CText));
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

    private static string SafeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
}
