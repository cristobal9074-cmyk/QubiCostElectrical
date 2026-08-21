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
        this.onChanged = onChanged ?? (() => {});
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

// ─── VM de recurso de conjunto eléctrico (editable) ───────────────────────────
public sealed class ConjuntoRecursoVm : ViewModelBase
{
    private readonly ConjuntoRecursoRow source;
    private readonly Action onChanged;
    private readonly Func<string, MaterialRow?>? lookupMaterial;

    public ConjuntoRecursoVm(ConjuntoRecursoRow source, Action onChanged, Func<string, MaterialRow?>? lookupMaterial = null)
    {
        this.source = source;
        this.onChanged = onChanged;
        this.lookupMaterial = lookupMaterial;
    }

    public ConjuntoRecursoRow Source => source;

    public ApuTipoRecurso Tipo
    {
        get => source.Tipo;
        set
        {
            if (source.Tipo != value)
            {
                source.Tipo = value;
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Codigo
    {
        get => source.Codigo;
        set
        {
            if (source.Codigo != value)
            {
                source.Codigo = value ?? "";
                OnPropertyChanged();

                if (lookupMaterial != null)
                {
                    var mat = lookupMaterial(source.Codigo);
                    if (mat != null)
                    {
                        source.Descripcion = mat.Descripcion;
                        source.Unidad = mat.Unidad;
                        OnPropertyChanged(nameof(Descripcion));
                        OnPropertyChanged(nameof(Unidad));
                    }
                }

                onChanged();
            }
        }
    }

    public string Descripcion
    {
        get => source.Descripcion;
        set
        {
            if (source.Descripcion != value)
            {
                source.Descripcion = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Unidad
    {
        get => source.Unidad;
        set
        {
            if (source.Unidad != value)
            {
                source.Unidad = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public decimal Rendimiento
    {
        get => source.Rendimiento;
        set
        {
            if (source.Rendimiento != value)
            {
                source.Rendimiento = value;
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public decimal Factor
    {
        get => source.Factor;
        set
        {
            if (source.Factor != value)
            {
                source.Factor = value;
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public decimal PrecioUf
    {
        get => source.PrecioUf;
        set
        {
            if (source.PrecioUf != value)
            {
                source.PrecioUf = value;
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public decimal MermaPct
    {
        get => source.MermaPct;
        set
        {
            if (source.MermaPct != value)
            {
                source.MermaPct = value;
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public TipoCalculoParametrico TipoCalculo
    {
        get => source.TipoCalculo;
        set
        {
            if (source.TipoCalculo != value)
            {
                source.TipoCalculo = value;
                OnPropertyChanged();
                // Reset Espaciamiento to sensible default when switching modes
                if (value == TipoCalculoParametrico.Espaciamiento && source.Espaciamiento <= 0m)
                    source.Espaciamiento = 1m;
                OnPropertyChanged(nameof(Espaciamiento));
                onChanged();
            }
        }
    }

    public decimal Espaciamiento
    {
        get => source.Espaciamiento;
        set
        {
            if (source.Espaciamiento != value)
            {
                source.Espaciamiento = value <= 0m ? 1m : value;
                OnPropertyChanged();
                onChanged();
            }
        }
    }
}

// ─── VM principal de Plantillas y Conjuntos ───────────────────────────────────
public sealed partial class PlantillasVm : ViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly BudgetProjectSession session;
    private readonly IApuTemplateStore? templateStore;
    private readonly ConjuntoElectricoService conjuntoSvc = new();

    private ApuTemplateItemVm? selectedTemplate;
    private ConjuntoItemVm? selectedConjunto;
    private string templateFilter = "";
    private string conjuntoFilter = "";
    private bool showEmptyState = false;
    private string statusMessage = "";
    private int activeTab;
}
