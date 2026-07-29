using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace QubiCostElectrical;

public sealed class PresentationLetterDocxExporter : IPresentationLetterDocxExporter
{
    public void Export(ExportProjectContext context, string path)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta DOCX vacía.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
        if (File.Exists(path)) File.Delete(path);

        var offer = context.OfferSettings ?? new OfferPdfSettings();
        string title = string.IsNullOrWhiteSpace(offer.PresentationLetterTitle)
            ? "CARTA DE PRESENTACIÓN"
            : offer.PresentationLetterTitle.Trim();
        string body = string.IsNullOrWhiteSpace(offer.PresentationLetterText)
            ? "Estimados señores:\r\n\r\nJunto con saludar, presentamos nuestra oferta técnica y comercial para el proyecto indicado."
            : offer.PresentationLetterText.Trim();

        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new Document(new Body());
        Body docBody = main.Document.Body!;

        AddParagraph(docBody, offer.CompanyName, 22, true, JustificationValues.Left);
        AddParagraph(docBody, BuildContactLine(offer), 16, false, JustificationValues.Left);
        AddParagraph(docBody, "", 8, false, JustificationValues.Left);
        AddParagraph(docBody, title.ToUpperInvariant(), 30, true, JustificationValues.Center);
        AddParagraph(docBody, "", 8, false, JustificationValues.Left);

        AddInfo(docBody, "Proyecto", context.ProjectTitle);
        AddInfo(docBody, "Cliente", context.ClientName);
        AddInfo(docBody, "Ubicación", context.Location);
        AddInfo(docBody, "Oferta N°", offer.OfferNumber);
        AddInfo(docBody, "Fecha", (offer.OfferDate == default ? DateTime.Today : offer.OfferDate).ToString("dd/MM/yyyy"));
        AddParagraph(docBody, "", 8, false, JustificationValues.Left);

        foreach (string paragraph in body.Replace("\r", "").Split('\n'))
            AddParagraph(docBody, string.IsNullOrWhiteSpace(paragraph) ? "" : paragraph.Trim(), 22, false, JustificationValues.Both);

        AddParagraph(docBody, "", 22, false, JustificationValues.Left);
        AddParagraph(docBody, "Atentamente,", 22, false, JustificationValues.Left);
        AddParagraph(docBody, "", 22, false, JustificationValues.Left);
        AddParagraph(docBody, offer.SignatureName, 22, true, JustificationValues.Left);
        AddParagraph(docBody, offer.SignatureRole, 18, false, JustificationValues.Left);
        AddParagraph(docBody, offer.CompanyName, 18, false, JustificationValues.Left);

        var section = new SectionProperties(
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin { Top = 1200, Right = 1100U, Bottom = 1200, Left = 1100U, Header = 720U, Footer = 720U, Gutter = 0U });
        docBody.Append(section);
        main.Document.Save();
    }

    private static string BuildContactLine(OfferPdfSettings offer)
    {
        return string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(offer.CompanyRut) ? "" : $"RUT: {offer.CompanyRut.Trim()}",
            offer.CompanyAddress,
            offer.CompanyPhone,
            offer.CompanyEmail,
            offer.CompanyWebsite
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static void AddInfo(Body body, string label, string value)
    {
        AddParagraph(body, $"{label}: {Safe(value)}", 20, false, JustificationValues.Left);
    }

    private static void AddParagraph(Body body, string text, int fontHalfPoints, bool bold, JustificationValues justification)
    {
        var runProperties = new RunProperties(
            new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
            new FontSize { Val = fontHalfPoints.ToString() });
        if (bold) runProperties.Append(new Bold());

        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = justification },
                new SpacingBetweenLines { After = "160", Line = "276", LineRule = LineSpacingRuleValues.Auto }),
            new Run(runProperties, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }));
        body.Append(paragraph);
    }

    private static string Safe(string? text) => string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
}
