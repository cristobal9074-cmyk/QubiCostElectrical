using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Templates;

/// <summary>
/// Coordina la jerarquía de recursos de conjuntos eléctricos (Maestro, Esclavo, Operación),
/// la selección múltiple en grilla, acciones masivas, detección de huérfanos y la vista previa de expansión.
/// </summary>
public sealed class PlantillasHierarchyCoordinator : ObservableObject
{
    private readonly Func<bool> canEdit;
    private readonly Func<ConjuntoItemVm?> getSelectedConjunto;
    private readonly ObservableCollection<ConjuntoRecursoVm> conjuntoRecursos;
    private readonly BudgetProjectSession session;
    private readonly ConjuntoElectricoService conjuntoSvc;
    private readonly Action markConjuntosDirty;
    private readonly Action updateKpis;
    private readonly Action<string> setStatusMessage;
    private readonly Action<string>? onVmPropertyChanged;

    private readonly List<ConjuntoRecursoVm> selectedRecursos = new();
    private MaestroOptionVm? maestroDestinoSeleccion;
    private int conteoMaestros;
    private int conteoEsclavos;
    private int conteoOperaciones;

    public PlantillasHierarchyCoordinator(
        Func<bool> canEdit,
        Func<ConjuntoItemVm?> getSelectedConjunto,
        ObservableCollection<ConjuntoRecursoVm> conjuntoRecursos,
        BudgetProjectSession session,
        ConjuntoElectricoService conjuntoSvc,
        Action markConjuntosDirty,
        Action updateKpis,
        Action<string> setStatusMessage,
        Action<string>? onVmPropertyChanged = null)
    {
        this.canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        this.getSelectedConjunto = getSelectedConjunto ?? throw new ArgumentNullException(nameof(getSelectedConjunto));
        this.conjuntoRecursos = conjuntoRecursos ?? throw new ArgumentNullException(nameof(conjuntoRecursos));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.conjuntoSvc = conjuntoSvc ?? throw new ArgumentNullException(nameof(conjuntoSvc));
        this.markConjuntosDirty = markConjuntosDirty ?? throw new ArgumentNullException(nameof(markConjuntosDirty));
        this.updateKpis = updateKpis ?? throw new ArgumentNullException(nameof(updateKpis));
        this.setStatusMessage = setStatusMessage ?? throw new ArgumentNullException(nameof(setStatusMessage));
        this.onVmPropertyChanged = onVmPropertyChanged;

        MarcarMaestrosSeleccionCommand = new RelayCommand(MarcarMaestrosSeleccion, PuedeEditarSeleccion);
        AsignarMaestroSeleccionCommand = new RelayCommand<string>(
            AsignarMaestroSeleccion,
            id => PuedeEditarSeleccion() && !string.IsNullOrWhiteSpace(id));
        LiberarSeleccionCommand = new RelayCommand(LiberarSeleccion, PuedeEditarSeleccion);
        EliminarSeleccionCommand = new RelayCommand(EliminarSeleccion, PuedeEditarSeleccion);
    }

    public ObservableCollection<JerarquiaNodoVm> Jerarquia { get; } = new();
    public ObservableCollection<PreviewFilaVm> PreviewFilas { get; } = new();

    public int SelectedRecursosCount => selectedRecursos.Count;
    public bool HasSelectedRecursos => selectedRecursos.Count > 0;
    public string SelectedRecursosLabel => $"{selectedRecursos.Count} fila(s) seleccionada(s)";
    public IReadOnlyList<MaestroOptionVm> MaestrosParaSeleccion { get; private set; } = Array.Empty<MaestroOptionVm>();

    public MaestroOptionVm? MaestroDestinoSeleccion
    {
        get => maestroDestinoSeleccion;
        set
        {
            maestroDestinoSeleccion = null;
            Notify(nameof(MaestroDestinoSeleccion));
            if (value != null && !string.IsNullOrWhiteSpace(value.Id))
                AsignarMaestroSeleccion(value.Id);
        }
    }

    public int ConteoMaestros => conteoMaestros;
    public int ConteoEsclavos => conteoEsclavos;
    public int ConteoOperaciones => conteoOperaciones;

    public RelayCommand MarcarMaestrosSeleccionCommand { get; }
    public RelayCommand<string> AsignarMaestroSeleccionCommand { get; }
    public RelayCommand LiberarSeleccionCommand { get; }
    public RelayCommand EliminarSeleccionCommand { get; }

    public void SetSelectedRecursos(IEnumerable<ConjuntoRecursoVm>? seleccion)
    {
        selectedRecursos.Clear();
        if (seleccion != null)
        {
            foreach (var vm in seleccion)
            {
                if (vm != null && !selectedRecursos.Contains(vm))
                    selectedRecursos.Add(vm);
            }
        }

        Notify(nameof(SelectedRecursosCount));
        Notify(nameof(HasSelectedRecursos));
        Notify(nameof(SelectedRecursosLabel));
        RefreshMaestrosParaSeleccion();
        RaiseSelectionCommands();
    }

    public bool PuedeEditarSeleccion() => canEdit() && getSelectedConjunto() != null && selectedRecursos.Count > 0;

    public void RaiseSelectionCommands()
    {
        ((RelayCommand)MarcarMaestrosSeleccionCommand).RaiseCanExecuteChanged();
        ((RelayCommand<string>)AsignarMaestroSeleccionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LiberarSeleccionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)EliminarSeleccionCommand).RaiseCanExecuteChanged();
    }

    public void RefreshMaestrosParaSeleccion()
    {
        MaestrosParaSeleccion = GetMaestroOptions("");
        Notify(nameof(MaestrosParaSeleccion));
    }

    public void RefreshMaestroOptions()
    {
        foreach (var vm in conjuntoRecursos)
            vm.NotifyStructureChanged();
        UpdateRoleCounts();
        RebuildJerarquia();
        RebuildPreview();
        RefreshMaestrosParaSeleccion();
        RaiseSelectionCommands();
    }

    public IReadOnlyList<MaestroOptionVm> GetMaestroOptions(string selfId)
    {
        var recursos = getSelectedConjunto()?.Source.Recursos;
        if (recursos == null) return Array.Empty<MaestroOptionVm>();

        var opciones = new List<MaestroOptionVm>();
        foreach (var r in recursos)
        {
            if (string.IsNullOrWhiteSpace(r.Id) ||
                string.Equals(r.Id, selfId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(r.MaestroId)) continue; // un esclavo nunca es maestro
            if (r.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH or ApuTipoRecurso.HERR) continue;
            opciones.Add(new MaestroOptionVm(r.Id, LabelOpcionRecurso(r)));
        }
        return opciones;
    }

    private static string LabelOpcionRecurso(ConjuntoRecursoRow r)
    {
        string codigo = (r.Codigo ?? "").Trim();
        string desc = (r.Descripcion ?? "").Trim();
        if (codigo.Length > 0 && desc.Length > 0) return $"{codigo} — {desc}";
        if (codigo.Length > 0) return codigo;
        return desc.Length > 0 ? desc : "(sin código)";
    }

    public void MarcarMaestrosSeleccion()
    {
        if (!PuedeEditarSeleccion()) return;

        int marcadas = 0;
        foreach (var vm in selectedRecursos.ToList())
        {
            bool eraDistinta = vm.EsEsclavo || vm.EsOperacion;
            vm.Operacion = "";
            vm.MaestroId = "";
            if (eraDistinta) marcadas++;
        }

        setStatusMessage(marcadas > 0
            ? $"{marcadas} fila(s) marcada(s) como maestro."
            : "Las filas seleccionadas ya eran maestros.");
        RefreshMaestrosParaSeleccion();
        RaiseSelectionCommands();
    }

    public void AsignarMaestroSeleccion(string? maestroId)
    {
        if (!PuedeEditarSeleccion() || string.IsNullOrWhiteSpace(maestroId)) return;

        string nombre = MaestrosParaSeleccion
            .FirstOrDefault(o => string.Equals(o.Id, maestroId, StringComparison.OrdinalIgnoreCase))?.Label
            ?? maestroId;

        int aplicados = 0, omitidos = 0;
        foreach (var vm in selectedRecursos.ToList())
        {
            if (string.Equals(vm.Id, maestroId, StringComparison.OrdinalIgnoreCase))
            {
                omitidos++;
                continue;
            }

            bool candidato = vm.MaestrosDisponibles
                .Any(o => string.Equals(o.Id, maestroId, StringComparison.OrdinalIgnoreCase));
            if (!candidato)
            {
                omitidos++;
                continue;
            }

            vm.Operacion = "";
            vm.MaestroId = maestroId;
            if (vm.EsEsclavo) aplicados++; else omitidos++;
        }

        setStatusMessage(aplicados > 0
            ? $"{aplicados} fila(s) vinculada(s) a «{nombre}»" +
              (omitidos > 0 ? $" · {omitidos} omitida(s)." : ".")
            : "Ninguna fila pudo vincularse a ese maestro.");
        RefreshMaestrosParaSeleccion();
        RaiseSelectionCommands();
    }

    public void LiberarSeleccion()
    {
        if (!PuedeEditarSeleccion()) return;

        int liberadas = 0;
        foreach (var vm in selectedRecursos.ToList())
        {
            if (!vm.EsEsclavo) continue;
            vm.MaestroId = "";
            liberadas++;
        }

        setStatusMessage(liberadas > 0
            ? $"{liberadas} fila(s) liberada(s) — ahora calculan su propia cantidad."
            : "Ninguna fila seleccionada estaba vinculada a un maestro.");
        RefreshMaestrosParaSeleccion();
        RaiseSelectionCommands();
    }

    public void EliminarSeleccion()
    {
        if (!PuedeEditarSeleccion()) return;

        var selectedConjunto = getSelectedConjunto();
        var filas = selectedRecursos.ToList();
        int eliminadas = 0, liberados = 0;
        foreach (var vm in filas)
        {
            if (selectedConjunto == null || !selectedConjunto.Source.Recursos.Contains(vm.Source)) continue;
            selectedConjunto.Source.Recursos.Remove(vm.Source);
            liberados += ConjuntoElectricoService.LiberarEsclavos(selectedConjunto.Source, vm.Source.Id);
            conjuntoRecursos.Remove(vm);
            eliminadas++;
        }

        selectedConjunto?.RefreshRecursoCount();
        markConjuntosDirty();
        SetSelectedRecursos(null);
        RefreshMaestroOptions();
        updateKpis();

        setStatusMessage(eliminadas > 0
            ? $"{eliminadas} material(es) eliminado(s)" +
              (liberados > 0 ? $" ({liberados} esclavo(s) liberado(s))." : ".")
            : "No se eliminó ninguna fila.");
    }

    public void UpdateRoleCounts()
    {
        int maestros = 0, esclavos = 0, operaciones = 0;
        foreach (var vm in conjuntoRecursos)
        {
            if (vm.EsMaestro) maestros++;
            else if (vm.EsEsclavo) esclavos++;
            else if (vm.EsOperacion) operaciones++;
        }

        conteoMaestros = maestros;
        conteoEsclavos = esclavos;
        conteoOperaciones = operaciones;
        Notify(nameof(ConteoMaestros));
        Notify(nameof(ConteoEsclavos));
        Notify(nameof(ConteoOperaciones));
    }

    public void RebuildJerarquia()
    {
        Jerarquia.Clear();
        if (conjuntoRecursos.Count == 0) return;

        var porId = new Dictionary<string, ConjuntoRecursoVm>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in conjuntoRecursos)
            if (!string.IsNullOrWhiteSpace(vm.Id))
                porId[vm.Id] = vm;

        var hijos = new List<(string MaestroId, ConjuntoRecursoVm Hijo)>();
        var huerfanos = new List<ConjuntoRecursoVm>();
        foreach (var vm in conjuntoRecursos)
        {
            if (!vm.EsEsclavo) continue;
            if (!string.IsNullOrWhiteSpace(vm.MaestroId) && porId.ContainsKey(vm.MaestroId))
                hijos.Add((vm.MaestroId, vm));
            else
                huerfanos.Add(vm);
        }

        foreach (var raiz in conjuntoRecursos.Where(r => r.EsMaestro))
        {
            var hijosDeRaiz = hijos
                .Where(h => string.Equals(h.MaestroId, raiz.Id, StringComparison.OrdinalIgnoreCase))
                .Select(h => BuildJerarquiaNodo(h.Hijo))
                .ToList();
            var nodo = BuildJerarquiaNodo(raiz, hijosDeRaiz.Count > 0);
            foreach (var hijo in hijosDeRaiz)
                nodo.Hijos.Add(hijo);
            Jerarquia.Add(nodo);
        }

        foreach (var op in conjuntoRecursos.Where(r => !r.EsMaestro && !r.EsEsclavo))
            Jerarquia.Add(BuildJerarquiaNodo(op));

        foreach (var huerfano in huerfanos)
            Jerarquia.Add(BuildJerarquiaNodo(huerfano, tieneHijos: false, kindOverride: "orphan"));
    }

    private static JerarquiaNodoVm BuildJerarquiaNodo(ConjuntoRecursoVm vm, bool tieneHijos = false, string? kindOverride = null)
    {
        string kind = kindOverride
            ?? (vm.EsMaestro ? "master"
                : vm.EsEsclavo ? "slave"
                : vm.EsOperacion ? "op"
                : "master");
        return new JerarquiaNodoVm
        {
            Kind = kind,
            Codigo = vm.Codigo,
            Descripcion = vm.Descripcion,
            Regla = DescribeRegla(vm),
            TieneHijos = tieneHijos
        };
    }

    private static string DescribeRegla(ConjuntoRecursoVm vm)
    {
        if (vm.EsOperacion)
            return string.IsNullOrWhiteSpace(vm.Operacion) ? "sin fórmula" : "ƒ " + vm.Operacion;
        if (vm.EsEsclavo)
            return vm.TipoCalculo == TipoCalculoParametrico.Espaciamiento
                ? $"÷ {vm.Espaciamiento:0.##} m"
                : $"× {vm.Factor:0.##}";
        return "base de la partida";
    }

    public void RebuildPreview()
    {
        PreviewFilas.Clear();
        var conjunto = getSelectedConjunto()?.Source;
        if (conjunto?.Recursos == null || conjunto.Recursos.Count == 0) return;

        var partida = new ItemizadoRow
        {
            Id = "",
            Descripcion = conjunto.Nombre,
            Unidad = conjunto.Unidad,
            Cantidad = 1m
        };

        var expandida = conjuntoSvc.ExpandToCubicacion(conjunto, partida, session.Materiales, cantidadesEnCero: false);
        foreach (var row in expandida.Rows)
        {
            PreviewFilas.Add(new PreviewFilaVm
            {
                Codigo = row.Codigo ?? "",
                Descripcion = row.Descripcion ?? "",
                Unidad = row.Unidad ?? "",
                Cantidad = row.Cantidad.ToString("0.###")
            });
        }
    }

    private void Notify(string propertyName)
    {
        OnPropertyChanged(propertyName);
        onVmPropertyChanged?.Invoke(propertyName);
    }
}

// ─── Nodo del árbol de jerarquía maestro→esclavo (solo presentación) ─────────
public sealed class JerarquiaNodoVm
{
    /// <summary>"master" | "slave" | "op" | "orphan" — decide el color del nodo en el XAML.</summary>
    public string Kind { get; init; } = "master";

    public string Codigo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Regla { get; init; } = "";

    /// <summary>True si el nodo tiene hijos en el árbol (dibuja la línea vertical).</summary>
    public bool TieneHijos { get; init; }

    public ObservableCollection<JerarquiaNodoVm> Hijos { get; } = new();
}

// ─── Fila de la vista previa de expansión (receta unitaria, base 1) ──────────
public sealed class PreviewFilaVm
{
    public string Codigo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Unidad { get; init; } = "";
    public string Cantidad { get; init; } = "";
}
