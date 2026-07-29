using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Epp;

public sealed partial class EppViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    public EppViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        GoToManoDeObraCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.ManoDeObra), () => !ShowEmptyState);
        GoToCostoCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Costo), () => !ShowEmptyState);
        SaveCommand = new RelayCommand(Save, () => HasPendingChanges && CanEdit);
        CancelCommand = new RelayCommand(Cancel, () => HasPendingChanges && CanEdit);
        SyncFromMoCommand = new RelayCommand(() => SyncFromMo(persist: true), () => !ShowEmptyState && CanEdit);
        ResetSeedCommand = new RelayCommand(ResetSeed, () => !ShowEmptyState && CanEdit);
        AddItemCommand = new RelayCommand<EppSeccionVm>(AddBlankItem, _ => CanEdit);
        AddOpcionalItemCommand = new RelayCommand(AddOpcionalBlank, () => CanEdit && AccOpcionalesSecciones.Count > 0);
        RemoveItemCommand = new RelayCommand<EppItemVm>(RemoveItem, _ => CanEdit);
        AddCatalogItemCommand = new RelayCommand<EppCatalogEntryVm>(AddFromCatalog, _ => CanEdit);

        foreach (var (concepto, precio, codigo) in EppBudgetData.Catalog)
            Catalog.Add(new EppCatalogEntryVm { Concepto = concepto, Precio = precio, Codigo = codigo });

        Refresh();
    }

    public ICommand OpenProjectCommand { get; }
    public ICommand GoToManoDeObraCommand { get; }
    public ICommand GoToCostoCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SyncFromMoCommand { get; }
    public ICommand ResetSeedCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand AddOpcionalItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand AddCatalogItemCommand { get; }

    public ObservableCollection<EppCatalogEntryVm> Catalog { get; } = new();
    public ObservableCollection<EppSeccionVm> MoDirectaSecciones { get; } = new();
    public ObservableCollection<EppSeccionVm> MoIndirectaSecciones { get; } = new();
    public ObservableCollection<EppSeccionVm> AccOpcionalesSecciones { get; } = new();

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public bool HasPendingChanges
    {
        get => hasPendingChanges;
        private set
        {
            if (SetProperty(ref hasPendingChanges, value))
            {
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    public bool CanEdit
    {
        get => canEdit;
        private set
        {
            if (SetProperty(ref canEdit, value))
            {
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SyncFromMoCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ResetSeedCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddItemCommand as RelayCommand<EppSeccionVm>)?.RaiseCanExecuteChanged();
                (AddOpcionalItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoveItemCommand as RelayCommand<EppItemVm>)?.RaiseCanExecuteChanged();
                (AddCatalogItemCommand as RelayCommand<EppCatalogEntryVm>)?.RaiseCanExecuteChanged();
            }
        }
    }

    public int MesesDirecta
    {
        get => mesesDirecta;
        set
        {
            if (SetProperty(ref mesesDirecta, Math.Clamp(value, 1, 24)))
            {
                if (!suppressDirty)
                    ApplyQuantityFormulaToUi();
                MarkDirty();
            }
        }
    }

    public int MesesIndirecta
    {
        get => mesesIndirecta;
        set
        {
            if (SetProperty(ref mesesIndirecta, Math.Clamp(value, 1, 24)))
            {
                if (!suppressDirty)
                    ApplyQuantityFormulaToUi();
                MarkDirty();
            }
        }
    }

    public int DotacionDirecta
    {
        get => dotacionDirecta;
        set
        {
            if (SetProperty(ref dotacionDirecta, Math.Max(0, value)))
            {
                UpdateMoSyncLabel();
                if (!suppressDirty)
                    ApplyQuantityFormulaToUi();
                MarkDirty();
            }
        }
    }

    public int DotacionIndirecta
    {
        get => dotacionIndirecta;
        set
        {
            if (SetProperty(ref dotacionIndirecta, Math.Max(0, value)))
            {
                UpdateMoSyncLabel();
                if (!suppressDirty)
                    ApplyQuantityFormulaToUi();
                MarkDirty();
            }
        }
    }

    public bool OpcionalesIncluidos
    {
        get => opcionalesIncluidos;
        set
        {
            if (SetProperty(ref opcionalesIncluidos, value))
            {
                RecalcTotals();
                MarkDirty();
            }
        }
    }

    public decimal TotalEpp { get => totalEpp; private set => SetProperty(ref totalEpp, value); }
    public decimal TotalMoDirecta { get => totalMoDirecta; private set => SetProperty(ref totalMoDirecta, value); }
    public decimal TotalMoIndirecta { get => totalMoIndirecta; private set => SetProperty(ref totalMoIndirecta, value); }
    public decimal TotalAccOpcionales { get => totalAccOpcionales; private set => SetProperty(ref totalAccOpcionales, value); }

    public string TotalEppLabel => $"$ {TotalEpp:N0}";
    public string TotalMoDirectaLabel => $"$ {TotalMoDirecta:N0}";
    public string TotalMoIndirectaLabel => $"$ {TotalMoIndirecta:N0}";
    public string TotalAccOpcionalesLabel => $"$ {TotalAccOpcionales:N0}";
    public string GranTotalLabel => TotalEppLabel;

    public string MoDirectaCountLabel { get; private set; } = "";
    public string MoIndirectaCountLabel { get; private set; } = "";
    public string AccOpcionalesCountLabel { get; private set; } = "";
    public string IndumentariaTotalLabel { get; private set; } = "";
    public string ItemsCountLabel { get; private set; } = "0";
    public string FooterText { get; private set; } = "";
    public string MoSyncLabel
    {
        get => moSyncLabel;
        private set => SetProperty(ref moSyncLabel, value);
    }

    public string StatusMessage
    {
        get => HasPendingChanges ? "Cambios sin guardar" : statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (suppressSessionReload)
            return;

        var scope = e.Scope ?? "";
        if (scope.Contains("mo_directa", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("mo_indirecta", StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (ShowEmptyState || !CanEdit)
                {
                    Refresh();
                    return;
                }

                // Si el usuario tiene edits locales, solo refresca KPIs de MO al sync manual.
                if (HasPendingChanges)
                {
                    StatusMessage = "MO actualizada — pulsa ↻ Sync MO para recalcular cantidades";
                    OnPropertyChanged(nameof(StatusMessage));
                    return;
                }

                SyncFromMo(persist: false);
                ReloadUiFromSessionEpp();
            });
            return;
        }

        if (string.Equals(scope, "epp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, "gastos_operacionales", StringComparison.OrdinalIgnoreCase))
        {
            if (HasPendingChanges)
                return;
        }

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);
    }

    public void Refresh() => LoadFromSession();

    public void PrepareAddFromCatalog(EppSeccionVm? seccion, string side)
    {
        catalogTargetSeccion = seccion ?? AccOpcionalesSecciones.FirstOrDefault();
        catalogTargetSide = side;
        (AddCatalogItemCommand as RelayCommand<EppCatalogEntryVm>)?.RaiseCanExecuteChanged();
    }

    private void AddBlankItem(EppSeccionVm? sec)
    {
        if (sec == null || !CanEdit) return;
        catalogTargetSide = GuessSide(sec);
        var cant = DefaultCantidad(catalogTargetSide);
        sec.AddItem("Nuevo elemento EPP", cant, 0m);
    }

    private void AddOpcionalBlank()
    {
        AddBlankItem(AccOpcionalesSecciones.FirstOrDefault());
    }

    private decimal DefaultCantidad(string side) => side switch
    {
        "dir" => EppBudgetData.ComputeCantidadDirecta(DotacionDirecta, MesesDirecta),
        "ind" => EppBudgetData.ComputeCantidadIndirecta(DotacionIndirecta, MesesIndirecta),
        _ => DotacionDirecta > 0 ? EppBudgetData.ComputeCantidadDirecta(DotacionDirecta, MesesDirecta) : 0m
    };

    private void LoadFromSession()
    {
        ShowEmptyState = !host.ProjectWorkspace.HasOpenProject;
        CanEdit = !ShowEmptyState && !host.Session.IsReadOnly;
        (GoToManoDeObraCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GoToCostoCommand as RelayCommand)?.RaiseCanExecuteChanged();
        if (ShowEmptyState) return;

        suppressDirty = true;
        try
        {
            var data = host.Session.Data;
            ProjectSnapshotIntegrity.Ensure(data);
            var (dotDir, dotInd) = EppBudgetData.ReadMoDotacion(data);

            var epp = data.Epp;
            if (epp == null || !epp.HasContent)
            {
                epp = EppBudgetData.CreateSeed(dotDir, dotInd);
                data.Epp = epp;
                if (CanEdit)
                {
                    EppBudgetData.PushToGoCategory(data, epp);
                    suppressSessionReload = true;
                    try { host.Session.MarkDirty("epp", "gastos_operacionales"); }
                    finally { suppressSessionReload = false; }
                }
            }
            else
            {
                // Siempre alinear dotación con MO al abrir (cantidades se recalculan).
                epp.ApplyMoDotacion(dotDir, dotInd);
                data.Epp = epp;
                if (CanEdit)
                    EppBudgetData.PushToGoCategory(data, epp);
            }

            ApplyEppToUi(epp);
            HasPendingChanges = false;
            StatusMessage = dotDir + dotInd > 0
                ? $"Sincronizado con MO · {dotDir} dir. · {dotInd} ind."
                : "MO en 0 — EPP listo para cuando cargues dotación";
        }
        finally
        {
            suppressDirty = false;
        }
    }

    private void ReloadUiFromSessionEpp()
    {
        suppressDirty = true;
        try
        {
            var epp = host.Session.Data.Epp ?? EppBudgetData.Default();
            ApplyEppToUi(epp);
            HasPendingChanges = false;
            StatusMessage = $"Sincronizado con MO · {DotacionDirecta} dir. · {DotacionIndirecta} ind. → GO 3.5";
        }
        finally
        {
            suppressDirty = false;
        }
    }

    private void ApplyEppToUi(EppBudgetData epp)
    {
        MesesDirecta = epp.MesesDirecta > 0 ? epp.MesesDirecta : 3;
        MesesIndirecta = epp.MesesIndirecta > 0 ? epp.MesesIndirecta : 6;
        DotacionDirecta = epp.DotacionDirecta;
        DotacionIndirecta = epp.DotacionIndirecta;
        OpcionalesIncluidos = epp.OpcionalesIncluidos;
        LoadSecciones(MoDirectaSecciones, epp.MoDirecta);
        LoadSecciones(MoIndirectaSecciones, epp.MoIndirecta);
        LoadSecciones(AccOpcionalesSecciones, epp.AccesoriosOpcionales);
        UpdateMoSyncLabel();
        RecalcTotals();
    }

    private void LoadSecciones(ObservableCollection<EppSeccionVm> target, System.Collections.Generic.List<EppSeccionRow>? source)
    {
        target.Clear();
        foreach (var row in source ?? Enumerable.Empty<EppSeccionRow>())
            target.Add(new EppSeccionVm(row, OnItemChanged));
    }

    private void OnItemChanged()
    {
        RecalcTotals();
        MarkDirty();
    }

    private void ApplyQuantityFormulaToUi()
    {
        var cantDir = EppBudgetData.ComputeCantidadDirecta(DotacionDirecta, MesesDirecta);
        var cantInd = EppBudgetData.ComputeCantidadIndirecta(DotacionIndirecta, MesesIndirecta);
        foreach (var item in MoDirectaSecciones.SelectMany(s => s.Items))
            item.Cantidad = cantDir;
        foreach (var item in MoIndirectaSecciones.SelectMany(s => s.Items))
            item.Cantidad = cantInd;

        var hasMo = DotacionDirecta > 0 || DotacionIndirecta > 0;
        foreach (var item in AccOpcionalesSecciones.SelectMany(s => s.Items))
        {
            if (item.Concepto.Contains("Cubrenuca", StringComparison.OrdinalIgnoreCase))
                item.Cantidad = cantDir;
            else if (!hasMo)
                item.Cantidad = 0;
        }

        RecalcTotals();
    }

    private void UpdateMoSyncLabel()
    {
        MoSyncLabel = DotacionDirecta + DotacionIndirecta > 0
            ? $"MO · {DotacionDirecta} dir · {DotacionIndirecta} ind"
            : "MO · sin dotación";
    }

    private void RecalcTotals()
    {
        TotalMoDirecta = MoDirectaSecciones.Sum(s => s.Subtotal);
        TotalMoIndirecta = MoIndirectaSecciones.Sum(s => s.Subtotal);
        TotalAccOpcionales = AccOpcionalesSecciones.Sum(s => s.Subtotal);
        TotalEpp = TotalMoDirecta + TotalMoIndirecta + (OpcionalesIncluidos ? TotalAccOpcionales : 0m);

        var dirItems = MoDirectaSecciones.Sum(s => s.Items.Count);
        var indItems = MoIndirectaSecciones.Sum(s => s.Items.Count);
        var opcItems = AccOpcionalesSecciones.Sum(s => s.Items.Count);
        MoDirectaCountLabel = $"{dirItems} ítems · dot. {DotacionDirecta}";
        MoIndirectaCountLabel = $"{indItems} ítems · roles {DotacionIndirecta}";
        AccOpcionalesCountLabel = OpcionalesIncluidos
            ? $"{opcItems} ítems · Costo fijo"
            : $"{opcItems} ítems · Excluido";
        IndumentariaTotalLabel = $"Valor indumentaria: $ {(TotalMoDirecta + TotalMoIndirecta):N0}";
        ItemsCountLabel = $"{dirItems + indItems + opcItems}";
        FooterText = $"M.O. Directa ({MesesDirecta}m): {TotalMoDirectaLabel} · M.O. Indirecta ({MesesIndirecta}m): {TotalMoIndirectaLabel} · Acc. Opcionales: {(OpcionalesIncluidos ? TotalAccOpcionalesLabel : "$ 0")} · GO 3.5 ← {TotalEppLabel}";

        OnPropertyChanged(nameof(TotalEppLabel));
        OnPropertyChanged(nameof(TotalMoDirectaLabel));
        OnPropertyChanged(nameof(TotalMoIndirectaLabel));
        OnPropertyChanged(nameof(TotalAccOpcionalesLabel));
        OnPropertyChanged(nameof(GranTotalLabel));
        OnPropertyChanged(nameof(MoDirectaCountLabel));
        OnPropertyChanged(nameof(MoIndirectaCountLabel));
        OnPropertyChanged(nameof(AccOpcionalesCountLabel));
        OnPropertyChanged(nameof(IndumentariaTotalLabel));
        OnPropertyChanged(nameof(ItemsCountLabel));
        OnPropertyChanged(nameof(FooterText));

        foreach (var s in MoDirectaSecciones.Concat(MoIndirectaSecciones).Concat(AccOpcionalesSecciones))
            s.RefreshLabels();
    }

    private void MarkDirty()
    {
        if (suppressDirty || !CanEdit) return;
        HasPendingChanges = true;
    }

    private void Save()
    {
        if (!CanEdit || ShowEmptyState) return;
        PersistEppAndGo();
        HasPendingChanges = false;
        StatusMessage = "EPP guardado · GO 3.5 actualizado en Desglose de costos";
    }

    private void Cancel()
    {
        if (!HasPendingChanges) return;
        LoadFromSession();
        StatusMessage = "Cambios descartados";
    }

    private EppBudgetData BuildData() => new()
    {
        MesesDirecta = MesesDirecta,
        MesesIndirecta = MesesIndirecta,
        DotacionDirecta = DotacionDirecta,
        DotacionIndirecta = DotacionIndirecta,
        OpcionalesIncluidos = OpcionalesIncluidos,
        MoDirecta = MoDirectaSecciones.Select(s => s.ToRow()).ToList(),
        MoIndirecta = MoIndirectaSecciones.Select(s => s.ToRow()).ToList(),
        AccesoriosOpcionales = AccOpcionalesSecciones.Select(s => s.ToRow()).ToList()
    };

    private void PersistEppAndGo()
    {
        var data = host.Session.Data;
        var epp = BuildData();
        data.Epp = epp;
        EppBudgetData.PushToGoCategory(data, epp);
        suppressSessionReload = true;
        try
        {
            host.Session.MarkDirty("epp", "gastos_operacionales");
        }
        finally
        {
            suppressSessionReload = false;
        }
    }

    private void SyncFromMo(bool persist)
    {
        if (!CanEdit) return;
        var data = host.Session.Data;
        var (dotDir, dotInd) = EppBudgetData.ReadMoDotacion(data);

        suppressDirty = true;
        try
        {
            DotacionDirecta = dotDir;
            DotacionIndirecta = dotInd;
            ApplyQuantityFormulaToUi();

            // También sincroniza opcionales con la misma regla de dominio.
            var temp = BuildData();
            temp.SyncQuantitiesFromDotacion();
            LoadSecciones(AccOpcionalesSecciones, temp.AccesoriosOpcionales);
            RecalcTotals();
        }
        finally
        {
            suppressDirty = false;
        }

        if (persist)
        {
            PersistEppAndGo();
            HasPendingChanges = false;
            StatusMessage = $"Sync MO → EPP → GO 3.5 · {dotDir} dir. · {dotInd} ind.";
        }
        else
        {
            // Persistencia ya hecha desde Mano de obra; solo UI.
            HasPendingChanges = false;
            StatusMessage = $"Cantidades alineadas con MO · {dotDir} dir. · {dotInd} ind.";
        }

        OnPropertyChanged(nameof(StatusMessage));
    }

    private void ResetSeed()
    {
        if (!CanEdit) return;
        suppressDirty = true;
        try
        {
            var (dotDir, dotInd) = EppBudgetData.ReadMoDotacion(host.Session.Data);
            var seed = EppBudgetData.CreateSeed(dotDir, dotInd);
            ApplyEppToUi(seed);
        }
        finally
        {
            suppressDirty = false;
        }
        MarkDirty();
        StatusMessage = "Plantilla EPP restablecida desde dotación MO";
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void RemoveItem(EppItemVm? item)
    {
        if (item == null || !CanEdit) return;
        foreach (var sec in MoDirectaSecciones.Concat(MoIndirectaSecciones).Concat(AccOpcionalesSecciones))
        {
            if (sec.Items.Contains(item))
            {
                sec.RemoveItem(item);
                return;
            }
        }
    }

    private void AddFromCatalog(EppCatalogEntryVm? entry)
    {
        if (entry == null || !CanEdit) return;
        var sec = catalogTargetSeccion;
        if (sec == null)
        {
            if (catalogTargetSide == "dir") sec = MoDirectaSecciones.FirstOrDefault();
            else if (catalogTargetSide == "ind") sec = MoIndirectaSecciones.FirstOrDefault();
            else sec = AccOpcionalesSecciones.FirstOrDefault();
        }
        if (sec == null) return;

        sec.AddItem(entry.Concepto, DefaultCantidad(catalogTargetSide), entry.Precio);
    }

    private string GuessSide(EppSeccionVm? sec)
    {
        if (sec == null) return "opc";
        if (MoDirectaSecciones.Contains(sec)) return "dir";
        if (MoIndirectaSecciones.Contains(sec)) return "ind";
        return "opc";
    }
}
