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

public sealed class SolicitudItemRowVm : ViewModelBase
{
    private readonly SolicitudesViewModel parent;
    private string codigo = "";
    private string descripcion = "";
    private string unidad = "UN";
    private decimal cantidad = 1;
    private string link = "";
    private string observaciones = "";

    public string Id { get; }
    public string Tipo { get; }

    public string Codigo
    {
        get => codigo;
        set
        {
            if (SetProperty(ref codigo, value))
                parent.SaveToSession();
        }
    }

    public string Descripcion
    {
        get => descripcion;
        set
        {
            if (SetProperty(ref descripcion, value))
            {
                parent.SaveToSession();
                OnPropertyChanged(nameof(Suggestions));
                OnPropertyChanged(nameof(ShowSuggestions));
            }
        }
    }

    public string Unidad
    {
        get => unidad;
        set
        {
            if (SetProperty(ref unidad, value))
                parent.SaveToSession();
        }
    }

    public decimal Cantidad
    {
        get => cantidad;
        set
        {
            if (SetProperty(ref cantidad, value))
                parent.SaveToSession();
        }
    }

    public string Link
    {
        get => link;
        set
        {
            if (SetProperty(ref link, value))
                parent.SaveToSession();
        }
    }

    public string Observaciones
    {
        get => observaciones;
        set
        {
            if (SetProperty(ref observaciones, value))
                parent.SaveToSession();
        }
    }

    // Autocomplete dropdown lists
    public List<AutocompleteItem> Suggestions
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Descripcion) || Descripcion.Length < 2)
                return new List<AutocompleteItem>();

            if (Tipo == "Material")
            {
                return parent.SearchMaterials(Descripcion);
            }
            else
            {
                return parent.SearchEpp(Descripcion);
            }
        }
    }

    public bool ShowSuggestions => Suggestions.Count > 0;

    public SolicitudItemRowVm(SolicitudesViewModel parent, SolicitudItemRow row)
    {
        this.parent = parent;
        Id = row.Id;
        Tipo = row.Tipo;
        codigo = row.Codigo;
        descripcion = row.Descripcion;
        unidad = row.Unidad;
        cantidad = row.Cantidad;
        link = row.Link;
        observaciones = row.Observaciones;
    }

    public SolicitudItemRow ToRow()
    {
        return new SolicitudItemRow
        {
            Id = Id,
            Tipo = Tipo,
            Codigo = Codigo,
            Descripcion = Descripcion,
            Unidad = Unidad,
            Cantidad = Cantidad,
            Link = Link,
            Observaciones = Observaciones
        };
    }
}

public sealed class AutocompleteItem
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
}

public sealed partial class SolicitudesViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly IPermissionService permissions;

    public ObservableCollection<SolicitudItemRowVm> Materiales { get; } = new();
    public ObservableCollection<SolicitudItemRowVm> Epps { get; } = new();

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

    // Static EPP Database matching HTML
}
