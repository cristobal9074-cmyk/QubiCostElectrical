using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Templates;

// ─── Formato de intercambio de biblioteca APU ─────────────────────────────────
internal sealed class ApuTemplateLibraryFile
{
    public string Format { get; set; } = "QubiCostElectrical.ApuTemplateLibrary";
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = AppClock.Now;
    public List<ApuTemplateRow> Templates { get; set; } = new();
}

// ─── VM de un ítem de plantilla APU ──────────────────────────────────────────
public sealed class ApuTemplateItemVm : ViewModelBase
{
    public ApuTemplateItemVm(ApuTemplateRow row)
    {
        Source = row;
        RecursoCount = TryDeserializeCount(row.JsonRecursos);
    }

    public ApuTemplateRow Source { get; }
    public string Nombre => Source.Nombre;
    public string Categoria => Source.Categoria;
    public string Descripcion => Source.Descripcion;
    public string Unidad => Source.Unidad;
    public int RecursoCount { get; }
    public DateTime ActualizadoEn => Source.ActualizadoEn;
    public string ActualizadoLabel => Source.ActualizadoEn.ToString("dd/MM/yyyy");

    private static int TryDeserializeCount(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            var rows = JsonSerializer.Deserialize<List<ApuRecursoRow>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return rows?.Count ?? 0;
        }
        catch { return 0; }
    }
}

// ─── VM de un conjunto eléctrico (editable) ──────────────────────────────────
public sealed class ConjuntoItemVm : ViewModelBase
{
    private readonly Action onChanged;

    public ConjuntoItemVm(ConjuntoElectricoRow row, Action? onChanged = null)
    {
        Source = row;
        this.onChanged = onChanged ?? (() => { });
    }

    public ConjuntoElectricoRow Source { get; }

    public string Codigo
    {
        get => Source.Codigo;
        set
        {
            if (Source.Codigo != value)
            {
                Source.Codigo = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Nombre
    {
        get => Source.Nombre;
        set
        {
            if (Source.Nombre != value)
            {
                Source.Nombre = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string TipoConjunto
    {
        get => Source.TipoConjunto;
        set
        {
            if (Source.TipoConjunto != value)
            {
                Source.TipoConjunto = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Descripcion
    {
        get => Source.Descripcion;
        set
        {
            if (Source.Descripcion != value)
            {
                Source.Descripcion = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Unidad
    {
        get => Source.Unidad;
        set
        {
            if (Source.Unidad != value)
            {
                Source.Unidad = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public int RecursoCount => Source.Recursos?.Count ?? 0;
    public string ActualizadoLabel => Source.ActualizadoEn.ToString("dd/MM/yyyy");

    public void RefreshRecursoCount()
    {
        OnPropertyChanged(nameof(RecursoCount));
    }
}

// ─── VM principal de Plantillas y Conjuntos ───────────────────────────────────
public sealed partial class PlantillasVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IPermissionService permissions;
    private readonly IPriceBankStore priceBankStore;
    private readonly IApuTemplateStore? templateStore;
    private readonly ConjuntoElectricoService conjuntoSvc = new();

    /// <summary>Biblioteca global de conjuntos (banco_precios.db); null = modo degradado al .qce.</summary>
    private IConjuntoTemplateStore? conjuntoStore;

    /// <summary>Colección de trabajo de conjuntos: filas del store global (nunca de la sesión,
    /// cuyo snapshot se clona al guardar el proyecto y dejaría filas huérfanas).</summary>
    private List<ConjuntoElectricoRow> conjuntosBase = new();

    /// <summary>Ediciones de conjuntos sin guardar en la biblioteca global.</summary>
    private bool hasPendingChanges;

    private ApuTemplateItemVm? selectedTemplate;
    private ConjuntoItemVm? selectedConjunto;
    private string templateFilter = "";
    private string conjuntoFilter = "";
    private bool showEmptyState = false;
    private string statusMessage = "";
    private int activeTab;
    private readonly PlantillasHierarchyCoordinator hierarchyCoordinator;
    private bool showRulesStrip = true;
}
