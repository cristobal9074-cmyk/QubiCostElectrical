using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class CubicacionScCatalogoDrawerCoordinator : ViewModelBase
{
    private readonly CubicacionScViewModel _vm;
    private readonly BudgetProjectSession _session;

    private bool _isOpen;
    private string _searchText = "";
    private string _selectedCategory = "Artefactos";
    private string _newItemText = "";
    private string _newItemTag = "";
    private string _newItemAltura = "";
    public string NewItemAltura
    {
        get => _newItemAltura;
        set => SetProperty(ref _newItemAltura, value);
    }

    private string _bulkAlturaInterruptorText = "1.30";
    public string BulkAlturaInterruptorText
    {
        get => _bulkAlturaInterruptorText;
        set => SetProperty(ref _bulkAlturaInterruptorText, value);
    }

    private string _bulkAlturaEnchufeText = "2.20";
    public string BulkAlturaEnchufeText
    {
        get => _bulkAlturaEnchufeText;
        set => SetProperty(ref _bulkAlturaEnchufeText, value);
    }

    private readonly List<CatalogoDrawerItem> _allItems = new();
    public ObservableCollection<CatalogoDrawerItem> FilteredItems { get; } = new();

    public static readonly string[] CategoryKeys = new[]
    {
        "Artefactos",
        "Alim",
        "Conductores",
        "Secciones",
        "TiposTuberia",
        "Diametros",
        "Sistemas",
        "Bandejas",
        "Cajas",
        "Flexibles"
    };

    private CatalogoDrawerItem? _selectedItem;
    public CatalogoDrawerItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                if (_selectedItem != null) _selectedItem.IsSelected = false;
                _selectedItem = value;
                if (_selectedItem != null) _selectedItem.IsSelected = true;
                OnPropertyChanged(nameof(SelectedItem));
            }
        }
    }

    public void NextCategory()
    {
        int idx = Array.IndexOf(CategoryKeys, SelectedCategory);
        if (idx < 0) idx = 0;
        int next = (idx + 1) % CategoryKeys.Length;
        SelectedCategory = CategoryKeys[next];
    }

    public void PreviousCategory()
    {
        int idx = Array.IndexOf(CategoryKeys, SelectedCategory);
        if (idx < 0) idx = 0;
        int prev = (idx - 1 + CategoryKeys.Length) % CategoryKeys.Length;
        SelectedCategory = CategoryKeys[prev];
    }

    public void SelectNextItem()
    {
        if (FilteredItems.Count == 0) return;
        if (SelectedItem == null)
        {
            SelectedItem = FilteredItems[0];
            return;
        }
        int idx = FilteredItems.IndexOf(SelectedItem);
        if (idx < 0)
        {
            SelectedItem = FilteredItems[0];
        }
        else if (idx < FilteredItems.Count - 1)
        {
            SelectedItem = FilteredItems[idx + 1];
        }
    }

    public void SelectPreviousItem()
    {
        if (FilteredItems.Count == 0) return;
        if (SelectedItem == null)
        {
            SelectedItem = FilteredItems[0];
            return;
        }
        int idx = FilteredItems.IndexOf(SelectedItem);
        if (idx > 0)
        {
            SelectedItem = FilteredItems[idx - 1];
        }
    }

    public void EditItemMerma(CatalogoDrawerItem? item)
    {
        if (item == null || item.RawItem is not SistemaSC s) return;

        var current = s.MermaPct.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var input = WpfDialogService.PromptText(
            "Editar Merma",
            $"Porcentaje de merma para {item.DisplayName} (%):",
            current);

        if (input == null) return;

        var clean = input.Trim().Replace('%', ' ').Trim();
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var newPct) ||
            decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out newPct))
        {
            if (newPct < 0) newPct = 0;
            if (newPct > 100) newPct = 100;

            s.MermaPct = newPct;
            item.MermaText = $"Merma {newPct:N0}%";

            // Sincronizar circuitos activos que usan esta fase
            foreach (var cuadro in _vm.Cuadros)
            {
                foreach (var c in cuadro.Source.Circuitos)
                {
                    if (string.Equals(c.Sistema, s.Nombre, StringComparison.OrdinalIgnoreCase))
                    {
                        c.MermaPct = newPct;
                    }
                }
            }

            _vm.MarkDirty();
            _vm.SetStatusMessage($"Merma de '{item.DisplayName}' actualizada a {newPct:N0}%.");
        }
    }

    public void EditItemAltura(CatalogoDrawerItem? item)
    {
        if (item == null || item.RawItem is not ArtefactoCatalogoItem a) return;

        var current = a.AlturaBajada > 0 ? a.AlturaBajada.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "0";
        var input = WpfDialogService.PromptText(
            "Editar Altura de Bajada",
            $"Metros de bajada / subida vertical para '{item.DisplayName}' (m):",
            current);

        if (input == null) return;

        var clean = input.Trim().Replace("m", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var newAlt) ||
            decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out newAlt))
        {
            if (newAlt < 0m) newAlt = 0m;
            a.AlturaBajada = newAlt;
            item.AlturaBajada = newAlt;

            // Recalcular circuitos activos que usan este artefacto
            foreach (var cuadro in _vm.Cuadros)
            {
                foreach (var cir in cuadro.Source.Circuitos)
                {
                    foreach (var art in cir.Artefactos)
                    {
                        if (string.Equals(art.Nombre, a.Nombre, StringComparison.OrdinalIgnoreCase))
                            art.AlturaBajada = newAlt;
                    }
                }
            }
            foreach (var c in _vm.Circuitos)
            {
                c.ReloadArtefactos();
            }

            _vm.MarkDirty();
            _vm.RefreshCatalogCollections();
            _vm.SetStatusMessage($"Altura de bajada para '{item.DisplayName}' actualizada a {newAlt:0.##} m.");
        }
    }

    public static bool IsInterruptor(ArtefactoCatalogoItem a) =>
        string.Equals(a.Categoria, "Interruptor", StringComparison.OrdinalIgnoreCase) ||
        a.Nombre.StartsWith("INT", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrEmpty(a.Tag) && a.Tag.StartsWith("INT", StringComparison.OrdinalIgnoreCase));

    public static bool IsEnchufe(ArtefactoCatalogoItem a) =>
        string.Equals(a.Categoria, "Enchufe", StringComparison.OrdinalIgnoreCase) ||
        a.Nombre.StartsWith("ENCH", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrEmpty(a.Tag) && a.Tag.StartsWith("ENCH", StringComparison.OrdinalIgnoreCase));

    public void ApplyBulkAlturaInterruptores() => ApplyBulkAltura(isInterruptor: true, BulkAlturaInterruptorText);

    public void ApplyBulkAlturaEnchufes() => ApplyBulkAltura(isInterruptor: false, BulkAlturaEnchufeText);

    private void ApplyBulkAltura(bool isInterruptor, string textValue)
    {
        var clean = (textValue ?? "").Trim().Replace("m", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (!decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var newAlt) &&
            !decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out newAlt))
        {
            _vm.SetStatusMessage("Por favor ingrese una altura numérica válida en metros (ej. 1.5).");
            return;
        }

        if (newAlt < 0m) newAlt = 0m;

        var config = _session.CubicacionScConfig;
        if (config == null) return;

        var targetItems = config.Artefactos
            .Where(a => isInterruptor ? IsInterruptor(a) : IsEnchufe(a))
            .ToList();

        if (targetItems.Count == 0)
        {
            _vm.SetStatusMessage($"No se encontraron {(isInterruptor ? "interruptores" : "enchufes")} en el catálogo.");
            return;
        }

        var targetNames = new HashSet<string>(targetItems.Select(a => a.Nombre), StringComparer.OrdinalIgnoreCase);

        foreach (var art in targetItems)
        {
            art.AlturaBajada = newAlt;
        }

        foreach (var drawerItem in _allItems)
        {
            if (drawerItem.RawItem is ArtefactoCatalogoItem art && targetNames.Contains(art.Nombre))
            {
                drawerItem.AlturaBajada = newAlt;
            }
        }

        foreach (var cuadro in _vm.Cuadros)
        {
            foreach (var cir in cuadro.Source.Circuitos)
            {
                foreach (var art in cir.Artefactos)
                {
                    if (targetNames.Contains(art.Nombre))
                        art.AlturaBajada = newAlt;
                }
            }
        }

        foreach (var c in _vm.Circuitos)
        {
            c.ReloadArtefactos();
        }

        _vm.MarkDirty();
        _vm.RefreshCatalogCollections();

        string label = isInterruptor ? "interruptores" : "enchufes";
        _vm.SetStatusMessage($"Altura de bajada actualizada a {newAlt:0.##} m para todos los {label} ({targetItems.Count} artefactos).");
    }

    public void EditItemTag(CatalogoDrawerItem? item)
    {
        if (item == null) return;

        var input = WpfDialogService.PromptText(
            "Editar Tag",
            $"Tag abreviado para '{item.DisplayName}':",
            item.Tag);

        if (input == null) return;

        var newTag = input.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(newTag)) return;

        if (item.RawItem is SistemaSC s)
        {
            var oldCode = s.Nombre;
            s.Nombre = newTag;
            item.Code = newTag;
            item.Tag = newTag;

            foreach (var cuadro in _vm.Cuadros)
            {
                foreach (var c in cuadro.Source.Circuitos)
                {
                    if (string.Equals(c.Sistema, oldCode, StringComparison.OrdinalIgnoreCase))
                    {
                        c.Sistema = newTag;
                    }
                }
            }

            _vm.MarkDirty();
            _vm.RefreshCatalogCollections();
            _vm.SetStatusMessage($"Tag de '{item.DisplayName}' actualizado a '{newTag}'.");
        }
        else if (item.RawItem is SistemaCircuitoSC sc)
        {
            var oldCode = sc.Codigo;
            sc.Codigo = newTag;
            item.Code = newTag;
            item.Tag = newTag;

            foreach (var cuadro in _vm.Cuadros)
            {
                foreach (var c in cuadro.Source.Circuitos)
                {
                    if (string.Equals(c.Tipo, oldCode, StringComparison.OrdinalIgnoreCase))
                    {
                        c.Tipo = newTag;
                    }
                }
            }

            _vm.MarkDirty();
            _vm.RefreshCatalogCollections();
            _vm.SetStatusMessage($"Tag de sistema actualizado a '{newTag}'.");
        }
        else if (item.RawItem is ArtefactoCatalogoItem art)
        {
            art.Tag = newTag;
            item.Tag = newTag;
            _vm.MarkDirty();
            _vm.SetStatusMessage($"Tag de '{art.Nombre}' actualizado a '{newTag}'.");
        }
    }

    public void EditItemName(CatalogoDrawerItem? item)
    {
        if (item == null) return;

        var input = WpfDialogService.PromptText(
            "Editar Nombre",
            "Nombre descriptivo:",
            item.DisplayName);

        if (input == null) return;

        var newName = input.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(newName)) return;

        if (item.RawItem is SistemaSC s)
        {
            s.Descripcion = newName;
            item.DisplayName = newName;
            _vm.MarkDirty();
            _vm.SetStatusMessage($"Nombre actualizado a '{newName}'.");
        }
        else if (item.RawItem is SistemaCircuitoSC sc)
        {
            sc.Nombre = newName;
            item.DisplayName = newName;
            _vm.MarkDirty();
            _vm.SetStatusMessage($"Sistema actualizado a '{newName}'.");
        }
        else if (item.RawItem is ArtefactoCatalogoItem art)
        {
            art.Nombre = newName;
            item.DisplayName = newName;
            _vm.MarkDirty();
            _vm.SetStatusMessage($"Artefacto actualizado a '{newName}'.");
        }
        else if (item.RawItem is string oldStr)
        {
            var config = _session.CubicacionScConfig;
            if (config != null)
            {
                switch (item.Category)
                {
                    case "Conductores":
                        int idxC = config.TiposConductor.FindIndex(c => string.Equals(c, oldStr, StringComparison.OrdinalIgnoreCase));
                        if (idxC >= 0) config.TiposConductor[idxC] = newName;
                        break;
                    case "Secciones":
                        int idxS = config.SeccionesConductor.FindIndex(s => string.Equals(s, oldStr, StringComparison.OrdinalIgnoreCase));
                        if (idxS >= 0) config.SeccionesConductor[idxS] = newName;
                        break;
                    case "TiposTuberia":
                        int idxT = config.TiposTuberia.FindIndex(t => string.Equals(t, oldStr, StringComparison.OrdinalIgnoreCase));
                        if (idxT >= 0) config.TiposTuberia[idxT] = newName;
                        break;
                    case "Diametros":
                        int idxD = config.DiametrosTuberia.FindIndex(d => string.Equals(d, oldStr, StringComparison.OrdinalIgnoreCase));
                        if (idxD >= 0) config.DiametrosTuberia[idxD] = newName;
                        break;
                    case "Bandejas":
                        int idxB = config.TiposBandeja.FindIndex(b => string.Equals(b, oldStr, StringComparison.OrdinalIgnoreCase));
                        if (idxB >= 0) config.TiposBandeja[idxB] = newName;
                        break;
                    case "Cajas":
                        int idxK = config.TiposCaja.FindIndex(k => string.Equals(k, oldStr, StringComparison.OrdinalIgnoreCase));
                        if (idxK >= 0) config.TiposCaja[idxK] = newName;
                        break;
                }
                item.Code = newName;
                item.DisplayName = newName;
                item.RawItem = newName;
                _vm.MarkDirty();
                _vm.RefreshCatalogCollections();
                _vm.SetStatusMessage($"Material actualizado a '{newName}'.");
            }
        }
    }

    public void ApplySelectedItem()
    {
        if (SelectedItem != null)
        {
            ApplyItemToActiveCircuit(SelectedItem);
        }
    }

    public void StartEditTag(CatalogoDrawerItem? item)
    {
        if (item == null || !item.CanEditTag) return;
        foreach (var it in _allItems)
        {
            if (it.IsEditingTag && it != item)
            {
                it.IsEditingTag = false;
            }
        }
        item.EditTagText = item.Tag;
        item.IsEditingTag = true;
    }

    public void CommitEditTag(CatalogoDrawerItem? item)
    {
        if (item == null) return;
        var newTag = (item.EditTagText ?? "").Trim().ToUpperInvariant();
        item.Tag = newTag;
        item.IsEditingTag = false;

        if (item.RawItem is ArtefactoCatalogoItem art)
        {
            art.Tag = newTag;
            _vm.MarkDirty();
            _vm.SetStatusMessage(string.IsNullOrWhiteSpace(newTag)
                ? $"Tag eliminado de '{art.Nombre}'."
                : $"Tag de '{art.Nombre}' actualizado a '{newTag}'.");
        }
    }

    public void CancelEditTag(CatalogoDrawerItem? item)
    {
        if (item == null) return;
        item.IsEditingTag = false;
    }

    public CubicacionScCatalogoDrawerCoordinator(CubicacionScViewModel vm, BudgetProjectSession session)
    {
        _vm = vm;
        _session = session;

        SelectCategoryCommand = new RelayCommand<string>(cat =>
        {
            if (!string.IsNullOrWhiteSpace(cat))
            {
                SelectedCategory = cat;
            }
        });

        ApplyItemCommand = new RelayCommand<CatalogoDrawerItem>(ApplyItemToActiveCircuit, item => item != null && HasActiveCircuit);
        DeleteItemCommand = new RelayCommand<CatalogoDrawerItem>(DeleteItem, item => item != null && _vm.CanEdit);
        AddItemCommand = new RelayCommand(AddItem, () => (!string.IsNullOrWhiteSpace(NewItemText) || !string.IsNullOrWhiteSpace(NewItemTag)) && _vm.CanEdit);
        ToggleDrawerCommand = new RelayCommand(Toggle);
        CloseDrawerCommand = new RelayCommand(() => IsOpen = false);
        ApplyBulkAlturaInterruptoresCommand = new RelayCommand(ApplyBulkAlturaInterruptores, () => _vm.CanEdit);
        ApplyBulkAlturaEnchufesCommand = new RelayCommand(ApplyBulkAlturaEnchufes, () => _vm.CanEdit);

        RefreshData();
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (SetProperty(ref _isOpen, value))
            {
                OnPropertyChanged(nameof(IsCatalogoDrawerOpen));
                _vm.NotifyCatalogoDrawerOpenChanged();
                if (value)
                {
                    if (_vm.ArtefactosDrawer?.IsOpen == true)
                    {
                        _vm.ArtefactosDrawer.IsOpen = false;
                    }
                    RefreshData();
                }
            }
        }
    }

    public bool IsCatalogoDrawerOpen => IsOpen;

    public void Toggle()
    {
        IsOpen = !IsOpen;
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(IsConductoresSelected));
                OnPropertyChanged(nameof(IsSeccionesSelected));
                OnPropertyChanged(nameof(IsTuberiasSelected));
                OnPropertyChanged(nameof(IsDiametrosSelected));
                OnPropertyChanged(nameof(IsAlimSelected));
                OnPropertyChanged(nameof(IsSistemasSelected));
                OnPropertyChanged(nameof(IsArtefactosSelected));
                OnPropertyChanged(nameof(IsBandejasSelected));
                OnPropertyChanged(nameof(IsCajasSelected));
                OnPropertyChanged(nameof(IsFlexiblesSelected));
                OnPropertyChanged(nameof(CategoryTitle));
                OnPropertyChanged(nameof(CategorySubtitle));
                ApplyFilter();
            }
        }
    }

    public bool IsConductoresSelected => SelectedCategory == "Conductores";
    public bool IsSeccionesSelected => SelectedCategory == "Secciones";
    public bool IsTuberiasSelected => SelectedCategory == "TiposTuberia";
    public bool IsDiametrosSelected => SelectedCategory == "Diametros";
    public bool IsAlimSelected => SelectedCategory == "Alim";
    public bool IsSistemasSelected => SelectedCategory == "Sistemas";
    public bool IsArtefactosSelected => SelectedCategory == "Artefactos";
    public bool IsBandejasSelected => SelectedCategory == "Bandejas";
    public bool IsCajasSelected => SelectedCategory == "Cajas";
    public bool IsFlexiblesSelected => SelectedCategory == "Flexibles";

    public string CategoryTitle => SelectedCategory switch
    {
        "Conductores" => "Conductores y Cables",
        "Secciones" => "Secciones de Conductor",
        "TiposTuberia" => "Tuberías y Canalizaciones",
        "Diametros" => "Diámetros de Tubería",
        "Alim" => "Alimentación (ALIM)",
        "Sistemas" => "Sistemas de Circuitos (SIST)",
        "Artefactos" => "Artefactos y Accesorios",
        "Bandejas" => "Tipos de Bandeja",
        "Cajas" => "Cajas de Derivación",
        "Flexibles" => "Terminales Flexibles",
        _ => SelectedCategory
    };

    public string CategorySubtitle => SelectedCategory switch
    {
        "Conductores" => "Cables disponibles para bandejas y tuberías en este proyecto",
        "Secciones" => "Calibres y secciones normalizadas en mm²",
        "TiposTuberia" => "Ductos para tramo PVC y tuberías conduit",
        "Diametros" => "Diámetros exteriores normalizados para canalizaciones",
        "Alim" => "Alimentación (M=Monofásico, T=Trifásico, EM=Emergencia)",
        "Sistemas" => "Códigos de función (A=Alumbrado, F=Fuerza, C=Clima, etc.)",
        "Artefactos" => "Equipos, luminarias y placas para distribución por zona",
        "Bandejas" => "Bandejas portaconductor para tramo de distribución",
        "Cajas" => "Cajas de paso, registro y derivación de circuito",
        "Flexibles" => "Terminales flexibles por diámetro de salida",
        _ => ""
    };

    public string NewItemText
    {
        get => _newItemText;
        set
        {
            if (SetProperty(ref _newItemText, value))
            {
                ((RelayCommand)AddItemCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string NewItemTag
    {
        get => _newItemTag;
        set
        {
            if (SetProperty(ref _newItemTag, value))
            {
                ((RelayCommand)AddItemCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public int CountConductores => _allItems.Count(i => i.Category == "Conductores");
    public int CountSecciones => _allItems.Count(i => i.Category == "Secciones");
    public int CountTuberias => _allItems.Count(i => i.Category == "TiposTuberia");
    public int CountDiametros => _allItems.Count(i => i.Category == "Diametros");
    public int CountAlim => _allItems.Count(i => i.Category == "Alim");
    public int CountSistemas => _allItems.Count(i => i.Category == "Sistemas");
    public int CountArtefactos => _allItems.Count(i => i.Category == "Artefactos");
    public int CountBandejas => _allItems.Count(i => i.Category == "Bandejas");
    public int CountCajas => _allItems.Count(i => i.Category == "Cajas");
    public int CountFlexibles => _allItems.Count(i => i.Category == "Flexibles");

    public CircuitoScRowVm? ActiveCircuit => _vm.SelectedCircuito;

    public bool HasActiveCircuit => ActiveCircuit != null && ActiveCircuit.CanEdit;

    public string ActiveCircuitTitle => ActiveCircuit != null
        ? $"Cto #{ActiveCircuit.Numero} • {_vm.SelectedCuadro?.Nombre ?? "Tablero"}"
        : "Sin circuito seleccionado";

    public ICommand SelectCategoryCommand { get; }
    public ICommand ApplyItemCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand ToggleDrawerCommand { get; }
    public ICommand CloseDrawerCommand { get; }
    public ICommand ApplyBulkAlturaInterruptoresCommand { get; }
    public ICommand ApplyBulkAlturaEnchufesCommand { get; }

    public void NotifyActiveCircuitChanged()
    {
        OnPropertyChanged(nameof(ActiveCircuit));
        OnPropertyChanged(nameof(HasActiveCircuit));
        OnPropertyChanged(nameof(ActiveCircuitTitle));
        ((RelayCommand<CatalogoDrawerItem>)ApplyItemCommand).RaiseCanExecuteChanged();
    }

    public void RefreshData()
    {
        var config = _session.CubicacionScConfig;
        if (config == null) return;

        _allItems.Clear();

        // 1. Conductores
        foreach (var c in config.TiposConductor)
        {
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Conductores",
                Code = c,
                Name = c,
                Detail = "Conductor eléctrico",
                Tag = "",
                BadgeColor = "#2563EB",
                UsedCount = CountCircuitsUsing("Conductores", c),
                RawItem = c
            });
        }

        // 2. Secciones
        foreach (var s in config.SeccionesConductor)
        {
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Secciones",
                Code = s,
                Name = s,
                Detail = "Sección transversal",
                Tag = "",
                BadgeColor = "#0284C7",
                UsedCount = CountCircuitsUsing("Secciones", s),
                RawItem = s
            });
        }

        // 3. TiposTuberia
        foreach (var t in config.TiposTuberia)
        {
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "TiposTuberia",
                Code = t,
                Name = t,
                Detail = "Canalización / Tubo",
                Tag = "",
                BadgeColor = "#059669",
                UsedCount = CountCircuitsUsing("TiposTuberia", t),
                RawItem = t
            });
        }

        // 4. Diámetros
        foreach (var d in config.DiametrosTuberia)
        {
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Diametros",
                Code = d,
                Name = d,
                Detail = "Diámetro nominal",
                Tag = "",
                BadgeColor = "#0D9488",
                UsedCount = CountCircuitsUsing("Diametros", d),
                RawItem = d
            });
        }

        // 5. Alimentación (M, T, EM)
        foreach (var s in config.Sistemas)
        {
            var upper = (s.Nombre ?? "").Trim().ToUpperInvariant();
            var dispName = !string.IsNullOrWhiteSpace(s.Descripcion)
                ? s.Descripcion.ToUpperInvariant()
                : upper switch
                {
                    "M" or "MONOFASICO" or "MONOFÁSICO" => "MONOFASICO",
                    "T" or "TRIFASICO" or "TRIFÁSICO" => "TRIFASICO",
                    "EM" or "EMERGENCIA" => "EMERGENCIA",
                    _ => upper
                };
            var tagCode = upper switch
            {
                "M" or "MONOFASICO" or "MONOFÁSICO" => "M",
                "T" or "TRIFASICO" or "TRIFÁSICO" => "T",
                "EM" or "EMERGENCIA" => "EM",
                _ => upper.Length <= 4 ? upper : upper.Substring(0, 3)
            };

            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Alim",
                Code = s.Nombre ?? "",
                DisplayName = dispName,
                Subtitle = $"{s.NumConductores:N0} conductores",
                MermaText = $"Merma {s.MermaPct:N0}%",
                Tag = tagCode,
                NumberValue = s.NumConductores,
                UsedCount = CountCircuitsUsing("Alim", s.Nombre ?? ""),
                RawItem = s
            });
        }

        // 6. Sistemas de circuito (A, F, C, CL, etc.)
        var scSource = config.SistemasCircuito != null && config.SistemasCircuito.Count > 0
            ? config.SistemasCircuito
            : CubicacionScConfig.Default().SistemasCircuito;
        foreach (var sc in scSource)
        {
            var dispName = string.IsNullOrWhiteSpace(sc.Nombre) ? sc.Codigo : sc.Nombre.ToUpperInvariant();
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Sistemas",
                Code = sc.Codigo,
                DisplayName = dispName,
                Subtitle = $"Sistema {sc.Codigo}",
                Tag = sc.Codigo,
                UsedCount = CountCircuitsUsing("Sistemas", sc.Codigo),
                RawItem = sc
            });
        }

        // 7. Artefactos
        foreach (var art in config.Artefactos)
        {
            var displayTag = !string.IsNullOrWhiteSpace(art.Tag) ? art.Tag : "";
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Artefactos",
                Code = art.Nombre,
                DisplayName = art.Nombre,
                Subtitle = art.Categoria ?? "Artefacto",
                Tag = displayTag,
                AlturaBajada = art.AlturaBajada,
                UsedCount = CountCircuitsUsing("Artefactos", art.Nombre),
                RawItem = art
            });
        }

        var firstInt = config.Artefactos.FirstOrDefault(a => IsInterruptor(a) && a.AlturaBajada > 0);
        if (firstInt != null)
        {
            _bulkAlturaInterruptorText = firstInt.AlturaBajada.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(BulkAlturaInterruptorText));
        }

        var firstEnch = config.Artefactos.FirstOrDefault(a => IsEnchufe(a) && a.AlturaBajada > 0);
        if (firstEnch != null)
        {
            _bulkAlturaEnchufeText = firstEnch.AlturaBajada.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(BulkAlturaEnchufeText));
        }

        // 8. Bandejas
        foreach (var b in config.TiposBandeja)
        {
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Bandejas",
                Code = b,
                Name = b,
                Detail = "Bandeja portaconductor",
                Tag = "",
                BadgeColor = "#EC4899",
                UsedCount = CountCircuitsUsing("Bandejas", b),
                RawItem = b
            });
        }

        // 9. Cajas
        foreach (var cj in config.TiposCaja)
        {
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Cajas",
                Code = cj,
                Name = cj,
                Detail = "Caja de paso / registro",
                Tag = "",
                BadgeColor = "#6366F1",
                UsedCount = CountCircuitsUsing("Cajas", cj),
                RawItem = cj
            });
        }

        // 10. Flexibles
        foreach (var fl in config.Flexibles)
        {
            _allItems.Add(new CatalogoDrawerItem
            {
                Category = "Flexibles",
                Code = fl.Diametro,
                Name = fl.TipoFlexible,
                Detail = $"{fl.MetrosPorCaja:N1} m/caja",
                Tag = "",
                BadgeColor = "#8B5CF6",
                UsedCount = CountCircuitsUsing("Flexibles", fl.Diametro),
                RawItem = fl
            });
        }

        OnPropertyChanged(nameof(CountConductores));
        OnPropertyChanged(nameof(CountSecciones));
        OnPropertyChanged(nameof(CountTuberias));
        OnPropertyChanged(nameof(CountDiametros));
        OnPropertyChanged(nameof(CountAlim));
        OnPropertyChanged(nameof(CountSistemas));
        OnPropertyChanged(nameof(CountArtefactos));
        OnPropertyChanged(nameof(CountBandejas));
        OnPropertyChanged(nameof(CountCajas));
        OnPropertyChanged(nameof(CountFlexibles));

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        var search = (_searchText ?? "").Trim();

        var query = _allItems.Where(i => i.Category == _selectedCategory);
        if (search.Length > 0)
        {
            query = query.Where(i =>
                i.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Subtitle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Detail.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(i.Tag) && i.Tag.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var item in query)
        {
            FilteredItems.Add(item);
        }

        SelectedItem = FilteredItems.FirstOrDefault();
    }

    private int CountCircuitsUsing(string category, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        int count = 0;
        foreach (var cuadro in _vm.Cuadros)
        {
            foreach (var c in cuadro.Source.Circuitos)
            {
                switch (category)
                {
                    case "Conductores":
                        if (string.Equals(c.ConductorDistribucion, value, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.ConductorDerivacion, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                    case "Secciones":
                        if (string.Equals(c.SeccionDistribucion, value, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.SeccionDerivacion, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                    case "TiposTuberia":
                        if (string.Equals(c.TipoTuberia, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                    case "Diametros":
                        if (string.Equals(c.SeccionTuberia, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                    case "Alim":
                        if (string.Equals(c.Sistema, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                    case "Sistemas":
                        if (string.Equals(c.Tipo, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                    case "Artefactos":
                        if (c.Artefactos.Any(a => string.Equals(a.Nombre, value, StringComparison.OrdinalIgnoreCase)))
                            count++;
                        break;
                    case "Bandejas":
                        if (string.Equals(c.TipoBandeja, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                    case "Cajas":
                        break;
                    case "Flexibles":
                        if (string.Equals(c.SeccionTuberia, value, StringComparison.OrdinalIgnoreCase))
                            count++;
                        break;
                }
            }
        }
        return count;
    }

    public void ApplyItemToActiveCircuit(CatalogoDrawerItem? item)
    {
        if (item == null || ActiveCircuit == null || !ActiveCircuit.CanEdit) return;

        var row = ActiveCircuit;
        switch (item.Category)
        {
            case "Conductores":
                row.ConductorDistribucion = item.Code;
                row.ConductorDerivacion = item.Code;
                _vm.SetStatusMessage($"Conductor '{item.Code}' aplicado al Cto #{row.Numero}.");
                break;
            case "Secciones":
                row.SeccionDistribucion = item.Code;
                row.SeccionDerivacion = item.Code;
                _vm.SetStatusMessage($"Sección '{item.Code}' aplicada al Cto #{row.Numero}.");
                break;
            case "TiposTuberia":
                row.TipoTuberia = item.Code;
                _vm.SetStatusMessage($"Tubería '{item.Code}' aplicada al Cto #{row.Numero}.");
                break;
            case "Diametros":
                row.SeccionTuberia = item.Code;
                _vm.SetStatusMessage($"Diámetro '{item.Code}' aplicado al Cto #{row.Numero}.");
                break;
            case "Alim":
                row.Fase = item.Code;
                _vm.SetStatusMessage($"Alimentación '{item.Code}' aplicada al Cto #{row.Numero}.");
                break;
            case "Sistemas":
                row.Tipo = item.Code;
                _vm.SetStatusMessage($"Sistema '{item.Code}' aplicado al Cto #{row.Numero}.");
                break;
            case "Artefactos":
                row.AddArtefacto(item.Code, string.IsNullOrWhiteSpace(item.Detail) ? "General" : item.Detail, 1m, item.AlturaBajada);
                _vm.SetStatusMessage($"Artefacto '{item.Code}' agregado al Cto #{row.Numero}.");
                break;
            case "Bandejas":
                _vm.SetStatusMessage($"Bandeja '{item.Code}' seleccionada.");
                break;
            case "Cajas":
                _vm.SetStatusMessage($"Tipo de caja '{item.Code}' seleccionada.");
                break;
            case "Flexibles":
                row.SeccionTuberia = item.Code;
                _vm.SetStatusMessage($"Flexible '{item.Code}' aplicado al Cto #{row.Numero}.");
                break;
        }

        _vm.MarkDirty();
    }

    private void AddItem()
    {
        var text = (NewItemText ?? "").Trim();
        var tag = (NewItemTag ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(tag)) return;

        if (string.IsNullOrWhiteSpace(text)) text = tag;

        var config = _session.CubicacionScConfig;
        if (config == null) return;

        switch (SelectedCategory)
        {
            case "Artefactos":
                decimal alt = 0m;
                if (!string.IsNullOrWhiteSpace(NewItemAltura) &&
                    (decimal.TryParse(NewItemAltura.Trim().Replace("m", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedAlt) ||
                     decimal.TryParse(NewItemAltura.Trim().Replace("m", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out parsedAlt)))
                {
                    alt = parsedAlt >= 0m ? parsedAlt : 0m;
                }
                else if (text.Contains("ENCH", StringComparison.OrdinalIgnoreCase))
                {
                    alt = 2.20m;
                }
                else if (text.Contains("INT", StringComparison.OrdinalIgnoreCase) || text.Contains("9/12", StringComparison.OrdinalIgnoreCase) || text.Contains("9/24", StringComparison.OrdinalIgnoreCase))
                {
                    alt = 1.30m;
                }

                if (string.IsNullOrWhiteSpace(tag))
                {
                    var parts = text.Split(new[] { ':', '-' }, 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && parts[0].Length <= 6)
                    {
                        tag = parts[0].ToUpperInvariant();
                        text = parts[1];
                    }
                    else if (text.Length <= 4)
                    {
                        tag = text.ToUpperInvariant();
                    }
                }
                if (!config.Artefactos.Any(a => string.Equals(a.Nombre, text, StringComparison.OrdinalIgnoreCase)))
                {
                    config.Artefactos.Add(new ArtefactoCatalogoItem { Tag = tag, Nombre = text, Categoria = "General", AlturaBajada = alt });
                }
                break;
            case "Alim":
                var alimCode = !string.IsNullOrWhiteSpace(tag) ? tag : text;
                if (!config.Sistemas.Any(s => string.Equals(s.Nombre, alimCode, StringComparison.OrdinalIgnoreCase)))
                {
                    config.Sistemas.Add(new SistemaSC { Nombre = alimCode, Descripcion = text.ToUpperInvariant(), NumConductores = 3, MermaPct = 5 });
                }
                break;
            case "Conductores":
                if (!config.TiposConductor.Contains(text, StringComparer.OrdinalIgnoreCase))
                    config.TiposConductor.Add(text);
                break;
            case "Secciones":
                if (!config.SeccionesConductor.Contains(text, StringComparer.OrdinalIgnoreCase))
                    config.SeccionesConductor.Add(text);
                break;
            case "TiposTuberia":
                if (!config.TiposTuberia.Contains(text, StringComparer.OrdinalIgnoreCase))
                    config.TiposTuberia.Add(text);
                break;
            case "Diametros":
                if (!config.DiametrosTuberia.Contains(text, StringComparer.OrdinalIgnoreCase))
                    config.DiametrosTuberia.Add(text);
                break;
            case "Sistemas":
                var sistCode = (!string.IsNullOrWhiteSpace(tag) ? tag : text).ToUpperInvariant();
                if (!config.SistemasCircuito.Any(s => string.Equals(s.Codigo, sistCode, StringComparison.OrdinalIgnoreCase)))
                    config.SistemasCircuito.Add(new SistemaCircuitoSC { Codigo = sistCode, Nombre = text });
                break;
            case "Bandejas":
                if (!config.TiposBandeja.Contains(text, StringComparer.OrdinalIgnoreCase))
                    config.TiposBandeja.Add(text);
                break;
            case "Cajas":
                if (!config.TiposCaja.Contains(text, StringComparer.OrdinalIgnoreCase))
                    config.TiposCaja.Add(text);
                break;
            case "Flexibles":
                if (!config.Flexibles.Any(f => string.Equals(f.Diametro, text, StringComparison.OrdinalIgnoreCase)))
                    config.Flexibles.Add(new FlexibleMapSC { Diametro = text, TipoFlexible = $"FLEXIBLE {text}", MetrosPorCaja = 0.5m });
                break;
        }

        NewItemText = "";
        NewItemTag = "";
        _vm.RefreshCatalogCollections();
        _vm.MarkDirty();
        RefreshData();
        var newlyAdded = FilteredItems.FirstOrDefault(i => string.Equals(i.Code, text, StringComparison.OrdinalIgnoreCase));
        if (newlyAdded != null)
        {
            SelectedItem = newlyAdded;
        }
        _vm.SetStatusMessage($"Material '{text}' agregado al catálogo de {CategoryTitle}.");
    }

    private void DeleteItem(CatalogoDrawerItem? item)
    {
        if (item == null) return;
        var config = _session.CubicacionScConfig;
        if (config == null) return;

        switch (item.Category)
        {
            case "Conductores":
                config.TiposConductor.RemoveAll(c => string.Equals(c, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Secciones":
                config.SeccionesConductor.RemoveAll(s => string.Equals(s, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "TiposTuberia":
                config.TiposTuberia.RemoveAll(t => string.Equals(t, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Diametros":
                config.DiametrosTuberia.RemoveAll(d => string.Equals(d, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Alim":
                config.Sistemas.RemoveAll(s => string.Equals(s.Nombre, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Sistemas":
                config.SistemasCircuito.RemoveAll(sc => string.Equals(sc.Codigo, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Artefactos":
                config.Artefactos.RemoveAll(a => string.Equals(a.Nombre, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Bandejas":
                config.TiposBandeja.RemoveAll(b => string.Equals(b, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Cajas":
                config.TiposCaja.RemoveAll(c => string.Equals(c, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
            case "Flexibles":
                config.Flexibles.RemoveAll(f => string.Equals(f.Diametro, item.Code, StringComparison.OrdinalIgnoreCase));
                break;
        }

        _vm.RefreshCatalogCollections();
        _vm.MarkDirty();
        RefreshData();
        _vm.SetStatusMessage($"Material '{item.Code}' eliminado del catálogo.");
    }
}
