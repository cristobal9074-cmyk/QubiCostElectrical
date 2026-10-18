using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public sealed partial class SolicitudesViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly IPermissionService permissions;
    private readonly IMaterialControlService materialControl;
    private readonly WpfUndoService? undo;

    public ObservableCollection<SolicitudItemRowVm> Materiales { get; } = new();
    public ObservableCollection<SolicitudItemRowVm> Epps { get; } = new();
    public ObservableCollection<HistorialSolicitudVm> HistorialSolicitudes { get; } = new();
    public ObservableCollection<SolicitudAutocompleteSuggestionVm> AutocompleteResults { get; } = new();

    private bool autocompleteVisible;
    public bool AutocompleteVisible
    {
        get => autocompleteVisible;
        set => SetProperty(ref autocompleteVisible, value);
    }

    private HistorialSolicitudVm? selectedHistorialSolicitud;
    public HistorialSolicitudVm? SelectedHistorialSolicitud
    {
        get => selectedHistorialSolicitud;
        set
        {
            if (SetProperty(ref selectedHistorialSolicitud, value))
            {
                OnPropertyChanged(nameof(HasSelectedHistorial));
            }
        }
    }

    public bool HasSelectedHistorial => SelectedHistorialSolicitud != null;

    private int selectedTabIndex;
    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set
        {
            if (SetProperty(ref selectedTabIndex, value))
            {
                AutocompleteVisible = false;
            }
        }
    }

    private bool showEmptyState = true;
    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public string CodigoSolicitud => $"SM {session?.ProjectInfo?.SolicitudCorrelativo ?? 1}";

    public bool HasProject => workspace.HasOpenProject;

    public bool CanApproveSolicitud => permissions.CurrentRole == UserRole.Admin;
    public bool CanImportSolicitud => permissions.CurrentRole == UserRole.Admin || permissions.CurrentRole == UserRole.Estimator;

    private string requestedBy = "";
    public string RequestedBy
    {
        get => requestedBy;
        set
        {
            if (SetProperty(ref requestedBy, value))
            {
                session.ProjectInfo.UsuarioNombre = value;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            }
        }
    }

    private string workName = "";
    public string WorkName
    {
        get => workName;
        set
        {
            if (SetProperty(ref workName, value))
            {
                session.ProjectInfo.Nombre = value;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            }
        }
    }

    private string requestDate = "";
    public string RequestDate
    {
        get => requestDate;
        set => SetProperty(ref requestDate, value);
    }

    private string statusLabel = "Borrador";
    public string StatusLabel
    {
        get => statusLabel;
        private set => SetProperty(ref statusLabel, value);
    }
}
