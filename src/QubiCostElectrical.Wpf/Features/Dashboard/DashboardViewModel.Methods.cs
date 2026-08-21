using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Data;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Features.Project;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public sealed partial class DashboardViewModel : ViewModelBase, IRefreshablePage
{
    public DashboardViewModel(IShellNavigationHost shell, WpfAppHost host)
    {
        this.shell = shell;
        this.host = host;
        recentProjectsService = new RecentProjectsService();
        KpiCards = new ObservableCollection<DashboardKpiCardVm>();
        CostShares = new ObservableCollection<DashboardCostShareVm>();
        WorkflowSteps = new ObservableCollection<DashboardWorkflowStepVm>();
        Alerts = new ObservableCollection<DashboardAlertVm>();
        TopPartidas = new ObservableCollection<DashboardPartidaVm>();
        RecentProjects = new ObservableCollection<DashboardRecentProjectVm>();
        QuickSummaryCards = new ObservableCollection<DashboardMetricVm>();
        RecentAuditEntries = new ObservableCollection<ProjectAuditEntryVm>();

        NewProjectCommand = new RelayCommand(() => _ = shell.NewProjectAsync());
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        continueTakeoffCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Takeoff), () => !ShowEmptyState);
        primaryCtaCommand = new RelayCommand(() => shell.RequestNavigate(primaryCtaRoute), () => !ShowEmptyState);
        goToProjectCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Project), () => !ShowEmptyState);
        goToReviewCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Review), () => !ShowEmptyState);
        goToItemizadoCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Itemized), () => !ShowEmptyState);
        toggleDetailCommand = new RelayCommand(() => IsDetailExpanded = !IsDetailExpanded);
        GoToPriceBankCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Prices));
        GoToTemplatesCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Templates));
        GoToPreferencesCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Preferences));
        OpenRecentProjectCommand = new RelayCommand<string>(path => _ = shell.OpenRecentProjectAsync(path ?? string.Empty), path => !string.IsNullOrWhiteSpace(path));
        ExitApplicationCommand = new RelayCommand(ExitApplication);

        ToggleThemeCommand = new RelayCommand(() =>
        {
            QubiCostElectrical.Wpf.DesignSystem.ThemeManager.Toggle();
            OnPropertyChanged(nameof(IsDarkMode));
        });

        ToggleFilterCommand = new RelayCommand<string>(category =>
        {
            SelectedCategoryFilter = SelectedCategoryFilter == category ? null : category;
        });
    }

    private static void ExitApplication() => Application.Current.Shutdown();

    public ObservableCollection<DashboardKpiCardVm> KpiCards { get; }
    public ObservableCollection<DashboardCostShareVm> CostShares { get; }
    public ObservableCollection<DashboardWorkflowStepVm> WorkflowSteps { get; }
    public ObservableCollection<DashboardAlertVm> Alerts { get; }
    public ObservableCollection<DashboardPartidaVm> TopPartidas { get; }
    public ObservableCollection<DashboardRecentProjectVm> RecentProjects { get; }
    public ObservableCollection<DashboardMetricVm> QuickSummaryCards { get; }
    public string ActiveProjectLabel => shell.ActiveProjectLabel;
    public bool HasOpenProject => host.ProjectWorkspace.HasOpenProject;

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set
        {
            if (!SetProperty(ref showEmptyState, value)) return;
            continueTakeoffCommand.RaiseCanExecuteChanged();
            primaryCtaCommand.RaiseCanExecuteChanged();
            goToProjectCommand.RaiseCanExecuteChanged();
            goToReviewCommand.RaiseCanExecuteChanged();
            goToItemizadoCommand.RaiseCanExecuteChanged();
        }
    }

    public string NextStepTitle
    {
        get => nextStepTitle;
        private set => SetProperty(ref nextStepTitle, value);
    }

    public string NextStepDetail
    {
        get => nextStepDetail;
        private set => SetProperty(ref nextStepDetail, value);
    }

    public string PrimaryCtaLabel
    {
        get => primaryCtaLabel;
        private set => SetProperty(ref primaryCtaLabel, value);
    }

    public int BlockingAlertCount
    {
        get => blockingAlertCount;
        private set => SetProperty(ref blockingAlertCount, value);
    }

    public string PartidasReadyLabel
    {
        get => partidasReadyLabel;
        private set => SetProperty(ref partidasReadyLabel, value);
    }

    public double PartidasReadyPercent
    {
        get => partidasReadyPercent;
        private set => SetProperty(ref partidasReadyPercent, value);
    }

    public bool IsDetailExpanded
    {
        get => isDetailExpanded;
        set => SetProperty(ref isDetailExpanded, value);
    }

    public string TopAlertTitle
    {
        get => topAlertTitle;
        private set => SetProperty(ref topAlertTitle, value);
    }

    public string TopAlertDetail
    {
        get => topAlertDetail;
        private set => SetProperty(ref topAlertDetail, value);
    }

    public bool HasTopAlert
    {
        get => hasTopAlert;
        private set => SetProperty(ref hasTopAlert, value);
    }

    public bool HasRecentProjects
    {
        get => hasRecentProjects;
        private set => SetProperty(ref hasRecentProjects, value);
    }

    public Task? LastRefreshTask { get; private set; }

    public string CostMaterials
    {
        get => costMaterials;
        private set => SetProperty(ref costMaterials, value);
    }

    public string CostLabor
    {
        get => costLabor;
        private set => SetProperty(ref costLabor, value);
    }

    public string CostOtherDirect
    {
        get => costOtherDirect;
        private set => SetProperty(ref costOtherDirect, value);
    }

    public string CostDirect
    {
        get => costDirect;
        private set => SetProperty(ref costDirect, value);
    }

    public string CostTotal
    {
        get => costTotal;
        private set => SetProperty(ref costTotal, value);
    }

    public string CostUfM2
    {
        get => costUfM2;
        private set => SetProperty(ref costUfM2, value);
    }

    public GridLength MaterialShareWidth
    {
        get => materialShareWidth;
        private set => SetProperty(ref materialShareWidth, value);
    }

    public GridLength LaborShareWidth
    {
        get => laborShareWidth;
        private set => SetProperty(ref laborShareWidth, value);
    }

    public GridLength OtherShareWidth
    {
        get => otherShareWidth;
        private set => SetProperty(ref otherShareWidth, value);
    }

    public double GastosGeneralesSimulatedPct
    {
        get => gastosGeneralesSimulatedPct;
        set
        {
            if (SetProperty(ref gastosGeneralesSimulatedPct, value))
            {
                if (!isRefreshing && HasOpenProject && host.Session.GastosGeneralesPct != (decimal)value)
                {
                    host.Session.GastosGeneralesPct = (decimal)value;
                    host.Session.MarkDirty("project_info");
                    Refresh();
                }
            }
        }
    }

    public double UtilidadSimulatedPct
    {
        get => utilidadSimulatedPct;
        set
        {
            if (SetProperty(ref utilidadSimulatedPct, value))
            {
                if (!isRefreshing && HasOpenProject && host.Session.UtilidadPct != (decimal)value)
                {
                    host.Session.UtilidadPct = (decimal)value;
                    host.Session.MarkDirty("project_info");
                    Refresh();
                }
            }
        }
    }

    public string? SelectedCategoryFilter
    {
        get => selectedCategoryFilter;
        set
        {
            if (SetProperty(ref selectedCategoryFilter, value))
            {
                OnPropertyChanged(nameof(FilteredTopPartidas));
                OnPropertyChanged(nameof(IsFilteringMateriales));
                OnPropertyChanged(nameof(IsFilteringLabor));
                OnPropertyChanged(nameof(IsFilteringOtros));
                OnPropertyChanged(nameof(IsAnyFilterActive));
            }
        }
    }

    public IEnumerable<DashboardPartidaVm> FilteredTopPartidas
    {
        get
        {
            if (string.IsNullOrEmpty(SelectedCategoryFilter))
                return TopPartidas;
            return TopPartidas.Where(p => p.Category == SelectedCategoryFilter);
        }
    }

    public bool IsFilteringMateriales => SelectedCategoryFilter == "Materiales";
    public bool IsFilteringLabor => SelectedCategoryFilter == "Mano de Obra";
    public bool IsFilteringOtros => SelectedCategoryFilter == "Otros";
    public bool IsAnyFilterActive => !string.IsNullOrEmpty(SelectedCategoryFilter);

    public ICommand OpenProjectCommand { get; }
    public ICommand ContinueTakeoffCommand => continueTakeoffCommand;
    public ICommand PrimaryCtaCommand => primaryCtaCommand;
    public ICommand ToggleDetailCommand => toggleDetailCommand;
    public ICommand GoToProjectCommand => goToProjectCommand;
    public ICommand GoToReviewCommand => goToReviewCommand;
    public ICommand GoToItemizadoCommand => goToItemizadoCommand;
    public ICommand GoToPriceBankCommand { get; }
    public ICommand GoToTemplatesCommand { get; }
    public ICommand GoToPreferencesCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand NewProjectCommand { get; }
    public ICommand ExitApplicationCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ToggleFilterCommand { get; }
    public ObservableCollection<ProjectAuditEntryVm> RecentAuditEntries { get; }
    public bool IsDarkMode => QubiCostElectrical.Wpf.DesignSystem.ThemeManager.IsDarkMode;
    public string VersionLabel => $"v{ProductVersionInfo.ProductVersion}";

    public void Refresh() => Load();

    public void Load()
    {
        OnPropertyChanged(nameof(ActiveProjectLabel));
        OnPropertyChanged(nameof(HasOpenProject));
        OnPropertyChanged(nameof(IsDarkMode));
        ShowEmptyState = !host.ProjectWorkspace.HasOpenProject;
        if (ShowEmptyState)
        {
            KpiCards.Clear();
            CostShares.Clear();
            WorkflowSteps.Clear();
            Alerts.Clear();
            TopPartidas.Clear();
            RecentAuditEntries.Clear();
            LoadLandingState();
            CostMaterials = CostLabor = CostOtherDirect = CostDirect = CostTotal = CostUfM2 = "—";
            MaterialShareWidth = new GridLength(0);
            LaborShareWidth = new GridLength(0);
            OtherShareWidth = new GridLength(0);
            NextStepTitle = "Abra un proyecto";
            NextStepDetail = "Cree o abra un archivo .qce para ver el resumen ejecutivo.";
            PrimaryCtaLabel = "Abrir proyecto";
            primaryCtaRoute = ShellRoute.Dashboard;
            PartidasReadyLabel = "—";
            BlockingAlertCount = 0;
            HasTopAlert = false;
            TopAlertTitle = "";
            TopAlertDetail = "";
            return;
        }

        isRefreshing = true;
        try
        {
            var state = host.Dashboard.BuildPageState();
            var project = state.Project;
            string FormatMoney(decimal clp) =>
                host.Currency.FormatMoneyFromClp(clp, project.MonedaPresupuesto, project.ValorUfClp, project.TipoCambioUsdClp);

            gastosGeneralesSimulatedPct = (double)project.GastosGeneralesPct;
            utilidadSimulatedPct = (double)project.UtilidadPct;
            OnPropertyChanged(nameof(GastosGeneralesSimulatedPct));
            OnPropertyChanged(nameof(UtilidadSimulatedPct));

            KpiCards.Clear();
            foreach (var kpi in state.Executive.Kpis)
            {
                string value = kpi.ValueKind switch
                {
                    DashboardExecutiveKpiValueKind.Money => FormatMoney(kpi.MoneyValueUf ?? 0m),
                    DashboardExecutiveKpiValueKind.ConfigurationHint => "Configurar m²",
                    _ => kpi.TextValue ?? ""
                };

                KpiCards.Add(new DashboardKpiCardVm
                {
                    Title = kpi.Title,
                    Value = value,
                    Caption = kpi.Caption,
                    IsPrimary = kpi.IsPrimary,
                    Accent = kpi.Accent,
                    AccentBrush = DashboardKpiBrushes.ForAccent(kpi.Accent),
                    AccentSoftBrush = DashboardKpiBrushes.ForAccentSoft(kpi.Accent)
                });
            }

            BuildWorkflow(state.Alerts);
            BuildAlerts(state.Alerts);
            UpdateNextStep(state.Alerts);

            CostMaterials = FormatMoney(state.Cost.MaterialesUf);
            CostLabor = FormatMoney(state.Cost.ManoObraUf);
            var costDirect = Math.Max(0m, state.Cost.CostoDirectoUf);
            var otherDirect = Math.Max(0m, costDirect - state.Cost.MaterialesUf - state.Cost.ManoObraUf);
            CostOtherDirect = FormatMoney(otherDirect);
            CostDirect = FormatMoney(costDirect);
            CostTotal = FormatMoney(state.Cost.TotalVentaUf);
            CostUfM2 = project.MetrosCuadrados > 0
                ? $"{FormatMoney(state.Cost.UfM2)} / m²"
                : "Configure m² en datos del proyecto";
            UpdateCostDistribution(costDirect, state.Cost.MaterialesUf, state.Cost.ManoObraUf, otherDirect);

            TopPartidas.Clear();
            foreach (var row in state.Executive.TopPartidas.Take(8))
            {
                double sharePct = costDirect > 0m ? (double)(row.TotalUf / costDirect * 100m) : 0;
                string category = GetCategoryForPartida(row.Item, row.Partida);
                TopPartidas.Add(new DashboardPartidaVm
                {
                    Item = row.Item,
                    Partida = row.Partida,
                    TotalLabel = FormatMoney(row.TotalUf),
                    SharePct = Math.Clamp(sharePct, 0, 100),
                    Category = category
                });
            }
            OnPropertyChanged(nameof(FilteredTopPartidas));

            RecentAuditEntries.Clear();
            foreach (var entry in host.Audit.GetRecentEntries(5))
            {
                RecentAuditEntries.Add(new ProjectAuditEntryVm(entry));
            }
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private static string GetCategoryForPartida(string item, string name)
    {
        string text = (name ?? "").ToLowerInvariant();
        if (text.Contains("conductor") || text.Contains("cable") || text.Contains("tubería") || text.Contains("tuberia") || 
            text.Contains("pvc") || text.Contains("canaliz") || text.Contains("artefacto") || text.Contains("led") ||
            text.Contains("enchufe") || text.Contains("interruptor"))
        {
            return "Materiales";
        }
        else if (text.Contains("montaje") || text.Contains("conexionado") || text.Contains("instalac") || text.Contains("mano de obra"))
        {
            return "Mano de Obra";
        }
        else
        {
            return "Otros";
        }
    }

    private void LoadLandingState()
    {
        var recent = recentProjectsService.GetProjects();
        var items = recent
            .Select(project => BuildRecentProject(project, project.IsMissing
                ? DashboardProjectStateKind.Unknown
                : DashboardProjectStateKind.Draft))
            .ToList();

        RecentProjects.Clear();
        foreach (var item in items)
            RecentProjects.Add(item);
        HasRecentProjects = RecentProjects.Count > 0;

        UpdateLandingSummary(items);
        LastRefreshTask = RefreshRecentProjectStatesAsync(recent, ++landingLoadGeneration);
    }

    private void UpdateLandingSummary(IReadOnlyList<DashboardRecentProjectVm> items)
    {
        QuickSummaryCards.Clear();
        AddSummaryCard("Proyectos totales", items.Count.ToString("N0"), "Según la lista de recientes", DashboardKpiBrushes.Blue, DashboardKpiBrushes.BlueSoft);
        AddSummaryCard("Completos", items.Count(x => x.StateKind == DashboardProjectStateKind.Complete).ToString("N0"), "Listos para entregar", DashboardKpiBrushes.Green, DashboardKpiBrushes.GreenSoft);
        AddSummaryCard("En progreso", items.Count(x => x.StateKind == DashboardProjectStateKind.InProgress).ToString("N0"), "Trabajo activo", DashboardKpiBrushes.Amber, DashboardKpiBrushes.AmberSoft);
        AddSummaryCard("Borradores", items.Count(x => x.StateKind is DashboardProjectStateKind.Draft or DashboardProjectStateKind.Unknown).ToString("N0"), "Pendientes de revisar", DashboardKpiBrushes.BlueDark, DashboardKpiBrushes.BlueSoft);
    }

    private async Task RefreshRecentProjectStatesAsync(IReadOnlyList<RecentProjectInfo> projects, int generation)
    {
        var resolved = await Task.Run(() => projects
            .Select(project => (Project: project, State: DetermineProjectState(project)))
            .ToList());

        if (!ShowEmptyState || generation != landingLoadGeneration)
            return;

        var updatedItems = resolved
            .Select(item => BuildRecentProject(item.Project, item.State))
            .ToList();

        RecentProjects.Clear();
        foreach (var item in updatedItems)
            RecentProjects.Add(item);
        HasRecentProjects = RecentProjects.Count > 0;
        UpdateLandingSummary(updatedItems);
    }

    private void AddSummaryCard(string title, string value, string caption, Brush accent, Brush accentSoft)
    {
        QuickSummaryCards.Add(new DashboardMetricVm
        {
            Title = title,
            Value = value,
            Caption = caption,
            AccentBrush = accent,
            AccentSoftBrush = accentSoft
        });
    }

    private DashboardRecentProjectVm BuildRecentProject(RecentProjectInfo project, DashboardProjectStateKind state)
    {
        var accent = state switch
        {
            DashboardProjectStateKind.Complete => DashboardKpiBrushes.Green,
            DashboardProjectStateKind.InProgress => DashboardKpiBrushes.Amber,
            DashboardProjectStateKind.Draft => DashboardKpiBrushes.Blue,
            _ => DashboardKpiBrushes.BlueDark
        };

        var soft = state switch
        {
            DashboardProjectStateKind.Complete => DashboardKpiBrushes.GreenSoft,
            DashboardProjectStateKind.InProgress => DashboardKpiBrushes.AmberSoft,
            DashboardProjectStateKind.Draft => DashboardKpiBrushes.BlueSoft,
            _ => DashboardKpiBrushes.BlueSoft
        };

        var statusText = project.IsMissing
            ? "No encontrado"
            : state switch
            {
                DashboardProjectStateKind.Complete => "Completo",
                DashboardProjectStateKind.InProgress => "En progreso",
                DashboardProjectStateKind.Draft => "Borrador",
                _ => "Sin estado"
            };

        return new DashboardRecentProjectVm
        {
            Name = string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileNameWithoutExtension(project.Path) : project.Name,
            Path = project.Path,
            StatusText = statusText,
            StatusCaption = project.IsMissing ? "Ruta no disponible" : state switch
            {
                DashboardProjectStateKind.Complete => "Listo para cerrar",
                DashboardProjectStateKind.InProgress => "En desarrollo",
                DashboardProjectStateKind.Draft => "Pendiente de trabajo",
                _ => "Estado no disponible"
            },
            DisplayDate = project.DisplayDate,
            IsMissing = project.IsMissing,
            StateKind = state,
            AccentBrush = accent,
            AccentSoftBrush = soft,
            StatusForeground = accent
        };
    }

    private DashboardProjectStateKind DetermineProjectState(RecentProjectInfo project)
    {
        if (project.IsMissing)
            return DashboardProjectStateKind.Unknown;

        try
        {
            var format = formatDetector.Detect(project.Path);
            // Solo se lee el campo "estado" (SQLite) en vez del snapshot completo (itemizado,
            // cubicación, materiales, APUs) para no bloquear la UI al listar recientes.
            string? estadoRaw = format switch
            {
                ProjectFileFormat.Sqlite => host.Services.ProjectRepository.PeekSQLiteEstadoPresupuesto(project.Path),
                ProjectFileFormat.LegacyJson => host.Services.ProjectRepository
                    .LoadProjectFile(project.Path, ProjectJsonSerializer.Options).Proyecto?.EstadoPresupuesto,
                _ => null
            };

            var estado = estadoRaw?.Trim();
            if (string.IsNullOrWhiteSpace(estado))
                return DashboardProjectStateKind.Draft;

            if (estado.Contains("complet", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("aprob", StringComparison.OrdinalIgnoreCase))
                return DashboardProjectStateKind.Complete;

            if (estado.Contains("progres", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("curso", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("revision", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("revisión", StringComparison.OrdinalIgnoreCase))
            {
                return DashboardProjectStateKind.InProgress;
            }

            if (estado.Contains("borr", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("draft", StringComparison.OrdinalIgnoreCase))
            {
                return DashboardProjectStateKind.Draft;
            }

            return DashboardProjectStateKind.Draft;
        }
        catch
        {
            return DashboardProjectStateKind.Unknown;
        }
    }

    private void UpdateCostDistribution(decimal direct, decimal materials, decimal labor, decimal other)
    {
        var safeDirect = direct <= 0m ? 1m : direct;
        var materialPct = materials / safeDirect * 100m;
        var laborPct = labor / safeDirect * 100m;
        var otherPct = Math.Max(0m, 100m - materialPct - laborPct);

        MaterialShareWidth = new GridLength((double)Math.Max(materialPct, 0.1m), GridUnitType.Star);
        LaborShareWidth = new GridLength((double)Math.Max(laborPct, 0.1m), GridUnitType.Star);
        OtherShareWidth = new GridLength((double)Math.Max(otherPct, 0.1m), GridUnitType.Star);

        CostShares.Clear();
        CostShares.Add(new DashboardCostShareVm
        {
            Label = "Materiales",
            AmountLabel = CostMaterials,
            PercentageLabel = $"{materialPct:N0}%",
            PercentageValue = (double)materialPct,
            Accent = DashboardExecutiveKpiAccent.Blue,
            AccentBrush = DashboardKpiBrushes.Blue,
            AccentSoftBrush = DashboardKpiBrushes.BlueSoft
        });
        CostShares.Add(new DashboardCostShareVm
        {
            Label = "Mano de obra",
            AmountLabel = CostLabor,
            PercentageLabel = $"{laborPct:N0}%",
            PercentageValue = (double)laborPct,
            Accent = DashboardExecutiveKpiAccent.RiskLow,
            AccentBrush = DashboardKpiBrushes.Green,
            AccentSoftBrush = DashboardKpiBrushes.GreenSoft
        });
        CostShares.Add(new DashboardCostShareVm
        {
            Label = "Otros directos",
            AmountLabel = CostOtherDirect,
            PercentageLabel = $"{otherPct:N0}%",
            PercentageValue = (double)otherPct,
            Accent = DashboardExecutiveKpiAccent.Orange,
            AccentBrush = DashboardKpiBrushes.Amber,
            AccentSoftBrush = DashboardKpiBrushes.AmberSoft
        });
    }

    private void BuildWorkflow(FinalReviewSummary review)
    {
        var session = host.Session;
        var project = session.ProjectInfo;
        int totalPartidas = session.Itemizado.Count(x => !x.EsTitulo);
        bool hasName = !string.IsNullOrWhiteSpace(project.Nombre) && project.Nombre != "Proyecto sin nombre";
        bool s1 = hasName;
        bool s2 = totalPartidas > 0;
        bool s3 = review.EmptyPartidas == 0 && totalPartidas > 0;
        bool s4 = review.MissingApus == 0 && totalPartidas > 0;
        // "Revisión" exige que el checklist quede realmente limpio (sin bloqueantes ni críticos);
        // "Exportar" solo depende del gate real de exportación (sin bloqueantes), igual que en
        // Revisión final / Exportar. Así ambos pasos pueden completarse en momentos distintos.
        bool s5 = review.BlockingCount == 0 && review.CriticalCount == 0;
        bool s6 = review.ReadyToExport;

        var steps = new[]
        {
            ("Proyecto",  project.Nombre,                                                     s1, !s1,       ShellRoute.Project),
            ("Itemizado", $"{totalPartidas:N0} partidas",                                     s2, s1 && !s2, ShellRoute.Itemized),
            ("Cubicación",$"{Math.Max(0, totalPartidas - review.EmptyPartidas):N0} cubiertas",s3, s2 && !s3, ShellRoute.Takeoff),
            ("APU/Costos",$"{Math.Max(0, totalPartidas - review.MissingApus):N0} APUs",       s4, s3 && !s4, ShellRoute.Apu),
            ("Revisión",  $"{review.BlockingCount} bloqueantes",                              s5, s4 && !s5, ShellRoute.Review),
            ("Exportar",  review.ReadyToExport ? "Listo" : "Bloqueado",                      s6, s5 && !s6, ShellRoute.OfferDocuments),
        };

        WorkflowSteps.Clear();
        for (int i = 0; i < steps.Length; i++)
        {
            var (title, detail, complete, current, route) = steps[i];
            var r = route;
            WorkflowSteps.Add(new DashboardWorkflowStepVm
            {
                Title = title,
                Detail = detail,
                IsComplete = complete,
                IsCurrent = current,
                IsLast = (i == steps.Length - 1),
                NavigateCommand = new RelayCommand(() => shell.RequestNavigate(r), () => !ShowEmptyState)
            });
        }
    }

    private void BuildAlerts(FinalReviewSummary review)
    {
        Alerts.Clear();
        foreach (var issue in review.Issues.OrderByDescending(x => x.Severity).Take(12))
        {
            var category = issue.Category;
            Alerts.Add(new DashboardAlertVm
            {
                Title = issue.Problem,
                Detail = issue.Recommendation,
                SeverityLabel = issue.Severity.ToString(),
                Category = category,
                NavigateCommand = new RelayCommand(() =>
                {
                    var target = ShellRouteMapper.FromReviewTarget(FinalReviewRouteResolver.Resolve(category));
                    shell.RequestNavigate(target);
                })
            });
        }
    }

    private void UpdateNextStep(FinalReviewSummary review)
    {
        var session = host.Session;
        int totalPartidas = session.Itemizado.Count(x => !x.EsTitulo);
        int readyPartidas = Math.Max(0, totalPartidas - review.EmptyPartidas);
        PartidasReadyLabel = $"{readyPartidas:N0} de {totalPartidas:N0} partidas listas";
        PartidasReadyPercent = totalPartidas == 0 ? 0 : Math.Min(100, readyPartidas * 100d / totalPartidas);
        BlockingAlertCount = review.BlockingCount;

        var current = WorkflowSteps.FirstOrDefault(s => s.IsCurrent)
                      ?? WorkflowSteps.LastOrDefault(s => !s.IsComplete)
                      ?? WorkflowSteps.LastOrDefault();

        if (current == null)
        {
            NextStepTitle = "Siguiente paso";
            NextStepDetail = PartidasReadyLabel;
            PrimaryCtaLabel = "Continuar";
            primaryCtaRoute = ShellRoute.Takeoff;
        }
        else
        {
            NextStepTitle = current.IsComplete && WorkflowSteps.All(s => s.IsComplete)
                ? "Proyecto listo para exportar"
                : $"Siguiente paso: {current.Title.ToLowerInvariant()}";
            NextStepDetail = $"{PartidasReadyLabel} · {review.BlockingCount:N0} alertas bloqueantes";
            PrimaryCtaLabel = current.Title switch
            {
                "Proyecto" => "Completar proyecto",
                "Itemizado" => "Ir al itemizado",
                "Cubicación" => "Continuar cubicación",
                "APU/Costos" => "Completar APU",
                "Revisión" => "Revisar alertas",
                "Exportar" => "Ir a oferta",
                _ => $"Continuar {current.Title.ToLowerInvariant()}"
            };
            primaryCtaRoute = current.Title switch
            {
                "Proyecto" => ShellRoute.Project,
                "Itemizado" => ShellRoute.Itemized,
                "Cubicación" => ShellRoute.Takeoff,
                "APU/Costos" => ShellRoute.Apu,
                "Revisión" => ShellRoute.Review,
                "Exportar" => ShellRoute.OfferDocuments,
                _ => ShellRoute.Takeoff
            };
        }

        var top = review.Issues.OrderByDescending(x => x.Severity).FirstOrDefault();
        if (top != null)
        {
            HasTopAlert = true;
            TopAlertTitle = top.Problem;
            TopAlertDetail = top.Recommendation;
        }
        else
        {
            HasTopAlert = false;
            TopAlertTitle = "";
            TopAlertDetail = "";
        }

        primaryCtaCommand.RaiseCanExecuteChanged();
    }
}



public interface IRefreshablePage
{
    void Refresh();
}