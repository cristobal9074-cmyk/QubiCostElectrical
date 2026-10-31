using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Genera la Carta de Oferta formal en formato Microsoft Word (.docx),
/// incluyendo membrete corporativo, datos de proyecto y destinatario,
/// tabla de planos, alcances, exclusiones, tabla de condiciones comerciales,
/// oferta económica destacada y bloque de firma formal.
/// </summary>
public sealed class PresentationLetterDocxExporter : IPresentationLetterDocxExporter
{
    private readonly IBudgetCalculationService budgetCalculation;

    public PresentationLetterDocxExporter()
        : this(new BudgetCalculationService())
    {
    }

    public PresentationLetterDocxExporter(IBudgetCalculationService budgetCalculation)
    {
        this.budgetCalculation = budgetCalculation ?? throw new ArgumentNullException(nameof(budgetCalculation));
    }

    public void Export(ExportProjectContext context, string path)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta DOCX vacía.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
        if (File.Exists(path)) File.Delete(path);

        var offer = context.OfferSettings ?? new OfferPdfSettings();
        var theme = context.CorporateTheme;
        var project = context.Project;
        var culture = CultureInfo.GetCultureInfo("es-CL");

        bool hasTheme = theme?.Enabled == true;
        string companyName = !string.IsNullOrWhiteSpace(offer.CompanyName) ? offer.CompanyName
            : (hasTheme && !string.IsNullOrWhiteSpace(theme!.CompanyName) ? theme.CompanyName : "EMPRESA CONTRATISTA");
        string companyRut = Safe(offer.CompanyRut);
        string companyAddress = Safe(offer.CompanyAddress);
        string companyPhone = Safe(offer.CompanyPhone);
        string companyEmail = Safe(offer.CompanyEmail);
        string companyWebsite = Safe(offer.CompanyWebsite);

        var date = offer.OfferDate == default ? DateTime.Today : offer.OfferDate;
        string folio = !string.IsNullOrWhiteSpace(offer.OfferNumber) ? offer.OfferNumber.Trim() : "S/N";
        string revision = !string.IsNullOrWhiteSpace(offer.RevisionTag) ? offer.RevisionTag.Trim() : "Rev 0";
        string offerTitle = !string.IsNullOrWhiteSpace(offer.OfferTitle) ? offer.OfferTitle.Trim()
            : "PRESUPUESTO INSTALACIONES ELÉCTRICAS";
        string recipient = Safe(offer.RecipientName);
        string recipientCompany = Safe(offer.RecipientCompany);
        string reference = Safe(offer.Reference);
        string obra = context.ProjectTitle;

        // Cálculo del monto total neto
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
            rawNetClp = context.OfferNetAmountUf;
            if (rawNetClp <= 0m)
            {
                var settings = context.BudgetSettings;
                var cost = budgetCalculation.CalculateBudget(
                    context.Cubicacion, settings, context.Apus, context.Itemizado, context.OrdenesCambio);
                rawNetClp = cost.TotalVentaNetoUf > 0m ? cost.TotalVentaNetoUf : cost.TotalVentaUf;
            }
            netUf = rawNetClp > 0m ? PriceConversion.ClpToUf(rawNetClp, valorUf) : 0m;
        }

        string totalMontoText = context.MuestraUf
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

        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new Document(new Body());
        Body docBody = main.Document.Body!;

        // 1. Membrete empresa
        AddParagraph(docBody, companyName.ToUpperInvariant(), 24, true, JustificationValues.Left, "0B2442");
        string contactLine = BuildContactLine(offer, companyRut, companyAddress, companyPhone, companyEmail, companyWebsite);
        if (!string.IsNullOrWhiteSpace(contactLine))
            AddParagraph(docBody, contactLine, 16, false, JustificationValues.Left, "5A6472");

        AddHorizontalRule(docBody, "E87722", 24);

        // 2. Fecha y títulos
        AddParagraph(docBody, $"Santiago, {fechaLarga}", 18, false, JustificationValues.Right, "1A2332");
        AddEmptyLine(docBody, 120);

        AddParagraph(docBody, offerTitle.ToUpperInvariant(), 24, true, JustificationValues.Center, "0B2442");
        AddParagraph(docBody, obra.ToUpperInvariant(), 22, true, JustificationValues.Center, "0B2442");
        AddParagraph(docBody, $"Presupuesto N° {folio} · {revision}", 18, true, JustificationValues.Center, "5A6472");
        AddEmptyLine(docBody, 160);

        // 3. Referencia y Destinatario
        if (!string.IsNullOrWhiteSpace(reference) && reference != "-")
            AddParagraph(docBody, $"Ref.: {reference}", 18, true, JustificationValues.Left, "0B2442");
        AddParagraph(docBody, $"Obra: {obra}", 18, true, JustificationValues.Left, "0B2442");
        AddEmptyLine(docBody, 120);

        AddParagraph(docBody, "Estimado(s):", 18, false, JustificationValues.Left);
        AddParagraph(docBody, string.IsNullOrWhiteSpace(recipient) || recipient == "-" ? "Señores Mandantes" : recipient, 19, true, JustificationValues.Left);
        if (!string.IsNullOrWhiteSpace(recipientCompany) && recipientCompany != "-")
            AddParagraph(docBody, recipientCompany, 18, false, JustificationValues.Left);
        AddParagraph(docBody, "Presente:", 18, false, JustificationValues.Left);
        AddEmptyLine(docBody, 140);

        // 4. Introducción
        if (offer.IncludePresentationLetter && introLines.Count > 0)
        {
            AddParagraph(docBody, "De nuestra consideración:", 19, true, JustificationValues.Left);
            foreach (var ln in introLines)
                AddParagraph(docBody, ln, 19, false, JustificationValues.Both);
            AddEmptyLine(docBody, 140);
        }

        int secNumber = 1;

        // 5. Planos y antecedentes
        foreach (var group in planoGroups)
        {
            AddSectionHeader(docBody, $"{secNumber++}- {group.Title.ToUpperInvariant()}");
            AddPlanosTable(docBody, group.Rows);
            AddEmptyLine(docBody, 160);
        }

        // 6. Alcances
        if (scopeLines.Count > 0)
        {
            AddSectionHeader(docBody, $"{secNumber++}- ALCANCES");
            foreach (var line in scopeLines)
                AddBulletParagraph(docBody, line);
            AddEmptyLine(docBody, 160);
        }

        // 7. Exclusiones
        if (exclusionLines.Count > 0)
        {
            AddSectionHeader(docBody, $"{secNumber++}- EXCLUSIONES");
            foreach (var line in exclusionLines)
                AddBulletParagraph(docBody, line);
            AddEmptyLine(docBody, 160);
        }

        // 8. Condiciones comerciales
        if (conditionsKv.Count > 0)
        {
            AddSectionHeader(docBody, $"{secNumber++}- CONDICIONES COMERCIALES");
            AddConditionsTable(docBody, conditionsKv);
            AddEmptyLine(docBody, 160);
        }

        // 9. Observaciones
        if (obsLines.Count > 0)
        {
            AddSectionHeader(docBody, $"{secNumber++}- OBSERVACIONES");
            foreach (var line in obsLines)
                AddParagraph(docBody, line, 19, false, JustificationValues.Both);
            AddEmptyLine(docBody, 160);
        }

        // 10. Oferta Comercial Destacada
        AddSectionHeader(docBody, $"{secNumber++}- OFERTA COMERCIAL");
        AddHighlightedBox(docBody, $"El Valor Total Neto del Presupuesto asciende a {totalMontoText}{ivaNote}.");
        AddEmptyLine(docBody, 180);

        // 11. Cierre y Firma
        AddParagraph(docBody, "Esperamos que nuestra oferta se ajuste a lo esperado por Uds. Cualquier duda o consulta la podemos aclarar oportunamente por reunión, correo o teléfono.", 18, false, JustificationValues.Both);
        AddEmptyLine(docBody, 100);
        AddParagraph(docBody, "Sin otro particular, les saluda cordialmente,", 18, false, JustificationValues.Left);
        AddEmptyLine(docBody, 300);

        // Bloque de Firma
        string signName = !string.IsNullOrWhiteSpace(offer.SignatureName) ? offer.SignatureName : "LUIS SAAVEDRA";
        string signRole = !string.IsNullOrWhiteSpace(offer.SignatureRole) ? offer.SignatureRole : "GERENTE GENERAL";

        AddParagraph(docBody, signName, 20, true, JustificationValues.Left, "0B2442");
        if (!string.IsNullOrWhiteSpace(signRole))
            AddParagraph(docBody, signRole, 18, false, JustificationValues.Left, "5A6472");
        AddParagraph(docBody, companyName, 18, false, JustificationValues.Left, "5A6472");
        if (!string.IsNullOrWhiteSpace(offer.SignaturePhone))
            AddParagraph(docBody, $"Fono: {offer.SignaturePhone}", 16, false, JustificationValues.Left, "5A6472");
        if (!string.IsNullOrWhiteSpace(offer.SignatureEmail))
            AddParagraph(docBody, $"Email: {offer.SignatureEmail}", 16, false, JustificationValues.Left, "5A6472");
        if (!string.IsNullOrWhiteSpace(companyRut) && companyRut != "-")
            AddParagraph(docBody, $"RUT: {companyRut}", 16, false, JustificationValues.Left, "5A6472");

        // Margen de página Letter
        var section = new SectionProperties(
            new PageSize { Width = 12240U, Height = 15840U },
            new PageMargin { Top = 1080, Right = 1080U, Bottom = 1080, Left = 1080U, Header = 720U, Footer = 720U, Gutter = 0U });
        docBody.Append(section);

        main.Document.Save();
    }

    private static string BuildContactLine(OfferPdfSettings offer, string rut, string address, string phone, string email, string website)
    {
        return string.Join("  |  ", new[]
        {
            rut != "-" ? $"RUT: {rut}" : "",
            address != "-" ? address : "",
            phone != "-" ? $"Fono: {phone}" : "",
            email != "-" ? email : "",
            website != "-" ? website : ""
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static void AddSectionHeader(Body body, string title)
    {
        AddParagraph(body, title, 20, true, JustificationValues.Left, "0B2442");
    }

    private static void AddPlanosTable(Body body, List<OfferPlanReferenceRow> rows)
    {
        var tbl = new Table();
        var tblPr = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "C8D8E8" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "C8D8E8" },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "E2E8F0" },
                new InsideVerticalBorder { Val = BorderValues.None }
            ),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
        );
        tbl.Append(tblPr);

        // Encabezado
        var hRow = new TableRow();
        AddTableCell(hRow, "REV.", 16, true, "EBF2FB", "0B2442", JustificationValues.Center, 1200);
        AddTableCell(hRow, "DESCRIPCIÓN / LÁMINA", 16, true, "EBF2FB", "0B2442", JustificationValues.Left, 7800);
        tbl.Append(hRow);

        foreach (var p in rows)
        {
            var row = new TableRow();
            var nombre = string.IsNullOrWhiteSpace(p.Codigo) || (p.Descripcion ?? "").Contains(p.Codigo, StringComparison.OrdinalIgnoreCase)
                ? (p.Descripcion ?? "")
                : $"{p.Codigo} {p.Descripcion}".Trim();

            AddTableCell(row, p.Revision, 16, false, "FFFFFF", "1A2332", JustificationValues.Center, 1200);
            AddTableCell(row, nombre, 16, false, "FFFFFF", "1A2332", JustificationValues.Left, 7800);
            tbl.Append(row);
        }

        body.Append(tbl);
    }

    private static void AddConditionsTable(Body body, List<(string Key, string Value)> conditions)
    {
        var tbl = new Table();
        var tblPr = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "C8D8E8" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "C8D8E8" },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "E2E8F0" },
                new InsideVerticalBorder { Val = BorderValues.None }
            ),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
        );
        tbl.Append(tblPr);

        bool alt = false;
        foreach (var (k, v) in conditions)
        {
            var row = new TableRow();
            string bg = alt ? "F8FAFC" : "FFFFFF";
            alt = !alt;

            AddTableCell(row, k, 17, true, bg, "0B2442", JustificationValues.Left, 2500);
            AddTableCell(row, v, 17, false, bg, "1A2332", JustificationValues.Left, 6500);
            tbl.Append(row);
        }

        body.Append(tbl);
    }

    private static void AddTableCell(TableRow tr, string text, int fontHalfPoints, bool bold, string bgHex, string textHex, JustificationValues align, int? widthDxa = null)
    {
        var tc = new TableCell();
        var tcPr = new TableCellProperties(
            new Shading { Val = ShadingPatternValues.Clear, Fill = bgHex },
            new TableCellMargin(
                new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
            )
        );
        if (widthDxa.HasValue)
            tcPr.Append(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = widthDxa.Value.ToString() });

        tc.Append(tcPr);

        var p = new Paragraph(
            new ParagraphProperties(new Justification { Val = align }, new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
                    new FontSize { Val = fontHalfPoints.ToString() },
                    bold ? new Bold() : new Color { Val = textHex },
                    new Color { Val = textHex }
                ),
                new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }
            )
        );
        tc.Append(p);
        tr.Append(tc);
    }

    private static void AddHighlightedBox(Body body, string text)
    {
        var tbl = new Table();
        var tblPr = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 12, Color = "0B2442" },
                new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "0B2442" },
                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "0B2442" },
                new RightBorder { Val = BorderValues.Single, Size = 12, Color = "0B2442" }
            ),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
        );
        tbl.Append(tblPr);

        var row = new TableRow();
        var cell = new TableCell();
        cell.Append(new TableCellProperties(
            new Shading { Val = ShadingPatternValues.Clear, Fill = "EBF2FB" },
            new TableCellMargin(
                new TopMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa }
            )
        ));

        var p = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }, new SpacingBetweenLines { After = "0", Line = "260", LineRule = LineSpacingRuleValues.Auto }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
                    new FontSize { Val = "22" },
                    new Bold(),
                    new Color { Val = "0B2442" }
                ),
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
        cell.Append(p);
        row.Append(cell);
        tbl.Append(row);
        body.Append(tbl);
    }

    private static void AddBulletParagraph(Body body, string text)
    {
        static RunProperties NewProps() => new(
            new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
            new FontSize { Val = "19" },
            new Color { Val = "1A2332" });

        var p = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Both },
                new Indentation { Left = "400", Hanging = "250" },
                new SpacingBetweenLines { After = "80", Line = "260", LineRule = LineSpacingRuleValues.Auto }
            ),
            new Run(NewProps(), new Text("•  ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(NewProps(), new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve })
        );
        body.Append(p);
    }

    private static void AddHorizontalRule(Body body, string colorHex, int heightDxa)
    {
        var p = new Paragraph(
            new ParagraphProperties(
                new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 12, Color = colorHex }),
                new SpacingBetweenLines { After = heightDxa.ToString(), Line = "100", LineRule = LineSpacingRuleValues.Auto }
            )
        );
        body.Append(p);
    }

    private static void AddEmptyLine(Body body, int spaceDxa)
    {
        var p = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = spaceDxa.ToString(), Line = "100", LineRule = LineSpacingRuleValues.Auto }));
        body.Append(p);
    }

    private static void AddParagraph(Body body, string text, int fontHalfPoints, bool bold, JustificationValues justification, string colorHex = "1A2332")
    {
        var runProperties = new RunProperties(
            new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
            new FontSize { Val = fontHalfPoints.ToString() },
            new Color { Val = colorHex });
        if (bold) runProperties.Append(new Bold());

        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = justification },
                new SpacingBetweenLines { After = "80", Line = "260", LineRule = LineSpacingRuleValues.Auto }),
            new Run(runProperties, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }));
        body.Append(paragraph);
    }

    private static string Safe(string? text) => string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
}
