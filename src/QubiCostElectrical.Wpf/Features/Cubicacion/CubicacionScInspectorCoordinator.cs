using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Coordinador de lógica y estado para el Panel Lateral Acoplado (Docked Inspector) de Cubicación SC.
/// Permite filtrar por tipo de circuito y transferir materiales directamente a la grilla de presupuesto.
/// </summary>
public sealed class CubicacionScInspectorCoordinator : ViewModelBase
{
    private readonly BudgetProjectSession _session;
    private readonly CubicacionViewModel _parent;

    private bool _isInspectorOpen;
    private string _filterSearchText = "";
    private ScInspectorSystemVm? _selectedSystem;
    private string _targetRowText = "Fila #1";

    public ObservableCollection<ScInspectorSystemVm> AvailableSystems { get; } = new();
    public ObservableCollection<ScInspectorGroupVm> MaterialGroups { get; } = new();

    public bool IsInspectorOpen
    {
        get => _isInspectorOpen;
        set
        {
            if (SetProperty(ref _isInspectorOpen, value))
            {
                if (value)
                {
                    RefreshAvailableSystems();
                    RefreshMaterials();
                    UpdateTargetRowText();
                }
                _parent.NotifyProperty(nameof(CubicacionViewModel.IsScInspectorOpen));
            }
        }
    }

    public string FilterSearchText
    {
        get => _filterSearchText;
        set
        {
            if (SetProperty(ref _filterSearchText, value))
            {
                ApplyTextFilter();
            }
        }
    }

    public ScInspectorSystemVm? SelectedSystem
    {
        get => _selectedSystem;
        set
        {
            if (SetProperty(ref _selectedSystem, value))
            {
                foreach (var sys in AvailableSystems)
                {
                    sys.IsSelected = sys == value;
                }
                RefreshMaterials();
            }
        }
    }

    public string TargetRowText
    {
        get => _targetRowText;
        private set => SetProperty(ref _targetRowText, value);
    }

    private ScInspectorMaterialVm? _selectedMaterial;
    public ScInspectorMaterialVm? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (SetProperty(ref _selectedMaterial, value))
            {
                foreach (var grp in MaterialGroups)
                {
                    foreach (var item in grp.Items)
                    {
                        item.IsSelected = item == value;
                    }
                }
                SelectedMaterialChanged?.Invoke(value);
            }
        }
    }

    public event Action<ScInspectorMaterialVm?>? SelectedMaterialChanged;

    public ICommand SelectMaterialCommand { get; }
    public ICommand ToggleInspectorCommand { get; }
    public ICommand CloseInspectorCommand { get; }
    public ICommand SelectSystemCommand { get; }
    public ICommand TransferItemCommand { get; }
    public ICommand TransferGroupCommand { get; }
    public ICommand TransferAllInSystemCommand { get; }

    public CubicacionScInspectorCoordinator(BudgetProjectSession session, CubicacionViewModel parent)
    {
        _session = session;
        _parent = parent;

        SelectMaterialCommand = new RelayCommand<ScInspectorMaterialVm>(mat => { if (mat != null) SelectedMaterial = mat; });
        ToggleInspectorCommand = new RelayCommand(ToggleInspector);
        CloseInspectorCommand = new RelayCommand(() => IsInspectorOpen = false);
        SelectSystemCommand = new RelayCommand<ScInspectorSystemVm>(sys => { if (sys != null) SelectedSystem = sys; });
        TransferItemCommand = new RelayCommand<ScInspectorMaterialVm>(TransferItem);
        TransferGroupCommand = new RelayCommand<ScInspectorGroupVm>(TransferGroup);
        TransferAllInSystemCommand = new RelayCommand(TransferActiveSystem);
    }

    public void ToggleInspector()
    {
        IsInspectorOpen = !IsInspectorOpen;
    }

    public void UpdateTargetRowText()
    {
        var row = _parent.SelectedRow;
        if (row != null)
        {
            var code = string.IsNullOrWhiteSpace(row.Codigo) ? "(Sin código)" : row.Codigo;
            var desc = string.IsNullOrWhiteSpace(row.Descripcion) ? "Fila vacía" : row.Descripcion;
            TargetRowText = $"Fila #{row.RowNumberDisplay} · {code} ({desc})";
        }
        else
        {
            TargetRowText = "Ninguna fila seleccionada";
        }
    }

    public void RefreshAvailableSystems()
    {
        var cuadros = _session.CubicacionSC;
        var circuitos = cuadros?.SelectMany(c => c.Circuitos ?? Enumerable.Empty<CircuitoSCRow>()).ToList() ?? new List<CircuitoSCRow>();

        AvailableSystems.Clear();

        var allPill = new ScInspectorSystemVm
        {
            Id = "ALL",
            Label = "Todos",
            Icon = "🌐",
            Count = circuitos.Count,
            IsSelected = true
        };
        AvailableSystems.Add(allPill);

        var groupedByType = circuitos
            .Where(c => !string.IsNullOrWhiteSpace(c.Tipo))
            .GroupBy(c => c.Tipo.Trim().ToUpperInvariant())
            .OrderBy(g => g.Key);

        foreach (var group in groupedByType)
        {
            var code = group.Key;
            var (label, icon) = ResolveSystemLabelAndIcon(code);
            var pill = new ScInspectorSystemVm
            {
                Id = code,
                Label = label,
                Icon = icon,
                Count = group.Count(),
                IsSelected = false
            };
            AvailableSystems.Add(pill);
        }

        _selectedSystem = allPill;
        OnPropertyChanged(nameof(SelectedSystem));
    }

    private static (string Label, string Icon) ResolveSystemLabelAndIcon(string code) => code switch
    {
        "A" => ("Luminarias", "💡"),
        "E" => ("Enchufes", "🔌"),
        "C" => ("Computación", "💻"),
        "CL" => ("Clima", "❄️"),
        "F" => ("Fuerza", "⚙️"),
        _ => (code, "⚡")
    };

    public void RefreshMaterials()
    {
        MaterialGroups.Clear();

        var cuadros = _session.CubicacionSC;
        var config = _session.CubicacionScConfig;
        if (cuadros == null || cuadros.Count == 0 || config == null)
            return;

        var activeSysId = SelectedSystem?.Id ?? "ALL";
        Func<CircuitoSCRow, bool>? filter = activeSysId == "ALL"
            ? null
            : circ => string.Equals(circ.Tipo?.Trim(), activeSysId, StringComparison.OrdinalIgnoreCase);

        var computed = CubicacionSCEngine.AggregateConZonas(cuadros, config, _session.ZonasProyecto, filter);
        if (computed.Count == 0) return;

        // Clasificar en grupos visuales de alto orden
        var groupDefs = new[]
        {
            ("CONDUCTORES", "⚡ CONDUCTORES Y LÍNEAS", "⚡"),
            ("CANALIZACIONES", "🔌 CANALIZACIONES Y TUBERÍAS", "🔌"),
            ("ACCESORIOS", "📦 CAJAS Y ACCESORIOS", "📦"),
            ("PROTECCIONES", "🛡️ PROTECCIONES Y TABLEROS", "🛡️"),
            ("ARTEFACTOS", "💡 ARTEFACTOS Y DISPOSITIVOS", "💡")
        };

        var mappedItems = computed
            .Where(mat => mat.Total > 0m && !string.IsNullOrWhiteSpace(mat.Nombre))
            .Select(mat =>
            {
                var catNormalized = CategorizeMaterial(mat.Nombre, mat.Categoria);
                var sysBadge = activeSysId == "ALL" ? "" : (SelectedSystem?.Label ?? "");
                return new ScInspectorMaterialVm
                {
                    Codigo = ResolveMaterialCode(mat.Nombre),
                    Descripcion = mat.Nombre,
                    Unidad = mat.Unidad,
                    Cantidad = mat.Total,
                    Categoria = catNormalized,
                    SystemCode = activeSysId,
                    SystemBadge = sysBadge,
                    Zonas = mat.Zonas
                };
            }).ToList();

        foreach (var (groupId, groupTitle, icon) in groupDefs)
        {
            var matching = mappedItems.Where(i => i.Categoria == groupId).ToList();
            if (matching.Count > 0)
            {
                var grp = new ScInspectorGroupVm
                {
                    GroupId = groupId,
                    Title = groupTitle,
                    Icon = icon,
                    IsExpanded = true
                };
                foreach (var item in matching)
                {
                    grp.Items.Add(item);
                }
                MaterialGroups.Add(grp);
            }
        }

        ApplyTextFilter();
        SelectedMaterial = GetFlatVisibleMaterials().FirstOrDefault();
    }

    public List<ScInspectorMaterialVm> GetFlatVisibleMaterials()
    {
        var list = new List<ScInspectorMaterialVm>();
        foreach (var grp in MaterialGroups)
        {
            if (!grp.IsExpanded && !grp.HasVisibleItems) continue;
            foreach (var item in grp.Items)
            {
                if (item.IsVisible)
                    list.Add(item);
            }
        }
        return list;
    }

    public void SelectNextMaterial()
    {
        var visible = GetFlatVisibleMaterials();
        if (visible.Count == 0) return;
        var idx = _selectedMaterial != null ? visible.IndexOf(_selectedMaterial) : -1;
        var nextIdx = idx < visible.Count - 1 ? idx + 1 : 0;
        SelectedMaterial = visible[nextIdx];
    }

    public void SelectPreviousMaterial()
    {
        var visible = GetFlatVisibleMaterials();
        if (visible.Count == 0) return;
        var idx = _selectedMaterial != null ? visible.IndexOf(_selectedMaterial) : 0;
        var prevIdx = idx > 0 ? idx - 1 : visible.Count - 1;
        SelectedMaterial = visible[prevIdx];
    }

    public void TransferSelectedMaterial()
    {
        var itemToTransfer = SelectedMaterial ?? GetFlatVisibleMaterials().FirstOrDefault();
        if (itemToTransfer != null)
        {
            TransferItem(itemToTransfer);
            // Avanzar automaticamente a la siguiente fila para inyeccion continua con Enter
            SelectNextMaterial();
        }
    }

    public void SelectNextSystem()
    {
        if (AvailableSystems.Count == 0) return;
        var currentIdx = SelectedSystem != null ? AvailableSystems.IndexOf(SelectedSystem) : -1;
        var nextIdx = (currentIdx + 1) % AvailableSystems.Count;
        SelectedSystem = AvailableSystems[nextIdx];
    }

    public void SelectPreviousSystem()
    {
        if (AvailableSystems.Count == 0) return;
        var currentIdx = SelectedSystem != null ? AvailableSystems.IndexOf(SelectedSystem) : 0;
        var prevIdx = currentIdx > 0 ? currentIdx - 1 : AvailableSystems.Count - 1;
        SelectedSystem = AvailableSystems[prevIdx];
    }

    private static string CategorizeMaterial(string nombre, string? categoriaOrigen)
    {
        var n = nombre.ToUpperInvariant();
        if (n.Contains("COND") || n.Contains("EVA") || n.Contains("H07Z1") || n.Contains("THHN") || n.Contains("ALAMBRE") || n.Contains("CABLE"))
            return "CONDUCTORES";
        if (n.Contains("TUB") || n.Contains("PVC") || n.Contains("CONDUIT") || n.Contains("EMT") || n.Contains("BANDEJA") || n.Contains("CANALETA"))
            return "CANALIZACIONES";
        if (n.Contains("CAJA") || n.Contains("TERM") || n.Contains("COPLA") || n.Contains("ABRAZADERA") || n.Contains("SOPORTE") || n.Contains("CURVA"))
            return "ACCESORIOS";
        if (n.Contains("DISYUNTOR") || n.Contains("AUTOM") || n.Contains("DIFERENCIAL") || n.Contains("TABLERO") || n.Contains("GABINETE"))
            return "PROTECCIONES";

        return "ARTEFACTOS";
    }

    private static string ResolveMaterialCode(string nombre)
    {
        var n = nombre.ToUpperInvariant();
        if (n.Contains("EVA 1.5") || n.Contains("EVA 1,5")) return "COND-EVA-1.5";
        if (n.Contains("EVA 2.5") || n.Contains("EVA 2,5")) return "COND-EVA-2.5";
        if (n.Contains("EVA 4.0") || n.Contains("EVA 4,0") || n.Contains("EVA 4")) return "COND-EVA-4.0";
        if (n.Contains("PVC 20") || n.Contains("PVCLH 20")) return "T-PVC-20";
        if (n.Contains("PVC 25") || n.Contains("PVCLH 25")) return "T-PVC-25";
        if (n.Contains("BANDEJA 200")) return "BAN-200X50";
        if (n.Contains("5/8")) return "ACC-CAJA-58";
        if (n.Contains("CHUQUI")) return "ACC-CAJA-CHUQUI";
        return nombre.Length > 14 ? nombre.Substring(0, 14).Trim() : nombre;
    }

    private void ApplyTextFilter()
    {
        var q = FilterSearchText.Trim().ToLowerInvariant();
        foreach (var grp in MaterialGroups)
        {
            foreach (var item in grp.Items)
            {
                var match = string.IsNullOrWhiteSpace(q) ||
                            item.Codigo.ToLowerInvariant().Contains(q) ||
                            item.Descripcion.ToLowerInvariant().Contains(q);
                item.IsVisible = match;
            }
            grp.NotifyVisibilityChanged();
        }

        var flat = GetFlatVisibleMaterials();
        if (_selectedMaterial == null || !_selectedMaterial.IsVisible)
        {
            SelectedMaterial = flat.FirstOrDefault();
        }
    }

    public void TransferItem(ScInspectorMaterialVm? item)
    {
        if (item == null) return;

        var row = _parent.SelectedRow ?? _parent.Rows.FirstOrDefault(r => r.IsSelected) ?? _parent.Rows.FirstOrDefault();
        if (row == null) return;

        row.BeginBatchUpdate();
        row.Codigo = item.Codigo;
        row.Descripcion = item.Descripcion;
        row.Unidad = item.Unidad;
        row.Tipo = "MAT";

        var zonasProyecto = _session.ZonasProyecto.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
        var zMap = item.Zonas.ToDictionary(z => z.ZonaNombre, z => z.Cantidad, StringComparer.OrdinalIgnoreCase);

        if (item.Zonas.Count > 0 && zonasProyecto.Count > 0)
        {
            decimal sumDistributed = 0m;
            for (int zIdx = 0; zIdx < zonasProyecto.Count; zIdx++)
            {
                var z = zonasProyecto[zIdx];
                if (zMap.TryGetValue(z.Nombre, out var zQty) && zQty > 0)
                {
                    _parent.SetSectorExpressionDirect(row, zIdx, zQty.ToString("0.##", CultureInfo.InvariantCulture));
                    sumDistributed += zQty;
                }
                else
                {
                    _parent.SetSectorExpressionDirect(row, zIdx, "");
                }
            }

            if (sumDistributed > 0)
            {
                row.Cantidad = sumDistributed;
            }
            else
            {
                row.Cantidad = item.Cantidad;
                _parent.SetSectorExpressionDirect(row, 0, item.Cantidad.ToString("0.##", CultureInfo.InvariantCulture));
            }
        }
        else
        {
            row.Cantidad = item.Cantidad;
            if (zonasProyecto.Count > 0)
            {
                _parent.SetSectorExpressionDirect(row, 0, item.Cantidad.ToString("0.##", CultureInfo.InvariantCulture));
            }
        }

        row.EndBatchUpdate();
        row.RefreshDisplayProperties();
        _parent.RightPanel.LoadRow(row);
        _parent.ValidateRow(row);
        _parent.MarkDirty();
        _parent.NotifyCubicacionSummaryChanged();

        item.IsTransferred = true;

            // Avanzar automaticamente a la siguiente fila para inyeccion continua con Enter
        AdvanceToNextRow(row);
        UpdateTargetRowText();
    }

    private void AdvanceToNextRow(CubicacionRowVm currentRow)
    {
        var currentIndex = _parent.Rows.IndexOf(currentRow);
        if (currentIndex >= 0)
        {
            if (currentIndex + 1 < _parent.Rows.Count)
            {
                var nextRow = _parent.Rows[currentIndex + 1];
                _parent.SelectedRow = nextRow;
            }
            else
            {
                // Si era la última fila, agregar una fila vacía para seguir inyectando fluidamente
                _parent.rowMutationService.AddRow();
            }
        }
    }

    public void TransferGroup(ScInspectorGroupVm? group)
    {
        if (group == null) return;
        foreach (var item in group.Items.Where(i => i.IsVisible))
        {
            TransferItem(item);
        }
    }

    public void TransferActiveSystem()
    {
        foreach (var grp in MaterialGroups)
        {
            foreach (var item in grp.Items.Where(i => i.IsVisible))
            {
                TransferItem(item);
            }
        }
    }
}
