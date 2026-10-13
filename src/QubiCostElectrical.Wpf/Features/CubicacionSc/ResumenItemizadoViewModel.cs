#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ResumenItemizadoViewModel : ViewModelBase
{
    private readonly BudgetProjectSession? session;
    private readonly ItemizadoPresenter? presenter;
    private readonly WpfUndoService? undo;
    private readonly List<ItemizadoPartidaVm> allItems = new List<ItemizadoPartidaVm>();

    private string searchText = "";
    private string activeFilter = "Todas";
    private bool showAll = true;
    private bool onlyCubicacion;
    private bool onlySinCubicar;
    private bool onlyTitulos;
    private ItemizadoPartidaVm? selectedRow;

    public string? InitialId { get; }
    public bool HasMutations { get; private set; }

    public ObservableCollection<FilterChipVm> Filters { get; }
    public FastObservableCollection<ItemizadoPartidaVm> FlatRows { get; } = new FastObservableCollection<ItemizadoPartidaVm>();

    public ICommand AddPartidaCommand { get; }
    public ICommand DeletePartidaCommand { get; }
    public ICommand RecomendarCodigoCommand { get; }
    public ICommand CerrarCommand { get; }

    public Action? OnCerrar { get; set; }
    public event Action? RequestFocusQuickEdit;

    public string ActiveFilter
    {
        get => activeFilter;
        set
        {
            if (!SetProperty(ref activeFilter, value, nameof(ActiveFilter)))
                return;

            showAll = string.Equals(value, "Todas", StringComparison.OrdinalIgnoreCase);
            onlyCubicacion = string.Equals(value, "Con cubicación", StringComparison.OrdinalIgnoreCase);
            onlySinCubicar = string.Equals(value, "Sin cubicar", StringComparison.OrdinalIgnoreCase);
            onlyTitulos = string.Equals(value, "Títulos", StringComparison.OrdinalIgnoreCase);

            foreach (FilterChipVm filter in Filters)
            {
                filter.IsActive = string.Equals(filter.Nombre, value, StringComparison.OrdinalIgnoreCase);
            }
            AplicarFiltros();
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value, nameof(SearchText)))
            {
                AplicarFiltros();
            }
        }
    }

    public ItemizadoPartidaVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (SetProperty(ref selectedRow, value, nameof(SelectedRow)))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SummaryText
    {
        get
        {
            int value = allItems.Count(i => !i.EsSeccion);
            int value2 = allItems.Count(i => !i.EsSeccion && i.TieneCubicacion);
            decimal num = allItems.Sum(i => i.MaterialTotal);
            decimal num2 = allItems.Sum(i => i.ManoObraTotal);
            return $"{value} partidas · {value2} con cubicación · Total: $ {num + num2:N0}";
        }
    }

    public int TotalPartidasCount => allItems.Count(i => !i.EsSeccion);
    public int CubicadasCount => allItems.Count(i => !i.EsSeccion && i.TieneCubicacion);
    public int PendientesCount => allItems.Count(i => !i.EsSeccion && !i.TieneCubicacion);
    public string TotalCostoDirectoText => $"$ {allItems.Sum(i => i.MaterialTotal + i.ManoObraTotal):N0}";
    public string AvancePorcentajeText => TotalPartidasCount > 0 ? $"{(CubicadasCount * 100 / TotalPartidasCount)}%" : "0%";

    public ResumenItemizadoViewModel(
        IReadOnlyList<ItemizadoPartidaVm> items,
        string? initialId = null,
        BudgetProjectSession? session = null,
        ItemizadoPresenter? presenter = null,
        WpfUndoService? undo = null)
    {
        this.session = session;
        this.presenter = presenter;
        this.undo = undo;
        InitialId = initialId;

        AddPartidaCommand = new RelayCommand(AddPartida, () => session == null || !session.IsReadOnly);
        DeletePartidaCommand = new RelayCommand(DeletePartida, () => (session == null || !session.IsReadOnly) && selectedRow != null && !selectedRow.EsSeccion);
        RecomendarCodigoCommand = new RelayCommand(RecomendarCodigo, () => (session == null || !session.IsReadOnly) && selectedRow != null && !selectedRow.EsSeccion);
        CerrarCommand = new RelayCommand(Cerrar);

        Filters = new ObservableCollection<FilterChipVm>
        {
            new FilterChipVm
            {
                Nombre = "Todas",
                IsActive = true,
                Count = items.Count
            },
            new FilterChipVm
            {
                Nombre = "Con cubicación",
                IsActive = false,
                Count = items.Count(i => !i.EsSeccion && i.TieneCubicacion)
            },
            new FilterChipVm
            {
                Nombre = "Sin cubicar",
                IsActive = false,
                Count = items.Count(i => !i.EsSeccion && !i.TieneCubicacion)
            },
            new FilterChipVm
            {
                Nombre = "Títulos",
                IsActive = false,
                Count = items.Count(i => i.EsSeccion)
            }
        };

        foreach (var item in items)
        {
            item.IsCurrentPartida = !string.IsNullOrEmpty(initialId) && string.Equals(item.Id, initialId, StringComparison.OrdinalIgnoreCase);
            WirePartidaVm(item);
            allItems.Add(item);
        }

        AplicarFiltros();
    }

    private void WirePartidaVm(ItemizadoPartidaVm vm)
    {
        vm.OnChanged = OnPartidaEdited;
    }

    private void OnPartidaEdited(ItemizadoPartidaVm vm)
    {
        if (session != null)
        {
            var row = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, vm.Id, StringComparison.OrdinalIgnoreCase));
            if (row != null)
            {
                row.Item = vm.ItemCode;
                row.WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(vm.ItemCode);
                row.Descripcion = vm.Descripcion;
                row.Unidad = vm.Unidad;
                row.Cantidad = vm.Cantidad;
                HasMutations = true;
                session.MarkDirty(ProjectDirtyScope.Itemizado);
            }
        }
        else
        {
            HasMutations = true;
        }
    }

    public void AddPartida()
    {
        if (session != null && session.IsReadOnly)
            return;

        try
        {
            ItemizadoPartidaVm? refItem = selectedRow;
            string? refCode = refItem?.ItemCode;
            string? fallbackChapter = null;
            if (refItem != null)
            {
                int currentIdx = allItems.IndexOf(refItem);
                for (int i = currentIdx; i >= 0; i--)
                {
                    if (allItems[i].EsSeccion && !string.IsNullOrWhiteSpace(allItems[i].ItemCode))
                    {
                        fallbackChapter = allItems[i].ItemCode;
                        break;
                    }
                }
            }

            string recommendedCode = RecommendNextItemCode(refCode, allItems.Select(x => x.ItemCode), fallbackChapter);

            if (session != null && presenter != null)
            {
                int insertIndex = -1;
                if (selectedRow != null)
                {
                    insertIndex = session.Itemizado.FindIndex(x => string.Equals(x.Id, selectedRow.Id, StringComparison.OrdinalIgnoreCase));
                }

                undo?.PushUndoPoint(session, "Agregar partida");
                var result = presenter.AddBelow(insertIndex, "Nueva partida", "GL");
                var newItem = session.Itemizado.ElementAtOrDefault(result.SelectedIndex);
                if (newItem != null)
                {
                    newItem.Item = recommendedCode;
                    newItem.WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(recommendedCode);

                    if (session.ProjectInfo.CommercialModel != CommercialCostingModel.ApuDirectCostWithCommercialFactors)
                    {
                        session.ProjectInfo.CommercialPartidaRules ??= new List<CommercialPartidaRule>();
                        session.ProjectInfo.CommercialPartidaRules.Add(new CommercialPartidaRule
                        {
                            PartidaId = newItem.PartidaId,
                            UseProjectModel = false,
                            Model = session.ProjectInfo.CommercialModel
                        });
                    }
                }

                HasMutations = true;
                session.MarkDirty(ProjectDirtyScope.Itemizado);
                SyncFromSession(selectId: newItem?.Id);
            }
            else
            {
                var newVm = new ItemizadoPartidaVm
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemCode = recommendedCode,
                    Descripcion = "Nueva partida",
                    Unidad = "GL",
                    Cantidad = 1m,
                    EsSeccion = false
                };
                WirePartidaVm(newVm);
                int idx = selectedRow != null ? allItems.IndexOf(selectedRow) : -1;
                if (idx >= 0 && idx < allItems.Count)
                    allItems.Insert(idx + 1, newVm);
                else
                    allItems.Add(newVm);

                HasMutations = true;
                UpdateFilterCounts();
                AplicarFiltros();
                SelectedRow = newVm;
            }

            RequestFocusQuickEdit?.Invoke();
        }
        catch (Exception ex)
        {
            AppLog.Error("ResumenItemizado", "Error al agregar partida", ex);
            WpfDialogService.Error("Agregar partida", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    public void DeletePartida()
    {
        if (selectedRow == null || selectedRow.EsSeccion)
            return;

        if (session != null && session.IsReadOnly)
            return;

        var target = selectedRow;
        string desc = string.IsNullOrWhiteSpace(target.ItemCode) ? target.Descripcion : $"{target.ItemCode} — {target.Descripcion}";

        try
        {
            if (session != null && presenter != null)
            {
                int index = session.Itemizado.FindIndex(x => string.Equals(x.Id, target.Id, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return;

                int affected = presenter.CountAffectedRows(index);
                if (!WpfDialogService.Confirm(
                        "Eliminar partida",
                        presenter.BuildDeleteConfirmationMessage(desc, affected),
                        isWarning: true))
                    return;

                undo?.PushUndoPoint(session, affected > 1 ? "Eliminar filas" : "Eliminar partida");
                var result = presenter.DeleteAt(index);
                HasMutations = true;

                string? nextId = result.SelectedIndex >= 0
                    ? session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id
                    : null;

                SyncFromSession(selectId: nextId);
            }
            else
            {
                allItems.Remove(target);
                HasMutations = true;
                UpdateFilterCounts();
                AplicarFiltros();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("ResumenItemizado", "Error al eliminar partida", ex);
            WpfDialogService.Error("Eliminar partida", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void SyncFromSession(string? selectId = null)
    {
        if (session == null) return;

        var costs = new Dictionary<string, PartidaCost>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in session.Cubicacion)
        {
            var itemId = item.ItemId ?? "";
            if (itemId.Length == 0) continue;

            var mat = item.MatClp * item.Cantidad;
            var mo = item.MoClp * item.Cantidad;
            if (costs.TryGetValue(itemId, out var existing))
                costs[itemId] = new PartidaCost(existing.Mat + mat, existing.Mo + mo, existing.Total + mat + mo);
            else
                costs[itemId] = new PartidaCost(mat, mo, mat + mo);
        }

        allItems.Clear();
        foreach (var row in session.Itemizado)
        {
            var esSeccion = row.EsTitulo;
            var cost = row.Id != null && costs.TryGetValue(row.Id, out var value)
                ? value
                : new PartidaCost(0m, 0m, 0m);

            var vm = new ItemizadoPartidaVm
            {
                EsSeccion = esSeccion,
                Id = row.Id ?? "",
                ItemCode = row.Item ?? "",
                Descripcion = row.Descripcion ?? "",
                Unidad = row.Unidad ?? "",
                Cantidad = row.Cantidad,
                MaterialTotal = cost.Mat,
                ManoObraTotal = cost.Mo,
                CostoDirecto = cost.Total,
                IsCurrentPartida = !string.IsNullOrEmpty(InitialId) && string.Equals(row.Id, InitialId, StringComparison.OrdinalIgnoreCase)
            };
            WirePartidaVm(vm);
            allItems.Add(vm);
        }

        UpdateFilterCounts();
        AplicarFiltros();

        if (!string.IsNullOrEmpty(selectId))
        {
            var match = FlatRows.FirstOrDefault(r => string.Equals(r.Id, selectId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                SelectedRow = match;
        }
    }

    private void UpdateFilterCounts()
    {
        if (Filters.Count >= 4)
        {
            Filters[0].Count = allItems.Count;
            Filters[1].Count = allItems.Count(i => !i.EsSeccion && i.TieneCubicacion);
            Filters[2].Count = allItems.Count(i => !i.EsSeccion && !i.TieneCubicacion);
            Filters[3].Count = allItems.Count(i => i.EsSeccion);
        }
        RefreshSummaryMetrics();
    }

    private void RefreshSummaryMetrics()
    {
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(TotalPartidasCount));
        OnPropertyChanged(nameof(CubicadasCount));
        OnPropertyChanged(nameof(PendientesCount));
        OnPropertyChanged(nameof(TotalCostoDirectoText));
        OnPropertyChanged(nameof(AvancePorcentajeText));
    }

    public void AplicarFiltros()
    {
        string text = SearchText?.Trim() ?? "";
        bool flag = text.Length > 0;
        var filtered = new List<ItemizadoPartidaVm>(allItems.Count);
        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];
            if (PassesFilter(item) && (!flag || MatchesSearch(item, text)))
            {
                filtered.Add(item);
            }
        }

        FlatRows.ReplaceRange(filtered);

        ItemizadoPartidaVm? candidate = null;
        if (selectedRow != null && filtered.Contains(selectedRow))
        {
            candidate = selectedRow;
        }
        else if (InitialId != null)
        {
            candidate = filtered.FirstOrDefault(r => string.Equals(r.Id, InitialId, StringComparison.OrdinalIgnoreCase));
        }

        SelectedRow = candidate ?? filtered.FirstOrDefault(r => !r.EsSeccion) ?? filtered.FirstOrDefault();
    }

    private bool PassesFilter(ItemizadoPartidaVm item)
    {
        if (showAll)
        {
            return true;
        }
        if (onlyTitulos)
        {
            return item.EsSeccion;
        }
        if (item.EsSeccion)
        {
            return false;
        }
        if (onlyCubicacion)
        {
            return item.TieneCubicacion;
        }
        if (onlySinCubicar)
        {
            return !item.TieneCubicacion;
        }
        return true;
    }

    private static bool MatchesSearch(ItemizadoPartidaVm item, string search)
    {
        if (item.Descripcion.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (item.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    public void RecomendarCodigo()
    {
        if (selectedRow == null || selectedRow.EsSeccion)
            return;

        int idx = allItems.IndexOf(selectedRow);
        ItemizadoPartidaVm? prevItem = idx > 0 ? allItems[idx - 1] : null;
        string? refCode = prevItem?.ItemCode;
        string? fallbackChapter = null;
        for (int i = idx; i >= 0; i--)
        {
            if (allItems[i].EsSeccion && !string.IsNullOrWhiteSpace(allItems[i].ItemCode))
            {
                fallbackChapter = allItems[i].ItemCode;
                break;
            }
        }

        string recommended = RecommendNextItemCode(refCode, allItems.Where(x => x != selectedRow).Select(x => x.ItemCode), fallbackChapter);
        selectedRow.ItemCode = recommended;
        HasMutations = true;
    }

    public static string RecommendNextItemCode(string? referenceCode, IEnumerable<string?> existingCodes, string? fallbackChapterCode = null)
    {
        var existingSet = new HashSet<string>(
            existingCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        string candidate = GenerateNextCode(referenceCode);
        if (string.IsNullOrWhiteSpace(candidate) && !string.IsNullOrWhiteSpace(fallbackChapterCode))
        {
            candidate = GenerateNextCode(fallbackChapterCode);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "1,1";
        }

        if (existingSet.Contains(candidate))
        {
            candidate = FindNextUnused(candidate, existingSet);
        }

        return candidate;
    }

    private static string GenerateNextCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "";

        code = code.Trim();

        int lastSeparator = Math.Max(code.LastIndexOf(','), code.LastIndexOf('.'));
        if (lastSeparator >= 0 && lastSeparator < code.Length - 1)
        {
            string prefix = code[..(lastSeparator + 1)];
            string numberPart = code[(lastSeparator + 1)..];

            if (int.TryParse(numberPart, out int num))
            {
                int nextNum = num == 0 ? 1 : num + 1;
                string formattedNum = numberPart.Length > 1 && numberPart.StartsWith('0')
                    ? nextNum.ToString(new string('0', numberPart.Length))
                    : nextNum.ToString();

                return prefix + formattedNum;
            }
        }

        if (int.TryParse(code, out int singleNum))
        {
            return (singleNum + 1).ToString();
        }

        return code + ".1";
    }

    private static string FindNextUnused(string baseCandidate, HashSet<string> existingSet)
    {
        int lastSeparator = Math.Max(baseCandidate.LastIndexOf(','), baseCandidate.LastIndexOf('.'));
        if (lastSeparator >= 0 && lastSeparator < baseCandidate.Length - 1)
        {
            string prefix = baseCandidate[..(lastSeparator + 1)];
            string numberPart = baseCandidate[(lastSeparator + 1)..];

            if (int.TryParse(numberPart, out int currentNum))
            {
                int nextNum = currentNum + 1;
                for (int i = 0; i < 1000; i++, nextNum++)
                {
                    string candidate = prefix + nextNum;
                    if (!existingSet.Contains(candidate))
                        return candidate;
                }
            }
        }

        for (int i = 2; i < 1000; i++)
        {
            string candidate = $"{baseCandidate} ({i})";
            if (!existingSet.Contains(candidate))
                return candidate;
        }

        return baseCandidate;
    }

    private void Cerrar()
    {
        OnCerrar?.Invoke();
    }
}

