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

public sealed partial class EppViewModel : SessionAwareViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    public EppViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace) { }

    public EppViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
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

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
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

    public int MesesObra
    {
        get => mesesObra;
        set
        {
            if (SetProperty(ref mesesObra, Math.Clamp(value, 0, 120)))
            {
                if (!suppressDirty)
                    ApplyQuantityFormulaToUi();
                MarkDirty();
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
    public decimal TotalEppUf => PriceConversion.ClpToUf(TotalEpp, session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp);
    public string TotalEppUfLabel => $"{TotalEppUf:N3} UF";

    // Política de moneda: en CLP el gran total no expone equivalencia en UF (solo pesos).
    private bool MuestraUf =>
        !string.Equals(session.ProjectInfo.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);

    public string GranTotalDualLabel => TotalEpp > 0m
        ? (MuestraUf ? $"$ {TotalEpp:N0} ({TotalEppUfLabel})" : $"$ {TotalEpp:N0}")
        : "$ 0";

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

    public void Refresh()
    {
        IsLoading = true;
        try
        {
            LoadFromSession();
        }
        finally
        {
            IsLoading = false;
        }
    }

    bool IPageWithPendingChanges.HasPendingChanges => HasPendingChanges;

    bool IPageWithPendingChanges.TryDiscardChanges()
    {
        Cancel();
        return true;
    }

    bool IPageWithCommitChanges.TryApplyChanges()
    {
        Save();
        return !HasPendingChanges;
    }

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

    private decimal DefaultCantidad(string side, string? concepto = null) => side switch
    {
        "dir" => EppBudgetData.ComputeCantidadDirecta(MesesObra, MesesDirecta, DotacionDirecta),
        "ind" => EppBudgetData.ComputeCantidadIndirecta(MesesObra, MesesIndirecta, DotacionIndirecta),
        _ => EppBudgetData.ComputeCantidadOpcional(concepto, MesesObra, MesesDirecta, MesesIndirecta, DotacionDirecta)
    };

    private void LoadFromSession()
    {
        ShowEmptyState = !workspace.HasOpenProject;
        CanEdit = !ShowEmptyState && !session.IsReadOnly;
        (GoToManoDeObraCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GoToCostoCommand as RelayCommand)?.RaiseCanExecuteChanged();
        if (ShowEmptyState) return;

        suppressDirty = true;
        try
        {
            var data = session.Data;
            ProjectSnapshotIntegrity.Ensure(data);
            var mo = EppBudgetData.ReadMoDrivers(data);

            var epp = data.Epp;
            if (epp == null || !epp.HasContent)
            {
                epp = EppBudgetData.CreateSeed(mo.MesesObra, mo.PromedioDirecto, mo.DotacionIndirecta);
                data.Epp = epp;
                if (CanEdit)
                {
                    EppBudgetData.PushToGoCategory(data, epp);
                    suppressSessionReload = true;
                    try { session.MarkDirty(ProjectDirtyScope.Epp, ProjectDirtyScope.GastosOperacionales); }
                    finally { suppressSessionReload = false; }
                }
            }
            else
            {
                // Siempre alinear dotación con MO al abrir (cantidades se recalculan).
                epp.ApplyMoDotacion(mo.MesesObra, mo.PromedioDirecto, mo.DotacionIndirecta);
                data.Epp = epp;
                if (CanEdit)
                    EppBudgetData.PushToGoCategory(data, epp);
            }

            ApplyEppToUi(epp);
            HasPendingChanges = false;
            StatusMessage = mo.MesesObra > 0 && mo.PromedioDirecto + mo.DotacionIndirecta > 0
                ? $"Sincronizado con MO · {mo.MesesObra} meses · promedio {mo.PromedioDirecto} dir. · {mo.DotacionIndirecta} ind."
                : "MO en 0 — EPP listo para cuando cargues la dotación";
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
            var epp = session.Data.Epp ?? EppBudgetData.Default();
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
        MesesObra = Math.Max(0, epp.MesesObra);
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
        var cantDir = EppBudgetData.ComputeCantidadDirecta(MesesObra, MesesDirecta, DotacionDirecta);
        var cantInd = EppBudgetData.ComputeCantidadIndirecta(MesesObra, MesesIndirecta, DotacionIndirecta);
        foreach (var item in MoDirectaSecciones.SelectMany(s => s.Items))
            item.Cantidad = cantDir;
        foreach (var item in MoIndirectaSecciones.SelectMany(s => s.Items))
            item.Cantidad = cantInd;
        foreach (var item in AccOpcionalesSecciones.SelectMany(s => s.Items))
            item.Cantidad = EppBudgetData.ComputeCantidadOpcional(item.Concepto, MesesObra, MesesDirecta, MesesIndirecta, DotacionDirecta);

        RecalcTotals();
    }

    private void UpdateMoSyncLabel()
    {
        MoSyncLabel = MesesObra > 0
            ? $"MO · {MesesObra} meses · promedio {DotacionDirecta} · ind. {DotacionIndirecta}"
            : "MO · sin meses de obra";
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
        FooterText = $"Obra {MesesObra} meses · Directa (ciclo {MesesDirecta} m): {TotalMoDirectaLabel} · Indirecta (ciclo {MesesIndirecta} m): {TotalMoIndirectaLabel} · Acc. opcionales: {(OpcionalesIncluidos ? TotalAccOpcionalesLabel : "$ 0")} · GO 3.5 ← {TotalEppLabel}";

        OnPropertyChanged(nameof(TotalEppLabel));
        OnPropertyChanged(nameof(TotalMoDirectaLabel));
        OnPropertyChanged(nameof(TotalMoIndirectaLabel));
        OnPropertyChanged(nameof(TotalAccOpcionalesLabel));
        OnPropertyChanged(nameof(GranTotalLabel));
        OnPropertyChanged(nameof(GranTotalDualLabel));
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
        MesesObra = MesesObra,
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
        var data = session.Data;
        var epp = BuildData();
        data.Epp = epp;
        EppBudgetData.PushToGoCategory(data, epp);
        suppressSessionReload = true;
        try
        {
            session.MarkDirty(ProjectDirtyScope.Epp, ProjectDirtyScope.GastosOperacionales);
        }
        finally
        {
            suppressSessionReload = false;
        }
    }

    private void SyncFromMo(bool persist)
    {
        if (!CanEdit) return;
        var data = session.Data;
        var mo = EppBudgetData.ReadMoDrivers(data);

        suppressDirty = true;
        try
        {
            MesesObra = mo.MesesObra;
            DotacionDirecta = mo.PromedioDirecto;
            DotacionIndirecta = mo.DotacionIndirecta;
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
            StatusMessage = $"Sync MO → EPP → GO 3.5 · {mo.MesesObra} meses · promedio {mo.PromedioDirecto} · ind. {mo.DotacionIndirecta}.";
        }
        else
        {
            HasPendingChanges = false;
            StatusMessage = $"Cantidades alineadas con MO · {mo.MesesObra} meses · promedio {mo.PromedioDirecto} · ind. {mo.DotacionIndirecta}.";
        }

        OnPropertyChanged(nameof(StatusMessage));
    }

    private void ResetSeed()
    {
        if (!CanEdit) return;
        suppressDirty = true;
        try
        {
            var mo = EppBudgetData.ReadMoDrivers(session.Data);
            var seed = EppBudgetData.CreateSeed(mo.MesesObra, mo.PromedioDirecto, mo.DotacionIndirecta);
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

        sec.AddItem(entry.Concepto, DefaultCantidad(catalogTargetSide, entry.Concepto), entry.Precio);
    }

    private string GuessSide(EppSeccionVm? sec)
    {
        if (sec == null) return "opc";
        if (MoDirectaSecciones.Contains(sec)) return "dir";
        if (MoIndirectaSecciones.Contains(sec)) return "ind";
        return "opc";
    }
}
