using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ProjectSnapshotIntegrityTests
{
    [Fact]
    public void Ensure_InitializesConjuntosAntecedentesAndApuCondiciones()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow(),
            Apus = [new ApuRow { Id = "A1" }],
            ConjuntosElectricos = [new ConjuntoElectricoRow { Id = "C1" }]
        };

        ProjectSnapshotIntegrity.Ensure(data);

        Assert.NotNull(data.Proyecto.AntecedentesTecnicos);
        Assert.NotNull(data.Proyecto.LaborDaySettings);
        Assert.NotNull(data.Apus[0].CondicionesLaborales);
        Assert.NotNull(data.Apus[0].Recursos);
        Assert.NotNull(data.ConjuntosElectricos[0].Recursos);
        Assert.NotNull(data.CubicacionSC);
        Assert.NotNull(data.CubicacionScConfig);
    }

    [Fact]
    public void Ensure_MigraMaterialesLegadosDeUfAClp()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { ValorUfClp = 39000m },
            Materiales = [new MaterialRow { Codigo = "A", MatUf = 0.1282m, MoUf = 0.0513m }]
        };

        ProjectSnapshotIntegrity.Ensure(data);

        var m = data.Materiales[0];
        Assert.Equal(5000m, m.MatClp);
        Assert.Equal(2001m, m.MoClp); // 0.0513 * 39000 = 2000.7 -> redondeo AwayFromZero
    }

    [Fact]
    public void Ensure_MaterialConClpYaCargado_NoLoPisa()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { ValorUfClp = 39000m },
            Materiales = [new MaterialRow { Codigo = "A", MatClp = 9999m, MatUf = 0.1282m }]
        };

        ProjectSnapshotIntegrity.Ensure(data);

        Assert.Equal(9999m, data.Materiales[0].MatClp); // no se sobreescribe
    }

    [Fact]
    public void Ensure_SinValorUfClp_NoMigraMateriales()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow { ValorUfClp = 0m },
            Materiales = [new MaterialRow { Codigo = "A", MatUf = 0.1282m }]
        };

        ProjectSnapshotIntegrity.Ensure(data);

        Assert.Equal(5000m, data.Materiales[0].MatClp); // Se migra usando el fallback de 39000
    }

    [Fact]
    public void Ensure_AgregaFasesEstandarFaltantes_AlCatalogoSC()
    {
        // Proyecto con un catálogo viejo: solo tenía fases con nombre largo, sin M/T/EM.
        var data = new ProjectFileData
        {
            CubicacionScConfig = new CubicacionScConfig
            {
                Sistemas = [new SistemaSC { Nombre = "Trifásico", NumConductores = 5m, MermaPct = 8m }]
            }
        };

        ProjectSnapshotIntegrity.Ensure(data);

        var t = data.CubicacionScConfig.Sistemas.Find(s => s.Nombre == "T");
        Assert.NotNull(t);
        Assert.Equal(5m, t!.NumConductores); // T estándar = 5 conductores
        Assert.Contains(data.CubicacionScConfig.Sistemas, s => s.Nombre == "M");
        Assert.Contains(data.CubicacionScConfig.Sistemas, s => s.Nombre == "EM");
        // No borra la fase antigua que ya tenía el usuario.
        Assert.Contains(data.CubicacionScConfig.Sistemas, s => s.Nombre == "Trifásico");
    }

    [Fact]
    public void FromProjectFileData_CopiesExtendedFields()
    {
        var source = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Test",
                AntecedentesTecnicos =
                [
                    new AntecedenteTecnicoRow { Id = "AT1", Codigo = "EL-01" }
                ]
            },
            ConjuntosElectricos =
            [
                new ConjuntoElectricoRow
                {
                    Id = "CE1",
                    Codigo = "PE-01",
                    Recursos = [new ConjuntoRecursoRow { Id = "R1", Codigo = "MAT001" }]
                }
            ],
            Apus =
            [
                new ApuRow
                {
                    Id = "A1",
                    CondicionesLaborales = new ApuCondicionesLaboralesRow { FactorAlturaPct = 12m }
                }
            ],
            CubicacionSC = [new CuadroCargaSCRow { Id = "SC1", Nombre = "TDA" }]
        };

        var version = BudgetVersionData.FromProjectFileData(source);

        Assert.Single(version.Proyecto.AntecedentesTecnicos);
        Assert.Equal("EL-01", version.Proyecto.AntecedentesTecnicos[0].Codigo);
        Assert.Single(version.ConjuntosElectricos);
        Assert.Equal("PE-01", version.ConjuntosElectricos[0].Codigo);
        Assert.Single(version.ConjuntosElectricos[0].Recursos);
        Assert.Equal(12m, version.Apus[0].CondicionesLaborales.FactorAlturaPct);
        Assert.Single(version.CubicacionSC);
    }
}
