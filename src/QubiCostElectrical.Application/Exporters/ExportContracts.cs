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
    public string CurrencyLabel => string.IsNullOrWhiteSpace(Project.MonedaPresupuesto) ? "UF" : Project.MonedaPresupuesto.Trim().ToUpperInvariant();
    public BudgetSettings BudgetSettings => ExcelCostosBridge.EnrichFromProjectData(BudgetSettings.FromProject(Project), Data);
    public OfferPdfSettings OfferSettings => Project.OfertaPdf ?? new OfferPdfSettings();
}
