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
        : this(shell, host.Session, host.ProjectWorkspace, host.Audit, host.Permissions, host.ProjectEdit) { }

    public ProjectViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService projectWorkspace, ProjectAuditRecorder audit, IPermissionService permissions, IProjectEditWorkspaceService projectEdit)
    {
        this.shell = shell;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.audit = audit;
        this.permissions = permissions;
        this.projectEdit = projectEdit;
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
    }

    public ICommand SetApuCostingModeCommand { get; }
    public ICommand SelectMonedaCommand { get; }

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set
        {
            if (!SetProperty(ref showEmptyState, value)) return;
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
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

    public string ScopeText { get => snapshot.ScopeText; set { snapshot.ScopeText = value; OnPropertyChanged(); NotifyDirty(); } }
    public string ExclusionsText { get => snapshot.ExclusionsText; set { snapshot.ExclusionsText = value; OnPropertyChanged(); NotifyDirty(); } }
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
            try
            {
                ProjectFichaExporter.Export(snapshot, dlg.FileName, FechaCreacionLabel, FechaModificacionLabel);
                WpfDialogService.Info("Exportar PDF", $"Ficha de datos generada exitosamente en:{Environment.NewLine}{dlg.FileName}");
            }
            catch (Exception ex)
            {
                WpfDialogService.Error("Error al generar PDF", ex.Message);
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
