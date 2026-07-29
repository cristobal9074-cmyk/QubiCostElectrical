using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace QubiCostElectrical;

public enum ProjectSessionChangeKind
{
    Unknown,
    ProjectInfo,
    Itemizado,
    Materiales,
    Cubicacion,
    Plantillas,
    Apu,
    OrdenesCambio,
    Versiones,
    QtyHeaders,
    Zonas,
    Saved,
    Reset
}

public sealed class ProjectSessionChangedEventArgs : EventArgs
{
    public ProjectSessionChangedEventArgs(ProjectSessionChangeKind kind, string scope, string reason, bool isDirty)
    {
        Kind = kind;
        Scope = string.IsNullOrWhiteSpace(scope) ? "unknown" : scope.Trim();
        Reason = string.IsNullOrWhiteSpace(reason) ? Scope : reason.Trim();
        IsDirty = isDirty;
        TimestampUtc = DateTime.UtcNow;
    }

    public ProjectSessionChangeKind Kind { get; }
    public string Scope { get; }
    public string Reason { get; }
    public bool IsDirty { get; }
    public DateTime TimestampUtc { get; }
}

/// <summary>
/// Estado vivo del proyecto abierto.
///
/// Antes este estado viv�a directamente en MainForm como decenas de campos
/// privados. Mantenerlo aquí permite que MainForm act�e como shell de UI y que
/// Cubicación, Itemizado, Materiales, APU, Exportación y Dashboard puedan
/// consumir una �nica sesi�n sin duplicar datos ni depender de estado global de
/// controles WinForms.
///
/// Hist.: adem�s de centralizar datos, esta clase publica eventos de cambio por
/// dominio. As� los m�dulos pueden suscribirse a MaterialsChanged,
/// CubicacionChanged, ItemizadoChanged, etc., sin revisar strings ni acoplarse a
/// MainForm. MainForm mantiene propiedades de compatibilidad, pero el estado real
/// y sus invalidaciones viven aquí.
/// </summary>
public sealed partial class BudgetProjectSession
{
    private ProjectFileData data = new();

    public event EventHandler<ProjectSessionChangedEventArgs>? Changed;
    public event EventHandler<ProjectSessionChangedEventArgs>? ProjectChanged;
    public event EventHandler<ProjectSessionChangedEventArgs>? ItemizadoChanged;
    public event EventHandler<ProjectSessionChangedEventArgs>? MaterialsChanged;
    public event EventHandler<ProjectSessionChangedEventArgs>? CubicacionChanged;
    public event EventHandler<ProjectSessionChangedEventArgs>? ApuChanged;
    public event EventHandler<ProjectSessionChangedEventArgs>? VersionsChanged;
    public event EventHandler<ProjectSessionChangedEventArgs>? Saved;

    public ProjectFileData Data
    {
        get => data;
    }

    public string CurrentProjectPath { get; set; } = "";
    public bool IsReadOnly => lockReadOnly || roleReadOnly || IsActiveVersionReadOnly;
    
    private bool IsActiveVersionReadOnly
    {
        get
        {
            if (string.IsNullOrEmpty(ActiveVersionId)) return false;
            var activeVer = Versiones.FirstOrDefault(v => v.Id == ActiveVersionId);
            return activeVer != null && (activeVer.Estado == "Aprobada" || activeVer.Estado == "Obsoleta");
        }
    }

    public string ReadOnlyReason
    {
        get
        {
            if (IsActiveVersionReadOnly)
            {
                var activeVer = Versiones.FirstOrDefault(v => v.Id == ActiveVersionId);
                return $"La versión activa '{activeVer?.Codigo}' está en estado '{activeVer?.Estado}' (Solo lectura).";
            }
            if (lockReadOnly && roleReadOnly)
                return $"{roleReadOnlyReason} {lockReadOnlyReason}".Trim();
            if (roleReadOnly)
                return roleReadOnlyReason;
            if (lockReadOnly)
                return lockReadOnlyReason;
            return "";
        }
    }

    private bool lockReadOnly;
    private bool roleReadOnly;
    private bool bypassReadOnlyCheck;
    private string lockReadOnlyReason = "";
    private string roleReadOnlyReason = "";

    public void RunBypassingReadOnly(Action action)
    {
        bool old = bypassReadOnlyCheck;
        bypassReadOnlyCheck = true;
        try
        {
            action();
        }
        finally
        {
            bypassReadOnlyCheck = old;
        }
    }

    public string CurrentProjectName
    {
        get => ProjectInfo.Nombre;
        set => ProjectInfo.Nombre = string.IsNullOrWhiteSpace(value) ? "Proyecto sin nombre" : value;
    }

    public string CurrentClientName
    {
        get => ProjectInfo.Cliente;
        set => ProjectInfo.Cliente = string.IsNullOrWhiteSpace(value) ? "Cliente sin nombre" : value;
    }

    public string CurrentLocation
    {
        get => ProjectInfo.Ubicacion;
        set => ProjectInfo.Ubicacion = string.IsNullOrWhiteSpace(value) ? "Sin ubicación" : value;
    }

    public string CurrentUserName
    {
        get => ProjectInfo.UsuarioNombre;
        set => ProjectInfo.UsuarioNombre = string.IsNullOrWhiteSpace(value) ? Environment.UserName : value;
    }

    /// <summary>Email del usuario autenticado. Identificador inmutable para verificaciones de SoD.</summary>
    public string CurrentUserEmail { get; set; } = "";

    public string CurrentUserRole
    {
        get => ProjectInfo.UsuarioRol;
        set => ProjectInfo.UsuarioRol = string.IsNullOrWhiteSpace(value) ? "Usuario" : value;
    }

    public string CurrentBudgetStatus
    {
        get => ProjectInfo.EstadoPresupuesto;
        set => ProjectInfo.EstadoPresupuesto = string.IsNullOrWhiteSpace(value) ? "Borrador" : value;
    }

    public DateTime ProjectModifiedAt
    {
        get => ProjectInfo.FechaModificacion;
        set => ProjectInfo.FechaModificacion = value == default ? AppClock.Now : value;
    }

    public DateTime ProjectCreatedAt
    {
        get => ProjectInfo.FechaCreacion;
        set => ProjectInfo.FechaCreacion = value == default ? AppClock.Now : value;
    }

    public DateTime FechaTipoCambio
    {
        get => ProjectInfo.FechaTipoCambio;
        set => ProjectInfo.FechaTipoCambio = value;
    }

    public decimal ValorUfClp
    {
        get => ProjectInfo.ValorUfClp;
        set => ProjectInfo.ValorUfClp = value;
    }

    public string ProjectCurrency
    {
        get => ProjectInfo.MonedaPresupuesto;
        set => ProjectInfo.MonedaPresupuesto = string.IsNullOrWhiteSpace(value) ? "CLP" : value;
    }

    public decimal TipoCambioUsdClp
    {
        get => ProjectInfo.TipoCambioUsdClp;
        set => ProjectInfo.TipoCambioUsdClp = value;
    }

    public bool AutoActualizarIndicadores
    {
        get => ProjectInfo.AutoActualizarIndicadores;
        set => ProjectInfo.AutoActualizarIndicadores = value;
    }

    public decimal MetrosCuadrados
    {
        get => ProjectInfo.MetrosCuadrados;
        set => ProjectInfo.MetrosCuadrados = value;
    }

    public decimal GastosGeneralesPct
    {
        get => ProjectInfo.GastosGeneralesPct;
        set => ProjectInfo.GastosGeneralesPct = value;
    }

    public decimal UtilidadPct
    {
        get => ProjectInfo.UtilidadPct;
        set => ProjectInfo.UtilidadPct = value;
    }

    public decimal HerramientasPct
    {
        get => ProjectInfo.HerramientasPct;
        set => ProjectInfo.HerramientasPct = value;
    }

    public decimal MermasPct
    {
        get => ProjectInfo.MermasPct;
        set => ProjectInfo.MermasPct = value;
    }

    public decimal RiesgoPct
    {
        get => ProjectInfo.RiesgoPct;
        set => ProjectInfo.RiesgoPct = value;
    }

    public decimal DescuentoPct
    {
        get => ProjectInfo.DescuentoPct;
        set => ProjectInfo.DescuentoPct = value;
    }

    public decimal IvaPct
    {
        get => ProjectInfo.IvaPct;
        set => ProjectInfo.IvaPct = value;
    }

    public bool IncluirIvaEnTotal
    {
        get => ProjectInfo.IncluirIvaEnTotal;
        set => ProjectInfo.IncluirIvaEnTotal = value;
    }

    public BudgetApuMode ApuCostingMode
    {
        get => BudgetSettings.ParseApuMode(ProjectInfo.ApuCostingMode);
        set => ProjectInfo.ApuCostingMode = value.ToString();
    }

    public OfferPdfSettings OfferPdfSettings
    {
        get => ProjectInfo.OfertaPdf ??= new OfferPdfSettings();
        set => ProjectInfo.OfertaPdf = value ?? new OfferPdfSettings();
    }

    public bool IsDirty { get; private set; }
    public int StateVersion { get; private set; }

    public string? PendingItemizadoSelectionId { get; set; }
    public ItemizadoPendingAction PendingItemizadoAction { get; set; }
    public TakeoffNavigationContext? PendingTakeoffNavigation { get; set; }
    public string? PendingMaterialSelectionCode { get; set; }
    public string? PendingBuscadorTab { get; set; }

    // Hist.: dirty-tracking granular por m�dulo. Permite autoguardado inteligente,
    // invalidaci�n incremental de KPIs y cach�s de gr�ficos sin depender de strings sueltos
    // repartidos en MainForm.
    private readonly Dictionary<string, int> scopeVersions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> dirtyScopes = new(StringComparer.OrdinalIgnoreCase);
}
