using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

public enum ShellProductionStep
{
    Datos = 1,
    Itemizado = 2,
    Cubicacion = 3,
    Precios = 4,
    Revision = 5,
    Exportar = 6
}

public sealed record ShellWorkflowStepDefinition(
    string Title,
    string Detail,
    ShellRoute NavigateRoute,
    bool IsComplete,
    bool IsActive,
    bool IsAuxiliary,
    int StepNumber);

public static class ShellWorkflowLogic
{
    public static ShellProductionStep? ResolveProductionStep(ShellRoute route) => route switch
    {
        ShellRoute.Project or ShellRoute.Documents or ShellRoute.Currency => ShellProductionStep.Datos,
        ShellRoute.Itemized => ShellProductionStep.Itemizado,
        ShellRoute.Takeoff => ShellProductionStep.Cubicacion,
        ShellRoute.CubicacionSc => ShellProductionStep.Cubicacion,
        ShellRoute.Apu or ShellRoute.Templates or ShellRoute.Prices or ShellRoute.Quotes or ShellRoute.Purchases =>
            ShellProductionStep.Precios,
        ShellRoute.Review or ShellRoute.Versions or ShellRoute.ChangeOrders => ShellProductionStep.Revision,
        ShellRoute.OfferDocuments => ShellProductionStep.Exportar,
        _ => null
    };

    public static bool IsCubicacionScActive(ShellRoute route) => route == ShellRoute.CubicacionSc;

    public static IReadOnlyList<ShellWorkflowStepDefinition> BuildSteps(
        ShellRoute currentRoute,
        FinalReviewSummary review,
        ProjectInfoRow project,
        IReadOnlyList<ItemizadoRow> itemizado,
        IReadOnlyList<CuadroCargaSCRow> cubicacionSc)
    {
        int totalPartidas = itemizado.Count(x => !x.EsTitulo);
        bool hasName = !string.IsNullOrWhiteSpace(project.Nombre) && project.Nombre != "Proyecto sin nombre";
        bool s1 = hasName;
        bool s2 = totalPartidas > 0;
        bool s3 = review.EmptyPartidas == 0 && totalPartidas > 0;
        bool s4 = review.MissingApus == 0 && totalPartidas > 0;
        bool s5 = review.ReadyToExport;
        bool s6 = review.ReadyToExport;

        int scCircuitos = cubicacionSc.Sum(c => c.Circuitos?.Count ?? 0);
        bool scComplete = scCircuitos > 0;

        var activeStep = ResolveProductionStep(currentRoute);
        bool scActive = IsCubicacionScActive(currentRoute);
        bool cubicacionActive = activeStep == ShellProductionStep.Cubicacion && !scActive;

        var steps = new List<ShellWorkflowStepDefinition>
        {
            Step("Datos", ProjectLabel(project), ShellRoute.Project, s1, activeStep == ShellProductionStep.Datos, 1),
            Step("Itemizado", $"{totalPartidas:N0} partidas", ShellRoute.Itemized, s2, activeStep == ShellProductionStep.Itemizado, 2),
            Step("Cubicación", CubicacionDetail(review, totalPartidas), ShellRoute.Takeoff, s3, cubicacionActive, 3),
            Step("C.SC", scCircuitos > 0 ? $"{scCircuitos:N0} circuitos" : "Servicios comunes", ShellRoute.CubicacionSc,
                scComplete, scActive, isAuxiliary: true, stepNumber: 0),
            Step("Precios", PreciosDetail(review, totalPartidas), ShellRoute.Prices, s4, activeStep == ShellProductionStep.Precios, 4),
            Step("Revisión", review.ReadyToExport ? "Listo" : $"{review.BlockingCount} bloqueantes", ShellRoute.Review, s5,
                activeStep == ShellProductionStep.Revision, 5),
            Step("Exportar", s6 ? "Listo" : "Bloqueado", ShellRoute.OfferDocuments, s6, activeStep == ShellProductionStep.Exportar, 6)
        };

        return steps;
    }

    private static string ProjectLabel(ProjectInfoRow project)
    {
        if (string.IsNullOrWhiteSpace(project.Nombre) || project.Nombre == "Proyecto sin nombre")
            return "Completar datos";

        return project.Nombre.Length <= 28 ? project.Nombre : project.Nombre[..25] + "…";
    }

    private static string CubicacionDetail(FinalReviewSummary review, int totalPartidas)
    {
        if (totalPartidas == 0)
            return "Sin partidas";

        return $"{Math.Max(0, totalPartidas - review.EmptyPartidas):N0} cubiertas";
    }

    private static string PreciosDetail(FinalReviewSummary review, int totalPartidas)
    {
        if (totalPartidas == 0)
            return "Sin partidas";

        return $"{Math.Max(0, totalPartidas - review.MissingApus):N0} APUs";
    }

    private static ShellWorkflowStepDefinition Step(
        string title,
        string detail,
        ShellRoute route,
        bool complete,
        bool active,
        int stepNumber,
        bool isAuxiliary = false) =>
        new(title, detail, route, complete, active, isAuxiliary, stepNumber);
}
