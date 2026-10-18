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
/// Genera la Carta de Oferta PDF en formato carta formal (formato carta formal):
/// encabezado con logo + datos empresa, folio, destinatario, cuerpo, tabla de planos,
/// alcances, exclusiones, condiciones comerciales, valor total destacado y firma.
/// </summary>
public sealed class QuestPdfOfferLetterExporter : IOfferLetterPdfExporter
{
    private readonly IBudgetCalculationService budgetCalculation;
    private readonly ICurrencyConversionService currencyConversion;

    public QuestPdfOfferLetterExporter(
        IBudgetCalculationService budgetCalculation,
        ICurrencyConversionService currencyConversion)
    {
        this.budgetCalculation = budgetCalculation;
        this.currencyConversion = currencyConversion;
    }

    public void Export(ExportProjectContext ctx, string path)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta vacia.", nameof(path));

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
        string cLine = "#C8D8E8";
        string cAlt = "#F7F9FC";

        string? logoPath = !string.IsNullOrWhiteSpace(offer.LogoPath) ? offer.LogoPath
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.LogoPath) ? theme.LogoPath : null);
        byte[]? logoBytes = logoPath != null && File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

        string? clientLogoPath = !string.IsNullOrWhiteSpace(offer.ClientLogoPath) && File.Exists(offer.ClientLogoPath) ? offer.ClientLogoPath : null;
        byte[]? clientLogoBytes = clientLogoPath != null ? File.ReadAllBytes(clientLogoPath) : null;

        string companyName = !string.IsNullOrWhiteSpace(offer.CompanyName) ? offer.CompanyName
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.CompanyName) ? theme.CompanyName : "");
        string companyRut = S(offer.CompanyRut);
        string companyAddress = S(offer.CompanyAddress);
        string companyPhone = S(offer.CompanyPhone);
        string companyEmail = S(offer.CompanyEmail);
        string companyWebsite = S(offer.CompanyWebsite);

        var date = offer.OfferDate == default ? DateTime.Today : offer.OfferDate;
        string folio = !string.IsNullOrWhiteSpace(offer.OfferNumber) ? offer.OfferNumber.Trim() : "S/N";
        string revision = !string.IsNullOrWhiteSpace(offer.RevisionTag) ? offer.RevisionTag.Trim() : "Rev 0";
        string offerTitle = !string.IsNullOrWhiteSpace(offer.OfferTitle) ? offer.OfferTitle.Trim()
            : "PRESUPUESTO INSTALACIONES ELECTRICAS";
        string recipient = S(offer.RecipientName);
        string recipientCompany = S(offer.RecipientCompany);
        string reference = S(offer.Reference);

        decimal valorUf = project.ValorUfClp > 0m ? project.ValorUfClp : PriceConversion.DefaultValorUfClp;
        decimal rawNetClp;
        decimal netUf;
        if (offer.UseManualNetAmountUf && offer.ManualNetAmountUf > 0m)
        {
            netUf = offer.ManualNetAmountUf;
            rawNetClp = PriceConversion.UfToClp(netUf, valorUf);
        }
        else
        {
            rawNetClp = ctx.OfferNetAmountUf;
            if (rawNetClp <= 0m)
            {
                var settings = ctx.BudgetSettings;
                var cost = budgetCalculation.CalculateBudget(
                    ctx.Cubicacion, settings, ctx.Apus, ctx.Itemizado, ctx.OrdenesCambio);
                rawNetClp = cost.TotalVentaNetoUf > 0m ? cost.TotalVentaNetoUf : cost.TotalVentaUf;
            }
            netUf = rawNetClp > 0m ? PriceConversion.ClpToUf(rawNetClp, valorUf) : 0m;
        }

        // El documento respeta la moneda del proyecto: con CLP nunca se menciona la UF.
        string totalMontoText = ctx.MuestraUf
            ? (netUf > 0m ? $"{netUf.ToString("#,##0.000", culture)} UF" : "S/I")
            : (rawNetClp > 0m ? $"$ {Money.Round(rawNetClp):N0} CLP" : "S/I");
        string ivaNote = offer.IncludeIvaNote ? " + IVA" : "";
        string fechaLarga = date.ToString("d 'de' MMMM 'de' yyyy", culture);

        static List<string> ToLines(string? text) =>
            (text ?? "").Replace("\r", "").Split('\n')
                .Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        static List<(string Key, string Value)> ToKvLines(string? text)
        {
            var result = new List<(string, string)>();
            foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var idx = line.IndexOf(':');
                if (idx > 0) result.Add((line[..idx].Trim(), line[(idx + 1)..].Trim()));
                else if (!string.IsNullOrWhiteSpace(line)) result.Add(("", line.Trim()));
            }
            return result;
        }

        var scopeLines = ToLines(offer.ScopeText);
        var exclusionLines = ToLines(offer.ExclusionsText);
        var conditionsKv = ToKvLines(offer.CommercialConditionsText);
        var obsLines = ToLines(offer.ObservationsText);
        var introLines = ToLines(offer.PresentationLetterText);

        var planoGroups = offer.IncludeDrawingList
            ? OfferLetterPlanCatalog.Group(OfferLetterPlanCatalog.Resolve(offer, project.AntecedentesTecnicos))
            : Array.Empty<(string Title, List<OfferPlanReferenceRow> Rows)>();
        string montaje = S(offer.MontajeText);
        string obra = ctx.ProjectTitle;
        string obraTitulo = culture.TextInfo.ToUpper(obra);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(18, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9).FontColor(cText));

                page.Content().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().AlignMiddle().Column(logo =>
                        {
                            if (logoBytes != null)
                                logo.Item().Width(168).Height(52).Image(logoBytes).FitArea();
                            else if (!string.IsNullOrWhiteSpace(companyName))
                                logo.Item().Text(companyName).Bold().FontSize(16).FontColor(cNavy);
                        });
                        if (clientLogoBytes != null)
                            r.AutoItem().AlignMiddle().Width(72).Height(40).Image(clientLogoBytes).FitArea();
                    });
                    col.Item().PaddingTop(6).Height(5).Background("#E87722");

                    col.Item().PaddingTop(10).AlignRight().Text($"Santiago, {fechaLarga}").FontSize(9).FontColor(cText);
                    col.Item().PaddingTop(10).AlignCenter().Text(offerTitle).FontSize(13).Bold().FontColor(cNavy);
                    col.Item().PaddingTop(6).AlignCenter().Text(obraTitulo).FontSize(12).Bold().FontColor(cNavy);
                    col.Item().PaddingTop(8).AlignCenter().Text("Presupuesto").FontSize(9).FontColor(cMuted);
                    col.Item().AlignCenter().Text(folio).FontSize(11).Bold().FontColor(cNavy);
                    if (!string.IsNullOrWhiteSpace(revision) && !revision.Equals("Rev 0", StringComparison.OrdinalIgnoreCase))
                        col.Item().AlignCenter().Text(revision).FontSize(8).FontColor(cMuted);

                    if (montaje.Length > 0)
                    {
                        col.Item().PaddingTop(8).Text("Oferta por Montaje de:").Bold().FontSize(10).FontColor(cNavy);
                        col.Item().PaddingTop(2).Text(montaje).FontSize(10);
                    }

                    col.Item().PaddingTop(8).Border(0.5f).BorderColor(cLine).Table(tbl =>
                    {
                        tbl.ColumnsDefinition(cd => cd.RelativeColumn());
                        if (reference.Length > 0)
                            tbl.Cell().PaddingHorizontal(5).PaddingVertical(3).Text(t => { t.Span("Ref.: ").Bold().FontSize(9); t.Span(reference).FontSize(9); });
                        tbl.Cell().BorderTop(0.3f).BorderColor(cLine).PaddingHorizontal(5).PaddingVertical(3).Text(t => { t.Span("Obra: ").Bold().FontSize(9); t.Span(obra).FontSize(9); });
                    });

                    col.Item().PaddingTop(10).Text("Estimado").FontSize(9);
                    col.Item().Text(string.IsNullOrWhiteSpace(recipient) ? "Sr." : recipient).FontSize(9);
                    if (!string.IsNullOrWhiteSpace(recipientCompany))
                        col.Item().Text(recipientCompany).FontSize(9);
                    col.Item().Text("Presente:").FontSize(9);

                    if (offer.IncludePresentationLetter && introLines.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text("De nuestra consideraci\u00f3n.").FontSize(9);
                        col.Item().PaddingTop(3).Column(b => { foreach (var ln in introLines) b.Item().PaddingBottom(3).Text(ln).FontSize(9).Justify(); });
                    }

                    int sec = 1;
                    void Section(string title) { col.Item().PaddingTop(10).Text($"{sec++}-{title}").Bold().FontSize(10).FontColor(cNavy); }
                    void ParaList(List<string> lines) { col.Item().PaddingTop(4).Column(b => { foreach (var l in lines) b.Item().PaddingBottom(4).Text(l).FontSize(9).Justify(); }); }

                    foreach (var group in planoGroups)
                    {
                        Section(group.Title);
                        col.Item().PaddingTop(4).Element(e => PlanosTable(e, group.Rows, cNavy, cHead, cLine, cAlt));
                    }

                    if (scopeLines.Count > 0) { Section("ALCANCES:"); ParaList(scopeLines); }
                    if (exclusionLines.Count > 0) { Section("EXCLUSIONES:"); ParaList(exclusionLines); }

                    if (conditionsKv.Count > 0)
                    {
                        Section("CONDICIONES COMERCIALES:");
                        col.Item().PaddingTop(4).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(5); });
                            bool alt = false;
                            foreach (var (key, value) in conditionsKv)
                            {
                                string bg = alt ? cAlt : Colors.White; alt = !alt;
                                if (key.Length > 0) tbl.Cell().Background(bg).PaddingVertical(3).PaddingHorizontal(5).Text(key).Bold().FontSize(8.5f).FontColor(cNavy);
                                else tbl.Cell().Background(bg);
                                tbl.Cell().Background(bg).PaddingVertical(3).PaddingHorizontal(5).Text(value).FontSize(8.5f);
                            }
                        });
                    }

                    var brandKv = offer.IncludeEquipmentBrands
                        ? (offer.Productos.Count > 0
                            ? offer.Productos.Select(p => (Key: p.Categoria, Value: string.IsNullOrWhiteSpace(p.Especificacion) ? p.Marca : $"{p.Marca} · {p.Especificacion}")).ToList()
                            : ToKvLines(offer.EquipmentBrandsText))
                        : new List<(string Key, string Value)>();

                    if (obsLines.Count > 0 || brandKv.Count > 0)
                    {
                        Section("OBSERVACIONES:");
                        if (obsLines.Count > 0) ParaList(obsLines);

                        if (brandKv.Count > 0)
                        {
                            col.Item().PaddingTop(3).Column(b =>
                            {
                                foreach (var (key, value) in brandKv)
                                    b.Item().PaddingBottom(3).Text(key.Length > 0 ? $"{key}: {value}" : value).FontSize(9);
                            });
                        }
                    }

                    Section("OFERTA COMERCIAL:");
                    col.Item().PaddingTop(4).Text($"El Valor Total Neto del Presupuesto asciende a {totalMontoText}{ivaNote}.").FontSize(10).Bold().FontColor(cNavy);

                    // Firma
                    col.Item().PaddingTop(18).Column(s =>
                    {
                        s.Item().Text("Esperamos que nuestra oferta se ajuste a lo esperado por Uds., cualquier duda o consulta a este presupuesto la podemos aclarar en reuni\u00f3n, por mail o tel\u00e9fono, como dispongan.").FontSize(9);
                        s.Item().PaddingTop(3).Text("Sin otro particular, les saluda cordialmente,").FontSize(9);
                        s.Item().PaddingTop(14).AlignCenter().Column(b =>
                        {
                            if (!string.IsNullOrWhiteSpace(offer.SignatureImagePath) && File.Exists(offer.SignatureImagePath))
                                b.Item().Width(OfferSignatureSizing.WidthPt(offer)).Height(OfferSignatureSizing.HeightPt(offer))
                                    .AlignCenter().AlignMiddle()
                                    .Image(File.ReadAllBytes(offer.SignatureImagePath)).FitArea();
                            b.Item().AlignCenter().PaddingTop(4).Text(offer.SignatureName).Bold().FontSize(11.5f);
                            if (!string.IsNullOrWhiteSpace(companyName)) b.Item().AlignCenter().Text(companyName).FontSize(10.5f);
                            if (!string.IsNullOrWhiteSpace(offer.SignatureRole)) b.Item().AlignCenter().Text(offer.SignatureRole).FontSize(10f).FontColor(cMuted);
                            if (!string.IsNullOrWhiteSpace(offer.SignaturePhone)) b.Item().AlignCenter().Text(offer.SignaturePhone).FontSize(10f).FontColor(cMuted);
                            if (!string.IsNullOrWhiteSpace(offer.SignatureEmail)) b.Item().AlignCenter().Text(offer.SignatureEmail).FontSize(10f).FontColor(cMuted);
                            if (companyRut.Length > 0) b.Item().AlignCenter().Text($"RUT: {companyRut}").FontSize(9.5f).FontColor(cMuted);
                            if (!string.IsNullOrWhiteSpace(offer.SignatureRegistroSec)) b.Item().AlignCenter().Text($"Reg. SEC {offer.SignatureSecClass}: {offer.SignatureRegistroSec}").FontSize(9.5f).FontColor(cMuted);
                        });
                    });
                });

                page.Footer().PaddingTop(5).BorderTop(0.5f).BorderColor(cLine).Row(r =>
                {
                    r.RelativeItem().Text(companyName).FontSize(7.5f).FontColor(cMuted);
                    r.RelativeItem().AlignCenter().Text(t => { t.Span($"Folio {folio} | {ctx.ProjectTitle}").FontSize(7.5f).FontColor(cMuted); });
                    r.RelativeItem().AlignRight().Text(t => { t.Span("Pagina ").FontSize(7.5f).FontColor(cMuted); t.CurrentPageNumber().FontSize(7.5f).FontColor(cMuted); t.Span(" / ").FontSize(7.5f).FontColor(cMuted); t.TotalPages().FontSize(7.5f).FontColor(cMuted); });
                });
            });
        }).GeneratePdf(path);
    }

    private static void PlanosTable(IContainer c, List<OfferPlanReferenceRow> planos, string cNavy, string cHead, string cLine, string cAlt)
    {
        c.Table(tbl =>
        {
            tbl.ColumnsDefinition(col => { col.ConstantColumn(52); col.RelativeColumn(); });
            tbl.Header(h =>
            {
                void H(IContainer x, string t) => x.Background(cHead).Padding(4).Text(t).Bold().FontSize(8).FontColor(cNavy);
                h.Cell().Element(x => H(x, "REVISION")); h.Cell().Element(x => H(x, "DESCRIPCION"));
            });
            bool alt = false;
            foreach (var p in planos)
            {
                string bg = alt ? cAlt : Colors.White; alt = !alt;
                void D(IContainer x, string t) => x.BorderBottom(0.3f).BorderColor(cLine).Background(bg).PaddingVertical(2).PaddingHorizontal(4).Text(t).FontSize(8);
                var nombre = string.IsNullOrWhiteSpace(p.Codigo) || (p.Descripcion ?? "").Contains(p.Codigo, StringComparison.OrdinalIgnoreCase)
                    ? (p.Descripcion ?? "")
                    : $"{p.Codigo} {p.Descripcion}".Trim();
                tbl.Cell().Element(x => D(x, p.Revision)); tbl.Cell().Element(x => D(x, nombre));
            }
        });
    }

    private static string S(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
}
