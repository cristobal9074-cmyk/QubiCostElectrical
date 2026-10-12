using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace QubiCostElectrical;

public sealed class QuestPdfCubicacionReportExporter : ICubicacionReportPdfExporter
{
    private const string CNavy = "#0B2442";
    private const string CBlue = "#1565C0";
    private const string CLight = "#E8F0FE";
    private const string CText = "#1A2332";
    private const string CMuted = "#5A6472";
    private const string CLine = "#C8D8E8";
    private const string CRowAlt = "#F7F9FC";
    private const string CHead = "#F0F5FB";
    private const string CWhite = "#FFFFFF";

    private readonly ICurrencyConversionService currencyConversion;

    public QuestPdfCubicacionReportExporter(ICurrencyConversionService currencyConversion)
    {
        this.currencyConversion = currencyConversion;
    }

    public void Export(ExportProjectContext ctx, string path)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta vacía.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        QuestPdfBootstrap.EnsureCommunityLicense();

        var cultureES = CultureInfo.GetCultureInfo("es-CL");
        string UF(decimal v) => v.ToString("#,##0.000", cultureES);
        string N(decimal v) => v.ToString("#,##0.###", cultureES);
        string Money(decimal uf) => currencyConversion.FormatMoneyFromUf(
            uf, ctx.CurrencyLabel, ctx.Project.ValorUfClp, ctx.Project.TipoCambioUsdClp);
        bool muestraUf = ctx.MuestraUf;
        string MontoUf(decimal uf) => muestraUf ? UF(uf) : Money(uf);
        string S(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s.Trim();

        var rowsByItem = ctx.Cubicacion
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId) && (!string.IsNullOrWhiteSpace(x.Codigo) || x.Cantidad != 0m))
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        Document.Create(composer =>
        {
            // ─── PORTADA ──────────────────────────────────────────────────────
            composer.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);

                page.Content().Column(col =>
                {
                    col.Item().Height(140).Background(CNavy)
                        .PaddingLeft(40).PaddingTop(36).Column(c =>
                    {
                        c.Item().Text(t =>
                            t.Span("MEMORIA DE CUBICACIÓN").FontSize(20).Bold().FontColor(CWhite));
                        c.Item().PaddingTop(8).Text(t =>
                            t.Span($"{ctx.ProjectTitle}  ·  {ctx.ClientName}")
                             .FontSize(10).FontColor("#8AADCC"));
                    });

                    col.Item().PaddingHorizontal(40).PaddingTop(40).Column(c =>
                    {
                        c.Item().Text(t =>
                            t.Span("MEMORIA DE CUBICACIÓN").FontSize(22).Bold().FontColor(CNavy));
                        c.Item().PaddingTop(12).LineHorizontal(1).LineColor(CLine);

                        c.Item().PaddingTop(20).Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                CoverField(left, "Proyecto", ctx.ProjectTitle);
                                CoverField(left, "Cliente", ctx.ClientName);
                                CoverField(left, "Ubicación", ctx.Location);
                            });
                            row.ConstantItem(24);
                            row.RelativeItem().Column(right =>
                            {
                                CoverField(right, "Moneda", ctx.CurrencyLabel);
                                CoverField(right, "Fecha emisión", ctx.ExportedAt.ToString("dd/MM/yyyy HH:mm"));
                            });
                        });
                    });

                    col.Item().PaddingTop(32).PaddingHorizontal(40)
                        .Background(CLight).Padding(20).Column(box =>
                    {
                        box.Item().Text(t =>
                            t.Span("Documento técnico · Revisión interna").FontSize(8).Bold().FontColor(CBlue));
                        box.Item().PaddingTop(4).Text(t =>
                            t.Span("Este reporte resume, por partida y área, los materiales y cantidades considerados en la cubicación.")
                             .FontSize(9).FontColor(CMuted).LineHeight(1.4f));
                        box.Item().PaddingTop(4).Text(t =>
                            t.Span("Es un documento de revisión técnica para constructora/mandante.")
                             .FontSize(9).FontColor(CMuted));
                    });
                });

                page.Footer().Height(28).Background(CNavy)
                    .PaddingHorizontal(40).AlignMiddle()
                    .Text(t => t.Span("Qubi Cost Electrical — Memoria de Cubicación")
                         .FontSize(8).FontColor(CWhite));
            });

            // ─── PÁGINAS POR PARTIDA ──────────────────────────────────────────
            var partidas = ctx.Itemizado.Where(x => !x.EsTitulo).ToList();
            foreach (var item in partidas)
            {
                rowsByItem.TryGetValue(item.Id, out var cubRows);
                cubRows ??= new();
                if (cubRows.Count == 0) continue;

                var orderedRows = cubRows.OrderBy(x => x.Area).ThenBy(x => x.Codigo).ToList();
                decimal totalUf = orderedRows.Sum(x => x.UfTotal);

                composer.Page(page =>
                {
                    SetupPage(page, ctx);
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        col.Item().Text(t =>
                            t.Span($"PARTIDA {S(item.Item)} · {Short(item.Descripcion, 72)}")
                             .FontSize(11).Bold().FontColor(CNavy));
                        col.Item().PaddingTop(4).Text(t =>
                            t.Span($"Unidad: {S(item.Unidad)}  ·  Cantidad: {N(item.Cantidad)}  ·  Subtotal: {(muestraUf ? "UF " : "")}{MontoUf(totalUf)}")
                             .FontSize(8).FontColor(CMuted));

                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(4);
                                c.ConstantColumn(24);
                                c.ConstantColumn(50);
                                c.ConstantColumn(56);
                                c.ConstantColumn(56);
                            });

                            table.Header(h =>
                            {
                                TH(h.Cell().PaddingHorizontal(4).PaddingVertical(3), "Área");
                                TH(h.Cell().PaddingHorizontal(4).PaddingVertical(3), "Código");
                                TH(h.Cell().PaddingHorizontal(4).PaddingVertical(3), "Material");
                                TH(h.Cell().PaddingHorizontal(4).PaddingVertical(3), "UD");
                                TH(h.Cell().PaddingHorizontal(4).PaddingVertical(3), "Cant.", right: true);
                                TH(h.Cell().PaddingHorizontal(4).PaddingVertical(3), muestraUf ? "Unit UF" : "Unit $", right: true);
                                TH(h.Cell().PaddingHorizontal(4).PaddingVertical(3), muestraUf ? "Total UF" : "Total $", right: true);
                            });

                            bool alt = false;
                            int lineIdx = 0;
                            foreach (var line in orderedRows)
                            {
                                string bg = alt ? CRowAlt : CWhite;
                                TD(table.Cell().Background(bg).PaddingHorizontal(4).PaddingVertical(3), Short(line.Area, 14));
                                TD(table.Cell().Background(bg).PaddingHorizontal(4).PaddingVertical(3), Short(line.Codigo, 12));
                                TD(table.Cell().Background(bg).PaddingHorizontal(4).PaddingVertical(3), Short(line.Descripcion, 38));
                                TD(table.Cell().Background(bg).PaddingHorizontal(4).PaddingVertical(3), line.Unidad);
                                TD(table.Cell().Background(bg).PaddingHorizontal(4).PaddingVertical(3), N(line.Cantidad), right: true);
                                TD(table.Cell().Background(bg).PaddingHorizontal(4).PaddingVertical(3), MontoUf(line.UfUnit), right: true);
                                TD(table.Cell().Background(bg).PaddingHorizontal(4).PaddingVertical(3), MontoUf(line.UfTotal), right: true);

                                if (lineIdx < 5)
                                {
                                    string expr = BuildExpressionSummary(ctx, line);
                                    if (!string.IsNullOrWhiteSpace(expr) && expr != "Cantidad directa")
                                    {
                                        table.Cell().ColumnSpan(7).Background(bg).PaddingLeft(8).PaddingVertical(1)
                                            .Text(t => t.Span($"  Calc: {Short(expr, 108)}").FontSize(6.5f).FontColor(CMuted).Italic());
                                    }
                                    string rules = BuildRuleSummary(line);
                                    if (!string.IsNullOrWhiteSpace(rules) && rules != "Global")
                                    {
                                        table.Cell().ColumnSpan(7).Background(bg).PaddingLeft(8).PaddingVertical(1)
                                            .Text(t => t.Span($"  Reglas: {Short(rules, 108)}").FontSize(6.5f).FontColor(CMuted).Italic());
                                    }
                                }
                                alt = !alt;
                                lineIdx++;
                            }
                        });
                    });
                });
            }

            // ─── RESUMEN POR ÁREA ─────────────────────────────────────────────
            composer.Page(page =>
            {
                SetupPage(page, ctx);
                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Item().Text(t =>
                        t.Span("RESUMEN POR ÁREA").FontSize(14).Bold().FontColor(CNavy));
                    col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(CNavy);

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            if (muestraUf)
                                c.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            TH(h.Cell().PaddingHorizontal(5).PaddingVertical(4), "Área");
                            TH(h.Cell().PaddingHorizontal(5).PaddingVertical(4), "Líneas", right: true);
                            TH(h.Cell().PaddingHorizontal(5).PaddingVertical(4), "Mat.", right: true);
                            TH(h.Cell().PaddingHorizontal(5).PaddingVertical(4), muestraUf ? "Total UF" : "Total " + ctx.Currency.EncabezadoPrincipal, right: true);
                            if (muestraUf)
                                TH(h.Cell().PaddingHorizontal(5).PaddingVertical(4), ctx.CurrencyLabel, right: true);
                        });

                        bool alt = false;
                        foreach (var area in ctx.Cubicacion
                            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo) || x.Cantidad != 0m)
                            .GroupBy(x => string.IsNullOrWhiteSpace(x.Area) ? "General" : x.Area.Trim())
                            .OrderByDescending(g => g.Sum(x => x.UfTotal))
                            .Take(40))
                        {
                            decimal totalAreaUf = area.Sum(x => x.UfTotal);
                            string bg = alt ? CRowAlt : CWhite;
                            TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(4), Short(area.Key, 40));
                            TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(4), area.Count().ToString("N0"), right: true);
                            TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(4), area.Select(x => x.Codigo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString("N0"), right: true);
                            TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(4), MontoUf(totalAreaUf), right: true);
                            if (muestraUf)
                                TD(table.Cell().Background(bg).PaddingHorizontal(5).PaddingVertical(4), Money(totalAreaUf), right: true);
                            alt = !alt;
                        }
                    });
                });
            });

        }).GeneratePdf(path);
    }

    private static void TH(IContainer c, string text, bool right = false)
    {
        var container = right ? c.AlignRight() : c;
        container.Text(t => t.Span(text).FontSize(7.5f).Bold().FontColor(CWhite));
    }

    private static void TD(IContainer c, string text, bool right = false)
    {
        var container = right ? c.AlignRight() : c;
        container.Text(t => t.Span(text).FontSize(7.5f).FontColor(CText));
    }

    private static void CoverField(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingTop(10).Column(c =>
        {
            c.Item().Text(t => t.Span(label).FontSize(7).FontColor(CMuted).Bold());
            c.Item().PaddingTop(3).Text(t => t.Span(value).FontSize(10).FontColor(CText));
        });
    }

    private void SetupPage(PageDescriptor page, ExportProjectContext ctx)
    {
        page.Size(PageSizes.A4);
        page.MarginTop(0);
        page.MarginBottom(44);
        page.MarginHorizontal(40);
        page.DefaultTextStyle(s => s.FontSize(9).FontColor(CText));

        page.Header().Height(38).Background(CNavy).Row(row =>
        {
            row.RelativeItem().PaddingLeft(40).AlignMiddle()
                .Text(t => t.Span("Qubi Cost Electrical").FontSize(8).FontColor(CWhite));
            row.RelativeItem().PaddingRight(40).AlignMiddle().AlignRight()
                .Text(t => t.Span($"{ctx.ProjectTitle}  ·  {ctx.ClientName}")
                     .FontSize(7.5f).FontColor("#8AADCC"));
        });

        page.Footer().Height(24).Row(row =>
        {
            row.RelativeItem().AlignMiddle()
                .Text(t => t.Span("Memoria de Cubicación").FontSize(7).FontColor(CMuted));
            row.RelativeItem().AlignMiddle().AlignRight().Text(t =>
            {
                t.CurrentPageNumber().FontSize(7).FontColor(CMuted);
                t.Span(" / ").FontSize(7).FontColor(CMuted);
                t.TotalPages().FontSize(7).FontColor(CMuted);
            });
        });
    }

    private static string BuildExpressionSummary(ExportProjectContext ctx, CubicacionRow line)
    {
        var expressions = line.QtyExpressions ?? [];
        if ((expressions.Length == 0 || expressions.All(string.IsNullOrWhiteSpace)) && string.IsNullOrWhiteSpace(line.TotalExpression))
            return "Cantidad directa";
        ctx.QtyHeadersByItem.TryGetValue(line.ItemId, out var headers);
        var parts = expressions
            .Select((expr, i) => new { Expr = (expr ?? "").Trim(), Index = i })
            .Where(x => !string.IsNullOrWhiteSpace(x.Expr))
            .Take(8)
            .Select(x =>
            {
                string label = headers != null && x.Index < headers.Length && !string.IsNullOrWhiteSpace(headers[x.Index])
                    ? headers[x.Index] : $"Q{x.Index + 1}";
                return $"{label}: {x.Expr}";
            }).ToList();
        if (!string.IsNullOrWhiteSpace(line.TotalExpression))
            parts.Add("Total: " + line.TotalExpression.Trim());
        return string.Join(" | ", parts);
    }

    private static string BuildRuleSummary(CubicacionRow line)
    {
        var rules = new List<string>();
        if (line.MermaPctOverride >= 0m) rules.Add($"Merma {line.MermaPctOverride:0.##}%");
        if (line.HerramientasPctOverride >= 0m) rules.Add($"Herr. {line.HerramientasPctOverride:0.##}%");
        if (line.RedondeoMultiplo > 0m) rules.Add($"Redondeo {line.RedondeoModo} {line.RedondeoMultiplo:0.###}");
        return rules.Count == 0 ? "Global" : string.Join(" · ", rules);
    }

    private static string Short(string text, int max)
    {
        text = (text ?? "").Trim();
        return text.Length <= max ? text : text[..Math.Max(0, max - 1)] + "...";
    }
}
