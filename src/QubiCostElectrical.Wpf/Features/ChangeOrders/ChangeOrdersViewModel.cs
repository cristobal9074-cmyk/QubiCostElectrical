using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public sealed partial class ChangeOrdersViewModel : ObservableObject, IDisposable, IRefreshablePage
{
    private readonly BudgetProjectSession _session;
    private readonly WpfUndoService _undo;

    internal Func<ChangeOrderRow, bool, bool> ShowEditor = (oc, isNew) =>
        ChangeOrderEditorDialog.Show(oc, isNew);

    internal Func<string, bool> ConfirmDelete = msg =>
        WpfDialogService.Confirm("Eliminar orden de cambio", msg, isWarning: true);

    internal Func<string, string, bool> ConfirmTransition = (title, msg) =>
        WpfDialogService.Confirm(title, msg, isWarning: false);

    public ObservableCollection<ChangeOrderRowVm> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(CanEnviarRevision))]
    [NotifyPropertyChangedFor(nameof(CanAprobar))]
    [NotifyPropertyChangedFor(nameof(CanRechazar))]
    [NotifyPropertyChangedFor(nameof(CanReabrir))]
    private ChangeOrderRowVm? _selected;

    [ObservableProperty]
    private string _filterEstado = "";

    partial void OnFilterEstadoChanged(string value) => ApplyFilter();

    [ObservableProperty]
    private string _searchText = "";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public bool CanEdit => Selected != null && !ChangeOrderStateMachine.IsLocked(Selected.Estado) && !_session.IsReadOnly;
    public bool CanDelete => Selected != null && !ChangeOrderStateMachine.IsLocked(Selected.Estado) && !_session.IsReadOnly;
    public bool CanEnviarRevision => Selected != null && ChangeOrderStateMachine.IsTransitionAllowed(Selected.Estado, "En revisión") && !_session.IsReadOnly;
    public bool CanAprobar => Selected != null && ChangeOrderStateMachine.IsTransitionAllowed(Selected.Estado, "Aprobado") && !_session.IsReadOnly;
    public bool CanRechazar => Selected != null && ChangeOrderStateMachine.IsTransitionAllowed(Selected.Estado, "Rechazado") && !_session.IsReadOnly;
    public bool CanReabrir => Selected != null && ChangeOrderStateMachine.IsTransitionAllowed(Selected.Estado, "Borrador") && Selected.Estado == "Rechazado" && !_session.IsReadOnly;

    private readonly ItemizadoPresenter? _itemizadoPresenter;
    private readonly IItemizadoWorkspaceService? _itemizadoWorkspace;
    private readonly IPriceBankStore? _priceBankStore;

    public int TotalOrdenes => _session.OrdenesCambio.Count;
    public int TotalBorrador => Count("Borrador");
    public int TotalEnRevision => Count("En revisión");
    public int TotalAprobado => Count("Aprobado");
    public int TotalRechazado => Count("Rechazado");
    public decimal MontoAprobado => _session.OrdenesCambio
                                          .Where(x => x.Estado == "Aprobado")
                                          .Sum(x => x.DeltaVentaUf);
    public decimal MontoPendiente => _session.OrdenesCambio
                                          .Where(x => x.Estado is "Borrador" or "En revisión")
                                          .Sum(x => x.DeltaVentaUf);

    public decimal MontoContratoOriginalUf => _session.Cubicacion.Sum(c => c.UfTotal);
    public string MontoContratoOriginalDisplay => FormatMonto(MontoContratoOriginalUf);

    public decimal TotalProyectadoObraUf => MontoContratoOriginalUf + MontoAprobado;
    public string TotalProyectadoObraDisplay => FormatMonto(TotalProyectadoObraUf);

    public string PorcentajeAumentoAprobadoDisplay => MontoContratoOriginalUf > 0
        ? $"+{(MontoAprobado / MontoContratoOriginalUf * 100m):N1}%"
        : "+0.0%";

    /// <summary>
    /// Política de moneda del proyecto: en CLP no se expone ninguna cifra en UF. Los montos se
    /// guardan en UF (motor), así que en CLP se convierten con el valor UF vigente.
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(_session.ProjectInfo.MonedaPresupuesto, CurrencyConversionService.CurrencyClp, StringComparison.OrdinalIgnoreCase);

    private decimal UfRate => _session.ValorUfClp > 0m ? _session.ValorUfClp : PriceConversion.DefaultValorUfClp;

    public string MontoAprobadoDisplay => FormatMonto(MontoAprobado);
    public string MontoPendienteDisplay => FormatMonto(MontoPendiente);
    public string ImpactoNetoDisplay => MuestraUf
        ? $"{MontoAprobado:+0.000;-0.000;0.000} UF"
        : ClpSigned(Math.Round(MontoAprobado * UfRate, 0, MidpointRounding.AwayFromZero));

    private string FormatMonto(decimal uf)
        => MuestraUf ? $"{uf:N3} UF" : $"$ {Math.Round(uf * UfRate, 0, MidpointRounding.AwayFromZero):N0}";

    private string FormatDelta(decimal uf)
        => MuestraUf ? $"{uf:+0.000;-0.000;0.000} UF" : ClpSigned(Math.Round(uf * UfRate, 0, MidpointRounding.AwayFromZero));

    private static string ClpSigned(decimal clp)
        => clp > 0m ? $"+$ {clp:N0}" : clp < 0m ? $"- $ {Math.Abs(clp):N0}" : "$ 0";

    private bool HasProject => !string.IsNullOrEmpty(_session.CurrentProjectPath);

    public ChangeOrdersViewModel(WpfAppHost host)
        : this(host.Session, host.UndoService, host.Itemizado, host.ItemizadoWorkspace, host.PriceBankStore) { }

    public ChangeOrdersViewModel(
        BudgetProjectSession session,
        WpfUndoService undo,
        ItemizadoPresenter? itemizadoPresenter = null,
        IItemizadoWorkspaceService? itemizadoWorkspace = null,
        IPriceBankStore? priceBankStore = null)
    {
        _session = session;
        _undo = undo;
        _itemizadoPresenter = itemizadoPresenter;
        _itemizadoWorkspace = itemizadoWorkspace;
        _priceBankStore = priceBankStore;
        ShowEditor = (oc, isNew) => ChangeOrderEditorDialog.Show(oc, isNew, MuestraUf, UfRate, GetContractPartidas(), _session.Cubicacion, _priceBankStore, _session);
        Reload();
    }

    public List<ChangeOrderPartidaPickerItem> GetContractPartidas()
    {
        var result = new List<ChangeOrderPartidaPickerItem>();
        if (_itemizadoWorkspace != null && _itemizadoPresenter != null)
        {
            try
            {
                var state = _itemizadoWorkspace.BuildPageState(_itemizadoPresenter, _session);
                foreach (var row in state.Rows.Where(r => !r.EsTitulo))
                {
                    decimal puUf = row.UnitPriceUf;
                    decimal puClp = Math.Round(puUf * UfRate, 0, MidpointRounding.AwayFromZero);
                    result.Add(new ChangeOrderPartidaPickerItem
                    {
                        Id = row.Id,
                        Item = row.Item,
                        Descripcion = row.Descripcion,
                        Unidad = row.Unidad,
                        CantidadContrato = row.Cantidad,
                        PrecioUnitarioUf = puUf,
                        PrecioUnitarioClp = puClp,
                        MuestraUf = MuestraUf
                    });
                }
                if (result.Count > 0)
                    return result;
            }
            catch
            {
                // Fallback directo a session si falla el workspace
            }
        }

        // Fallback a partir de session.Itemizado y session.Cubicacion
        var cubByItem = _session.Cubicacion
            .Where(c => !string.IsNullOrWhiteSpace(c.ItemId))
            .GroupBy(c => c.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in _session.Itemizado.Where(i => !i.EsTitulo))
        {
            decimal cant = item.Cantidad;
            decimal totalClp = 0m;
            decimal totalUf = 0m;

            if (cubByItem.TryGetValue(item.Id, out var cubRows))
            {
                totalClp = cubRows.Sum(c => c.Cantidad * (c.MatClpConFallback + c.MoClpConFallback));
                totalUf = cubRows.Sum(c => c.UfTotal);
            }

            decimal puUf = cant > 0 ? totalUf / cant : 0m;
            decimal puClp = cant > 0 ? totalClp / cant : (UfRate > 0 ? puUf * UfRate : 0m);

            result.Add(new ChangeOrderPartidaPickerItem
            {
                Id = item.Id,
                Item = item.Item,
                Descripcion = item.Descripcion,
                Unidad = item.Unidad,
                CantidadContrato = cant,
                PrecioUnitarioUf = puUf,
                PrecioUnitarioClp = puClp,
                MuestraUf = MuestraUf
            });
        }

        return result;
    }

    // ── IRefreshablePage ──────────────────────────────────────────────────
    public void Refresh() => Reload();

    // ── Comandos ──────────────────────────────────────────────────────────

    public void AddNew()
    {
        if (!HasProject || _session.IsReadOnly) return;
        var oc = new ChangeOrderRow
        {
            Numero = NextNumero(),
            Titulo = "Nueva orden de cambio",
            Estado = "Borrador",
            CreadoPor = _session.ProjectInfo.UsuarioNombre,
            GastosGeneralesPct = _session.ProjectInfo.GastosGeneralesPct,
            UtilidadPct = _session.ProjectInfo.UtilidadPct,
        };

        var before = _session.CreateSnapshot();
        if (!ShowEditor(oc, true)) return;

        _undo.PushSnapshot(before, "Nueva orden de cambio");
        _session.OrdenesCambio.Add(oc);
        _session.MarkDirty(ProjectDirtyScope.OrdenesCambio);
        Reload();
        Selected = Items.FirstOrDefault(i => i.Id == oc.Id);
    }

    public void EditSelected()
    {
        if (_session.IsReadOnly || Selected is not { } vm) return;
        if (ChangeOrderStateMachine.IsLocked(vm.Estado)) return;
        var oc = FindRow(vm.Id);
        if (oc == null) return;

        var before = _session.CreateSnapshot();
        if (!ShowEditor(oc, false)) return;
        _undo.PushSnapshot(before, $"Editar orden {oc.Numero}");

        oc.FechaModificacion = AppClock.Now;
        _session.MarkDirty(ProjectDirtyScope.OrdenesCambio);
        Reload();
        Selected = Items.FirstOrDefault(i => i.Id == oc.Id);
    }

    public void DeleteSelected()
    {
        if (_session.IsReadOnly || Selected is not { } vm) return;
        if (ChangeOrderStateMachine.IsLocked(vm.Estado)) return;
        var oc = FindRow(vm.Id);
        if (oc == null) return;

        if (!ConfirmDelete($"¿Eliminar «{oc.Numero} — {oc.Titulo}»? Esta acción no se puede deshacer."))
            return;

        _undo.PushUndoPoint(_session, $"Eliminar orden {oc.Numero}");
        _session.OrdenesCambio.Remove(oc);
        _session.MarkDirty(ProjectDirtyScope.OrdenesCambio);
        Reload();
    }

    public void EnviarARevision()
    {
        if (!CanEnviarRevision) return;
        var oc = FindRow(Selected!.Id);
        if (oc == null) return;
        _undo.PushUndoPoint(_session, $"Enviar a revisión {oc.Numero}");
        Transition(oc, "En revisión");
    }

    public void AprobarSelected()
    {
        if (!CanAprobar) return;
        var oc = FindRow(Selected!.Id);
        if (oc == null) return;
        if (!ConfirmTransition("Aprobar orden", $"¿Aprobar «{oc.Numero}»? Una vez aprobada no podrá editarse."))
            return;
        _undo.PushUndoPoint(_session, $"Aprobar orden {oc.Numero}");
        Transition(oc, "Aprobado");
    }

    public void RechazarSelected()
    {
        if (!CanRechazar) return;
        var oc = FindRow(Selected!.Id);
        if (oc == null) return;
        if (!ConfirmTransition("Rechazar orden", $"¿Rechazar «{oc.Numero}»?"))
            return;
        _undo.PushUndoPoint(_session, $"Rechazar orden {oc.Numero}");
        Transition(oc, "Rechazado");
    }

    public void ReabrirSelected()
    {
        if (!CanReabrir) return;
        var oc = FindRow(Selected!.Id);
        if (oc == null) return;
        if (!ConfirmTransition("Reabrir orden", $"¿Reabrir «{oc.Numero}» y pasarla a Borrador?"))
            return;
        _undo.PushUndoPoint(_session, $"Reabrir orden {oc.Numero}");
        Transition(oc, "Borrador");
    }

    // ── Estado vacío ──────────────────────────────────────────────────────
    public bool HasNoProject => !HasProject;

    // ── Helpers internos ──────────────────────────────────────────────────

    private void Transition(ChangeOrderRow oc, string newEstado)
    {
        oc.Estado = newEstado;
        oc.FechaModificacion = AppClock.Now;
        _session.MarkDirty(ProjectDirtyScope.OrdenesCambio);
        Reload();
        Selected = Items.FirstOrDefault(i => i.Id == oc.Id);
    }

    private void Reload()
    {
        var prevId = Selected?.Id;
        Items.Clear();

        if (!HasProject)
        {
            RefreshSummary();
            return;
        }

        var all = _session.OrdenesCambio
            .OrderByDescending(r => r.FechaCreacion)
            .ToList();

        var filtered = FilterItems(all);

        int seq = 1;
        foreach (var row in filtered)
            Items.Add(new ChangeOrderRowVm(row, seq++, FormatDelta));

        Selected = Items.FirstOrDefault(i => i.Id == prevId);
        RefreshSummary();
    }

    private IEnumerable<ChangeOrderRow> FilterItems(IEnumerable<ChangeOrderRow> source)
    {
        if (!string.IsNullOrWhiteSpace(FilterEstado))
            source = source.Where(r => r.Estado == FilterEstado);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToUpperInvariant();
            source = source.Where(r =>
                r.Numero.ToUpperInvariant().Contains(q) ||
                r.Titulo.ToUpperInvariant().Contains(q) ||
                r.CreadoPor.ToUpperInvariant().Contains(q) ||
                r.Nota.ToUpperInvariant().Contains(q));
        }

        return source;
    }

    private void ApplyFilter() => Reload();

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(TotalOrdenes));
        OnPropertyChanged(nameof(TotalBorrador));
        OnPropertyChanged(nameof(TotalEnRevision));
        OnPropertyChanged(nameof(TotalAprobado));
        OnPropertyChanged(nameof(TotalRechazado));
        OnPropertyChanged(nameof(MontoAprobado));
        OnPropertyChanged(nameof(MontoPendiente));
        OnPropertyChanged(nameof(MuestraUf));
        OnPropertyChanged(nameof(MontoAprobadoDisplay));
        OnPropertyChanged(nameof(MontoPendienteDisplay));
        OnPropertyChanged(nameof(ImpactoNetoDisplay));
        OnPropertyChanged(nameof(MontoContratoOriginalDisplay));
        OnPropertyChanged(nameof(TotalProyectadoObraDisplay));
        OnPropertyChanged(nameof(PorcentajeAumentoAprobadoDisplay));
        OnPropertyChanged(nameof(HasNoProject));
    }

    private string NextNumero()
    {
        int max = 0;
        foreach (var oc in _session.OrdenesCambio)
        {
            if (oc.Numero.StartsWith("OC-") &&
                int.TryParse(oc.Numero[3..], out int n) && n > max)
                max = n;
        }
        return $"OC-{max + 1:D3}";
    }

    private ChangeOrderRow? FindRow(string id) =>
        _session.OrdenesCambio.FirstOrDefault(r => r.Id == id);

    private int Count(string estado) =>
        _session.OrdenesCambio.Count(x => x.Estado == estado);


    public void Dispose() { }
}

public sealed class ChangeOrderRowVm
{
    private readonly ChangeOrderRow _row;
    private readonly Func<decimal, string> _formatDelta;

    public ChangeOrderRowVm(ChangeOrderRow row, int seq, Func<decimal, string>? formatDelta = null)
    {
        _row = row;
        Seq = seq;
        _formatDelta = formatDelta ?? (uf => $"{uf:+0.000;-0.000;0.000} UF");
    }

    public string Id => _row.Id;
    public int Seq { get; }
    public string Numero => _row.Numero;
    public string Titulo => _row.Titulo;
    public string Estado => _row.Estado;
    public string CreadoPor => _row.CreadoPor;
    public DateTime FechaCreacion => _row.FechaCreacion;
    public DateTime FechaModificacion => _row.FechaModificacion;
    public decimal DeltaVentaUf => _row.DeltaVentaUf;
    public decimal DeltaCostoUf => _row.DeltaCostoUf;
    public string Nota => _row.Nota;

    public string EstadoDisplay => Estado switch
    {
        "Aprobado" => "✓ Aprobado",
        "Rechazado" => "✗ Rechazado",
        "En revisión" => "◷ En revisión",
        _ => "○ Borrador"
    };

    public string FechaDisplay => FechaCreacion.ToString("dd/MM/yyyy");
    public string ModificadoDisplay => FechaModificacion.ToString("dd/MM/yyyy");
    public string DeltaDisplay => _formatDelta(DeltaVentaUf);
    public string DeltaKind => DeltaVentaUf > 0 ? "Aumenta" : DeltaVentaUf < 0 ? "Reduce" : "Neutro";
    public bool EsBorrador => Estado == "Borrador";
    public bool EsAprobado => Estado == "Aprobado";
}
