using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class MaterialMoRecalibrationServiceTests
{
    private sealed class FakeBank : IPriceBankStore
    {
        private readonly List<PriceBankEntry> entries;

        public FakeBank(params PriceBankEntry[] entries) => this.entries = entries.ToList();

        public IReadOnlyList<PriceBankEntry> GetAll() => entries.ToList();
        public PriceBankEntry? GetByCodigo(string codigo) => entries.FirstOrDefault(e => e.Codigo == codigo);
        public IReadOnlyList<PriceBankEntry> Search(string text) => GetAll();
        public void Upsert(PriceBankEntry entry) { }
        public bool Delete(string codigo) => false;
        public int DeleteAll() => 0;
        public int UpsertMany(IEnumerable<PriceBankEntry> nuevos) => 0;
        public int Count() => entries.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> seed) => 0;
    }

    private static MaterialRow MaterialPorcentaje(string codigo, decimal matClp, decimal moGuardada, decimal pct = 2.5m) => new()    {
        Codigo = codigo,
        MatClp = matClp,
        MoClp = moGuardada,
        MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
        MoPorcentajeMaterial = pct
    };

    [Fact]
    public void Recalibrar_ActualizaSoloLosMaterialesDeFormulaPorcentaje()
    {
        var porcentaje = MaterialPorcentaje("TAB-001", 100_000m, moGuardada: 1_570m);
        var directo = new MaterialRow { Codigo = "INT-01", MatClp = 585m, MoClp = 1_170m };
        var proyecto = new ProjectInfoRow { IndiceMoFa = 79.20m };

        var cambios = MaterialMoRecalibrationService.Recalibrar([porcentaje, directo], proyecto);

        // 100.000 × 2,5 % × 79,20 = 198.000
        Assert.Single(cambios);
        Assert.Equal("TAB-001", cambios[0].Codigo);
        Assert.Equal(1_570m, cambios[0].AnteriorClp);
        Assert.Equal(198_000m, cambios[0].NuevoClp);
        Assert.Equal(198_000m, porcentaje.MoClp);
        Assert.Equal(1_170m, directo.MoClp);
    }

    [Fact]
    public void Recalibrar_SinCambioDeIndice_NoReportaNada()
    {
        var material = MaterialPorcentaje("TAB-001", 100_000m, moGuardada: 157_000m);
        var proyecto = new ProjectInfoRow { IndiceMoFa = 62.80m };

        var cambios = MaterialMoRecalibrationService.Recalibrar([material], proyecto);

        Assert.Empty(cambios);
        Assert.Equal(157_000m, material.MoClp);
    }

    [Fact]
    public void Recalibrar_SinProyecto_UsaFactorNeutro()
    {
        var material = MaterialPorcentaje("TAB-001", 1_000m, moGuardada: 1_570m);

        MaterialMoRecalibrationService.Recalibrar([material], null);

        Assert.Equal(25m, material.MoClp); // sin proyecto: factor 1 → Material × %
    }

    [Fact]
    public void Recalibrar_ProyectoSinFaCalibrado_UsaFactorNeutro()
    {
        var material = MaterialPorcentaje("TAB-001", 100_000m, moGuardada: 157_000m);

        MaterialMoRecalibrationService.Recalibrar([material], new ProjectInfoRow());

        Assert.Equal(2_500m, material.MoClp);
    }

    [Fact]
    public void Recalibrar_ActualizaMaterialesDeFormulaFactor()
    {
        var material = new MaterialRow
        {
            Codigo = "H07Z1-K2.5",
            MoFactorBase = 20m,
            MoClp = 20m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaFactor
        };
        var proyecto = new ProjectInfoRow { IndiceMoFa = 62.80m };

        var cambios = MaterialMoRecalibrationService.Recalibrar([material], proyecto);

        Assert.Single(cambios);
        Assert.Equal("H07Z1-K2.5", cambios[0].Codigo);
        Assert.Equal(20m, cambios[0].AnteriorClp);
        Assert.Equal(1_256m, cambios[0].NuevoClp); // 20 × 62,80 = 1.256
        Assert.Equal(1_256m, material.MoClp);
        Assert.Equal(20m, material.MoFactorBase); // Preserva la base original
    }

    [Fact]
    public void SincronizarBanco_LlevaElNuevoSnapshotDeMoAlBanco()
    {
        var banco = new FakeBank(
            new PriceBankEntry { Codigo = "TAB-001", MatClp = 100_000m, MoClp = 1_570m },
            new PriceBankEntry { Codigo = "INT-01", MatClp = 585m, MoClp = 1_170m });
        var cambios = new[] { new MaterialMoRecalibrado("TAB-001", 1_570m, 198_000m) };

        int actualizados = MaterialMoRecalibrationService.SincronizarBanco(cambios, banco, valorUfClp: 0m);

        Assert.Equal(1, actualizados);
        Assert.Equal(198_000m, banco.GetByCodigo("TAB-001")!.MoClp);
        Assert.Equal(1_170m, banco.GetByCodigo("INT-01")!.MoClp);
    }

    [Fact]
    public void SincronizarBanco_SinCambiosONull_NoHaceNada()
    {
        var banco = new FakeBank(new PriceBankEntry { Codigo = "TAB-001", MoClp = 1_570m });

        Assert.Equal(0, MaterialMoRecalibrationService.SincronizarBanco([], banco, 0m));
        Assert.Equal(0, MaterialMoRecalibrationService.SincronizarBanco(null, banco, 0m));
        Assert.Equal(0, MaterialMoRecalibrationService.SincronizarBanco(
            new[] { new MaterialMoRecalibrado("TAB-001", 1_570m, 198_000m) }, null, 0m));
        Assert.Equal(1_570m, banco.GetByCodigo("TAB-001")!.MoClp);
    }
}
