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
    // Paleta corporativa base
    private const string FallbackNavy = "#0B2442";
    private const string FallbackBlue = "#1565C0";
    private const string CLight = "#E8F0FE";
    private const string CText = "#1A2332";
    private const string CMuted = "#5A6472";
    private const string CLine = "#C8D8E8";
    private const string CRowAlt = "#F7F9FC";
    private const string FallbackHead = "#EBF2FB";
    private const string CWhite = "#FFFFFF";
    private const string FallbackAccent = "#2962CC";

    private readonly IBudgetCalculationService budgetCalculation;
    private readonly ICurrencyConversionService currencyConversion;

    public QuestPdfProjectExporter(
        IBudgetCalculationService budgetCalculation,
        ICurrencyConversionService currencyConversion)
    {
        this.budgetCalculation = budgetCalculation;
        this.currencyConversion = currencyConversion;
    }
}
