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
    {
        this.shell = shell;
        this.host = host;
        SaveCommand = new RelayCommand(Save, () => HasPendingChanges && !ShowEmptyState && !host.Session.IsReadOnly);
        CancelCommand = new RelayCommand(Cancel, () => HasPendingChanges && !ShowEmptyState && !host.Session.IsReadOnly);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        GenerateFichaPdfCommand = new RelayCommand(GenerateFichaPdf, () => !ShowEmptyState);
        SelectSectionCommand = new RelayCommand<string>(SelectSection);
        SetEstadoCommand = new RelayCommand<string>(SetEstado, _ => !ShowEmptyState && !host.Session.IsReadOnly);
    }

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set
        {
            if (!SetProperty(ref showEmptyState, value)) return;
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
        }
    }

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
            NotifyDirty();
        }
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

    public string GastosGeneralesText
    {
        get => snapshot.GastosGeneralesPct.ToString("N1", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                snapshot.GastosGeneralesPct = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GastosGeneralesKpi));
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
            NotifyDirty();
        }
    }

    public string MetrosCuadradosText
    {
        get => snapshot.MetrosCuadrados > 0 ? snapshot.MetrosCuadrados.ToString("N2", MoneyCulture) : "";
        set { if (decimal.TryParse(value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) snapshot.MetrosCuadrados = v; else if (string.IsNullOrWhiteSpace(value)) snapshot.MetrosCuadrados = 0; OnPropertyChanged(); NotifyDirty(); }
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
    public string ArchivoProyectoLabel => string.IsNullOrWhiteSpace(host.Session.CurrentProjectPath)
        ? "Sin archivo guardado"
        : host.Session.CurrentProjectPath;
    public string ArchivoProyectoShortLabel => string.IsNullOrWhiteSpace(host.Session.CurrentProjectPath)
        ? "Sin archivo guardado"
        : Path.GetFileName(host.Session.CurrentProjectPath);

    public string GastosGeneralesKpi => $"{snapshot.GastosGeneralesPct.ToString("N1", CultureInfo.CurrentCulture)}%";
    public string UtilidadKpi => $"{snapshot.UtilidadPct.ToString("N1", CultureInfo.CurrentCulture)}%";
    public string IvaKpi => $"{snapshot.IvaPct.ToString("N1", CultureInfo.CurrentCulture)}%";

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
            if (!SetProperty(ref selectedSection, value))
                return;
            OnPropertyChanged(nameof(IsSectionIdentidad));
            OnPropertyChanged(nameof(IsSectionComercial));
            OnPropertyChanged(nameof(IsSectionEquipo));
            OnPropertyChanged(nameof(IsSectionAlcance));
            OnPropertyChanged(nameof(IsSectionAuditoria));
        }
    }

    public bool IsSectionIdentidad => SelectedSection == "identidad";
    public bool IsSectionComercial => SelectedSection == "comercial";
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

    private void NotifyEstadoFlags()
    {
        OnPropertyChanged(nameof(IsEstadoBorrador));
        OnPropertyChanged(nameof(IsEstadoEnRevision));
        OnPropertyChanged(nameof(IsEstadoAprobado));
    }

    public void Refresh() => Load();

    public void Load()
    {
        ShowEmptyState = !host.ProjectWorkspace.HasOpenProject;
        if (ShowEmptyState)
        {
            baseline = null;
            ValidationSummary = "";
            OnPropertyChanged(nameof(HasPendingChanges));
            return;
        }

        snapshot = host.ProjectEdit.Load();
        baseline = snapshot.Clone();
        var project = host.Session.ProjectInfo;
        FechaCreacionLabel = project.FechaCreacion == DateTime.MinValue ? "No disponible" : project.FechaCreacion.ToString("dd/MM/yyyy");
        FechaModificacionLabel = project.FechaModificacion == DateTime.MinValue ? "No disponible" : project.FechaModificacion.ToString("dd/MM/yyyy HH:mm");
        ReloadAuditEntries();
        ValidationSummary = "";
        NotifyAll();
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
        foreach (var entry in host.Audit.GetRecentEntries(300))
            AuditEntries.Add(new ProjectAuditEntryVm(entry));
    }

    private void Save()
    {
        if (host.Session.IsReadOnly)
            return;
        var errors = host.ProjectEdit.Validate(snapshot);
        if (errors.Count > 0)
        {
            ValidationSummary = string.Join(Environment.NewLine, errors);
            return;
        }

        host.ProjectEdit.Apply(snapshot);
        baseline = snapshot.Clone();
        ValidationSummary = "Cambios aplicados al proyecto. Use Guardar en la barra superior para persistir el archivo.";
        shell.RefreshChrome();
        OnPropertyChanged(nameof(HasPendingChanges));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private void Cancel()
    {
        if (host.Session.IsReadOnly)
            return;
        host.ProjectEdit.Discard();
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
        OnPropertyChanged(nameof(HasProjectName));
        NotifyEstadoFlags();
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    private void GenerateFichaPdf()
    {
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
