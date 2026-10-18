using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Project;

public sealed partial class ProjectViewModel : ViewModelBase, IPageWithPendingChanges, IPageWithCommitChanges, IRefreshablePage
{
    public ProjectViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Audit, host.Permissions, host.ProjectEdit, host.UserPreferencesStore) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ProjectViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService projectWorkspace, ProjectAuditRecorder audit, IPermissionService permissions, IProjectEditWorkspaceService projectEdit, IUserPreferencesStore? preferencesStore = null)
    {
        this.shell = shell;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.audit = audit;
        this.permissions = permissions;
        this.projectEdit = projectEdit;
        this.preferencesStore = preferencesStore ?? WpfAppHost.Current?.UserPreferencesStore ?? new UserPreferencesStore();
        SaveCommand = new RelayCommand(Save, () => HasPendingChanges && !ShowEmptyState && !session.IsReadOnly);
        CancelCommand = new RelayCommand(Cancel, () => HasPendingChanges && !ShowEmptyState && !session.IsReadOnly);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        GenerateFichaPdfCommand = new RelayCommand(GenerateFichaPdf, () => !ShowEmptyState && permissions.CanExportDocuments);
        SelectSectionCommand = new RelayCommand<string>(SelectSection);
        SetEstadoCommand = new RelayCommand<string>(SetEstado, _ => !ShowEmptyState && !session.IsReadOnly);
        SetCommercialModelCommand = new RelayCommand<string>(SetCommercialModel, _ => !ShowEmptyState && !session.IsReadOnly);
        ApplyCommercialProfileCommand = new RelayCommand<string>(ApplyCommercialProfile, _ => !ShowEmptyState && !session.IsReadOnly);
        SetApuCostingModeCommand = new RelayCommand<string>(SetApuCostingMode, _ => !ShowEmptyState && !session.IsReadOnly);
        SelectMonedaCommand = new RelayCommand<string>(SelectMoneda, _ => !ShowEmptyState && !session.IsReadOnly);
        ApplyPreferencesDefaultsCommand = new RelayCommand(ApplyPreferencesDefaults, () => !ShowEmptyState && !session.IsReadOnly);

        LoadScopeResidentialCommand = new RelayCommand(LoadScopeResidential, () => CanEdit);
        LoadScopeCommercialCommand = new RelayCommand(LoadScopeCommercial, () => CanEdit);
        LoadScopeIndustrialCommand = new RelayCommand(LoadScopeIndustrial, () => CanEdit);
        ClearScopeCommand = new RelayCommand(ClearScope, () => CanEdit && !string.IsNullOrWhiteSpace(ScopeText));
        CopyScopeCommand = new RelayCommand(CopyScope, () => !string.IsNullOrWhiteSpace(ScopeText));

        LoadExclusionsStandardCommand = new RelayCommand(LoadExclusionsStandard, () => CanEdit);
        LoadExclusionsCivilCommand = new RelayCommand(LoadExclusionsCivil, () => CanEdit);
        ClearExclusionsCommand = new RelayCommand(ClearExclusions, () => CanEdit && !string.IsNullOrWhiteSpace(ExclusionsText));
        CopyExclusionsCommand = new RelayCommand(CopyExclusions, () => !string.IsNullOrWhiteSpace(ExclusionsText));

        SetCurrentUserAsEstimatorCommand = new RelayCommand(SetCurrentUserAsEstimator, () => CanEdit);
        SetUserRoleCommand = new RelayCommand<string>(SetUserRole, _ => CanEdit);
    }

    public ICommand SetApuCostingModeCommand { get; }
    public ICommand SelectMonedaCommand { get; }
    public ICommand ApplyPreferencesDefaultsCommand { get; }

    public ICommand LoadScopeResidentialCommand { get; }
    public ICommand LoadScopeCommercialCommand { get; }
    public ICommand LoadScopeIndustrialCommand { get; }
    public ICommand ClearScopeCommand { get; }
    public ICommand CopyScopeCommand { get; }

    public ICommand LoadExclusionsStandardCommand { get; }
    public ICommand LoadExclusionsCivilCommand { get; }
    public ICommand ClearExclusionsCommand { get; }
    public ICommand CopyExclusionsCommand { get; }

    public ICommand SetCurrentUserAsEstimatorCommand { get; }
    public ICommand SetUserRoleCommand { get; }

    public string ScopeLengthSummary => $"{ScopeText?.Length ?? 0:N0} caracteres";
    public string ExclusionsLengthSummary => $"{ExclusionsText?.Length ?? 0:N0} caracteres";
    public string AuditCountSummary => $"{AuditEntries.Count:N0} registros de actividad persistidos en .qce";

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set
        {
            if (!SetProperty(ref showEmptyState, value)) return;
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ApplyPreferencesDefaultsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LoadScopeResidentialCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LoadScopeCommercialCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LoadScopeIndustrialCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearScopeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CopyScopeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LoadExclusionsStandardCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LoadExclusionsCivilCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearExclusionsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CopyExclusionsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SetCurrentUserAsEstimatorCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanEdit));
        }
    }

    public bool CanEdit => !showEmptyState && !session.IsReadOnly;
    public bool IsLoading { get; private set; }

    public string Nombre { get => snapshot.Nombre; set { snapshot.Nombre = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProjectName)); NotifyDirty(); } }
    public string Cliente { get => snapshot.Cliente; set { snapshot.Cliente = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClienteChip)); NotifyDirty(); } }
    public string Ubicacion { get => snapshot.Ubicacion; set { snapshot.Ubicacion = value; OnPropertyChanged(); NotifyDirty(); } }
    public string TipoProyecto { get => snapshot.TipoProyecto; set { snapshot.TipoProyecto = value; OnPropertyChanged(); NotifyDirty(); } }
    public string UsuarioNombre { get => snapshot.UsuarioNombre; set { snapshot.UsuarioNombre = value; OnPropertyChanged(); NotifyDirty(); } }
    public string UsuarioRol { get => snapshot.UsuarioRol; set { snapshot.UsuarioRol = value; OnPropertyChanged(); NotifyDirty(); } }
    public string MonedaPresupuesto
    {
        get => snapshot.MonedaPresupuesto;
        set
        {
            snapshot.MonedaPresupuesto = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MonedaChip));
            OnPropertyChanged(nameof(IsMonedaUf));
            OnPropertyChanged(nameof(IsMonedaClp));
            OnPropertyChanged(nameof(IsMonedaUsd));
            NotifyDirty();
        }
    }

    public bool IsMonedaUf => string.Equals(MonedaPresupuesto, "UF", StringComparison.OrdinalIgnoreCase);
    public bool IsMonedaClp => string.Equals(MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);
    public bool IsMonedaUsd => string.Equals(MonedaPresupuesto, "USD", StringComparison.OrdinalIgnoreCase);

    private void SelectMoneda(string? moneda)
    {
        if (string.IsNullOrWhiteSpace(moneda)) return;
        MonedaPresupuesto = moneda.Trim().ToUpperInvariant();
    }

    public string EstadoPresupuesto
    {
        get => snapshot.EstadoPresupuesto;
        set
        {
            snapshot.EstadoPresupuesto = value;
            OnPropertyChanged();
            NotifyEstadoFlags();
            NotifyDirty();
        }
    }

    // Formato fijo es-CL (punto = miles, coma = decimal) para que el getter y el setter sean
    // simétricos sin importar la configuración regional de Windows del usuario.
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-CL");

    public string ValorUfClpText
    {
        get => snapshot.ValorUfClp > 0 ? snapshot.ValorUfClp.ToString("N2", MoneyCulture) : "";
        set
        {
            if (decimal.TryParse(value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                snapshot.ValorUfClp = parsed;
            else if (string.IsNullOrWhiteSpace(value))
                snapshot.ValorUfClp = 0;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ValorUfClpDisplay));
            OnPropertyChanged(nameof(ValorUfClpShort));
            NotifyDirty();
        }
    }

    public string TipoCambioUsdText
    {
        get => snapshot.TipoCambioUsdClp > 0 ? snapshot.TipoCambioUsdClp.ToString("N2", MoneyCulture) : "";
        set
        {
            if (decimal.TryParse(value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                snapshot.TipoCambioUsdClp = parsed;
            else if (string.IsNullOrWhiteSpace(value))
                snapshot.TipoCambioUsdClp = 0;
            OnPropertyChanged();
            NotifyDirty();
        }
    }

    public string TipoCambioEurText
    {
        get => snapshot.TipoCambioEurClp > 0 ? snapshot.TipoCambioEurClp.ToString("N2", MoneyCulture) : "";
        set
        {
            if (decimal.TryParse(value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                snapshot.TipoCambioEurClp = parsed;
            else if (string.IsNullOrWhiteSpace(value))
                snapshot.TipoCambioEurClp = 0;
            OnPropertyChanged();
            NotifyDirty();
        }
    }

    public string GastosGeneralesText
    {
        get => snapshot.GastosGeneralesPct.ToString("N1", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                snapshot.GastosGeneralesPct = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GastosGeneralesKpi));
            OnPropertyChanged(nameof(MarkupRatioText));
            OnPropertyChanged(nameof(MarkupSubtext));
            NotifyDirty();
        }
    }

    public string UtilidadText
    {
        get => snapshot.UtilidadPct.ToString("N1", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                snapshot.UtilidadPct = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UtilidadKpi));
            OnPropertyChanged(nameof(MarkupRatioText));
            OnPropertyChanged(nameof(MarkupSubtext));
            NotifyDirty();
        }
    }

    public string RiesgoText { get => snapshot.RiesgoPct.ToString("N1", CultureInfo.CurrentCulture); set { if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) snapshot.RiesgoPct = v; OnPropertyChanged(); NotifyDirty(); } }
    public string DescuentoText { get => snapshot.DescuentoPct.ToString("N1", CultureInfo.CurrentCulture); set { if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) snapshot.DescuentoPct = v; OnPropertyChanged(); NotifyDirty(); } }
    public bool OcultarGgYUtilidadEnResumen
    {
        get => snapshot.OcultarGgYUtilidadEnResumen;
        set
        {
            if (snapshot.OcultarGgYUtilidadEnResumen != value)
            {
                snapshot.OcultarGgYUtilidadEnResumen = value;
                OnPropertyChanged();
                NotifyDirty();
            }
        }
    }
    public bool AllowPartidaCommercialOverrides { get => snapshot.AllowPartidaCommercialOverrides; set { snapshot.AllowPartidaCommercialOverrides = value; OnPropertyChanged(); NotifyDirty(); } }
    public CommercialCostingModel CommercialModel => snapshot.CommercialModel;
    public string CommercialModelName => snapshot.CommercialModel.DisplayName();
    public string CommercialProfileName => snapshot.CommercialProfile.DisplayName();
    public string CommercialFormulaSummary => snapshot.CommercialModel switch
    {
        CommercialCostingModel.UnitPrice => "Cantidad contractual × precio unitario, con la política comercial del proyecto.",
        CommercialCostingModel.LumpSum => "Monto global de la partida; la cantidad no modifica el total, con la política comercial del proyecto.",
        CommercialCostingModel.TimeAndMaterials => "Horas × tarifa + materiales + gastos reembolsables, con la política comercial del proyecto.",
        _ => "Costo técnico + riesgo + GG + utilidad − descuento + IVA."
    };
    public string CommercialExceptionsSummary
    {
        get
        {
            var rules = session.ProjectInfo.CommercialPartidaRules ?? new List<CommercialPartidaRule>();
            var exceptions = rules.Where(x => x != null && !x.UseProjectModel).ToList();
            if (exceptions.Count == 0)
                return "Todas las partidas heredan el modelo del proyecto.";

            int incomplete = exceptions.Count(x => x.Validate().Count > 0);
            return incomplete == 0
                ? $"{exceptions.Count:N0} partida(s) usa(n) una excepción comercial completa."
                : $"{exceptions.Count:N0} partida(s) usa(n) excepción; {incomplete:N0} requiere(n) completar datos antes de exportar.";
        }
    }
    public bool IsCommercialModelApu => snapshot.CommercialModel == CommercialCostingModel.ApuDirectCostWithCommercialFactors;
    public bool IsCommercialModelUnitPrice => snapshot.CommercialModel == CommercialCostingModel.UnitPrice;
    public bool IsCommercialModelLumpSum => snapshot.CommercialModel == CommercialCostingModel.LumpSum;
    public bool IsCommercialModelTimeAndMaterials => snapshot.CommercialModel == CommercialCostingModel.TimeAndMaterials;

    public string MetrosCuadradosText
    {
        get => snapshot.MetrosCuadrados > 0 ? snapshot.MetrosCuadrados.ToString("N2", MoneyCulture) : "";
        set
        {
            if (decimal.TryParse(value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                snapshot.MetrosCuadrados = v;
            else if (string.IsNullOrWhiteSpace(value))
                snapshot.MetrosCuadrados = 0;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RatioM2Subtext));
            OnPropertyChanged(nameof(SuperficieM2Formatted));
            NotifyDirty();
        }
    }

    public string IvaPctText
    {
        get => snapshot.IvaPct.ToString("N1", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                snapshot.IvaPct = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IvaKpi));
            NotifyDirty();
        }
    }

    public string ScopeText
    {
        get => snapshot.ScopeText;
        set
        {
            snapshot.ScopeText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScopeLengthSummary));
            ((RelayCommand)ClearScopeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CopyScopeCommand).RaiseCanExecuteChanged();
            NotifyDirty();
        }
    }

    public string ExclusionsText
    {
        get => snapshot.ExclusionsText;
        set
        {
            snapshot.ExclusionsText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExclusionsLengthSummary));
            ((RelayCommand)ClearExclusionsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CopyExclusionsCommand).RaiseCanExecuteChanged();
            NotifyDirty();
        }
    }
    public string ValidityDaysText
    {
        get => snapshot.ValidityDays.ToString(CultureInfo.CurrentCulture);
        set { if (int.TryParse(value, out var days)) snapshot.ValidityDays = days; OnPropertyChanged(); NotifyDirty(); }
    }

    public string FechaCreacionLabel { get; private set; } = "No disponible";
    public string FechaModificacionLabel { get; private set; } = "No disponible";
    public string ArchivoProyectoLabel => string.IsNullOrWhiteSpace(session.CurrentProjectPath)
        ? "Sin archivo guardado"
        : session.CurrentProjectPath;
    public string ArchivoProyectoShortLabel => string.IsNullOrWhiteSpace(session.CurrentProjectPath)
        ? "Sin archivo guardado"
        : Path.GetFileName(session.CurrentProjectPath);

    public string GastosGeneralesKpi => $"{snapshot.GastosGeneralesPct.ToString("N1", CultureInfo.CurrentCulture)}%";
    public string UtilidadKpi => $"{snapshot.UtilidadPct.ToString("N1", CultureInfo.CurrentCulture)}%";
    public string IvaKpi => $"{snapshot.IvaPct.ToString("N1", CultureInfo.CurrentCulture)}%";

    public decimal MarkupRatio => (1 + (snapshot.GastosGeneralesPct / 100m)) * (1 + (snapshot.UtilidadPct / 100m));
    public string MarkupRatioText => $"{MarkupRatio:N3}x";
    public string MarkupSubtext => $"(GG {snapshot.GastosGeneralesPct:N1}% × Utilidad {snapshot.UtilidadPct:N1}%)";
    public string ValorUfClpDisplay => snapshot.ValorUfClp > 0 ? $"${snapshot.ValorUfClp.ToString("N0", MoneyCulture)} CLP" : "No asignado";
    public string ValorUfClpShort => snapshot.ValorUfClp > 0 ? $"${snapshot.ValorUfClp.ToString("N0", MoneyCulture)}" : "—";
    public string RatioM2Subtext => snapshot.MetrosCuadrados > 0
        ? $"Superficie: {snapshot.MetrosCuadrados.ToString("N0", MoneyCulture)} m²"
        : "Superficie no definida";
    public string SuperficieM2Formatted => snapshot.MetrosCuadrados > 0 ? $"{snapshot.MetrosCuadrados.ToString("N0", MoneyCulture)} m²" : "—";

    public string ClienteChip => string.IsNullOrWhiteSpace(Cliente) ? "Sin cliente" : $"Cliente · {Cliente}";
    public string MonedaChip => string.IsNullOrWhiteSpace(MonedaPresupuesto) ? "—" : MonedaPresupuesto;
    public bool HasProjectName => !string.IsNullOrWhiteSpace(Nombre);

    public bool IsEstadoBorrador => string.Equals(EstadoPresupuesto, "Borrador", StringComparison.OrdinalIgnoreCase);
    public bool IsEstadoEnRevision => string.Equals(EstadoPresupuesto, "En revisión", StringComparison.OrdinalIgnoreCase)
        || string.Equals(EstadoPresupuesto, "En revision", StringComparison.OrdinalIgnoreCase);
    public bool IsEstadoAprobado => string.Equals(EstadoPresupuesto, "Aprobado", StringComparison.OrdinalIgnoreCase);

    public string SelectedSection
    {
        get => selectedSection;
        private set
        {
            var norm = value?.Trim().ToLowerInvariant() switch
            {
                "identidad" or "comercial" or "proyectofinanzas" => "proyectofinanzas",
                _ => value?.Trim().ToLowerInvariant() ?? "proyectofinanzas"
            };
            if (!SetProperty(ref selectedSection, norm))
                return;
            OnPropertyChanged(nameof(IsSectionProyectoFinanzas));
            OnPropertyChanged(nameof(IsSectionIdentidad));
            OnPropertyChanged(nameof(IsSectionComercial));
            OnPropertyChanged(nameof(IsSectionEquipo));
            OnPropertyChanged(nameof(IsSectionAlcance));
            OnPropertyChanged(nameof(IsSectionAuditoria));
        }
    }

    public bool IsSectionProyectoFinanzas => SelectedSection is "proyectofinanzas" or "identidad" or "comercial";
    public bool IsSectionIdentidad => IsSectionProyectoFinanzas;
    public bool IsSectionComercial => IsSectionProyectoFinanzas;
    public bool IsSectionEquipo => SelectedSection == "equipo";
    public bool IsSectionAlcance => SelectedSection == "alcance";
    public bool IsSectionAuditoria => SelectedSection == "auditoria";

    public string ValidationSummary
    {
        get => validationSummary;
        private set => SetProperty(ref validationSummary, value);
    }

    public bool HasPendingChanges => baseline != null && !snapshot.ContentEquals(baseline);
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand GenerateFichaPdfCommand { get; }
    public ICommand SelectSectionCommand { get; }
    public ICommand SetEstadoCommand { get; }
    public ICommand SetCommercialModelCommand { get; }
    public ICommand ApplyCommercialProfileCommand { get; }
    public ObservableCollection<ProjectAuditEntryVm> AuditEntries { get; } = new();

    private void SelectSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return;
        SelectedSection = section.Trim().ToLowerInvariant();
    }

    private void SetEstado(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado))
            return;
        EstadoPresupuesto = estado.Trim();
    }

    private void SetCommercialModel(string? model)
    {
        if (Enum.TryParse<CommercialCostingModel>(model, true, out var value))
        {
            snapshot.CommercialModel = value;
            NotifyCommercialModel();
            NotifyDirty();
        }
    }

    private void ApplyCommercialProfile(string? profile)
    {
        if (!Enum.TryParse<CommercialProfile>(profile, true, out var value)) return;
        snapshot.CommercialProfile = value;
        if (value != CommercialProfile.StandardConstructor)
        {
            (snapshot.GastosGeneralesPct, snapshot.UtilidadPct, snapshot.RiesgoPct, snapshot.DescuentoPct) = value switch
            {
                CommercialProfile.ElectricalSubcontract => (6m, 12m, 2m, 0m),
                CommercialProfile.PrivateClient => (10m, 18m, 5m, 0m),
                CommercialProfile.PreliminaryBudget => (12m, 15m, 10m, 0m),
                CommercialProfile.CompetitiveOffer => (5m, 8m, 2m, 0m),
                _ => (snapshot.GastosGeneralesPct, snapshot.UtilidadPct, snapshot.RiesgoPct, snapshot.DescuentoPct)
            };
        }
        OnPropertyChanged(nameof(GastosGeneralesText)); OnPropertyChanged(nameof(UtilidadText));
        OnPropertyChanged(nameof(RiesgoText)); OnPropertyChanged(nameof(DescuentoText));
        OnPropertyChanged(nameof(CommercialProfileName)); NotifyDirty();
    }

    private void ApplyPreferencesDefaults()
    {
        if (ShowEmptyState || session.IsReadOnly) return;
        var prefs = preferencesStore.Load();
        snapshot.GastosGeneralesPct = prefs.DefaultGastosGeneralesPct;
        snapshot.UtilidadPct = prefs.DefaultUtilidadPct;
        OnPropertyChanged(nameof(GastosGeneralesText));
        OnPropertyChanged(nameof(UtilidadText));
        OnPropertyChanged(nameof(GastosGeneralesKpi));
        OnPropertyChanged(nameof(UtilidadKpi));
        OnPropertyChanged(nameof(MarkupRatioText));
        OnPropertyChanged(nameof(MarkupSubtext));
        NotifyDirty();
    }

    private void NotifyCommercialModel()
    {
        OnPropertyChanged(nameof(CommercialModel)); OnPropertyChanged(nameof(CommercialModelName));
        OnPropertyChanged(nameof(CommercialFormulaSummary)); OnPropertyChanged(nameof(CommercialExceptionsSummary));
        OnPropertyChanged(nameof(IsCommercialModelApu)); OnPropertyChanged(nameof(IsCommercialModelUnitPrice));
        OnPropertyChanged(nameof(IsCommercialModelLumpSum)); OnPropertyChanged(nameof(IsCommercialModelTimeAndMaterials));
    }

    public string ApuCostingMode
    {
        get => snapshot.ApuCostingMode;
        set
        {
            if (snapshot.ApuCostingMode != value)
            {
                snapshot.ApuCostingMode = value;
                NotifyApuCostingMode();
                NotifyDirty();
            }
        }
    }

    public bool IsCostingModeCubicacion => string.Equals(snapshot.ApuCostingMode, "CubicacionManda", StringComparison.OrdinalIgnoreCase);
    public bool IsCostingModeApu => string.Equals(snapshot.ApuCostingMode, "ApuManda", StringComparison.OrdinalIgnoreCase);
    public bool IsCostingModeHibrido => string.Equals(snapshot.ApuCostingMode, "CubicacionMasApuComplementarios", StringComparison.OrdinalIgnoreCase)
                                     || (!IsCostingModeCubicacion && !IsCostingModeApu);

    public string ApuCostingModeTitle => IsCostingModeCubicacion
        ? "Lista de Precios de Materiales (Cubicación Directa)"
        : (IsCostingModeApu ? "Precios tomados del APU (opcional)" : "Mixto / Híbrido (Cubicación + APU Complementario)");

    public string ApuCostingModeBadge => IsCostingModeCubicacion
        ? "Lista de materiales"
        : (IsCostingModeApu ? "Precios del APU" : "Cubicación + APU");

    private void SetApuCostingMode(string? mode)
    {
        if (!string.IsNullOrWhiteSpace(mode))
        {
            snapshot.ApuCostingMode = mode.Trim();
            NotifyApuCostingMode();
            NotifyDirty();
        }
    }

    private void NotifyApuCostingMode()
    {
        OnPropertyChanged(nameof(ApuCostingMode));
        OnPropertyChanged(nameof(IsCostingModeCubicacion));
        OnPropertyChanged(nameof(IsCostingModeApu));
        OnPropertyChanged(nameof(IsCostingModeHibrido));
        OnPropertyChanged(nameof(ApuCostingModeTitle));
        OnPropertyChanged(nameof(ApuCostingModeBadge));
    }

    private void NotifyEstadoFlags()
    {
        OnPropertyChanged(nameof(IsEstadoBorrador));
        OnPropertyChanged(nameof(IsEstadoEnRevision));
        OnPropertyChanged(nameof(IsEstadoAprobado));
    }

    public void Refresh() => Load();

    public void Load()
    {
        IsLoading = true;
        OnPropertyChanged(nameof(IsLoading));
        try
        {
            ShowEmptyState = !projectWorkspace.HasOpenProject;
            if (ShowEmptyState)
            {
                baseline = null;
                ValidationSummary = "";
                OnPropertyChanged(nameof(HasPendingChanges));
                return;
            }

            snapshot = projectEdit.Load();
            baseline = snapshot.Clone();
            var project = session.ProjectInfo;
            FechaCreacionLabel = project.FechaCreacion == DateTime.MinValue ? "No disponible" : project.FechaCreacion.ToString("dd/MM/yyyy");
            FechaModificacionLabel = project.FechaModificacion == DateTime.MinValue ? "No disponible" : project.FechaModificacion.ToString("dd/MM/yyyy HH:mm");
            ReloadAuditEntries();
            ValidationSummary = "";
            NotifyAll();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    public bool TryDiscardChanges()
    {
        if (!HasPendingChanges)
            return true;
        Cancel();
        return true;
    }

    public bool TryApplyChanges()
    {
        Save();
        return !HasPendingChanges;
    }

    private void ReloadAuditEntries()
    {
        AuditEntries.Clear();
        foreach (var entry in audit.GetRecentEntries(300))
            AuditEntries.Add(new ProjectAuditEntryVm(entry));
        OnPropertyChanged(nameof(AuditCountSummary));
    }

    private void LoadScopeResidential()
    {
        const string text = "• Suministro e instalación de alimentadores principales y subalimentadores bajo pliegos SEC (RIC N°03 y RIC N°04).\n"
            + "• Montaje y conexionado de Tablero General de Alumbrado y Fuerza (TDAF) con protecciones termomagnéticas y diferenciales tipo A/AC según RIC N°02.\n"
            + "• Canalizaciones embutidas y a la vista para circuitos de alumbrado y enchufes según RIC N°10.\n"
            + "• Instalación y conexionado de artefactos eléctricos, módulos de tomacorrientes e interruptores según planos.\n"
            + "• Sistema de Puesta a Tierra de protección y de servicio con medición de resistencia según pliego RIC N°06.\n"
            + "• Pruebas de aislación, continuidad y rotulación de tableros según pliego RIC N°19.\n"
            + "• Tramitación y obtención del Certificado de Declaración de Instalación Eléctrica Interior TE1 ante la SEC.";

        ScopeText = string.IsNullOrWhiteSpace(ScopeText) ? text : ScopeText.TrimEnd() + Environment.NewLine + Environment.NewLine + text;
    }

    private void LoadScopeCommercial()
    {
        const string text = "• Suministro e instalación de tableros de distribución comercial con protecciones diferenciales inmunizadas según pliego RIC N°02.\n"
            + "• Canalizaciones en bandejas portaconductores tipo malla/chapa perforada y tubería EMT según pliego RIC N°04.\n"
            + "• Circuitos dedicados para enchufes comerciales, puestos de trabajo regulados y líneas de fuerza.\n"
            + "• Sistema de iluminación general y circuitos de alumbrado de emergencia autoenergizados según pliego RIC N°08.\n"
            + "• Canalización y ductería para corrientes débiles (datos, CCTV y control de acceso).\n"
            + "• Pruebas de carga, equilibrio de fases, medición de impedancia de lazo y rotulación de circuitos bajo norma SEC.\n"
            + "• Preparación de carpeta técnica y tramitación de declaración SEC TE1.";

        ScopeText = string.IsNullOrWhiteSpace(ScopeText) ? text : ScopeText.TrimEnd() + Environment.NewLine + Environment.NewLine + text;
    }

    private void LoadScopeIndustrial()
    {
        const string text = "• Montaje y conexionado de Tablero General de Fuerza (TGF) y Centro de Control de Motores (CCM) según pliego RIC N°02.\n"
            + "• Tendido de alimentadores de potencia en escalerillas portaconductores y ductos galvanizados pesados según RIC N°04.\n"
            + "• Compensación de reactivos mediante banco automático de condensadores con filtro de armónicos.\n"
            + "• Malla de puesta a tierra equipotencial industrial con valor de resistencia certificado bajo pliego RIC N°06.\n"
            + "• Conexionado eléctrico de motores, bombas y actuadores de maquinaria de proceso.\n"
            + "• Protecciones diferenciales tipo B / superinmunizadas y relés de protección térmica industrial.\n"
            + "• Ensayos dieléctricos, termografía preliminar de tableros y declaración TE1 SEC.";

        ScopeText = string.IsNullOrWhiteSpace(ScopeText) ? text : ScopeText.TrimEnd() + Environment.NewLine + Environment.NewLine + text;
    }

    private void ClearScope()
    {
        ScopeText = string.Empty;
    }

    private void CopyScope()
    {
        if (string.IsNullOrWhiteSpace(ScopeText)) return;
        try
        {
            System.Windows.Clipboard.SetText(ScopeText);
            ValidationSummary = "Alcance incluido copiado al portapapeles.";
            ShellNotificationService.Instance.ShowCopied("Alcance incluido copiado al portapapeles.");
        }
        catch { }
    }

    private void LoadExclusionsStandard()
    {
        const string text = "• Obras civiles, pasadas en muros/losas diamantadas, sellos cortafuego, excavación de zanjas y reposición de pavimentos.\n"
            + "• Pago de derechos de empalme, estudio de conexión, aportes financieros o tarifas de la empresa distribuidora de energía.\n"
            + "• Suministro y montaje de luminarias decorativas de arquitectura o equipos especiales proporcionados por el mandante.\n"
            + "• Equipos activos de climatización (HVAC), grupos generadores y sistemas UPS (salvo canalización expresamente indicada).\n"
            + "• Cableado y equipamiento activo de corrientes débiles, racks, switches, cámaras CCTV y sistemas contra incendio.\n"
            + "• Permisos municipales de edificación y pagos ajenos a la tramitación SEC TE1.";

        ExclusionsText = string.IsNullOrWhiteSpace(ExclusionsText) ? text : ExclusionsText.TrimEnd() + Environment.NewLine + Environment.NewLine + text;
    }

    private void LoadExclusionsCivil()
    {
        const string text = "• Demoliciones, picados de muros, zanjas exteriores, rellenos compactados y terminaciones de albañilería/yeso.\n"
            + "• Remates de pintura, reposición de baldosas o cerámica dañada por trazados no previstos.\n"
            + "• Andamios tubulares, alzaprimas o plataformas de levante para trabajos a alturas superiores a 3,0 metros.\n"
            + "• Retiro de escombros de faena fuera del recinto de la obra.";

        ExclusionsText = string.IsNullOrWhiteSpace(ExclusionsText) ? text : ExclusionsText.TrimEnd() + Environment.NewLine + Environment.NewLine + text;
    }

    private void ClearExclusions()
    {
        ExclusionsText = string.Empty;
    }

    private void CopyExclusions()
    {
        if (string.IsNullOrWhiteSpace(ExclusionsText)) return;
        try
        {
            System.Windows.Clipboard.SetText(ExclusionsText);
            ValidationSummary = "Exclusiones copiadas al portapapeles.";
            ShellNotificationService.Instance.ShowCopied("Exclusiones copiadas al portapapeles.");
        }
        catch { }
    }

    private void SetCurrentUserAsEstimator()
    {
        UsuarioNombre = Environment.UserName;
        if (string.IsNullOrWhiteSpace(UsuarioRol))
            UsuarioRol = "Estimador Eléctrico";
    }

    private void SetUserRole(string? role)
    {
        if (!string.IsNullOrWhiteSpace(role))
            UsuarioRol = role.Trim();
    }

    private void Save()
    {
        if (session.IsReadOnly)
            return;
        var errors = projectEdit.Validate(snapshot);
        if (errors.Count > 0)
        {
            ValidationSummary = string.Join(Environment.NewLine, errors);
            return;
        }

        var previous = baseline?.Clone();
        projectEdit.Apply(snapshot);
        if (previous != null && (previous.CommercialModel != snapshot.CommercialModel || previous.CommercialProfile != snapshot.CommercialProfile
            || previous.ApuCostingMode != snapshot.ApuCostingMode
            || previous.GastosGeneralesPct != snapshot.GastosGeneralesPct || previous.UtilidadPct != snapshot.UtilidadPct
            || previous.RiesgoPct != snapshot.RiesgoPct || previous.DescuentoPct != snapshot.DescuentoPct || previous.IvaPct != snapshot.IvaPct))
            audit.RecordLifecycle("ModeloComercial", $"{snapshot.CommercialModel.DisplayName()} · Modelo Costeo {snapshot.ApuCostingMode} · Perfil {snapshot.CommercialProfile.DisplayName()} · GG {snapshot.GastosGeneralesPct:0.##}% · UT {snapshot.UtilidadPct:0.##}% · Riesgo {snapshot.RiesgoPct:0.##}% · Desc. {snapshot.DescuentoPct:0.##}% · IVA {snapshot.IvaPct:0.##}%");
        baseline = snapshot.Clone();
        ValidationSummary = "Cambios aplicados al proyecto. Use Guardar en la barra superior para persistir el archivo.";
        shell.RefreshChrome();
        OnPropertyChanged(nameof(HasPendingChanges));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private void Cancel()
    {
        if (session.IsReadOnly)
            return;
        projectEdit.Discard();
        Load();
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private void NotifyDirty()
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
        shell.RefreshChrome();
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Cliente));
        OnPropertyChanged(nameof(Ubicacion));
        OnPropertyChanged(nameof(TipoProyecto));
        OnPropertyChanged(nameof(UsuarioNombre));
        OnPropertyChanged(nameof(UsuarioRol));
        OnPropertyChanged(nameof(MonedaPresupuesto));
        OnPropertyChanged(nameof(EstadoPresupuesto));
        OnPropertyChanged(nameof(ValorUfClpText));
        OnPropertyChanged(nameof(TipoCambioUsdText));
        OnPropertyChanged(nameof(GastosGeneralesText));
        OnPropertyChanged(nameof(UtilidadText));
        OnPropertyChanged(nameof(IvaPctText));
        OnPropertyChanged(nameof(RiesgoText));
        OnPropertyChanged(nameof(DescuentoText));
        OnPropertyChanged(nameof(AllowPartidaCommercialOverrides));
        OnPropertyChanged(nameof(CommercialProfileName));
        NotifyCommercialModel();
        NotifyApuCostingMode();
        OnPropertyChanged(nameof(MetrosCuadradosText));
        OnPropertyChanged(nameof(ScopeText));
        OnPropertyChanged(nameof(ExclusionsText));
        OnPropertyChanged(nameof(ValidityDaysText));
        OnPropertyChanged(nameof(FechaCreacionLabel));
        OnPropertyChanged(nameof(FechaModificacionLabel));
        OnPropertyChanged(nameof(ArchivoProyectoLabel));
        OnPropertyChanged(nameof(ArchivoProyectoShortLabel));
        OnPropertyChanged(nameof(GastosGeneralesKpi));
        OnPropertyChanged(nameof(UtilidadKpi));
        OnPropertyChanged(nameof(IvaKpi));
        OnPropertyChanged(nameof(ClienteChip));
        OnPropertyChanged(nameof(MonedaChip));
        OnPropertyChanged(nameof(IsMonedaUf));
        OnPropertyChanged(nameof(IsMonedaClp));
        OnPropertyChanged(nameof(IsMonedaUsd));
        OnPropertyChanged(nameof(MarkupRatioText));
        OnPropertyChanged(nameof(MarkupSubtext));
        OnPropertyChanged(nameof(ValorUfClpDisplay));
        OnPropertyChanged(nameof(ValorUfClpShort));
        OnPropertyChanged(nameof(SuperficieM2Formatted));
        OnPropertyChanged(nameof(RatioM2Subtext));
        OnPropertyChanged(nameof(HasProjectName));
        OnPropertyChanged(nameof(ScopeLengthSummary));
        OnPropertyChanged(nameof(ExclusionsLengthSummary));
        OnPropertyChanged(nameof(AuditCountSummary));
        NotifyEstadoFlags();
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(CanEdit));
    }

    private void GenerateFichaPdf()
    {
        if (!permissions.CanExportDocuments)
            return;
        var dlg = new SaveFileDialog
        {
            Title = "Exportar Ficha de Datos del Proyecto",
            FileName = $"Ficha-Datos-{SanitizeFileName(snapshot.Nombre)}.pdf",
            Filter = "PDF (*.pdf)|*.pdf"
        };

        if (dlg.ShowDialog() == true)
        {
            if (OperationFailureMessages.IsFileLocked(dlg.FileName))
            {
                WpfDialogService.Error("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                return;
            }

            try
            {
                ProjectFichaExporter.Export(snapshot, dlg.FileName, FechaCreacionLabel, FechaModificacionLabel);
                WpfDialogService.Info("Exportar PDF", $"Ficha de datos generada exitosamente en:{Environment.NewLine}{dlg.FileName}");
            }
            catch (Exception ex)
            {
                WpfDialogService.Error("Error al generar PDF", OperationFailureMessages.ToUserMessage(ex));
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Proyecto";
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
