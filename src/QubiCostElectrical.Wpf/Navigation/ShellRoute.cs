namespace QubiCostElectrical.Wpf.Navigation;

public enum ShellRoute
{
    Dashboard,
    Project,
    Documents,
    Itemized,
    Takeoff,
    CubicacionSc,
    Apu,
    Templates,
    Prices,
    Quotes,
    Purchases,
    Review,
    Versions,
    OfferDocuments,
    Currency,
    Preferences,
    ChangeOrders,
    FloorSummary,
    Solicitudes,
    Costo,
    Epp,
    ManoDeObra
}

public sealed record ShellRouteInfo(
    ShellRoute Route,
    string Group,
    string Title,
    string BreadcrumbProject = "");

public static class ShellRouteCatalog
{
  private static readonly IReadOnlyDictionary<ShellRoute, ShellRouteInfo> Routes =
      new Dictionary<ShellRoute, ShellRouteInfo>
      {
          [ShellRoute.Dashboard] = new(ShellRoute.Dashboard, "Proyecto", "Resumen"),
          [ShellRoute.Project] = new(ShellRoute.Project, "Proyecto", "Datos del proyecto"),
          [ShellRoute.Documents] = new(ShellRoute.Documents, "Proyecto", "Antecedentes y planos"),
          [ShellRoute.Itemized] = new(ShellRoute.Itemized, "Presupuesto", "Itemizado"),
          [ShellRoute.Takeoff] = new(ShellRoute.Takeoff, "Cubicación", "Cubicación"),
          [ShellRoute.CubicacionSc] = new(ShellRoute.CubicacionSc, "Cubicación", "Cubicación SC"),
          [ShellRoute.Apu] = new(ShellRoute.Apu, "Presupuesto", "APU"),
          [ShellRoute.Templates] = new(ShellRoute.Templates, "Más", "Plantillas"),
          [ShellRoute.Prices] = new(ShellRoute.Prices, "Precios", "Banco de precios"),
          [ShellRoute.Quotes] = new(ShellRoute.Quotes, "Precios", "Cotizaciones"),
          [ShellRoute.Purchases] = new(ShellRoute.Purchases, "Precios", "Lista de compra"),
          [ShellRoute.Review] = new(ShellRoute.Review, "Cierre", "Revisión final"),
          [ShellRoute.Versions] = new(ShellRoute.Versions, "Cierre", "Versiones"),
          [ShellRoute.OfferDocuments] = new(ShellRoute.OfferDocuments, "Cierre", "Oferta y exportación"),
          [ShellRoute.Currency] = new(ShellRoute.Currency, "Proyecto", "Monedas e indicadores"),
          [ShellRoute.Preferences] = new(ShellRoute.Preferences, "Configuración", "Preferencias globales"),
          [ShellRoute.ChangeOrders] = new(ShellRoute.ChangeOrders, "Cierre", "Órdenes de cambio"),
          [ShellRoute.FloorSummary] = new(ShellRoute.FloorSummary, "Más", "Resumen por piso"),
          [ShellRoute.Solicitudes] = new(ShellRoute.Solicitudes, "Precios", "Solicitudes"),
          [ShellRoute.Costo] = new(ShellRoute.Costo, "Costos", "Desglose de costos"),
          [ShellRoute.Epp] = new(ShellRoute.Epp, "Costos", "EPP"),
          [ShellRoute.ManoDeObra] = new(ShellRoute.ManoDeObra, "Costos", "Mano de obra")
};

  public static ShellRouteInfo Get(ShellRoute route) => Routes[route];
}
