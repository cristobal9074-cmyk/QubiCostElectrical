using System.Collections.ObjectModel;
using System.ComponentModel;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Currency;

public sealed class CurrencyViewModel : ViewModelBase, IPageWithPendingChanges, IPageWithCommitChanges
{
    private readonly BudgetProjectSession _session;
    private readonly WpfUndoService _undo;
    private readonly CurrencyConversionService _svc = new();

    private string _monedaBase = "CLP";
    private string _monedaSeleccionada = "CLP";
    private decimal _valorUfClp;
    private decimal _tipoCambioUsdClp;
    private DateTime _fechaVigencia = DateTime.Today;
    private string _editUfClp = "";
    private string _editUsdClp = "";
    private string _editFecha = "";
    private bool _hasPendingChanges;
    private string _validationError = "";
    private bool _cacheStale;

    public CurrencyViewModel(WpfAppHost host) : this(host.Session, host.UndoService) { }

    public CurrencyViewModel(BudgetProjectSession session, WpfUndoService undo)
    {
        _session = session;
        _undo = undo;
        LoadFromSession();
    }

    // ─── Propiedades del proyecto ───────────────────────────────────────────

    public string MonedaBase
    {
        get => _monedaBase;
        private set => SetProperty(ref _monedaBase, value);
    }

    public string MonedaSeleccionada
    {
        get => _monedaSeleccionada;
        set
        {
            if (!SetProperty(ref _monedaSeleccionada, value)) return;
            HasPendingChanges = true;
        }
    }

    public decimal ValorUfClp
    {
        get => _valorUfClp;
        private set
        {
            if (!SetProperty(ref _valorUfClp, value)) return;
            OnPropertyChanged(nameof(ResumenClp));
            OnPropertyChanged(nameof(ResumenUsd));
        }
    }

    public decimal TipoCambioUsdClp
    {
        get => _tipoCambioUsdClp;
        private set
        {
            if (!SetProperty(ref _tipoCambioUsdClp, value)) return;
            OnPropertyChanged(nameof(ResumenUsd));
        }
    }

    public DateTime FechaVigencia
    {
        get => _fechaVigencia;
        private set => SetProperty(ref _fechaVigencia, value);
    }

    // ─── Campos de edición ──────────────────────────────────────────────────

    public string EditUfClp
    {
        get => _editUfClp;
        set
        {
            if (!SetProperty(ref _editUfClp, value)) return;
            HasPendingChanges = true;
            ValidateEdits();
        }
    }

    public string EditUsdClp
    {
        get => _editUsdClp;
        set
        {
            if (!SetProperty(ref _editUsdClp, value)) return;
            HasPendingChanges = true;
            ValidateEdits();
        }
    }

    public string EditFecha
    {
        get => _editFecha;
        set
        {
            if (!SetProperty(ref _editFecha, value)) return;
            HasPendingChanges = true;
            ValidateEdits();
        }
    }

    public bool HasPendingChanges
    {
        get => _hasPendingChanges;
        private set => SetProperty(ref _hasPendingChanges, value);
    }

    public string ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    public bool CacheStale
    {
        get => _cacheStale;
        private set => SetProperty(ref _cacheStale, value);
    }

    private bool _showSuccessFeedback;
    public bool ShowSuccessFeedback
    {
        get => _showSuccessFeedback;
        set => SetProperty(ref _showSuccessFeedback, value);
    }

    private bool _hasValidCache;
    public bool HasValidCache
    {
        get => _hasValidCache;
        private set => SetProperty(ref _hasValidCache, value);
    }

    private decimal _cachedUfValue;
    public decimal CachedUfValue
    {
        get => _cachedUfValue;
        private set => SetProperty(ref _cachedUfValue, value);
    }

    private decimal _cachedUsdValue;
    public decimal CachedUsdValue
    {
        get => _cachedUsdValue;
        private set => SetProperty(ref _cachedUsdValue, value);
    }

    private string _cachedUfPrompt = "";
    public string CachedUfPrompt
    {
        get => _cachedUfPrompt;
        private set => SetProperty(ref _cachedUfPrompt, value);
    }

    public void ApplyCachedRates()
    {
        if (!HasValidCache) return;
        EditUfClp = CachedUfValue.ToString("N2");
        if (CachedUsdValue > 0)
            EditUsdClp = CachedUsdValue.ToString("N2");
        EditFecha = DateTime.Today.ToString("dd/MM/yyyy");
        HasPendingChanges = true;
        ShowSuccessFeedback = false;
    }

    public bool CanEdit => !_session.IsReadOnly;

    // ─── Presentación calculada ─────────────────────────────────────────────

    public string ResumenClp => ValorUfClp > 0
        ? $"1 UF = $ {ValorUfClp:N0} CLP"
        : "Sin tasa UF configurada";

    public string ResumenUsd => TipoCambioUsdClp > 0
        ? $"1 USD = $ {TipoCambioUsdClp:N0} CLP"
        : "Sin tasa USD configurada";

    public string FechaVigenciaDisplay => FechaVigencia == default
        ? "—"
        : FechaVigencia.ToString("dd/MM/yyyy");

    public ObservableCollection<string> MonedasDisponibles { get; } =
        new(["CLP", "UF", "USD"]);

    // ─── Historial ──────────────────────────────────────────────────────────

    public ObservableCollection<CurrencyHistoryEntry> Historial { get; } = new();

    // ─── Carga / guardado ───────────────────────────────────────────────────

    private void LoadFromSession()
    {
        var info = _session.ProjectInfo;
        MonedaBase = _svc.NormalizeCurrency(info.MonedaPresupuesto);
        _monedaSeleccionada = MonedaBase;
        OnPropertyChanged(nameof(MonedaSeleccionada));
        ValorUfClp = info.ValorUfClp;
        TipoCambioUsdClp = info.TipoCambioUsdClp;
        FechaVigencia = info.FechaTipoCambio == default ? DateTime.Today : info.FechaTipoCambio;

        _editUfClp = ValorUfClp > 0 ? ValorUfClp.ToString("N2") : "";
        _editUsdClp = TipoCambioUsdClp > 0 ? TipoCambioUsdClp.ToString("N2") : "";
        _editFecha = FechaVigencia.ToString("dd/MM/yyyy");
        HasPendingChanges = false;
        ValidationError = "";

        var cache = EconomicIndicatorCache.TryRead();
        CacheStale = cache == null || EconomicIndicatorCache.IsStale(cache.FetchedAt);
        HasValidCache = cache != null && !CacheStale && cache.UfClp > 0;
        CachedUfValue = cache?.UfClp ?? 0;
        CachedUsdValue = cache?.UsdClp ?? 0;
        CachedUfPrompt = HasValidCache ? $"Cargar valor sugerido del Banco Central ($ {CachedUfValue:N0})" : "";

        RefreshHistorial(info);
    }

    private void RefreshHistorial(ProjectInfoRow info)
    {
        Historial.Clear();
        if (info.HistorialTasas == null) return;
        foreach (var entry in info.HistorialTasas.OrderByDescending(e => e.Fecha))
            Historial.Add(new CurrencyHistoryEntry(entry.Fecha, entry.UfClp, entry.UsdClp, entry.Activa));
    }

    public bool TryApplyChanges()
    {
        if (!CanEdit) return false;
        ValidateEdits();
        if (!string.IsNullOrWhiteSpace(ValidationError)) return false;

        var parsedUf = ParseDecimal(_editUfClp);
        var parsedUsd = ParseDecimal(_editUsdClp);
        var parsedFecha = ParseDate(_editFecha);

        if (parsedFecha == null) return false;

        // Validación de moneda: solo USD requiere ambas tasas configuradas
        var moneda = _svc.NormalizeCurrency(MonedaSeleccionada);
        if (moneda == CurrencyConversionService.CurrencyUsd && ((parsedUf == null || parsedUf <= 0) || (parsedUsd == null || parsedUsd <= 0)))
        {
            ValidationError = "Para usar USD debes indicar UF en CLP y dólar observado.";
            return false;
        }

        // Usar 0 si los campos están vacíos (UF permite vacío para moneda UF)
        decimal ufFinal = parsedUf ?? 0m;
        decimal usdFinal = parsedUsd ?? 0m;

        bool cambioTasa = ufFinal != _session.ProjectInfo.ValorUfClp
            || usdFinal != _session.ProjectInfo.TipoCambioUsdClp;

        if (cambioTasa && _session.ProjectInfo.ValorUfClp > 0)
        {
            if (!Infrastructure.WpfDialogService.Confirm("Reemplazar tasa vigente",
                    $"Esto reemplazará la tasa activa:\n  UF: $ {_session.ProjectInfo.ValorUfClp:N0}\n  USD: $ {_session.ProjectInfo.TipoCambioUsdClp:N0}\n\n¿Continuar?",
                    isWarning: true))
                return false;
        }

        var beforeEdit = _session.CreateSnapshot();

        var info = _session.ProjectInfo;
        info.MonedaPresupuesto = moneda;

        if (cambioTasa)
        {
            info.HistorialTasas ??= new();
            if (info.HistorialTasas.Count > 0)
                foreach (var h in info.HistorialTasas) h.Activa = false;

            info.HistorialTasas.Add(new TasaCambioHistorialRow
            {
                Fecha = parsedFecha.Value,
                UfClp = ufFinal,
                UsdClp = usdFinal,
                Activa = true
            });
        }

        info.ValorUfClp = ufFinal;
        info.TipoCambioUsdClp = usdFinal;
        info.FechaTipoCambio = parsedFecha.Value;

        _undo.PushSnapshot(beforeEdit, "Actualizar tasas de cambio");
        _session.MarkDirty();
        LoadFromSession();
        ShowSuccessFeedback = true;
        return true;
    }

    public bool TryDiscardChanges()
    {
        if (!CanEdit) return true;
        LoadFromSession();
        ShowSuccessFeedback = false;
        return true;
    }

    private void ValidateEdits()
    {
        var uf = ParseDecimal(_editUfClp);
        var usd = ParseDecimal(_editUsdClp);
        var fecha = ParseDate(_editFecha);

        if (_editUfClp.Length > 0 && uf == null)
        { ValidationError = "Valor UF inválido."; return; }
        if (uf <= 0 && _editUfClp.Length > 0)
        { ValidationError = "El valor UF debe ser mayor que cero."; return; }
        if (_editUsdClp.Length > 0 && usd == null)
        { ValidationError = "Valor USD inválido."; return; }
        if (usd <= 0 && _editUsdClp.Length > 0)
        { ValidationError = "El valor USD debe ser mayor que cero."; return; }
        if (_editFecha.Length > 0 && fecha == null)
        { ValidationError = "Fecha inválida. Use dd/MM/yyyy."; return; }
        ValidationError = "";
    }

    private static decimal? ParseDecimal(string s)
    {
        s = s.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static DateTime? ParseDate(string s)
    {
        if (DateTime.TryParseExact(s, "dd/MM/yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(s, out var d2)) return d2;
        return null;
    }
}

public sealed record CurrencyHistoryEntry(
    DateTime Fecha,
    decimal UfClp,
    decimal UsdClp,
    bool Activa)
{
    public string FechaDisplay => Fecha.ToString("dd/MM/yyyy");
    public string UfDisplay => $"$ {UfClp:N0}";
    public string UsdDisplay => $"$ {UsdClp:N0}";
    public string EstadoDisplay => Activa ? "Activa" : "Histórica";
}
