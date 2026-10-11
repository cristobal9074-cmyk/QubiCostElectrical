using System.Collections.Generic;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

/// <summary>
/// Gestiona la pila histórica de navegación entre módulos (Atrás / Adelante),
/// permitiendo alternar de forma instantánea entre vistas visitadas (ej. Itemizado ↔ Cubicación ↔ APU).
/// </summary>
public sealed class ShellNavigationHistory
{
    private readonly Stack<ShellRoute> backStack = new();
    private readonly Stack<ShellRoute> forwardStack = new();
    private bool isNavigating;

    public bool CanNavigateBack => backStack.Count > 0;
    public bool CanNavigateForward => forwardStack.Count > 0;

    public int BackCount => backStack.Count;
    public int ForwardCount => forwardStack.Count;

    /// <summary>
    /// Registra un cambio de ruta si no fue originado por una navegación del propio historial.
    /// </summary>
    public void RecordNavigation(ShellRoute newRoute, ShellRoute? currentRoute)
    {
        if (isNavigating) return;

        if (currentRoute.HasValue && currentRoute.Value != newRoute)
        {
            backStack.Push(currentRoute.Value);
            forwardStack.Clear();
        }
    }

    /// <summary>
    /// Intenta navegar a la ruta previa en el historial.
    /// </summary>
    public bool TryNavigateBack(ShellRoute currentRoute, out ShellRoute targetRoute)
    {
        if (backStack.Count == 0)
        {
            targetRoute = default;
            return false;
        }

        isNavigating = true;
        try
        {
            targetRoute = backStack.Pop();
            forwardStack.Push(currentRoute);
            return true;
        }
        finally
        {
            isNavigating = false;
        }
    }

    /// <summary>
    /// Intenta navegar a la siguiente ruta en el historial hacia adelante.
    /// </summary>
    public bool TryNavigateForward(ShellRoute currentRoute, out ShellRoute targetRoute)
    {
        if (forwardStack.Count == 0)
        {
            targetRoute = default;
            return false;
        }

        isNavigating = true;
        try
        {
            targetRoute = forwardStack.Pop();
            backStack.Push(currentRoute);
            return true;
        }
        finally
        {
            isNavigating = false;
        }
    }

    /// <summary>
    /// Limpia el historial completo (ej. al cambiar de proyecto o reiniciar sesión).
    /// </summary>
    public void Clear()
    {
        backStack.Clear();
        forwardStack.Clear();
        isNavigating = false;
    }
}
