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
/// estado vivo del proyecto abierto.
///
/// Antes este estado vivía directamente en MainForm como decenas de campos
/// privados. Mantenerlo aquí permite que MainForm actúe como shell de UI y que
/// Cubicación, Itemizado, Materiales, APU, Exportación y Dashboard puedan
/// consumir una única sesión sin duplicar datos ni depender de estado global de
/// controles WinFormás.
///
/// Hist.: además de centralizar datos, esta clase publica eventos de cambio por
/// dominio. Así los módulos pueden suscribirse a MaterialsChanged,
/// CubicacionChanged, ItemizadoChanged, etc., sin revisar strings ni acoplarse a
/// MainForm. MainForm mantiene propiedades de compatibilidad, pero el estado AREAl
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
    public bool IsReadOnly => accessGate.IsReadOnly;
    public string ReadOnlyReason => accessGate.ReadOnlyReason;

    private readonly ProjectAccessGate accessGate = new();
    private readonly ProjectDirtyTracker dirtyTracker = new();
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

    public bool IsDirty
    {
        get => dirtyTracker.IsDirty;
        private set
        {
            if (value)
                dirtyTracker.MarkDirtyFlag();
            else
                dirtyTracker.MarkSaved();
        }
    }
    public int StateVersion { get; private set; }

    public string? PendingItemizadoSelectionId { get; set; }
    public ItemizadoPendingAction PendingItemizadoAction { get; set; }
    public TakeoffNavigationContext? PendingTakeoffNavigation { get; set; }
    public string? PendingMaterialSelectionCode { get; set; }
    public string? PendingBuscadorTab { get; set; }

    private List<string>? pendingTakeoffQueue;

    /// <summary>IDs de partidas pendientes de cubicación secuencial (cola FIFO).</summary>
    public int PendingTakeoffQueueCount => pendingTakeoffQueue?.Count ?? 0;

    public void SetPendingTakeoffQueue(List<string>? queue)
    {
        pendingTakeoffQueue = queue is { Count: > 0 } ? queue : null;
    }

    /// <summary>Extrae el siguiente ID de la cola y lo elimina. Devuelve null si la cola está vacía.</summary>
    public string? TryDequeueNextTakeoff()
    {
        if (pendingTakeoffQueue == null || pendingTakeoffQueue.Count == 0)
        {
            pendingTakeoffQueue = null;
            return null;
        }
        var next = pendingTakeoffQueue[0];
        pendingTakeoffQueue.RemoveAt(0);
        if (pendingTakeoffQueue.Count == 0)
            pendingTakeoffQueue = null;
        return next;
    }

    // Hist.: dirty-tracking granular por módulo vive en ProjectDirtyTracker.
}
