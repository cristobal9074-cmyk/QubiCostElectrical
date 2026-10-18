using System;
using System.Collections.ObjectModel;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Delivery;

public sealed class DeliveryDocumentItemVm
{
    private bool isSelected;

    public DeliveryDocumentItemVm(string id, string title, string description, string formatHint, bool isPrimary = false, bool isDefaultSelected = false)
    {
        Id = id;
        Title = title;
        Description = description;
        FormatHint = formatHint;
        IsPrimary = isPrimary;
        isSelected = isDefaultSelected;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string FormatHint { get; }
    public bool IsPrimary { get; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectionChanged;
}

public sealed class DeliveryDocumentSelectorVm
{
    private readonly DashboardPresenter dashboard;
    public ObservableCollection<DeliveryDocumentItemVm> Documents { get; } = new();
    public ObservableCollection<DeliveryDocumentItemVm> PrimaryDocuments { get; } = new();
    public ObservableCollection<DeliveryDocumentItemVm> OptionalDocuments { get; } = new();
    public event EventHandler? SelectionChanged;

    public DeliveryDocumentSelectorVm(DashboardPresenter dashboard)
    {
        this.dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));

        // Documentos principales para el mandante (por defecto SOLO Carta PDF y Resumen Excel)
        AddDoc(new DeliveryDocumentItemVm(
            "OfferLetter",
            "Carta de oferta PDF",
            "Carta de oferta formal con logo, planos de proyecto, alcances, condiciones comerciales y valor total.",
            "PDF",
            isPrimary: true,
            isDefaultSelected: true));

        AddDoc(new DeliveryDocumentItemVm(
            "ResumenExcel",
            "Resumen presupuesto Excel",
            "Hoja de resumen para el mandante formato Ingelsam con logo, datos clientes, partidas, fórmulas vivas e IVA.",
            "XLSX",
            isPrimary: true,
            isDefaultSelected: true));

        AddDoc(new DeliveryDocumentItemVm(
            "PartidasAbiertasExcel",
            "Presupuesto con partidas abiertas Excel",
            "Desglose detallado partida por partida con sus materiales, cubicación y fórmulas vivas (formato DETALLE).",
            "XLSX",
            isPrimary: true,
            isDefaultSelected: false));

        AddDoc(new DeliveryDocumentItemVm(
            "LetterDocx",
            "Carta de oferta Word",
            "Carta de oferta formal en formato DOCX editable para el mandante.",
            "DOCX",
            isPrimary: true,
            isDefaultSelected: false));

        // Documentos opcionales y técnicos (desmarcados por defecto)
        AddDoc(new DeliveryDocumentItemVm(
            "OfferPdf",
            "Oferta comercial PDF",
            "Documento principal con portada, totales, alcance, exclusiones y condiciones.",
            "PDF",
            isPrimary: false,
            isDefaultSelected: false));

        AddDoc(new DeliveryDocumentItemVm(
            "ResumenPdf",
            "Resumen presupuesto PDF",
            "Resumen ejecutivo formal del presupuesto para mandante en formato PDF.",
            "PDF",
            isPrimary: false,
            isDefaultSelected: false));

        AddDoc(new DeliveryDocumentItemVm(
            "ControlExcel",
            "Excel interno de control",
            "Libro de control con trazabilidad completa, itemizado y cubicación.",
            "XLSX",
            isPrimary: false,
            isDefaultSelected: false));

        AddDoc(new DeliveryDocumentItemVm(
            "MemoriaExcel",
            "Memoria cubicación Excel",
            "Detalle técnico de cubicación en formato Excel.",
            "XLSX",
            isPrimary: false,
            isDefaultSelected: false));

        AddDoc(new DeliveryDocumentItemVm(
            "MemoriaPdf",
            "Memoria cubicación PDF",
            "Detalle técnico de cubicación en formato PDF.",
            "PDF",
            isPrimary: false,
            isDefaultSelected: false));

        AddDoc(new DeliveryDocumentItemVm(
            "ReporteCSEC",
            "Reporte técnico C.SEC",
            "Reporte de tableros, circuitos, parámetros y materiales.",
            "XLSX",
            isPrimary: false,
            isDefaultSelected: false));
    }

    private void AddDoc(DeliveryDocumentItemVm doc)
    {
        Documents.Add(doc);
        if (doc.IsPrimary)
            PrimaryDocuments.Add(doc);
        else
            OptionalDocuments.Add(doc);

        doc.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public ExportProjectContext GetSelectedContext(BudgetProjectSession session)
    {
        var snapshot = session.CreateSnapshot();
        var dashboardState = dashboard.BuildPageState(snapshot, session.IsDirty, session.StateVersion);
        var prefs = UserPreferencesStore.Load();

        var offerSnapshot = snapshot.Proyecto.OfertaPdf != null
            ? CloneOfferSettings(snapshot.Proyecto.OfertaPdf)
            : new OfferPdfSettings();
        LetterheadApplicator.Apply(prefs, offerSnapshot);
        snapshot.Proyecto.OfertaPdf = offerSnapshot;

        var alerts = dashboardState.Alerts;
        var frozenSummary = new FinalReviewSummary
        {
            Issues = alerts.Issues.ToList()
        };

        return new ExportProjectContext
        {
            Data = snapshot,
            CurrentProjectPath = session.CurrentProjectPath,
            ExportedBy = session.CurrentUserName,
            ExportedAt = AppClock.Now,
            OfferNetAmountUf = dashboardState.Cost.TotalVentaNetoUf,
            ReviewSummary = frozenSummary,
            CorporateTheme = new ExportCorporateThemeSettings
            {
                Enabled = prefs.CorporateTheme.Enabled,
                Name = prefs.CorporateTheme.Name,
                CompanyName = prefs.CorporateTheme.CompanyName,
                LogoPath = prefs.CorporateTheme.LogoPath,
                PrimaryHex = prefs.CorporateTheme.PrimaryHex,
                AccentHex = prefs.CorporateTheme.AccentHex,
                BackgroundHex = prefs.CorporateTheme.BackgroundHex,
                FontFamily = prefs.CorporateTheme.FontFamily
            },
            ReportTemplate = new ExportReportTemplateSettings
            {
                TemplateName = prefs.ReportTemplate.TemplateName,
                CoverTitle = prefs.ReportTemplate.CoverTitle,
                CoverSubtitle = prefs.ReportTemplate.CoverSubtitle,
                ShowDraftWatermark = prefs.ReportTemplate.ShowDraftWatermark,
                ShowCover = prefs.ReportTemplate.ShowCover,
                ShowExecutiveSummary = prefs.ReportTemplate.ShowExecutiveSummary,
                ShowItemizedDetail = prefs.ReportTemplate.ShowItemizedDetail,
                ShowCubicacionDetail = prefs.ReportTemplate.ShowCubicacionDetail,
                ShowPriceValidity = prefs.ReportTemplate.ShowPriceValidity,
                ShowTermsAndConditions = prefs.ReportTemplate.ShowTermsAndConditions,
                ShowCoverTraceability = prefs.ReportTemplate.ShowCoverTraceability,
                OrderedFields = prefs.ReportTemplate.OrderedFields?.ToList() ?? new()
            }
        };
    }

    public string[] GetSelectedDocumentIds()
    {
        return Documents.Where(d => d.IsSelected).Select(d => d.Id).ToArray();
    }

    private static OfferPdfSettings CloneOfferSettings(OfferPdfSettings src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        return System.Text.Json.JsonSerializer.Deserialize<OfferPdfSettings>(json) ?? new OfferPdfSettings();
    }
}
