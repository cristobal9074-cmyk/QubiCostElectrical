using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Aplicar el FA es el equivalente en la app a reescribir COSTOS!E192 en la planilla original:
/// el índice, la M.O. guardada del catálogo, el snapshot del banco y las filas de cubicación
/// tienen que quedar con la misma cifra en un solo movimiento.
/// </summary>
public sealed class AplicarIndiceMoFaTests
{
    private sealed class FakeBank : IPriceBankStore
    {
        private readonly List<PriceBankEntry> entries;

        public FakeBank(params PriceBankEntry[] entries) => this.entries = entries.ToList();

        public IReadOnlyList<PriceBankEntry> GetAll() => entries.ToList();
        public PriceBankEntry? GetByCodigo(string codigo) => entries.FirstOrDefault(e => e.Codigo == codigo);
        public IReadOnlyList<PriceBankEntry> Search(string text) => GetAll();
        public void Upsert(PriceBankEntry entry)
        {
            entries.RemoveAll(e => string.Equals(e.Codigo, entry.Codigo, StringComparison.OrdinalIgnoreCase));
            entries.Add(entry);
        }
        public bool Delete(string codigo) => false;
        public int DeleteAll() => 0;
        public int UpsertMany(IEnumerable<PriceBankEntry> nuevos) => 0;
        public int Count() => entries.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> seed) => 0;
    }

    private static BudgetProjectSession SesionConMaterialPorcentaje(decimal indiceInicial = 62.80m)
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.IndiceMoFa = indiceInicial;
        session.Materiales.Add(new MaterialRow
        {
            Codigo = "TAB-001",
            Descripcion = "Tablero",
            MatClp = 100_000m,
            MoClp = 157_000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "c1",
            ItemId = "it1",
            Codigo = "TAB-001",
            Cantidad = 2m,
            MatClp = 100_000m,
            MoClp = 157_000m
        });
        return session;
    }

    [Fact]
    public void AplicarIndiceMoFa_DejaElIndiceElCatalogoYLaCubicacionConLaMismaMo()
    {
        var session = SesionConMaterialPorcentaje();
        var banco = new FakeBank(new PriceBankEntry { Codigo = "TAB-001", MatClp = 100_000m, MoClp = 157_000m });

        var aplicado = session.AplicarIndiceMoFa(79.20m, banco);

        Assert.Equal(62.80m, aplicado.IndiceAnterior);
        Assert.Equal(79.20m, aplicado.IndiceMoFa);
        Assert.Equal(79.20m, session.ProjectInfo.IndiceMoFa);

        // 100.000 × 2,5 % × 79,20 = 198.000 en las tres capas.
        Assert.Equal(1, aplicado.MaterialesRecalibrados);
        Assert.Equal(198_000m, session.Materiales[0].MoClp);
        Assert.Equal(198_000m, banco.GetByCodigo("TAB-001")!.MoClp);
        Assert.Equal(198_000m, session.Cubicacion[0].MoClp);
        Assert.True(aplicado.CubicacionSincronizada >= 0);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void AplicarIndiceMoFa_ConElMismoIndice_NoReportaCambios()
    {
        var session = SesionConMaterialPorcentaje(indiceInicial: 62.80m);

        var aplicado = session.AplicarIndiceMoFa(62.80m);

        Assert.Empty(aplicado.Materiales);
        Assert.Equal(62.80m, session.ProjectInfo.IndiceMoFa);
        Assert.Equal(157_000m, session.Materiales[0].MoClp);
    }

    [Fact]
    public void AplicarIndiceMoFa_IndiceNegativo_NoTocaElProyecto()
    {
        var session = SesionConMaterialPorcentaje();

        var aplicado = session.AplicarIndiceMoFa(-1m);

        Assert.Empty(aplicado.Materiales);
        Assert.Equal(62.80m, session.ProjectInfo.IndiceMoFa);
        Assert.Equal(157_000m, session.Materiales[0].MoClp);
    }

    /// <summary>
    /// FA 0 = "sin calibrar": es un valor válido que deja la M.O. por % del catálogo y del itemizado
    /// en Material × % (factor 1), no en $0, hasta que el plan de obra permita derivar el índice.
    /// </summary>
    [Fact]
    public void AplicarIndiceMoFa_FaCero_UsaFactorNeutroEnLaMoPorcentaje()
    {
        var session = SesionConMaterialPorcentaje();

        var aplicado = session.AplicarIndiceMoFa(0m);

        Assert.Equal(0m, aplicado.IndiceMoFa);
        Assert.Equal(0m, session.ProjectInfo.IndiceMoFa);
        Assert.Equal(1, aplicado.MaterialesRecalibrados);
        // 100.000 × 2,5 % = 2.500 en el catálogo y en la cubicación.
        Assert.Equal(2_500m, session.Materiales[0].MoClp);
        Assert.Equal(2_500m, session.Cubicacion[0].MoClp);
    }

    [Fact]
    public void AplicarIndiceMoFa_NoRompeLaMoDirectaDelCatalogo()
    {
        var session = SesionConMaterialPorcentaje();
        session.Materiales.Add(new MaterialRow { Codigo = "INT-01", MatClp = 585m, MoClp = 1_170m });

        session.AplicarIndiceMoFa(79.20m);

        Assert.Equal(1_170m, session.Materiales[1].MoClp);
    }
}
