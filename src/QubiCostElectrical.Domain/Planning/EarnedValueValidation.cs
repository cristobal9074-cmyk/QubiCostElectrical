using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class EarnedValueValidation
{
    public static IReadOnlyList<EarnedValueValidationIssue> Validate(EarnedValueInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var issues = new List<EarnedValueValidationIssue>();

        if (!input.HasStatusDate)
            issues.Add(Error("NO_STATUS_DATE", "El proyecto no tiene fecha de estado."));
        if (!input.HasBaseline)
            issues.Add(Error("NO_BASELINE", "El proyecto no tiene baseline para EVM."));
        if (!input.HasBudget || input.Bac <= 0m)
            issues.Add(Error("NO_BUDGET", "El proyecto no tiene presupuesto (BAC) válido."));
        if (input.Bac == 0m)
            issues.Add(Error("BAC_ZERO", "BAC no puede ser cero."));

        if (input.Ac < 0m) issues.Add(Error("AC_NEG", "AC no puede ser negativo."));
        if (input.Ev < 0m) issues.Add(Error("EV_NEG", "EV no puede ser negativo."));
        if (input.Pv < 0m) issues.Add(Error("PV_NEG", "PV no puede ser negativo."));

        if (!input.HasProgress && input.Ev == 0m && input.Ac == 0m && input.PercentEarned == 0m)
            issues.Add(Warn("NO_PROGRESS", "El proyecto no tiene avance registrado."));

        if (input.IsProjectFinished)
            issues.Add(Warn("FINISHED", "El proyecto está finalizado; los índices futuros tienen valor limitado."));
        if (input.IsProjectCancelled)
            issues.Add(Error("CANCELLED", "El proyecto está cancelado; EVM no aplica operativamente."));
        if (input.IsProjectSuspended)
            issues.Add(Warn("SUSPENDED", "El proyecto está suspendido."));

        if (input.Pv == 0m && input.Ev > 0m)
            issues.Add(Warn("SPI_UNDEF", "PV es cero; SPI no se puede calcular (evita infinito)."));
        if (input.Ac == 0m && input.Ev > 0m)
            issues.Add(Warn("CPI_UNDEF", "AC es cero; CPI no se puede calcular (evita infinito)."));

        return issues;
    }

    public static bool HasErrors(IReadOnlyList<EarnedValueValidationIssue> issues)
    {
        foreach (var i in issues)
            if (i.IsError) return true;
        return false;
    }

    private static EarnedValueValidationIssue Error(string code, string message) =>
        new() { Code = code, Message = message, IsError = true };

    private static EarnedValueValidationIssue Warn(string code, string message) =>
        new() { Code = code, Message = message, IsError = false };
}
