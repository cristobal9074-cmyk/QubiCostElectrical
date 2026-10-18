namespace QubiCostElectrical;

public interface IProjectExporter
{
    void Export(ProjectFileData data, string path);
}

public interface IExcelProjectExporter
{
    void Export(ExportProjectContext context, string path);
}

public interface IPdfProjectExporter
{
    void Export(ExportProjectContext context, string path);
}

public interface IPresentationLetterDocxExporter
{
    void Export(ExportProjectContext context, string path);
}

public interface ICubicacionReportExcelExporter
{
    void Export(ExportProjectContext context, string path);
}

public interface ICubicacionReportPdfExporter
{
    void Export(ExportProjectContext context, string path);
}

public interface ICubicacionSCReportExcelExporter
{
    void Export(ExportProjectContext context, string path);
    void ExportCuadroCargasSec(ExportProjectContext context, string path);
}

/// <summary>
/// Genera la Carta de Oferta PDF en formato carta formal (formato carta formal):
/// encabezado con logo, tabla de planos, alcances, condiciones comerciales, valor destacado y firma.
/// Es un documento independiente de la oferta completa (QuestPdfProjectExporter).
/// </summary>
public interface IOfferLetterPdfExporter
{
    void Export(ExportProjectContext context, string path);
}

/// <summary>
/// Genera la hoja de resumen ejecutivo de presupuesto para el mandante en formato Excel
/// idéntica al formato histórico de Ingelsam (logo, datos clientes, partidas, fórmulas vivas e IVA).
/// </summary>
public interface IIngelsamOfferSummaryExcelExporter
{
    void Export(ExportProjectContext context, string path);
}

/// <summary>
/// Genera el resumen ejecutivo del presupuesto en PDF (hoja de cotización mandante).
/// </summary>
public interface IOfferSummaryPdfExporter
{
    void Export(ExportProjectContext context, string path);
}

/// <summary>
/// Genera el libro Excel de Presupuesto con Partidas Abiertas (formato DETALLE),
/// con cada partida y su desglose de materiales, cantidades, precios unitarios y fórmulas vivas.
/// </summary>
public interface IPartidasAbiertasExcelExporter
{
    void Export(ExportProjectContext context, string path);
}


public sealed class ExportCorporateThemeSettings
{
    public bool Enabled { get; set; }
    public string Name { get; set; } = "Corporativo";
    public string CompanyName { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public string PrimaryHex { get; set; } = "#0C3B5E";
    public string AccentHex { get; set; } = "#0F6CBD";
    public string BackgroundHex { get; set; } = "#F7F9FC";
    public string FontFamily { get; set; } = "Segoe UI";
}

public sealed class ExportReportTemplateSettings
{
    public string TemplateName { get; set; } = "Oferta profesional";
    public string CoverTitle { get; set; } = "Oferta comercial";
    public string CoverSubtitle { get; set; } = "Resumen ejecutivo";
    public bool ShowDraftWatermark { get; set; } = true;
    public bool ShowCover { get; set; } = true;
    public bool ShowExecutiveSummary { get; set; } = true;
    public bool ShowItemizedDetail { get; set; } = true;
    public bool ShowCubicacionDetail { get; set; } = false;
    public bool ShowPriceValidity { get; set; } = true;
    public bool ShowTermsAndConditions { get; set; } = true;
    public bool ShowNormativeAssumptions { get; set; } = true;
    public bool ShowChapterDistributionChart { get; set; } = true;
    public bool ShowCoverTraceability { get; set; } = true;
    public List<string> OrderedFields { get; set; } = new()
    {
        "Logo", "Proyecto", "Cliente", "Resumen UF", "Itemizado", "Condiciones", "Firma"
    };
}

public sealed class ExportProjectContext
{
    public ProjectFileData Data { get; set; } = new();
    public string CurrentProjectPath { get; set; } = "";
    public string ExportedBy { get; set; } = "";
    public DateTime ExportedAt { get; set; } = AppClock.Now;
    public decimal SensitivityMaterialPct { get; set; }
    public decimal OfferNetAmountUf { get; set; }
    public FinalReviewSummary? ReviewSummary { get; set; }
    public string ProductVersion => ProductVersionInfo.ProductVersion;
    public int ProjectFormatVersion => ProductVersionInfo.ProjectFormatVersion;
    public int DatabaseSchemaVersion => ProductVersionInfo.DatabaseSchemaVersion;
    public int CalculationEngineVersion => ProductVersionInfo.CalculationEngineVersion;
    public string VersionSummary => ProductVersionInfo.TechnicalSummary;
    public ExportCorporateThemeSettings CorporateTheme { get; set; } = new();
    public ExportReportTemplateSettings ReportTemplate { get; set; } = new();

    public ProjectInfoRow Project => Data?.Proyecto ?? new ProjectInfoRow();
    public IReadOnlyList<ItemizadoRow> Itemizado => Data?.Itemizado ?? new List<ItemizadoRow>();
    public IReadOnlyList<MaterialRow> Materiales => Data?.Materiales ?? new List<MaterialRow>();
    public IReadOnlyList<CubicacionRow> Cubicacion => Data?.Cubicacion ?? new List<CubicacionRow>();
    public IReadOnlyList<ApuRow> Apus => Data?.Apus ?? new List<ApuRow>();
    public IReadOnlyList<ChangeOrderRow> OrdenesCambio => Data?.OrdenesCambio ?? new List<ChangeOrderRow>();
    public IReadOnlyDictionary<string, string[]> QtyHeadersByItem => Data?.QtyHeadersByItem ?? new Dictionary<string, string[]>();

    public string ProjectTitle => string.IsNullOrWhiteSpace(Project.Nombre) ? "Proyecto sin nombre" : Project.Nombre;
    public string ClientName => string.IsNullOrWhiteSpace(Project.Cliente) ? "Cliente sin nombre" : Project.Cliente;
    public string Location => string.IsNullOrWhiteSpace(Project.Ubicacion) ? "Sin ubicación" : Project.Ubicacion;
    public string BudgetStatus => string.IsNullOrWhiteSpace(Project.EstadoPresupuesto) ? "Borrador" : Project.EstadoPresupuesto;
    public bool IsDraft => string.Equals(BudgetStatus, "Borrador", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Moneda de presentación del proyecto. Es la única fuente de verdad para los exportadores:
    /// sale de <see cref="ProjectInfoRow.MonedaPresupuesto"/> y por defecto es CLP (nunca UF).
    /// </summary>
    public string CurrencyLabel =>
        string.IsNullOrWhiteSpace(Project.MonedaPresupuesto) ? Moneda.CLP : Project.MonedaPresupuesto.Trim().ToUpperInvariant();

    /// <summary>
    /// <c>true</c> cuando el proyecto permite mostrar UF (UF/USD/EUR). Con CLP los documentos
    /// exportados no emiten valores ni columnas en UF.
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(CurrencyLabel, CurrencyConversionService.CurrencyClp, StringComparison.OrdinalIgnoreCase);

    /// <summary>Política de moneda compartida por los exportadores PDF y Excel.</summary>
    public ExportCurrencyPresenter Currency => new(Project.MonedaPresupuesto, Project.ValorUfClp, Project.TipoCambioUsdClp);

    public BudgetSettings BudgetSettings => ExcelCostosBridge.EnrichFromProjectData(BudgetSettings.FromProject(Project), Data);
    public OfferPdfSettings OfferSettings => Project.OfertaPdf ?? new OfferPdfSettings();
}
