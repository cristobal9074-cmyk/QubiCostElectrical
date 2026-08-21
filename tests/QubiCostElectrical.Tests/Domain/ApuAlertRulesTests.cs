using System;
using System.Collections.Generic;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Antes de extraer ApuAlertRules, estas reglas vivían hardcodeadas en
/// Features/Apu/ApuWorkspaceVm.cs y no se podían testear sin instanciar la pantalla completa.
/// </summary>
public sealed class ApuAlertRulesTests
{
    private static ApuRow MakeApu(List<ApuRecursoRow>? recursos = null, DateTime? fechaPrecio = null) => new()
    {
        Recursos = recursos ?? new List<ApuRecursoRow>
        {
            new() { PrecioUf = 1m, Rendimiento = 1m }
        },
        FechaPrecio = fechaPrecio ?? DateTime.Today
    };

    [Fact]
    public void Build_ReturnsNoAlerts_WhenApuIsHealthy()
    {
        var apu = MakeApu();

        var alerts = ApuAlertRules.Build(apu, DateTime.Today);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Build_FlagsApuWithNoRecursos()
    {
        var apu = MakeApu(recursos: new List<ApuRecursoRow>());

        var alerts = ApuAlertRules.Build(apu, DateTime.Today);

        Assert.Contains("APU sin recursos.", alerts);
    }

    [Fact]
    public void Build_FlagsRecursoWithoutPrice()
    {
        var apu = MakeApu(recursos: new List<ApuRecursoRow> { new() { PrecioUf = 0m, Rendimiento = 1m } });

        var alerts = ApuAlertRules.Build(apu, DateTime.Today);

        Assert.Contains("Hay recursos sin precio.", alerts);
    }

    [Fact]
    public void Build_FlagsRecursoWithoutRendimiento()
    {
        var apu = MakeApu(recursos: new List<ApuRecursoRow> { new() { PrecioUf = 1m, Rendimiento = 0m } });

        var alerts = ApuAlertRules.Build(apu, DateTime.Today);

        Assert.Contains("Hay recursos sin rendimiento.", alerts);
    }

    [Fact]
    public void Build_DoesNotFlagStalePrice_AtExactlyTheThreshold()
    {
        var today = new DateTime(2026, 1, 1);
        var apu = MakeApu(fechaPrecio: today.AddDays(-ApuAlertRules.PrecioAntiguoWarningDays));

        var alerts = ApuAlertRules.Build(apu, today);

        Assert.DoesNotContain(alerts, a => a.Contains("antigüedad"));
    }

    [Fact]
    public void Build_FlagsStalePrice_OneDayPastThreshold()
    {
        var today = new DateTime(2026, 1, 1);
        var apu = MakeApu(fechaPrecio: today.AddDays(-(ApuAlertRules.PrecioAntiguoWarningDays + 1)));

        var alerts = ApuAlertRules.Build(apu, today);

        Assert.Contains(alerts, a => a.Contains("antigüedad"));
    }
}
