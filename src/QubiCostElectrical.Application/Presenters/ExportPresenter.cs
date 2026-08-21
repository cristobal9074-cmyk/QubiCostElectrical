using System;

namespace QubiCostElectrical;

public sealed class ExportPresenterServices
{
    public DashboardPresenterServices Dashboard { get; }

    public ExportPresenterServices(DashboardPresenterServices dashboard)
    {
        Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
    }
}

public sealed class ExportPresenter
{
    private readonly ExportPresenterServices _services;
    private readonly BudgetProjectSession _session;
    private readonly DashboardPresenter _dashboard;

    public ExportPresenter(ExportPresenterServices services, BudgetProjectSession session)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dashboard = new DashboardPresenter(services.Dashboard, null);
    }

    public IReadOnlyList<ExportPartidaOption> BuildPartidaOptions()
        => ExportScopeService.BuildPartidaOptions(_session.CreateSnapshot());

    public IReadOnlyList<string> BuildAreaOptions()
        => ExportScopeService.BuildAreaOptions(_session.CreateSnapshot());

    public ExportProjectContext BuildContext(
        ExportScopeMode scopeMode,
        string? partidaId,
        string? area,
        OfferPdfSettings offerSettings,
        ExportCorporateThemeSettings corporateTheme,
        ExportReportTemplateSettings reportTemplate)
    {
        var snapshot = _session.CreateSnapshot();
        var scopedData = ExportScopeService.ApplyScope(snapshot, scopeMode, partidaId, area);
        scopedData.Proyecto.OfertaPdf = offerSettings;
        var dashboard = _dashboard.BuildPageState(scopedData, _session.IsDirty, _session.StateVersion);

        return new ExportProjectContext
        {
            Data = scopedData,
            CurrentProjectPath = _session.CurrentProjectPath,
            ExportedBy = _session.CurrentUserName,
            ExportedAt = AppClock.Now,
            OfferNetAmountUf = dashboard.Cost.TotalVentaNetoUf,
            ReviewSummary = dashboard.Alerts,
            CorporateTheme = corporateTheme,
            ReportTemplate = reportTemplate
        };
    }
}
