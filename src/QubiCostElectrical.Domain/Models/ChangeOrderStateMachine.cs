namespace QubiCostElectrical;

/// <summary>
/// Reglas de transición de estado para las órdenes de cambio.
/// Flujo principal: Borrador → Solicitado → En Aprobación → Ejecutado.
/// Mantiene compatibilidad total con los estados heredados 'En revisión' y 'Aprobado'.
/// </summary>
public static class ChangeOrderStateMachine
{
    public static bool IsTransitionAllowed(string from, string to) => (from, to) switch
    {
        // Flujo horizontal secuencial: Borrador → Solicitado → En Aprobación → Ejecutado
        ("Borrador", "Solicitado") => true,
        ("Borrador", "En revisión") => true,
        ("Borrador", "En Aprobación") => true,

        ("Solicitado", "En Aprobación") => true,
        ("Solicitado", "En revisión") => true,
        ("Solicitado", "Aprobado") => true,
        ("Solicitado", "Ejecutado") => true,
        ("Solicitado", "Rechazado") => true,

        ("En revisión", "Aprobado") => true,
        ("En revisión", "Ejecutado") => true,
        ("En revisión", "Rechazado") => true,

        ("En Aprobación", "Aprobado") => true,
        ("En Aprobación", "Ejecutado") => true,
        ("En Aprobación", "Rechazado") => true,

        ("Aprobado", "Ejecutado") => true,
        ("Rechazado", "Borrador") => true,
        _ => false
    };

    /// <summary>
    /// Una orden aprobada o ejecutada está bloqueada: no puede editarse ni eliminarse.
    /// </summary>
    public static bool IsLocked(string estado) =>
        string.Equals(estado, "Aprobado", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(estado, "Ejecutado", StringComparison.OrdinalIgnoreCase);
}
