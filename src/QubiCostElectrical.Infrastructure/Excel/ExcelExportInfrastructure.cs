using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Exportador Excel reusable/testeable. La UI solo arma ExportProjectContext y este servicio genera el archivo.
/// </summary>
public sealed partial class ExcelExportInfrastructure : IExcelProjectExporter
{
    private static readonly Regex FormulaCharRegex = new(@"^[0-9qQzZoOnNaA_\.\s\+\-\*/\^\(\)]+$", RegexOptions.Compiled);
    private static readonly Regex LetterRegex = new(@"[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex[] QRegexes = new Regex[]
    {
        new(@"\bq_?1\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\bq_?2\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\bq_?3\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };
    private static readonly Regex[] ZonaRegexes = new Regex[]
    {
        new(@"\bzona_?1\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\bzona_?2\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\bzona_?3\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    private readonly IBudgetCalculationService budgetCalculation;
    private readonly IFinalReviewService finalReview;
    private readonly IPriceTraceabilityService priceTraceability;
    private readonly ICurrencyConversionService currencyConversion;
    private readonly IPurchaseListService purchaseList = new PurchaseListService();

    private ExportProjectContext ctx = new();
    private CostBreakdown? _cachedCost;
    private FinalReviewSummary? _cachedReview;
}
