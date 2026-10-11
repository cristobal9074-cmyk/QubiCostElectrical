namespace QubiCostElectrical;

/// <summary>
/// Reglas de transición de estado para las órdenes de cambio.
/// Los estados válidos son: Borrador, En revisión, Aprobado, Rechazado.
///
/// Fuente de referencia: OrdenesCambioWorkspacePage (WinForms) usaba un ComboBox libre
/// sin máquina explícita. Las transiciones aquí definidas son más estrictas que WinForms
/// y se derivan de la semántica de negocio: una orden aprobada no puede revertirse.
///
/// Transiciones permitidas:
///   Borrador      → En revisión  (sin confirmación)
///   En revisión   → Aprobado     (con confirmación; bloquea edición posterior)
///   En revisión   → Rechazado    (con confirmación)
///   Rechazado     → Borrador     (reabrir, con confirmación)
/// </summary>
public static class ChangeOrderStateMachine
{
    public static bool IsTransitionAllowed(string from, string to) => (from, to) switch
    {
        ("Borrador", "En revisión") => true,
        ("En revisión", "Aprobado") => true,
        ("En revisión", "Rechazado") => true,
        ("Rechazado", "Borrador") => true,
        _ => false
    };

    /// <summary>
    /// Una orden aprobada está bloqueada: no puede editarse ni eliminarse.
    /// WinForms no tenía este bloqueo (ComboBox libre); WPF lo añade por seguridad.
    /// </summary>
    public static bool IsLocked(string estado) => string.Equals(estado, "Aprobado", StringComparison.OrdinalIgnoreCase);
}
