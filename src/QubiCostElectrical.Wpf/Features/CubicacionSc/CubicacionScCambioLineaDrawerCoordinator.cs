using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class CambioLineaItemUi : ViewModelBase
{
    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string SourceCode { get; set; } = "";
    public string SourceDescription { get; set; } = "";
    public string TargetCode { get; set; } = "";
    public string TargetDescription { get; set; } = "";
    public string Detail { get; set; } = "";
    public decimal CantidadTotal { get; set; }
    public int Occurrences { get; set; }
    public string Status { get; set; } = "Listo";
}

public sealed class CubicacionScCambioLineaDrawerCoordinator : ViewModelBase
{
    private readonly CubicacionScViewModel _vm;
    private readonly BudgetProjectSession _session;
    private readonly WpfUndoService _undo;
    private readonly IFamilyBatchReplaceService _batchService;
    private readonly IFamilyConversionPresetStore _presetStore;

    private bool _isOpen;
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (SetProperty(ref _isOpen, value))
            {
                OnPropertyChanged(nameof(IsOpen));
            }
        }
    }

    private string _activePreset = "pvc_pvclh";
    public string ActivePreset
    {
        get => _activePreset;
        private set
        {
            if (SetProperty(ref _activePreset, value))
            {
                OnPropertyChanged(nameof(IsPvcSelected));
                OnPropertyChanged(nameof(IsMagicSelected));
                OnPropertyChanged(nameof(IsEvaSelected));
                OnPropertyChanged(nameof(IsCustomSelected));
            }
        }
    }

    public bool IsPvcSelected => _activePreset == "pvc_pvclh";
    public bool IsMagicSelected => _activePreset == "magic_matix";
    public bool IsEvaSelected => _activePreset == "eva_h07z1k";
    public bool IsCustomSelected => _activePreset == "custom";

    private string _customSourceText = "";
    public string CustomSourceText
    {
        get => _customSourceText;
        set
        {
            if (SetProperty(ref _customSourceText, value) && IsCustomSelected)
            {
                ScanAndLoad();
            }
        }
    }

    private string _customTargetText = "";
    public string CustomTargetText
    {
        get => _customTargetText;
        set
        {
            if (SetProperty(ref _customTargetText, value) && IsCustomSelected)
            {
                ScanAndLoad();
            }
        }
    }

    private string _itemsBadgeText = "0 materiales";
    public string ItemsBadgeText
    {
        get => _itemsBadgeText;
        private set => SetProperty(ref _itemsBadgeText, value);
    }

    public ObservableCollection<CambioLineaItemUi> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    public ICommand ToggleDrawerCommand { get; }
    public ICommand CloseDrawerCommand { get; }
    public ICommand SelectPresetCommand { get; }
    public ICommand ApplyCommand { get; }

    public CubicacionScCambioLineaDrawerCoordinator(
        CubicacionScViewModel vm,
        BudgetProjectSession session,
        WpfUndoService undo,
        IFamilyBatchReplaceService? batchService = null,
        IFamilyConversionPresetStore? presetStore = null)
    {
        _vm = vm;
        _session = session;
        _undo = undo;
        _batchService = batchService ?? new FamilyBatchReplaceService();
        _presetStore = presetStore ?? new FamilyConversionPresetStore();

        ToggleDrawerCommand = new RelayCommand(Toggle);
        CloseDrawerCommand = new RelayCommand(Close);
        SelectPresetCommand = new RelayCommand<string>(SelectPreset);
        ApplyCommand = new RelayCommand(ApplyChanges, () => HasItems && !_session.IsReadOnly);
    }

    public void Open()
    {
        IsOpen = true;
        SelectPreset(_activePreset);
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void SelectPreset(string? presetKey)
    {
        ActivePreset = string.IsNullOrWhiteSpace(presetKey) ? "pvc_pvclh" : presetKey;
        ScanAndLoad();
    }

    public void ScanAndLoad()
    {
        Items.Clear();

        string srcPrefix = "";
        string tgtPrefix = "";
        IReadOnlyDictionary<string, string>? overrides = null;

        if (IsPvcSelected)
        {
            var p = _presetStore.GetAllPresets().FirstOrDefault(x => x.Id == "factory_pvc_to_pvclh")
                    ?? FamilyConversionPresetStore.GetFactoryPresets().First(x => x.Id == "factory_pvc_to_pvclh");
            srcPrefix = p.SourcePrefix;
            tgtPrefix = p.TargetPrefix;
            overrides = p.ItemOverrides;
        }
        else if (IsMagicSelected)
        {
            var p = _presetStore.GetAllPresets().FirstOrDefault(x => x.Id == "factory_magic_to_matix")
                    ?? FamilyConversionPresetStore.GetFactoryPresets().First(x => x.Id == "factory_magic_to_matix");
            srcPrefix = p.SourcePrefix;
            tgtPrefix = p.TargetPrefix;
            overrides = p.ItemOverrides;
        }
        else if (IsEvaSelected)
        {
            var p = _presetStore.GetAllPresets().FirstOrDefault(x => x.Id == "factory_eva_to_h07z1k")
                    ?? FamilyConversionPresetStore.GetFactoryPresets().First(x => x.Id == "factory_eva_to_h07z1k");
            srcPrefix = p.SourcePrefix;
            tgtPrefix = p.TargetPrefix;
            overrides = p.ItemOverrides;
        }
        else if (IsCustomSelected)
        {
            srcPrefix = _customSourceText.Trim();
            tgtPrefix = _customTargetText.Trim();
            if (string.IsNullOrWhiteSpace(srcPrefix))
            {
                ItemsBadgeText = "Ingresa el texto a buscar";
                OnPropertyChanged(nameof(HasItems));
                return;
            }
        }

        var scanned = _batchService.ScanMatchingMaterials(_session, srcPrefix, category: null, scanCubicacionOnly: false);
        if (scanned.Count == 0)
        {
            ItemsBadgeText = "Sin materiales coincidentes";
            OnPropertyChanged(nameof(HasItems));
            return;
        }

        // Auto-mapear con los overrides o sustitución
        _batchService.AutoMapWithPriceBank(scanned, srcPrefix, tgtPrefix, new PriceBankStore(), _session.ProjectInfo.ValorUfClp, overrides, _session);

        foreach (var m in scanned)
        {
            string detailText;
            if (m.SourceCategoria == "Cubicación SC")
            {
                detailText = $"Detectado en {m.OccurrencesCount} circuitos • {m.CantidadTotal:N1} {m.SourceUnidad} totales";
            }
            else
            {
                detailText = $"Detectado en cubicación de partidas • {m.CantidadTotal:N1} {m.SourceUnidad} totales";
            }

            string displayTarget = !string.IsNullOrWhiteSpace(m.TargetDescripcion)
                ? m.TargetDescripcion
                : (!string.IsNullOrWhiteSpace(m.TargetCodigo) ? m.TargetCodigo : tgtPrefix);

            Items.Add(new CambioLineaItemUi
            {
                IsSelected = true,
                SourceCode = m.SourceCodigo,
                SourceDescription = !string.IsNullOrWhiteSpace(m.SourceDescripcion) ? m.SourceDescripcion : m.SourceCodigo,
                TargetCode = m.TargetCodigo,
                TargetDescription = displayTarget,
                Detail = detailText,
                CantidadTotal = m.CantidadTotal,
                Occurrences = m.OccurrencesCount,
                Status = "Listo"
            });
        }

        ItemsBadgeText = $"{Items.Count} material{(Items.Count == 1 ? "" : "es")} detectado{(Items.Count == 1 ? "" : "s")}";
        OnPropertyChanged(nameof(HasItems));
    }

    public void ApplyChanges()
    {
        if (_session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Cambio de línea", _session.ReadOnlyReason);
            return;
        }

        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;

        var mappings = selected.Select(i => new FamilyItemMapping
        {
            IsSelected = true,
            SourceCodigo = i.SourceCode,
            SourceDescripcion = i.SourceDescription,
            TargetCodigo = i.TargetCode,
            TargetDescripcion = i.TargetDescription,
            NuevoPrecioClp = 0m
        }).ToList();

        var result = _batchService.ExecuteReplacement(
            _session,
            (s, desc) => _undo.PushUndoPoint(s, desc),
            mappings,
            replaceCubicacion: true,
            replaceApu: true);

        // Cerrar drawer y refrescar grilla
        Close();
        _vm.Refresh();
        _vm.SetStatusMessage($"Línea actualizada con éxito: {result.ItemsReemplazados} materiales modificados en el proyecto (Deshacer con Ctrl+Z).");
    }
}
