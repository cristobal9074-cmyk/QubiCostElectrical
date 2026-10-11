namespace QubiCostElectrical.Wpf.Navigation;



public enum NavVisibility

{

    Visible,

    Advanced,

    Hidden

}



/// <summary>

/// Política de visibilidad del shell.

/// Ciclo de vida completo (Presupuesto / Planificación / Ejecución) es Visible;

/// Hidden queda disponible para congelar rutas puntuales sin borrar Features.

/// </summary>

public static class NavVisibilityPolicy

{

    public static NavVisibility Get(ShellRoute route) => route switch

    {

        _ => NavVisibility.Visible

    };



    public static bool IsListed(ShellRoute route) => Get(route) == NavVisibility.Visible;



    public static bool IsNavigable(ShellRoute route) => Get(route) != NavVisibility.Hidden;

}


