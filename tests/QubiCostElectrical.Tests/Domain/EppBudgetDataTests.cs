using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class EppBudgetDataTests
{
    [Fact]
    public void Epp030_Quantities_MatchExcelEppSheet()
    {
        // Escenario base: 8 meses de obra, promedio 12 trabajadores directos, dotación indirecta 5.
        var epp = EppBudgetData.CreateSeed(mesesObra: 8, promedioDirecto: 12, dotacionIndirecta: 5);

        Assert.Equal(32m, epp.MoDirecta.SelectMany(s => s.Items).First().Cantidad);
        Assert.Equal(782720m, epp.TotalDirecta);
        Assert.Equal(370533m, epp.TotalIndirecta);
        Assert.Equal(465720m, epp.TotalOpcionales);
        Assert.Equal(1618973m, epp.GranTotal);
    }

    [Fact]
    public void ReadMoDrivers_UsesDurationAndRoundedAverageCrew()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { MoDuracionMeses = 8 },
            MoDirecta = new()
            {
                new MoDirectaRow { Dotacion = 6, DotacionMensual = new() { 6, 6, 6, 6, 6, 6, 6, 6 } },
                new MoDirectaRow { Dotacion = 6, DotacionMensual = new() { 6, 6, 6, 6, 6, 6, 6, 6 } }
            },
            MoIndirecta = new()
            {
                new MoIndirectaRow { Dotacion = 1 },
                new MoIndirectaRow { Dotacion = 1 },
                new MoIndirectaRow { Dotacion = 1 },
                new MoIndirectaRow { Dotacion = 1 },
                new MoIndirectaRow { Dotacion = 1 }
            }
        };

        var mo = EppBudgetData.ReadMoDrivers(data);

        Assert.Equal(8, mo.MesesObra);
        Assert.Equal(12, mo.PromedioDirecto);
        Assert.Equal(5, mo.DotacionIndirecta);
    }
}
