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

        decimal netUf = ctx.OfferNetAmountUf > 0m ? ctx.OfferNetAmountUf
                      : cost.TotalVentaNetoUf  > 0m ? cost.TotalVentaNetoUf
                      : cost.TotalVentaUf;

        var offer   = ctx.OfferSettings;
        var template = ctx.ReportTemplate;
        var project = ctx.Project;
        var date    = offer.OfferDate == default ? DateTime.Today : offer.OfferDate;
        var valid   = date.AddDays(Math.Max(1, offer.ValidityDays));
        var fechaMaxima = ctx.Materiales.Where(m => m.FechaPrecio != default).Select(m => m.FechaPrecio).DefaultIfEmpty(date).Max();
        string folio = BuildFolio(ctx, date);
        string hash  = BuildHash(ctx, cost, netUf, folio);

        var cultureES = CultureInfo.GetCultureInfo("es-CL");
        string UF(decimal v)  => v.ToString("#,##0.000", cultureES);
        string N(decimal v)   => v.ToString("N0", cultureES);
        string Money(decimal uf) => currencyConversion.FormatMoneyFromUf(
            uf, ctx.CurrencyLabel, project.ValorUfClp, project.TipoCambioUsdClp);
        string S(string? s)   => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
        string DateLong(DateTime d) => d.ToString("d 'de' MMMM 'de' yyyy", cultureES);

        var offerRows = BuildOfferRows(ctx, cost, netUf);
        var areas     = budgetCalculation.GetAreaTotals(ctx.Cubicacion).ToList();

        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(offer.LogoPath) && File.Exists(offer.LogoPath))
        {
            try { logoBytes = File.ReadAllBytes(offer.LogoPath); }
            catch (Exception ex)
            {
                // El archivo existe (ya se filtró con File.Exists) pero no se pudo leer — bloqueado,
                // corrupto, permisos, etc. Antes esto se tragaba en silencio y el PDF salía sin
                // logo sin ningún aviso; ahora al menos queda en el log para poder diagnosticarlo.
                AppLog.Warning("Pdf", $"No se pudo leer el logo '{offer.LogoPath}' para la oferta", ex);
                logoBytes = null;
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
                        e.Text(t => t.Span(string.IsNullOrWhiteSpace(offer.CompanyName)
                            ? "Qubi Cost Electrical"
                            : S(offer.CompanyName)).FontSize(8).FontColor(CWhite));
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

        static void PageTitle(ColumnDescriptor col, string title)
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
                    RenderCover(page.Content(), ctx, offer, template, logoBytes, coverTitle, coverSubtitle, netUf, folio, hash, date, valid, cultureES, signatureBytes, coverLayout);
                });
            }

            if (offer.IncludePresentationLetter && !string.IsNullOrWhiteSpace(offer.PresentationLetterText))
            {
                var letterTitle = string.IsNullOrWhiteSpace(offer.PresentationLetterTitle)
                    ? "Carta de Presentación"
                    : offer.PresentationLetterTitle.Trim();
                var letterText  = offer.PresentationLetterText!;

                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, letterTitle);
                        col.Item().PaddingTop(14).Text(t =>
                            t.Span(letterText).FontSize(9.5f).FontColor(CText).LineHeight(1.6f));
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
                                SummaryField(c, "Proyecto",   ctx.ProjectTitle);
                                SummaryField(c, "Cliente",    ctx.ClientName);
                                SummaryField(c, "Referencia", S(offer.Reference));
                                SummaryField(c, "N° Oferta",  $"{S(offer.OfferNumber)} · {S(offer.RevisionTag)}");
                            });
                            row.ConstantItem(24);
                            row.RelativeItem().Column(c =>
                            {
                                SummaryField(c, "Fecha",          DateLong(date));
                                if (template.ShowPriceValidity)
                                    SummaryField(c, "Validez",    $"{offer.ValidityDays} días · hasta {valid:dd/MM/yyyy}");
                                SummaryField(c, "Plazo ejec.",    S(offer.ProjectDuration));
                                SummaryField(c, "Estado",         ctx.BudgetStatus);
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
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Concepto");
                                TH(h.Cell().Background(CNavy).Padding(5), "UF",             right: true);
                                TH(h.Cell().Background(CNavy).Padding(5), ctx.CurrencyLabel, right: true);
                            });

                            void CostRow(string label, decimal uf, bool alt, bool bold = false, bool highlight = false)
                            {
                                string bg = highlight ? CLight : (alt ? CRowAlt : CWhite);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), label,      bold: bold);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), UF(uf),     right: true, bold: bold);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), Money(uf),  right: true, bold: bold);
                            }

                            CostRow("Materiales",        cost.MaterialesUf,       false);
                            CostRow("Mano de obra",      cost.ManoObraUf,         true);
                            CostRow("Equipos",           cost.EquiposUf,          false);
                            CostRow("Subcontratos",      cost.SubcontratosUf,     true);
                            CostRow("Herramientas/mermas", cost.HerramientasUf + cost.MermasUf, false);
                            CostRow("Costo directo",     cost.CostoDirectoUf,     true,  bold: true);
                            CostRow("Gastos generales",  cost.GastosGeneralesUf,  false);
                            CostRow("Utilidad",          cost.UtilidadUf,         true);
                            CostRow("TOTAL NETO OFERTA", netUf,                   false, bold: true, highlight: true);
                        });

                        if (cost.UfM2 > 0m)
                            col.Item().PaddingTop(6).Text(t =>
                                t.Span($"UF/m² referencial: {cost.UfM2:N3}")
                                 .FontSize(8).FontColor(CMuted).Italic());

                        col.Item().PaddingTop(16)
                            .Background(CHead).BorderLeft(3).BorderColor(CNavy).Padding(12)
                            .Text(t => t.Span("Este resumen corresponde a la valorización comercial del alcance indicado. El detalle de cubicación y APU queda como respaldo interno.")
                                 .FontSize(8).FontColor(CMuted).LineHeight(1.4f));
                    });
                });
            }
            // Text pages
            if (template.ShowTermsAndConditions)
            {
            foreach (var section in new[]
            {
                (Title: "Alcance Incluido",         Text: offer.ScopeText),
                (Title: "Exclusiones",              Text: offer.ExclusionsText),
                (Title: "Condiciones Comerciales",  Text: offer.CommercialConditionsText),
                (Title: "Observaciones",            Text: offer.ObservationsText)
            })
            {
                if (string.IsNullOrWhiteSpace(section.Text)) continue;
                var sTitle = section.Title;
                var sText  = section.Text;
                composer.Page(page =>
                {
                    SetupPage(page);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        PageTitle(col, sTitle);
                        col.Item().PaddingTop(14).Text(t =>
                            t.Span(sText).FontSize(9.5f).FontColor(CText).LineHeight(1.6f));
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
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(38);    // Ítem
                            c.RelativeColumn(4);     // Descripción
                            c.ConstantColumn(28);    // UD
                            c.ConstantColumn(42);    // Cant.
                            c.ConstantColumn(60);    // UF
                            c.ConstantColumn(72);    // CLP
                        });
                        table.Header(h =>
                        {
                            TH(h.Cell().Background(CNavy).Padding(5), "Ítem");
                            TH(h.Cell().Background(CNavy).Padding(5), "Descripción");
                            TH(h.Cell().Background(CNavy).Padding(5), "UD");
                            TH(h.Cell().Background(CNavy).Padding(5), "Cant.",          right: true);
                            TH(h.Cell().Background(CNavy).Padding(5), "UF",             right: true);
                            TH(h.Cell().Background(CNavy).Padding(5), ctx.CurrencyLabel, right: true);
                        });

                        bool alt = false;
                        foreach (var r in offerRows)
                        {
                            if (r.EsTitulo)
                            {
                                table.Cell().ColumnSpan(6)
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
                                TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), r.TotalUf > 0m ? UF(r.TotalUf) : "",   right: true);
                                TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(3), r.TotalUf > 0m ? Money(r.TotalUf) : "", right: true);
                                alt = !alt;
                            }
                        }

                        // Fila de total
                        table.Cell().ColumnSpan(4).Background(CNavy).PaddingVertical(6)
                            .Text(t => t.Span("TOTAL NETO").FontSize(8).Bold().FontColor(CWhite));
                        TDNavy(table.Cell().Background(CNavy).PaddingHorizontal(5).PaddingVertical(6), $"UF {UF(netUf)}", right: true);
                        TDNavy(table.Cell().Background(CNavy).PaddingHorizontal(5).PaddingVertical(6), Money(netUf),      right: true);
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
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                TH(h.Cell().Background(CNavy).Padding(5), "Área");
                                TH(h.Cell().Background(CNavy).Padding(5), "UF",             right: true);
                                TH(h.Cell().Background(CNavy).Padding(5), ctx.CurrencyLabel, right: true);
                            });
                            bool alt = false;
                            foreach (var a in areas)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), a.Area);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), UF(a.Total),    right: true);
                                TD(table.Cell().Background(bg).PaddingHorizontal(6).PaddingVertical(4), Money(a.Total), right: true);
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
                                c.Item().Height(50).AlignLeft()
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
                                t.Span(S(offer.CompanyName)).FontSize(8).FontColor(CMuted));
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
        string coverTitle,
        string coverSubtitle,
        decimal netUf,
        string folio,
        string hash,
        DateTime date,
        DateTime valid,
        CultureInfo cultureES,
        byte[]? signatureBytes,
        ReportTemplateCoverLayout layout)
    {
        content.Column(col =>
        {
            col.Item().Height(180).Background(CNavy)
                .PaddingLeft(40).PaddingRight(40).PaddingTop(28).Column(header =>
                {
                    if (layout.ShowLogo && logoBytes != null)
                    {
                        header.Item().Height(56).AlignLeft().Image(logoBytes).FitHeight();
                    }
                    else
                    {
                        header.Item().Text(t =>
                            t.Span(SafeText(offer.CompanyName)).FontSize(18).Bold().FontColor(CWhite));
                    }

                    header.Item().PaddingTop(10).Text(t =>
                        t.Span(SafeText(offer.CompanyName)).FontSize(11).FontColor("#8AADCC"));
                    header.Item().PaddingTop(8).Text(t =>
                        t.Span($"{SafeText(offer.CompanyRut)}  ·  {SafeText(offer.CompanyAddress)}")
                         .FontSize(9).FontColor("#8AADCC"));
                    header.Item().PaddingTop(3).Text(t =>
                        t.Span($"{SafeText(offer.CompanyPhone)}  ·  {SafeText(offer.CompanyEmail)}")
                         .FontSize(9).FontColor("#8AADCC"));
                    if (!string.IsNullOrWhiteSpace(offer.CompanyWebsite))
                        header.Item().PaddingTop(3).Text(t =>
                            t.Span(offer.CompanyWebsite!).FontSize(9).FontColor("#8AADCC"));
                });

            col.Item().PaddingHorizontal(40).PaddingTop(40).Column(body =>
            {
                body.Item().Text(t =>
                    t.Span(SafeText(coverTitle).ToUpperInvariant())
                     .FontSize(22).Bold().FontColor(CNavy));
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

                RenderCoverSections(body, ctx, offer, template, netUf, folio, hash, date, valid, cultureES, signatureBytes, layout);
            });
        });
    }

    private void RenderCoverSections(
        ColumnDescriptor col,
        ExportProjectContext ctx,
        OfferPdfSettings offer,
        ExportReportTemplateSettings template,
        decimal netUf,
        string folio,
        string hash,
        DateTime date,
        DateTime valid,
        CultureInfo cultureES,
        byte[]? signatureBytes,
        ReportTemplateCoverLayout layout)
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
                    RenderCoverSectionCard(c, left, ctx, offer, template, netUf, folio, hash, date, valid, cultureES, signatureBytes));

                if (right.HasValue)
                {
                    row.ConstantItem(18);
                    row.RelativeItem().Element(c =>
                        RenderCoverSectionCard(c, right.Value, ctx, offer, template, netUf, folio, hash, date, valid, cultureES, signatureBytes));
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
        string folio,
        string hash,
        DateTime date,
        DateTime valid,
        CultureInfo cultureES,
        byte[]? signatureBytes)
    {
        container.Background(CWhite).Border(1).BorderColor(CLine).Padding(14).Column(card =>
        {
            card.Item().Text(t =>
                t.Span(CoverSectionTitle(section)).FontSize(10).Bold().FontColor(CNavy));

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
                    CoverField(card, "Empresa", SafeText(offer.CompanyName));
                    CoverField(card, "Referencia", SafeText(offer.Reference));
                    break;

                case ReportCoverSectionKind.Summary:
                    card.Item().PaddingTop(6).Text(t =>
                        t.Span($"UF {netUf.ToString("#,##0.000", cultureES)}").FontSize(24).Bold().FontColor(CNavy));
                    card.Item().PaddingTop(2).Text(t =>
                        t.Span(currencyConversion.FormatMoneyFromUf(netUf, ctx.CurrencyLabel, ctx.Project.ValorUfClp, ctx.Project.TipoCambioUsdClp))
                         .FontSize(12).FontColor(CBlue));
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
                        card.Item().PaddingTop(10).Height(42).Image(signatureBytes).FitHeight();
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

    private static string CoverSectionTitle(ReportCoverSectionKind section) => section switch
    {
        ReportCoverSectionKind.Project => "Proyecto",
        ReportCoverSectionKind.Client => "Cliente",
        ReportCoverSectionKind.Summary => "Resumen UF",
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
        public string  Item        { get; set; } = "";
        public string  Descripcion { get; set; } = "";
        public string  Unidad      { get; set; } = "";
        public decimal Cantidad    { get; set; }
        public decimal TotalUf     { get; set; }
        public bool    EsTitulo    { get; set; }
        public string  Observaciones { get; set; } = "";
    }
}
