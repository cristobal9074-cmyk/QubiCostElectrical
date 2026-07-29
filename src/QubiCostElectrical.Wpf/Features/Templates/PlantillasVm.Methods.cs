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

public sealed partial class PlantillasVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    public PlantillasVm(IShellNavigationHost shell, WpfAppHost host)
    {
        this.shell = shell;
        this.host = host;
        session = host.Session;
        templateStore = TryGetTemplateStore();

        Templates = new ObservableCollection<ApuTemplateItemVm>();
        Conjuntos = new ObservableCollection<ConjuntoItemVm>();
        TemplateRecursos = new ObservableCollection<ApuRecursoRow>();
        ConjuntoRecursos = new ObservableCollection<ConjuntoRecursoVm>();

        CreateTemplateCommand = new RelayCommand(CreateTemplate, () => templateStore != null);
        DeleteTemplateCommand = new RelayCommand<ApuTemplateItemVm>(DeleteTemplate, t => t != null);
        DuplicateTemplateCommand = new RelayCommand<ApuTemplateItemVm>(DuplicateTemplate, t => t != null);
        RenameTemplateCommand = new RelayCommand<ApuTemplateItemVm>(RenameTemplate, t => t != null);
        ImportCommand = new RelayCommand(ImportLibrary, () => templateStore != null);
        ExportCommand = new RelayCommand(ExportLibrary, () => templateStore != null && Templates.Count > 0 && host.Permissions.CanExportDocuments);

        // Conjunto Commands
        CreateConjuntoCommand = new RelayCommand(CreateConjunto, () => CanEdit);
        DeleteConjuntoCommand = new RelayCommand<ConjuntoItemVm>(DeleteConjunto, c => c != null && CanEdit);
        DuplicateConjuntoCommand = new RelayCommand<ConjuntoItemVm>(DuplicateConjunto, c => c != null && CanEdit);
        RenameConjuntoCommand = new RelayCommand<ConjuntoItemVm>(RenameConjunto, c => c != null && CanEdit);
        AddRecursoCommand = new RelayCommand(AddRecurso, () => SelectedConjunto != null && CanEdit);
        DeleteRecursoCommand = new RelayCommand<ConjuntoRecursoVm>(DeleteRecurso, r => r != null && CanEdit);
        MoveRecursoUpCommand = new RelayCommand<ConjuntoRecursoVm>(MoveRecursoUp, r => r != null && CanEdit);
        MoveRecursoDownCommand = new RelayCommand<ConjuntoRecursoVm>(MoveRecursoDown, r => r != null && CanEdit);
        AddAnotherMaestroCommand = new RelayCommand(AddAnotherMaestro, () => SelectedConjunto != null && CanEdit);
        RemoveMaestroCommand = new RelayCommand<ConjuntoRecursoVm>(RemoveMaestro, r => r != null && CanEdit);
        ClearFilterCommand = new RelayCommand(() => { ConjuntoFilter = ""; });

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
    public ICommand MoveRecursoUpCommand { get; }
    public ICommand MoveRecursoDownCommand { get; }
    public ICommand AddAnotherMaestroCommand { get; }
    public ICommand RemoveMaestroCommand { get; }
    public ICommand ClearFilterCommand { get; }

    public ObservableCollection<ConjuntoRecursoVm> MaestroRecursoList { get; } = new();

    public int MaestroCount => MaestroRecursoList.Count;
    public int SlaveCount => ConjuntoRecursos.Count(r => r.IsSlave);
    public bool HasOrphanedSlaves => ConjuntoRecursos.Any(r => r.IsSlave && MaestroRecursoList.All(m => m.Id != r.MaestroRecursoId));
    public string OrphanedWarning => HasOrphanedSlaves
        ? $"⚠ {ConjuntoRecursos.Count(r => r.IsSlave && MaestroRecursoList.All(m => m.Id != r.MaestroRecursoId))} esclavo(s) hacen referencia a un recurso que ya no es maestro."
        : "";

    public void Refresh()
    {
        pendingSelectedConjuntoId = SelectedConjunto?.Source.Id;
        Load();
    }

    public void Load()
    {
        LoadTemplates();
        LoadConjuntos();
        RestoreSelectedConjunto();
        ShowEmptyState = templateStore == null && Conjuntos.Count == 0;
        UpdateKpis();
        ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
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

    private void LoadConjuntos()
    {
        Conjuntos.Clear();
        var filter = conjuntoFilter.Trim().ToLowerInvariant();
        foreach (var c in session.ConjuntosElectricos ?? new List<ConjuntoElectricoRow>())
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                c.Nombre.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.Codigo.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                Conjuntos.Add(new ConjuntoItemVm(c, () =>
                {
                    session.MarkDirty("conjuntos");
                    hasPendingChanges = true;
                    UpdateKpis();
                }));
            }
        }
    }

    private void RestoreSelectedConjunto()
    {
        if (pendingSelectedConjuntoId == null) return;
        var found = Conjuntos.FirstOrDefault(c =>
            string.Equals(c.Source.Id, pendingSelectedConjuntoId, StringComparison.OrdinalIgnoreCase));
        if (found != null)
            SelectedConjunto = found;
        pendingSelectedConjuntoId = null;
    }

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
        if (item?.Source.Recursos == null)
        {
            MaestroRecursoList.Clear();
            return;
        }

        // Build VMs first without adding to ConjuntoRecursos
        var vms = new List<ConjuntoRecursoVm>(item.Source.Recursos.Count);
        foreach (var r in item.Source.Recursos)
        {
            vms.Add(new ConjuntoRecursoVm(r,
                onValueChanged: () =>
                {
                    session.MarkDirty("conjuntos");
                    hasPendingChanges = true;
                    UpdateKpis();
                },
                onMaestroChanged: RefreshMaestroList,
                lookupMaterial: LookupMaterial));
        }

        // Populate MaestroRecursoList BEFORE adding VMs so DataGrid
        // ComboBox cells see the full ItemsSource on first bind.
        MaestroRecursoList.Clear();
        foreach (var vm in vms)
        {
            if (vm.IsMaster)
                MaestroRecursoList.Add(vm);
        }
        OnPropertyChanged(nameof(MaestroCount));
        OnPropertyChanged(nameof(SlaveCount));
        OnPropertyChanged(nameof(HasOrphanedSlaves));
        OnPropertyChanged(nameof(OrphanedWarning));

        // Now add to ObservableCollection → DataGrid creates rows
        // with MaestroRecursoList already populated
        foreach (var vm in vms)
        {
            ConjuntoRecursos.Add(vm);
            vm.RefreshMaestroBinding();
        }
    }

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
        if (templateStore == null) return;

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
        if (item == null || templateStore == null) return;
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

        session.ConjuntosElectricos.Add(row);
        session.MarkDirty("conjuntos");
        hasPendingChanges = true;
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

        session.ConjuntosElectricos.Remove(item.Source);
        session.MarkDirty("conjuntos");
        hasPendingChanges = true;
        Conjuntos.Remove(item);
        if (SelectedConjunto == item) SelectedConjunto = null;
        UpdateKpis();
        StatusMessage = $"Plantilla '{item.Nombre}' eliminada.";
    }

    private void DuplicateConjunto(ConjuntoItemVm? item)
    {
        if (item == null) return;
        var clone = new ConjuntoElectricoRow
        {
            Codigo = item.Codigo + "_COPIA",
            Nombre = item.Nombre + " (copia)",
            TipoConjunto = item.TipoConjunto,
            Descripcion = item.Descripcion,
            Unidad = item.Unidad,
            Recursos = CloneRecursos(item.Source.Recursos)
        };

        session.ConjuntosElectricos.Add(clone);
        session.MarkDirty("conjuntos");
        hasPendingChanges = true;
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
        session.MarkDirty("conjuntos");
        hasPendingChanges = true;
        StatusMessage = $"Plantilla renombrada a '{nuevoNombre.Trim()}'.";
    }

    private void AddRecurso()
    {
        if (SelectedConjunto == null || !CanEdit) return;

        var combinados = GetCombinedMateriales();
        var dialog = new MaterialPickerDialog(combinados);
        if (System.Windows.Application.Current.MainWindow != null)
            dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true && dialog.SelectedMaterials.Count > 0)
        {
            foreach (var mat in dialog.SelectedMaterials)
            {
                var r = CreateRecursoRow(mat);
                SelectedConjunto.Source.Recursos.Add(r);

                var vm = new ConjuntoRecursoVm(r,
                    onValueChanged: () =>
                    {
                        session.MarkDirty("conjuntos");
                        hasPendingChanges = true;
                        UpdateKpis();
                    },
                    onMaestroChanged: RefreshMaestroList,
                    lookupMaterial: LookupMaterial);

                ConjuntoRecursos.Add(vm);
            }

            SelectedConjunto.RefreshRecursoCount();
            session.MarkDirty("conjuntos");
            hasPendingChanges = true;
            UpdateKpis();
            StatusMessage = $"{dialog.SelectedMaterials.Count} material(es) agregado(s) a la plantilla.";
        }
    }

    private void DeleteRecurso(ConjuntoRecursoVm? recurso)
    {
        if (SelectedConjunto == null || recurso == null || !CanEdit) return;
        SelectedConjunto.Source.Recursos.Remove(recurso.Source);
        SelectedConjunto.RefreshRecursoCount();
        session.MarkDirty("conjuntos");
        hasPendingChanges = true;
        ConjuntoRecursos.Remove(recurso);
        UpdateKpis();
        StatusMessage = "Material eliminado de la plantilla.";
        RefreshMaestroList();
    }

    private void MoveRecursoUp(ConjuntoRecursoVm? recurso)
    {
        if (SelectedConjunto == null || recurso == null || !CanEdit) return;
        var idx = ConjuntoRecursos.IndexOf(recurso);
        if (idx <= 0) return;
        ConjuntoRecursos.Move(idx, idx - 1);
        SelectedConjunto.Source.Recursos.RemoveAt(idx);
        SelectedConjunto.Source.Recursos.Insert(idx - 1, recurso.Source);
        session.MarkDirty("conjuntos");
        hasPendingChanges = true;
    }

    private void MoveRecursoDown(ConjuntoRecursoVm? recurso)
    {
        if (SelectedConjunto == null || recurso == null || !CanEdit) return;
        var idx = ConjuntoRecursos.IndexOf(recurso);
        if (idx < 0 || idx >= ConjuntoRecursos.Count - 1) return;
        ConjuntoRecursos.Move(idx, idx + 1);
        SelectedConjunto.Source.Recursos.RemoveAt(idx);
        SelectedConjunto.Source.Recursos.Insert(idx + 1, recurso.Source);
        session.MarkDirty("conjuntos");
        hasPendingChanges = true;
    }

    private void RefreshMaestroList()
    {
        var masters = ConjuntoRecursos.Where(r => r.IsMaster).ToList();
        var existingIds = new HashSet<string>(MaestroRecursoList.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        var newIds = new HashSet<string>(masters.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        for (int i = MaestroRecursoList.Count - 1; i >= 0; i--)
        {
            if (!newIds.Contains(MaestroRecursoList[i].Id))
                MaestroRecursoList.RemoveAt(i);
        }
        foreach (var m in masters)
        {
            if (!existingIds.Contains(m.Id))
                MaestroRecursoList.Add(m);
        }
        OnPropertyChanged(nameof(MaestroCount));
        OnPropertyChanged(nameof(SlaveCount));
        OnPropertyChanged(nameof(HasOrphanedSlaves));
        OnPropertyChanged(nameof(OrphanedWarning));
        foreach (var r in ConjuntoRecursos)
            r.RefreshMaestroBinding();
    }

    private void AddAnotherMaestro()
    {
        if (SelectedConjunto == null || !CanEdit) return;

        var combinados = GetCombinedMateriales();
        var dialog = new MaterialPickerDialog(combinados);
        if (System.Windows.Application.Current.MainWindow != null)
            dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true && dialog.SelectedMaterials.Count > 0)
        {
            foreach (var mat in dialog.SelectedMaterials)
            {
                var r = CreateRecursoRow(mat, esMaestro: true);
                SelectedConjunto.Source.Recursos.Add(r);

                var vm = new ConjuntoRecursoVm(r,
                    onValueChanged: () =>
                    {
                        session.MarkDirty("conjuntos");
                        hasPendingChanges = true;
                        UpdateKpis();
                    },
                    onMaestroChanged: RefreshMaestroList,
                    lookupMaterial: LookupMaterial);

                ConjuntoRecursos.Add(vm);
            }

            SelectedConjunto.RefreshRecursoCount();
            session.MarkDirty("conjuntos");
            hasPendingChanges = true;
            UpdateKpis();
            RefreshMaestroList();
            StatusMessage = $"{dialog.SelectedMaterials.Count} maestro(s) agregado(s) a la plantilla.";
        }
    }

    private void RemoveMaestro(ConjuntoRecursoVm? recurso)
    {
        if (recurso == null || !CanEdit) return;
        recurso.EsMaestro = false;
        RefreshMaestroList();
    }

    private List<MaterialRow> GetCombinedMateriales()
    {
        var materialesProyecto = session.Materiales ?? new List<MaterialRow>();
        var materialesBanco = host.Services.PriceBankStore.GetAll()
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
                FechaPrecio = e.FechaPrecio == default ? DateTime.Today : e.FechaPrecio,
                OrigenPrecio = "Banco de precios"
            })
            .ToList();

        var codigosProyecto = new HashSet<string>(
            materialesProyecto.Select(m => CodeHelper.Normalize(m.Codigo)),
            StringComparer.OrdinalIgnoreCase);

        return materialesProyecto
            .Concat(materialesBanco.Where(m => !codigosProyecto.Contains(CodeHelper.Normalize(m.Codigo))))
            .ToList();
    }

    private static ConjuntoRecursoRow CreateRecursoRow(MaterialRow mat, bool esMaestro = false)
    {
        return new ConjuntoRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            Codigo = mat.Codigo,
            Descripcion = mat.Descripcion,
            Unidad = mat.Unidad,
            Rendimiento = 1m,
            Factor = 1m,
            EsMaestro = esMaestro,
            TipoCalculo = TipoCalculoParametrico.Factor,
            PrecioUf = mat.MatUf,
            MermaPct = 0m
        };
    }

    private static List<ConjuntoRecursoRow> CloneRecursos(List<ConjuntoRecursoRow> source)
    {
        var oldToNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clones = source.Select(r =>
        {
            var newId = Guid.NewGuid().ToString("N");
            oldToNew[r.Id] = newId;
            return new ConjuntoRecursoRow
            {
                Id = newId,
                Tipo = r.Tipo,
                Codigo = r.Codigo,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Rendimiento = r.Rendimiento,
                Factor = r.Factor,
                PrecioUf = r.PrecioUf,
                ValorHHUf = r.ValorHHUf,
                MermaPct = r.MermaPct,
                TipoCalculo = r.TipoCalculo,
                Espaciamiento = r.Espaciamiento,
                EsMaestro = r.EsMaestro,
                MaestroRecursoId = "",
                FormulaOperator = r.FormulaOperator,
                Formula = r.Formula
            };
        }).ToList();

        foreach (var clone in clones)
        {
            var orig = source.FirstOrDefault(s => oldToNew[s.Id] == clone.Id);
            if (orig == null || string.IsNullOrWhiteSpace(orig.MaestroRecursoId)) continue;
            if (oldToNew.TryGetValue(orig.MaestroRecursoId, out var newMaestroId))
                clone.MaestroRecursoId = newMaestroId;
        }

        return clones;
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
}
