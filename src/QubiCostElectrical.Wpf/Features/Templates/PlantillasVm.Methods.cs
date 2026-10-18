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

public sealed partial class PlantillasVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    public PlantillasVm(IShellNavigationHost shell, WpfAppHost host) : this(shell, host.Session, host.Permissions, host.PriceBankStore) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public PlantillasVm(IShellNavigationHost shell, BudgetProjectSession session, IPermissionService permissions, IPriceBankStore priceBankStore)
    {
        this.shell = shell;
        this.session = session;
        this.permissions = permissions;
        this.priceBankStore = priceBankStore;
        templateStore = TryGetTemplateStore();
        conjuntoStore = TryGetConjuntoStore();

        Templates = new ObservableCollection<ApuTemplateItemVm>();
        Conjuntos = new ObservableCollection<ConjuntoItemVm>();
        TemplateRecursos = new ObservableCollection<ApuRecursoRow>();
        ConjuntoRecursos = new ObservableCollection<ConjuntoRecursoVm>();

        CreateTemplateCommand = new RelayCommand(CreateTemplate, () => templateStore != null && CanEdit);
        DeleteTemplateCommand = new RelayCommand<ApuTemplateItemVm>(DeleteTemplate, t => t != null && CanEdit);
        DuplicateTemplateCommand = new RelayCommand<ApuTemplateItemVm>(DuplicateTemplate, t => t != null && CanEdit);
        RenameTemplateCommand = new RelayCommand<ApuTemplateItemVm>(RenameTemplate, t => t != null && CanEdit);
        ImportCommand = new RelayCommand(ImportLibrary, () => templateStore != null && CanEdit);
        ExportCommand = new RelayCommand(ExportLibrary, () => templateStore != null && Templates.Count > 0 && permissions.CanExportDocuments);

        // Conjunto Commands
        CreateConjuntoCommand = new RelayCommand(CreateConjunto, () => CanEdit);
        DeleteConjuntoCommand = new RelayCommand<ConjuntoItemVm>(DeleteConjunto, c => c != null && CanEdit);
        DuplicateConjuntoCommand = new RelayCommand<ConjuntoItemVm>(DuplicateConjunto, c => c != null && CanEdit);
        RenameConjuntoCommand = new RelayCommand<ConjuntoItemVm>(RenameConjunto, c => c != null && CanEdit);
        AddRecursoCommand = new RelayCommand(AddRecurso, () => SelectedConjunto != null && CanEdit);
        DeleteRecursoCommand = new RelayCommand<ConjuntoRecursoVm>(DeleteRecurso, r => r != null && CanEdit);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());

        // Guardar conjuntos en la biblioteca global (banco_precios.db). Activo solo con cambios.
        SaveCommand = new RelayCommand(() => SaveConjuntos(), () => CanEdit && hasPendingChanges);

        hierarchyCoordinator = new PlantillasHierarchyCoordinator(
            () => CanEdit,
            () => SelectedConjunto,
            ConjuntoRecursos,
            session,
            conjuntoSvc,
            MarkConjuntosDirty,
            UpdateKpis,
            msg => StatusMessage = msg,
            OnPropertyChanged);

        // Selección múltiple de filas (barra contextual ámbar)
        MarcarMaestrosSeleccionCommand = hierarchyCoordinator.MarcarMaestrosSeleccionCommand;
        AsignarMaestroSeleccionCommand = hierarchyCoordinator.AsignarMaestroSeleccionCommand;
        LiberarSeleccionCommand = hierarchyCoordinator.LiberarSeleccionCommand;
        EliminarSeleccionCommand = hierarchyCoordinator.EliminarSeleccionCommand;
        ToggleAyudaCommand = new RelayCommand(() => ShowRulesStrip = !ShowRulesStrip);

        Load();
    }

    public ObservableCollection<ApuTemplateItemVm> Templates { get; }
    public ObservableCollection<ConjuntoItemVm> Conjuntos { get; }
    public ObservableCollection<ApuRecursoRow> TemplateRecursos { get; }
    public ObservableCollection<ConjuntoRecursoVm> ConjuntoRecursos { get; }

    public ApuTemplateItemVm? SelectedTemplate
    {
        get => selectedTemplate;
        set
        {
            if (!SetProperty(ref selectedTemplate, value)) return;
            LoadTemplateRecursos(value);
            ((RelayCommand<ApuTemplateItemVm>)DeleteTemplateCommand).RaiseCanExecuteChanged();
            ((RelayCommand<ApuTemplateItemVm>)DuplicateTemplateCommand).RaiseCanExecuteChanged();
            ((RelayCommand<ApuTemplateItemVm>)RenameTemplateCommand).RaiseCanExecuteChanged();
        }
    }

    public ConjuntoItemVm? SelectedConjunto
    {
        get => selectedConjunto;
        set
        {
            if (!SetProperty(ref selectedConjunto, value)) return;
            LoadConjuntoRecursos(value);
            ((RelayCommand)AddRecursoCommand).RaiseCanExecuteChanged();
            ((RelayCommand<ConjuntoRecursoVm>)DeleteRecursoCommand).RaiseCanExecuteChanged();
        }
    }

    public string TemplateFilter
    {
        get => templateFilter;
        set { SetProperty(ref templateFilter, value); ApplyTemplateFilter(); }
    }

    public string ConjuntoFilter
    {
        get => conjuntoFilter;
        set { SetProperty(ref conjuntoFilter, value); ApplyConjuntoFilter(); }
    }

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public int ActiveTab { get => activeTab; set => SetProperty(ref activeTab, value); }
    public bool HasTemplateStore => templateStore != null;
    public int KpiConjuntosCount => Conjuntos.Count;
    public int KpiTemplatesCount => Templates.Count;
    public int KpiTotalRecursos => Conjuntos.Sum(c => c.RecursoCount) + Templates.Sum(t => t.RecursoCount);
    public bool CanEdit => !session.IsReadOnly;
    public bool IsReadOnly => session.IsReadOnly;

    /// <summary>Ediciones de conjuntos sin guardar en la biblioteca global (indador «● sin guardar»).</summary>
    public bool HasPendingChanges => hasPendingChanges;

    // ── Selección múltiple de filas de la grilla ─────────────────────────────
    public int SelectedRecursosCount => hierarchyCoordinator.SelectedRecursosCount;
    public bool HasSelectedRecursos => hierarchyCoordinator.HasSelectedRecursos;
    public string SelectedRecursosLabel => hierarchyCoordinator.SelectedRecursosLabel;
    public IReadOnlyList<MaestroOptionVm> MaestrosParaSeleccion => hierarchyCoordinator.MaestrosParaSeleccion;

    /// <summary>Selección del combo «Convertir en esclavos de…»: ejecuta la acción masiva
    /// y vuelve a null para que el combo quede limpio (el label de al lado indica la acción).</summary>
    public MaestroOptionVm? MaestroDestinoSeleccion
    {
        get => hierarchyCoordinator.MaestroDestinoSeleccion;
        set => hierarchyCoordinator.MaestroDestinoSeleccion = value;
    }

    // ── Contadores de rol del conjunto activo (footer del editor) ────────────
    public int ConteoMaestros => hierarchyCoordinator.ConteoMaestros;
    public int ConteoEsclavos => hierarchyCoordinator.ConteoEsclavos;
    public int ConteoOperaciones => hierarchyCoordinator.ConteoOperaciones;

    // ── Jerarquía maestro→esclavo y vista previa de expansión ────────────────
    public ObservableCollection<JerarquiaNodoVm> Jerarquia => hierarchyCoordinator.Jerarquia;
    public ObservableCollection<PreviewFilaVm> PreviewFilas => hierarchyCoordinator.PreviewFilas;

    /// <summary>Strip de ayuda colapsable sobre la grilla (UI state).</summary>
    public bool ShowRulesStrip { get => showRulesStrip; set => SetProperty(ref showRulesStrip, value); }

    public ICommand CreateTemplateCommand { get; }
    public ICommand DeleteTemplateCommand { get; }
    public ICommand DuplicateTemplateCommand { get; }
    public ICommand RenameTemplateCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }

    // Conjunto Commands
    public ICommand CreateConjuntoCommand { get; }
    public ICommand DeleteConjuntoCommand { get; }
    public ICommand DuplicateConjuntoCommand { get; }
    public ICommand RenameConjuntoCommand { get; }
    public ICommand AddRecursoCommand { get; }
    public ICommand DeleteRecursoCommand { get; }
    public ICommand OpenProjectCommand { get; }

    /// <summary>Guarda los conjuntos en la biblioteca global (+ espejo al proyecto abierto).</summary>
    public ICommand SaveCommand { get; }

    // Selección múltiple (barra contextual ámbar de la grilla)
    public ICommand MarcarMaestrosSeleccionCommand { get; }
    public ICommand AsignarMaestroSeleccionCommand { get; }
    public ICommand LiberarSeleccionCommand { get; }
    public ICommand EliminarSeleccionCommand { get; }
    public ICommand ToggleAyudaCommand { get; }

    /// <summary>
    /// Política de moneda del proyecto: en CLP no se expone ninguna cifra en UF (columna P. UF
    /// del preview de recursos del conjunto).
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(session.ProjectInfo.MonedaPresupuesto, CurrencyConversionService.CurrencyClp, StringComparison.OrdinalIgnoreCase);

    public void Refresh()
    {
        Load();
        OnPropertyChanged(nameof(MuestraUf));
    }

    public void Load()
    {
        LoadTemplates();
        ReloadConjuntosBase();
        LoadConjuntos();
        // Plantillas APU y conjuntos viven en el store global (banco_precios.db): la
        // pantalla no depende del .qce abierto. El .qce solo recibe el espejo al guardar.
        ShowEmptyState = !HasTemplateStore;
        hasPendingChanges = false;
        OnPropertyChanged(nameof(HasPendingChanges));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        UpdateKpis();
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsReadOnly));
        ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CreateTemplateCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ImportCommand).RaiseCanExecuteChanged();
    }

    private void LoadTemplates()
    {
        Templates.Clear();
        if (templateStore == null) return;
        var filter = templateFilter.Trim();
        var all = string.IsNullOrWhiteSpace(filter)
            ? templateStore.GetAll()
            : templateStore.Search(filter);
        foreach (var t in all) Templates.Add(new ApuTemplateItemVm(t));
    }

    /// <summary>
    /// Carga la colección de trabajo desde la biblioteca global. Si la biblioteca no está
    /// disponible (banco_precios.db inaccesible) degrada a la sesión del proyecto, que es
    /// el comportamiento anterior. Adopción: conjuntos del .qce abierto que aún no están
    /// en la biblioteca (proyectos antiguos) se copian ahí para no perderlos.
    /// </summary>
    private void ReloadConjuntosBase()
    {
        if (conjuntoStore != null)
        {
            try
            {
                conjuntosBase = conjuntoStore.GetAll().ToList();
            }
            catch (System.Exception ex)
            {
                // Base ilegible: degradar esta sesión al modo anterior (solo .qce) para
                // no dejar la pantalla inutilizable ni arriesgar borrar la biblioteca.
                AppLog.Error("Templates", "Biblioteca global de conjuntos ilegible; se degrada a la sesión del proyecto", ex);
                conjuntoStore = null;
            }
        }

        if (conjuntoStore == null)
        {
            conjuntosBase = session.ConjuntosElectricos ?? new List<ConjuntoElectricoRow>();
            return;
        }

        var sesion = session.ConjuntosElectricos ?? new List<ConjuntoElectricoRow>();
        var faltantes = sesion
            .Where(s => s != null && !conjuntosBase.Any(g =>
                string.Equals(g.Id, s.Id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (faltantes.Count == 0) return;

        conjuntosBase.AddRange(faltantes);
        try
        {
            conjuntoStore.SaveAll(conjuntosBase);
        }
        catch (System.Exception ex)
        {
            // Visibles en memoria aunque falle la adopción persistida; el Guardar reintenta.
            AppLog.Error("Templates", "No se pudieron adoptar conjuntos del proyecto a la biblioteca global", ex);
        }
    }

    private void LoadConjuntos()
    {
        Conjuntos.Clear();
        var filter = conjuntoFilter.Trim().ToLowerInvariant();
        foreach (var c in conjuntosBase)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                c.Nombre.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.Codigo.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                Conjuntos.Add(new ConjuntoItemVm(c, () =>
                {
                    MarkConjuntosDirty();
                    UpdateKpis();
                }));
            }
        }
    }

    /// <summary>Marca la pantalla con cambios sin guardar en la biblioteca global.</summary>
    private void MarkConjuntosDirty()
    {
        if (conjuntoStore == null)
            session.MarkDirty(ProjectDirtyScope.ConjuntosElectricos); // modo degradado: solo .qce

        if (hasPendingChanges) return;
        hasPendingChanges = true;
        OnPropertyChanged(nameof(HasPendingChanges));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Guarda los conjuntos en la biblioteca global y, si hay proyecto abierto, deja una
    /// copia clonada en la sesión (el snapshot del .qce se clona al guardar, por eso no se
    /// comparten referencias con las filas de la VM). Nunca lanza: el error va a StatusMessage
    /// y la navegación no queda bloqueada.
    /// </summary>
    private bool SaveConjuntos()
    {
        string? errorGlobal = null;
        try
        {
            conjuntoStore?.SaveAll(conjuntosBase);
        }
        catch (System.Exception ex)
        {
            AppLog.Error("Templates", "No se pudieron guardar los conjuntos en la biblioteca global", ex);
            errorGlobal = ex.Message;
        }

        bool proyectoAbierto = !string.IsNullOrEmpty(session.CurrentProjectPath) && !session.IsReadOnly;
        bool espejoHecho = false;
        if (conjuntoStore != null && proyectoAbierto)
        {
            try
            {
                session.ConjuntosElectricos.Clear();
                foreach (var c in conjuntosBase)
                    session.ConjuntosElectricos.Add(ConjuntoElectricoService.CloneConjunto(c));
                session.MarkDirty(ProjectDirtyScope.ConjuntosElectricos);
                espejoHecho = true;
            }
            catch (System.Exception ex)
            {
                AppLog.Error("Templates", "No se pudo reflejar la biblioteca en el proyecto abierto", ex);
            }
        }

        hasPendingChanges = false;
        OnPropertyChanged(nameof(HasPendingChanges));
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();

        StatusMessage = errorGlobal != null
            ? "No se pudo guardar en la biblioteca global: " + errorGlobal +
              (espejoHecho ? " Los cambios quedaron en el proyecto abierto." : "")
            : conjuntoStore == null
                ? "Biblioteca global no disponible: plantillas solo en el proyecto abierto."
                : "Plantillas guardadas.";
        return true;
    }

    bool IPageWithPendingChanges.HasPendingChanges => hasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges()
    {
        Load();
        return true;
    }
    bool IPageWithCommitChanges.TryApplyChanges() => SaveConjuntos();

    private void LoadTemplateRecursos(ApuTemplateItemVm? item)
    {
        TemplateRecursos.Clear();
        if (item == null || string.IsNullOrWhiteSpace(item.Source.JsonRecursos)) return;
        try
        {
            var rows = JsonSerializer.Deserialize<List<ApuRecursoRow>>(item.Source.JsonRecursos,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rows != null) foreach (var r in rows) TemplateRecursos.Add(r);
        }
        catch { /* json inválido — ignorar */ }
    }

    private MaterialRow? LookupMaterial(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        return session.Materiales?.FirstOrDefault(m => string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadConjuntoRecursos(ConjuntoItemVm? item)
    {
        ConjuntoRecursos.Clear();
        if (item?.Source.Recursos != null)
            foreach (var r in item.Source.Recursos)
                ConjuntoRecursos.Add(CreateRecursoVm(r));

        hierarchyCoordinator.SetSelectedRecursos(null);
        hierarchyCoordinator.RefreshMaestroOptions();
    }

    private ConjuntoRecursoVm CreateRecursoVm(ConjuntoRecursoRow r) => new(
        r,
        () =>
        {
            MarkConjuntosDirty();
            UpdateKpis();
            hierarchyCoordinator.RebuildPreview();
        },
        LookupMaterial,
        hierarchyCoordinator.GetMaestroOptions,
        hierarchyCoordinator.RefreshMaestroOptions,
        msg => StatusMessage = msg,
        () => (IReadOnlyList<ConjuntoRecursoRow>?)SelectedConjunto?.Source.Recursos
            ?? Array.Empty<ConjuntoRecursoRow>());

    public void SetSelectedRecursos(IEnumerable<ConjuntoRecursoVm>? seleccion)
        => hierarchyCoordinator.SetSelectedRecursos(seleccion);

    public void RefreshMaestroOptions()
        => hierarchyCoordinator.RefreshMaestroOptions();

    public void RebuildPreview()
        => hierarchyCoordinator.RebuildPreview();

    private void ApplyTemplateFilter() => LoadTemplates();
    private void ApplyConjuntoFilter() => LoadConjuntos();

    private void UpdateKpis()
    {
        OnPropertyChanged(nameof(KpiConjuntosCount));
        OnPropertyChanged(nameof(KpiTemplatesCount));
        OnPropertyChanged(nameof(KpiTotalRecursos));
    }

    private void CreateTemplate()
    {
        if (!CanEdit || templateStore == null) return;

        string? nombre = WpfDialogService.PromptText("Nueva plantilla", "Nombre de la plantilla:", "");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        string? categoria = WpfDialogService.PromptText("Nueva plantilla", "Categoría:", "APU");
        if (categoria == null) return;

        var row = new ApuTemplateRow
        {
            Nombre = nombre.Trim(),
            Categoria = string.IsNullOrWhiteSpace(categoria) ? "APU" : categoria.Trim(),
            Descripcion = "",
            Unidad = "GL",
            JsonRecursos = "[]",
        };
        templateStore.Save(row);
        LoadTemplates();
        SelectedTemplate = Templates.FirstOrDefault(t =>
            string.Equals(t.Source.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        UpdateKpis();
        ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
    }

    private void DeleteTemplate(ApuTemplateItemVm? item)
    {
        if (!CanEdit || item == null || templateStore == null) return;
        if (!WpfDialogService.Confirm("Eliminar plantilla",
            $"¿Eliminar '{item.Nombre}'? Esta acción no se puede deshacer.", isWarning: true))
            return;
        templateStore.Delete(item.Source.Id);
        Templates.Remove(item);
        if (SelectedTemplate == item) SelectedTemplate = null;
        UpdateKpis();
        ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
        StatusMessage = $"Plantilla '{item.Nombre}' eliminada.";
    }

    private void DuplicateTemplate(ApuTemplateItemVm? item)
    {
        if (item == null || templateStore == null) return;
        var clone = new ApuTemplateRow
        {
            Nombre = item.Nombre + " (copia)",
            Categoria = item.Categoria,
            Descripcion = item.Descripcion,
            Unidad = item.Unidad,
            JsonRecursos = item.Source.JsonRecursos,
        };
        templateStore.Save(clone);
        Templates.Add(new ApuTemplateItemVm(clone));
        UpdateKpis();
        ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
    }

    private void RenameTemplate(ApuTemplateItemVm? item)
    {
        if (item == null || templateStore == null) return;

        string? nuevoNombre = WpfDialogService.PromptText(
            "Renombrar plantilla", "Nuevo nombre:", item.Nombre);
        if (string.IsNullOrWhiteSpace(nuevoNombre) ||
            string.Equals(nuevoNombre.Trim(), item.Nombre, StringComparison.Ordinal))
            return;

        item.Source.Nombre = nuevoNombre.Trim();
        item.Source.ActualizadoEn = AppClock.Now;
        templateStore.Save(item.Source);
        LoadTemplates();
        SelectedTemplate = Templates.FirstOrDefault(t =>
            string.Equals(t.Source.Id, item.Source.Id, StringComparison.OrdinalIgnoreCase));
        StatusMessage = $"Plantilla renombrada a '{nuevoNombre.Trim()}'.";
    }

    private void CreateConjunto()
    {
        if (!CanEdit) return;
        string? nombre = WpfDialogService.PromptText("Nueva plantilla de takeoff", "Nombre de la plantilla:", "");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var row = new ConjuntoElectricoRow
        {
            Codigo = "CONJ_" + (Conjuntos.Count + 1),
            Nombre = nombre.Trim(),
            TipoConjunto = "General",
            Descripcion = "",
            Unidad = "UN",
            Recursos = new List<ConjuntoRecursoRow>()
        };

        conjuntosBase.Add(row);
        MarkConjuntosDirty();
        LoadConjuntos();
        SelectedConjunto = Conjuntos.FirstOrDefault(c => string.Equals(c.Source.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        UpdateKpis();
        StatusMessage = $"Plantilla '{nombre.Trim()}' creada.";
    }

    private void DeleteConjunto(ConjuntoItemVm? item)
    {
        if (item == null || !CanEdit) return;
        if (!WpfDialogService.Confirm("Eliminar plantilla de takeoff", $"¿Eliminar la plantilla '{item.Nombre}'? Esta acción no se puede deshacer.", isWarning: true))
            return;

        conjuntosBase.Remove(item.Source);
        MarkConjuntosDirty();
        Conjuntos.Remove(item);
        if (SelectedConjunto == item) SelectedConjunto = null;
        UpdateKpis();
        StatusMessage = $"Plantilla '{item.Nombre}' eliminada.";
    }

    private void DuplicateConjunto(ConjuntoItemVm? item)
    {
        if (item == null) return;
        var clone = ConjuntoElectricoService.CloneConjunto(item.Source);
        clone.Codigo = item.Codigo + "_COPIA";
        clone.Nombre = item.Nombre + " (copia)";

        conjuntosBase.Add(clone);
        MarkConjuntosDirty();
        LoadConjuntos();
        SelectedConjunto = Conjuntos.FirstOrDefault(c => string.Equals(c.Source.Id, clone.Id, StringComparison.OrdinalIgnoreCase));
        UpdateKpis();
        StatusMessage = $"Plantilla '{item.Nombre}' duplicada.";
    }

    private void RenameConjunto(ConjuntoItemVm? item)
    {
        if (item == null || !CanEdit) return;
        string? nuevoNombre = WpfDialogService.PromptText("Renombrar plantilla de takeoff", "Nuevo nombre:", item.Nombre);
        if (string.IsNullOrWhiteSpace(nuevoNombre) || string.Equals(nuevoNombre.Trim(), item.Nombre, StringComparison.Ordinal))
            return;

        item.Nombre = nuevoNombre.Trim();
        MarkConjuntosDirty();
        StatusMessage = $"Plantilla renombrada a '{nuevoNombre.Trim()}'.";
    }

    private void AddRecurso()
    {
        if (SelectedConjunto == null || !CanEdit) return;

        // Combinar: materiales del proyecto activo + materiales del Banco de Precios global.
        // El banco global es la fuente principal cuando se definen plantillas de takeoff.
        var materialesProyecto = session.Materiales ?? new System.Collections.Generic.List<MaterialRow>();
        var materialesBanco = priceBankStore.GetAll()
            .Select(e => new MaterialRow
            {
                Codigo = e.Codigo ?? "",
                Descripcion = e.Descripcion ?? "",
                Unidad = e.Unidad ?? "",
                Categoria = e.Categoria ?? "",
                MatClp = e.MatClp,
                MoClp = e.MoClp,
                MonedaPrecio = string.IsNullOrWhiteSpace(e.MonedaPrecio) ? Moneda.CLP : e.MonedaPrecio,
                Proveedor = e.Proveedor ?? "",
                FechaPrecio = e.FechaPrecio == default ? System.DateTime.Today : e.FechaPrecio,
                OrigenPrecio = "Banco de precios"
            })
            .ToList();

        // Unir: primero el proyecto (prioridad para precios congelados), luego el banco
        var codigosProyecto = new System.Collections.Generic.HashSet<string>(
            materialesProyecto.Select(m => CodeHelper.Normalize(m.Codigo)),
            System.StringComparer.OrdinalIgnoreCase);
        var combinados = materialesProyecto
            .Concat(materialesBanco.Where(m => !codigosProyecto.Contains(CodeHelper.Normalize(m.Codigo))))
            .ToList();

        var dialog = new MaterialPickerDialog(combinados);
        if (System.Windows.Application.Current.MainWindow != null)
        {
            dialog.Owner = System.Windows.Application.Current.MainWindow;
        }

        if (dialog.ShowDialog() == true && dialog.SelectedMaterials.Count > 0)
        {
            foreach (var mat in dialog.SelectedMaterials)
            {
                var r = new ConjuntoRecursoRow
                {
                    Tipo = ApuTipoRecurso.MAT,
                    Codigo = mat.Codigo,
                    Descripcion = mat.Descripcion,
                    Unidad = mat.Unidad,
                    Rendimiento = 1m,
                    Factor = 1m,
                    PrecioUf = mat.MatUf,
                    MermaPct = 0m
                };

                SelectedConjunto.Source.Recursos.Add(r);
                ConjuntoRecursos.Add(CreateRecursoVm(r));
            }

            SelectedConjunto.RefreshRecursoCount();
            RefreshMaestroOptions();
            MarkConjuntosDirty();
            UpdateKpis();
            StatusMessage = $"{dialog.SelectedMaterials.Count} material(es) agregado(s) a la plantilla.";
        }
    }

    private void DeleteRecurso(ConjuntoRecursoVm? recurso)
    {
        if (SelectedConjunto == null || recurso == null || !CanEdit) return;
        SelectedConjunto.Source.Recursos.Remove(recurso.Source);
        int liberados = ConjuntoElectricoService.LiberarEsclavos(SelectedConjunto.Source, recurso.Source.Id);
        SelectedConjunto.RefreshRecursoCount();
        MarkConjuntosDirty();
        ConjuntoRecursos.Remove(recurso);
        RefreshMaestroOptions();
        UpdateKpis();
        StatusMessage = liberados > 0
            ? $"Material eliminado ({liberados} esclavo(s) liberado(s) como maestro)."
            : "Material eliminado de la plantilla.";
    }

    private void ImportLibrary()
    {
        if (templateStore == null) return;

        string? path = WpfDialogService.PromptOpenJson("Importar biblioteca de plantillas APU");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            var imported = ParseLibraryFile(path);
            if (imported.Count == 0)
            {
                WpfDialogService.Info("Importar biblioteca", "El archivo no contiene plantillas APU reconocidas.");
                return;
            }

            int added = 0;
            int updated = 0;
            var existing = templateStore.GetAll().ToDictionary(
                t => t.Nombre.Trim().ToUpperInvariant(), t => t);

            foreach (var tpl in imported)
            {
                string key = (tpl.Nombre ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (existing.TryGetValue(key, out var found))
                {
                    tpl.Id = found.Id;
                    tpl.CreadoEn = found.CreadoEn;
                    updated++;
                }
                else
                {
                    added++;
                }
                templateStore.Save(tpl);
            }

            LoadTemplates();
            UpdateKpis();
            ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
            StatusMessage = $"Biblioteca importada: +{added} nuevas, {updated} actualizadas.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Templates", "Falló importar biblioteca de plantillas", ex);
            WpfDialogService.Error("Importar biblioteca", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void ExportLibrary()
    {
        if (templateStore == null) return;

        string defaultName = "PlantillasAPU_" + AppClock.Now.ToString("yyyyMMdd");
        string? path = WpfDialogService.PromptSaveJson(defaultName);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var all = templateStore.GetAll().ToList();
            var file = new ApuTemplateLibraryFile
            {
                ExportedAt = AppClock.Now,
                Templates = all
            };
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            string json = JsonSerializer.Serialize(file, options);
            File.WriteAllText(path, json, Encoding.UTF8);
            StatusMessage = $"{all.Count} plantilla(s) exportadas a {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Templates", "Falló exportar biblioteca de plantillas", ex);
            WpfDialogService.Error("Exportar biblioteca", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private static List<ApuTemplateRow> ParseLibraryFile(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Intentar formato nativo ApuTemplateLibraryFile
        try
        {
            var lib = JsonSerializer.Deserialize<ApuTemplateLibraryFile>(json, options);
            if (lib?.Templates != null && lib.Templates.Count > 0)
                return lib.Templates;
        }
        catch { /* continuar */ }

        // Intentar lista directa de ApuTemplateRow
        try
        {
            var direct = JsonSerializer.Deserialize<List<ApuTemplateRow>>(json, options);
            if (direct != null && direct.Count > 0)
                return direct;
        }
        catch { /* continuar */ }

        throw new InvalidDataException("El archivo JSON no contiene plantillas APU reconocidas.");
    }

    private static IApuTemplateStore? TryGetTemplateStore()
    {
        try { return new ApuTemplateStore(); }
        catch { return null; }
    }

    private static IConjuntoTemplateStore? TryGetConjuntoStore()
    {
        try { return new ConjuntoTemplateStore(); }
        catch { return null; }
    }
}
