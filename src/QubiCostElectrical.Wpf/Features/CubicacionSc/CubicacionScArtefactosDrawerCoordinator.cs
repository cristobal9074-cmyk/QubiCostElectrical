using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class CubicacionScArtefactosDrawerCoordinator : ViewModelBase
{
    private readonly CubicacionScViewModel _vm;
    private readonly BudgetProjectSession _session;
    private CubicacionScConfig Config => _session.CubicacionScConfig ?? CubicacionScConfig.Default();

    private bool _isOpen;
    private CircuitoScRowVm? _activeCircuit;
    private ZonaArtefactoUi? _selectedZona;
    private string _searchText = "";
    private bool _isSearchPopupOpen;
    private readonly List<ArtefactoZonaItem> _todos = new();

    public event Action? RequestFocusSearch;
    public event Action? RequestFocusQuantity;

    // Modo cantidad (Calculadora inline)
    private bool _isQuantityMode;
    private ArtefactoSugerencia? _quantityTarget;
    private string _quantityTargetName = "";
    private string _quantityTargetTag = "";
    private string _quantityPrompt = "";
    private string _quantityText = "1";

    public ObservableCollection<ZonaArtefactoUi> Zonas { get; } = new();
    public ObservableCollection<ArtefactoZonaItem> Visibles { get; } = new();
    public ObservableCollection<ArtefactoSugerencia> Sugerencias { get; } = new();

    public bool IsSearchPopupOpen
    {
        get => _isOpen && _isSearchPopupOpen && !_isQuantityMode;
        set
        {
            if (SetProperty(ref _isSearchPopupOpen, value))
            {
                OnPropertyChanged(nameof(IsSearchPopupOpen));
            }
        }
    }

    public ICommand ToggleDrawerCommand { get; }
    public ICommand CloseDrawerCommand { get; }
    public ICommand SelectZonaCommand { get; }
    public ICommand SelectSugerenciaCommand { get; }
    public ICommand SelectAssignedCommand { get; }
    public ICommand ConfirmQuantityCommand { get; }
    public ICommand CancelQuantityCommand { get; }
    public ICommand AddQuickQtyCommand { get; }
    public ICommand IncrementItemCommand { get; }
    public ICommand DecrementItemCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand EditTagCommand { get; }

    public CubicacionScArtefactosDrawerCoordinator(CubicacionScViewModel vm, BudgetProjectSession session)
    {
        _vm = vm;
        _session = session;

        ToggleDrawerCommand = new RelayCommand(Toggle);
        CloseDrawerCommand = new RelayCommand(() => IsOpen = false);
        SelectZonaCommand = new RelayCommand<ZonaArtefactoUi>(SelectZona);
        SelectSugerenciaCommand = new RelayCommand<ArtefactoSugerencia>(OpenQuantityFromSugerencia);
        SelectAssignedCommand = new RelayCommand<ArtefactoZonaItem>(OpenQuantityFromAssigned);
        ConfirmQuantityCommand = new RelayCommand(ConfirmQuantity, () => _isQuantityMode);
        CancelQuantityCommand = new RelayCommand(CancelQuantity);
        AddQuickQtyCommand = new RelayCommand<string>(AddQuickQty);
        IncrementItemCommand = new RelayCommand<ArtefactoZonaItem>(IncrementItem);
        DecrementItemCommand = new RelayCommand<ArtefactoZonaItem>(DecrementItem);
        DeleteItemCommand = new RelayCommand<ArtefactoZonaItem>(DeleteItem);
        EditTagCommand = new RelayCommand<ArtefactoSugerencia>(EditTag);
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (SetProperty(ref _isOpen, value))
            {
                OnPropertyChanged(nameof(IsArtefactosDrawerOpen));
                _vm.NotifyArtefactosDrawerOpenChanged();
                if (value)
                {
                    if (_vm.CatalogoDrawer?.IsOpen == true)
                    {
                        _vm.CatalogoDrawer.IsOpen = false;
                    }
                    CancelQuantity();
                    RefreshSugerencias();
                    RequestFocusSearch?.Invoke();
                }
                else
                {
                    IsSearchPopupOpen = false;
                    CancelQuantity();
                }
            }
        }
    }

    public bool IsArtefactosDrawerOpen => IsOpen;

    public void Toggle()
    {
        IsOpen = !IsOpen;
    }

    public CircuitoScRowVm? ActiveCircuit => _activeCircuit;

    public string CircuitTitle => _activeCircuit != null
        ? $"Circuito {_activeCircuit.Numero}: {_activeCircuit.Descripcion}"
        : "Sin circuito seleccionado";

    public string CircuitTablero => _vm.SelectedCuadro != null
        ? $"({_vm.SelectedCuadro.Nombre})"
        : "";

    public string CircuitUbicacion => _activeCircuit?.Ubicacion ?? "";

    public bool HasMultipleZonas => Zonas.Count > 1;

    public ZonaArtefactoUi? SelectedZona
    {
        get => _selectedZona;
        set
        {
            if (SetProperty(ref _selectedZona, value))
            {
                CancelQuantity();
                RefreshVisibles();
                OnPropertyChanged(nameof(AssignedSectionTitle));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshSugerencias();
                IsSearchPopupOpen = !string.IsNullOrWhiteSpace(value) && Sugerencias.Count > 0 && !_isQuantityMode;
            }
        }
    }

    public string AssignedSectionTitle => _selectedZona != null
        ? $"EN {_selectedZona.Nombre.ToUpperInvariant()} ({Visibles.Count} ELEMENTOS)"
        : "EN ESTE CIRCUITO";

    public int TotalPisoActivo => (int)Math.Round(Visibles.Sum(v => v.Cantidad));

    public int TotalCircuito => (int)Math.Round(_todos.Where(t => t.Cantidad > 0).Sum(t => t.Cantidad));

    public string FooterPisoLabel => $"{TotalPisoActivo} u.";

    public string FooterCircuitoLabel => $"{TotalCircuito} unidades";

    // Propiedades de la calculadora / cantidad inline
    public bool IsQuantityMode
    {
        get => _isQuantityMode;
        private set
        {
            if (SetProperty(ref _isQuantityMode, value))
            {
                ((RelayCommand)ConfirmQuantityCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string QuantityTargetName
    {
        get => _quantityTargetName;
        private set => SetProperty(ref _quantityTargetName, value);
    }

    public string QuantityTargetTag
    {
        get => _quantityTargetTag;
        private set => SetProperty(ref _quantityTargetTag, value);
    }

    public string QuantityPrompt
    {
        get => _quantityPrompt;
        private set => SetProperty(ref _quantityPrompt, value);
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetProperty(ref _quantityText, value);
    }

    /// <summary>
    /// Abre el drawer para un circuito determinado. Si ya está abierto para este circuito, lo refresca.
    /// </summary>
    public void OpenForCircuit(CircuitoScRowVm circuit)
    {
        _activeCircuit = circuit;
        OnPropertyChanged(nameof(ActiveCircuit));
        OnPropertyChanged(nameof(CircuitTitle));
        OnPropertyChanged(nameof(CircuitTablero));
        OnPropertyChanged(nameof(CircuitUbicacion));

        LoadCircuitData(circuit);
        IsOpen = true;
    }

    private void LoadCircuitData(CircuitoScRowVm circuit)
    {
        _todos.Clear();
        foreach (var a in circuit.Source.Artefactos)
        {
            var artCfg = Config.Artefactos.FirstOrDefault(x => string.Equals(x.Nombre, a.Nombre, StringComparison.OrdinalIgnoreCase));
            var tag = artCfg?.Tag ?? "";
            var alt = a.AlturaBajada > 0 ? a.AlturaBajada : (artCfg?.AlturaBajada ?? 0m);
            _todos.Add(new ArtefactoZonaItem
            {
                Nombre = a.Nombre,
                Categoria = a.Categoria,
                Detalle = string.IsNullOrWhiteSpace(tag) ? a.Categoria : $"{a.Categoria} · Tag: {tag}",
                Tag = tag,
                ZonaId = a.ZonaId ?? "",
                Cantidad = a.Cantidad,
                AlturaBajada = alt
            });
        }

        Zonas.Clear();
        foreach (var piso in CubicacionScArtefactosDialog.ResolverPisos(circuit.Source, _session.ZonasProyecto, _todos.Select(a => a.ZonaId)))
        {
            Zonas.Add(new ZonaArtefactoUi { Id = piso.Id, Nombre = piso.Nombre });
        }

        var ids = Zonas.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var a in _todos.Where(a => !ids.Contains(a.ZonaId ?? "")))
        {
            a.ZonaId = "";
        }

        OnPropertyChanged(nameof(HasMultipleZonas));

        var defaultZona = Zonas.FirstOrDefault();
        _selectedZona = defaultZona;
        OnPropertyChanged(nameof(SelectedZona));

        RefreshConteos();
        RefreshVisibles();
        RefreshSugerencias();
        CancelQuantity();
    }

    public void SelectZona(ZonaArtefactoUi? zona)
    {
        if (zona == null || zona == _selectedZona) return;
        SelectedZona = zona;
    }

    public void ShiftZona(int delta)
    {
        if (Zonas.Count < 2 || _isQuantityMode) return;
        int i = _selectedZona == null ? 0 : Zonas.IndexOf(_selectedZona);
        i = (i + delta + Zonas.Count) % Zonas.Count;
        SelectZona(Zonas[i]);
    }

    private void RefreshConteos()
    {
        foreach (var z in Zonas)
        {
            z.Conteo = (int)Math.Round(_todos.Where(a => a.ZonaId == z.Id && a.Cantidad > 0).Sum(a => a.Cantidad));
        }

        OnPropertyChanged(nameof(TotalPisoActivo));
        OnPropertyChanged(nameof(TotalCircuito));
        OnPropertyChanged(nameof(FooterPisoLabel));
        OnPropertyChanged(nameof(FooterCircuitoLabel));
    }

    public void RefreshVisibles()
    {
        Visibles.Clear();
        if (_selectedZona == null) return;

        foreach (var a in _todos.Where(a => a.ZonaId == _selectedZona.Id).OrderBy(a => a.Categoria).ThenBy(a => a.Nombre))
        {
            Visibles.Add(a);
        }

        OnPropertyChanged(nameof(AssignedSectionTitle));
        OnPropertyChanged(nameof(TotalPisoActivo));
        OnPropertyChanged(nameof(FooterPisoLabel));
    }

    public static string DeduceTag(string nombre, string? currentTag)
    {
        if (!string.IsNullOrWhiteSpace(currentTag)) return currentTag;
        if (string.IsNullOrWhiteSpace(nombre)) return "";
        var parts = nombre.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && (parts[0].Equals("INT", StringComparison.OrdinalIgnoreCase)
                              || parts[0].Equals("ENCH", StringComparison.OrdinalIgnoreCase)
                              || parts[0].Equals("CINTA", StringComparison.OrdinalIgnoreCase)
                              || parts[0].Equals("DOWNLIGHT", StringComparison.OrdinalIgnoreCase)))
        {
            return parts[1];
        }
        if (parts.Length == 1 && parts[0].Length <= 6) return parts[0];
        return "";
    }

    public void RefreshSugerencias()
    {
        var q = (_searchText ?? "").Trim();
        Sugerencias.Clear();

        var query = Config.Artefactos.AsEnumerable();
        if (q.Length > 0)
        {
            query = query
                .Select(a =>
                {
                    string tagEff = DeduceTag(a.Nombre, a.Tag);
                    bool tagExact = !string.IsNullOrWhiteSpace(tagEff) && string.Equals(tagEff, q, StringComparison.OrdinalIgnoreCase);
                    bool tagStarts = !string.IsNullOrWhiteSpace(tagEff) && tagEff.StartsWith(q, StringComparison.OrdinalIgnoreCase);
                    bool tagContains = !string.IsNullOrWhiteSpace(tagEff) && tagEff.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool nameStarts = a.Nombre.StartsWith(q, StringComparison.OrdinalIgnoreCase);
                    bool nameContains = a.Nombre.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    return new
                    {
                        Item = a,
                        TagEff = tagEff,
                        TagExact = tagExact,
                        TagStarts = tagStarts,
                        TagContains = tagContains,
                        NameStarts = nameStarts,
                        NameContains = nameContains
                    };
                })
                .Where(x => x.TagContains || x.NameContains)
                .OrderByDescending(x => x.TagExact)
                .ThenByDescending(x => x.TagStarts)
                .ThenByDescending(x => x.NameStarts)
                .ThenByDescending(x => x.TagContains)
                .Select(x => x.Item);
        }

        foreach (var a in query.Take(15))
        {
            var tagDisplay = DeduceTag(a.Nombre, a.Tag);
            Sugerencias.Add(new ArtefactoSugerencia
            {
                Tag = tagDisplay,
                Nombre = a.Nombre,
                Categoria = a.Categoria,
                Detalle = string.IsNullOrWhiteSpace(tagDisplay) ? a.Categoria : $"{a.Categoria} • Tag: {tagDisplay}",
                AlturaBajada = a.AlturaBajada
            });
        }

        bool existe = Config.Artefactos.Any(a => string.Equals(a.Nombre, q, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(a.Tag) && string.Equals(a.Tag, q, StringComparison.OrdinalIgnoreCase)));
        if (q.Length > 0 && !existe)
        {
            var parts = q.Split(new[] { ':', '-' }, 2, StringSplitOptions.TrimEntries);
            string newTag = "";
            string newNombre = q;
            if (parts.Length == 2 && parts[0].Length <= 6)
            {
                newTag = parts[0].ToUpperInvariant();
                newNombre = parts[1];
            }
            else if (q.Length <= 5)
            {
                newTag = q.ToUpperInvariant();
            }

            Sugerencias.Add(new ArtefactoSugerencia
            {
                Tag = newTag,
                Nombre = newNombre,
                Categoria = "Nuevo",
                Detalle = string.IsNullOrWhiteSpace(newTag) ? "Crear y asignar nuevo material" : $"Crear nuevo material con tag [{newTag}]",
                EsNuevo = true
            });
        }
    }

    public void OpenQuantityFromSugerencia(ArtefactoSugerencia? sugerencia)
    {
        if (sugerencia == null || _selectedZona == null || string.IsNullOrWhiteSpace(sugerencia.Nombre)) return;

        IsSearchPopupOpen = false;
        _quantityTarget = sugerencia;
        QuantityTargetName = sugerencia.Nombre;
        QuantityTargetTag = string.IsNullOrWhiteSpace(sugerencia.Tag) ? "ART" : sugerencia.Tag;

        var existente = BuscarEnZona(sugerencia.Nombre);
        if (existente != null)
        {
            QuantityPrompt = $"Ya hay {FormatQty(existente.Cantidad)} en {_selectedZona.Nombre}. ¿Cuántos quedan en total o a sumar?";
            QuantityText = FormatQty(existente.Cantidad);
        }
        else
        {
            QuantityPrompt = $"Asignando a {_selectedZona.Nombre}. Ingrese cantidad (o suma ej: 5+3):";
            QuantityText = "1";
        }

        IsQuantityMode = true;
        RequestFocusQuantity?.Invoke();
    }

    public void OpenQuantityFromAssigned(ArtefactoZonaItem? item)
    {
        if (item == null || _selectedZona == null) return;

        var sugerencia = new ArtefactoSugerencia
        {
            Nombre = item.Nombre,
            Categoria = item.Categoria,
            Tag = item.Tag,
            Detalle = item.Categoria
        };

        OpenQuantityFromSugerencia(sugerencia);
    }

    public void CancelQuantity()
    {
        IsQuantityMode = false;
        _quantityTarget = null;
        RequestFocusSearch?.Invoke();
    }

    private void AddQuickQty(string? deltaStr)
    {
        if (int.TryParse(deltaStr, out int delta))
        {
            if (QuantityFormulaService.Evaluate(QuantityText) is { Success: true } res)
            {
                QuantityText = FormatQty(Math.Max(1, res.Value + delta));
            }
            else
            {
                QuantityText = delta.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    public void ConfirmQuantity()
    {
        if (!_isQuantityMode || _quantityTarget == null || _selectedZona == null) return;

        var eval = QuantityFormulaService.Evaluate(QuantityText);
        if (!eval.Success)
        {
            return;
        }

        decimal qty = eval.Value;
        AplicarCantidad(_quantityTarget, qty);

        CancelQuantity();
        SearchText = "";
        IsSearchPopupOpen = false;
        RefreshVisibles();
        RefreshConteos();
        RefreshSugerencias();
        CommitChangesToCircuit();
        RequestFocusSearch?.Invoke();
    }

    private void AplicarCantidad(ArtefactoSugerencia sugerencia, decimal qty)
    {
        if (_selectedZona == null) return;
        var nombre = sugerencia.Nombre.Trim();
        var categoria = string.IsNullOrWhiteSpace(sugerencia.Categoria) ? "Otro" : sugerencia.Categoria;
        bool esNuevo = sugerencia.EsNuevo || !Config.Artefactos.Any(a => string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        var existente = BuscarEnZona(nombre);

        if (qty <= 0)
        {
            if (existente != null) _todos.Remove(existente);
        }
        else if (existente != null)
        {
            existente.Cantidad = qty;
            existente.Categoria = categoria;
            if (!string.IsNullOrWhiteSpace(sugerencia.Tag)) existente.Tag = sugerencia.Tag;
        }
        else
        {
            var artCfg = Config.Artefactos.FirstOrDefault(x => string.Equals(x.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
            var alt = sugerencia.AlturaBajada > 0 ? sugerencia.AlturaBajada : (artCfg?.AlturaBajada ?? 0m);
            _todos.Add(new ArtefactoZonaItem
            {
                Nombre = nombre,
                Categoria = categoria,
                Tag = sugerencia.Tag,
                Detalle = string.IsNullOrWhiteSpace(sugerencia.Tag) ? categoria : $"{categoria} · Tag: {sugerencia.Tag}",
                ZonaId = _selectedZona.Id,
                Cantidad = qty,
                AlturaBajada = alt
            });
        }

        if (esNuevo && qty > 0)
        {
            if (!Config.Artefactos.Any(a => string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase)))
            {
                Config.Artefactos.Add(new ArtefactoCatalogoItem
                {
                    Nombre = nombre,
                    Categoria = categoria,
                    Tag = sugerencia.Tag ?? ""
                });
            }
        }
    }

    public void IncrementItem(ArtefactoZonaItem? item)
    {
        if (item == null) return;
        item.Cantidad += 1;
        RefreshConteos();
        CommitChangesToCircuit();
    }

    public void DecrementItem(ArtefactoZonaItem? item)
    {
        if (item == null) return;
        if (item.Cantidad > 1)
        {
            item.Cantidad -= 1;
        }
        else
        {
            _todos.Remove(item);
            Visibles.Remove(item);
        }
        RefreshConteos();
        CommitChangesToCircuit();
    }

    public void DeleteItem(ArtefactoZonaItem? item)
    {
        if (item == null) return;
        _todos.Remove(item);
        Visibles.Remove(item);
        RefreshConteos();
        CommitChangesToCircuit();
    }

    public void EditTag(ArtefactoSugerencia? sugerencia)
    {
        if (sugerencia == null || string.IsNullOrWhiteSpace(sugerencia.Nombre)) return;

        var art = Config.Artefactos.FirstOrDefault(a => string.Equals(a.Nombre, sugerencia.Nombre, StringComparison.OrdinalIgnoreCase));
        var currentTag = art?.Tag ?? sugerencia.Tag ?? "";

        var input = WpfDialogService.PromptText("Editar Tag Rápido", $"Tag abreviado para '{sugerencia.Nombre}' (ej: 9/12, FOCO, ENCH, EMER):", currentTag);
        if (input == null) return;

        var newTag = input.Trim().ToUpperInvariant();
        if (art != null)
        {
            art.Tag = newTag;
        }
        else
        {
            Config.Artefactos.Add(new ArtefactoCatalogoItem
            {
                Nombre = sugerencia.Nombre,
                Categoria = sugerencia.Categoria,
                Tag = newTag
            });
        }

        sugerencia.Tag = newTag;

        // Actualizar en items ya cargados si coincide el nombre
        foreach (var item in _todos.Where(t => string.Equals(t.Nombre, sugerencia.Nombre, StringComparison.OrdinalIgnoreCase)))
        {
            item.Tag = newTag;
        }

        RefreshSugerencias();
        RefreshVisibles();
        CommitChangesToCircuit();
    }

    public void CommitChangesToCircuit()
    {
        if (_activeCircuit == null) return;

        _activeCircuit.Source.Artefactos.Clear();
        foreach (var a in _todos.Where(a => a.Cantidad > 0))
        {
            _activeCircuit.Source.Artefactos.Add(new ArtefactoSCRow
            {
                Nombre = a.Nombre.Trim(),
                Categoria = a.Categoria,
                Cantidad = a.Cantidad,
                ZonaId = a.ZonaId ?? "",
                AlturaBajada = a.AlturaBajada
            });
        }

        _activeCircuit.ReloadArtefactos();
        _vm.SincronizarGrillaConSesion();
        _vm.MarkDirty();
        _vm.SetStatusMessage($"Artefactos de Circuito {_activeCircuit.Numero} actualizados ({TotalCircuito} u.).");
    }

    private ArtefactoZonaItem? BuscarEnZona(string nombre) =>
        _selectedZona == null ? null : _todos.FirstOrDefault(a =>
            a.ZonaId == _selectedZona.Id && string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    private static string FormatQty(decimal qty) =>
        qty == decimal.Truncate(qty) ? decimal.Truncate(qty).ToString(CultureInfo.InvariantCulture) : qty.ToString(CultureInfo.InvariantCulture);
}
