using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class PriceBankApplicationServiceTests
{
    /// <summary>Repositorio en memoria que imita el upsert por código del store SQLite.</summary>
    internal sealed class FakePriceBankStore : IPriceBankStore
    {
        public readonly Dictionary<string, PriceBankEntry> Data = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<PriceBankEntry> GetAll() => Data.Values.ToList();
        public PriceBankEntry? GetByCodigo(string codigo) => Data.TryGetValue(codigo, out var e) ? e : null;
        public IReadOnlyList<PriceBankEntry> Search(string text) => Data.Values.Where(e => e.Codigo.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        public void Upsert(PriceBankEntry entry) => Data[entry.Codigo] = entry;
        public bool Delete(string codigo) => Data.Remove(codigo);
        public int DeleteAll() { int n = Data.Count; Data.Clear(); return n; }
        public int UpsertMany(IEnumerable<PriceBankEntry> entries)
        {
            int n = 0;
            foreach (var e in entries) { Data[e.Codigo] = e; n++; }
            return n;
        }
        public int Count() => Data.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> entries)
        {
            if (Data.Count > 0) return 0;
            return UpsertMany(entries);
        }
    }

    private static MaterialRow Mat(string codigo, decimal matUf, decimal moUf, DateTime fecha)
        => new() { Codigo = codigo, Descripcion = "d", Unidad = "UN", MatUf = matUf, MoUf = moUf, FechaPrecio = fecha };

    [Fact]
    public void ImportFromProject_ConsolidatesAndPersists()
    {
        var repo = new FakePriceBankStore();
        var svc = new PriceBankApplicationService(repo);

        var result = svc.ImportFromProject(new[]
        {
            Mat("A", 10m, 1m, new DateTime(2026, 1, 1)),
            Mat("a", 12m, 1m, new DateTime(2026, 6, 1)), // mismo código más nuevo
            Mat("B", 5m, 0m, new DateTime(2026, 1, 1))
        }, proyectoOrigen: "Edificio X");

        Assert.Equal(3, result.MaterialesProcesados);
        Assert.Equal(2, result.EntradasGuardadas); // A y B
        Assert.Equal(12m, repo.Data["A"].MatUf);   // gana el precio de junio
        Assert.Equal("Edificio X", repo.Data["A"].ProyectoOrigen);
    }

    [Fact]
    public void PreviewProjectUpdates_ListsOnlyNewerChangedPrices()
    {
        var repo = new FakePriceBankStore();
        repo.Upsert(new PriceBankEntry { Codigo = "A", MatUf = 20m, MoUf = 2m, FechaPrecio = new DateTime(2026, 6, 1) });
        var svc = new PriceBankApplicationService(repo);

        var project = new[] { Mat("A", 10m, 1m, new DateTime(2026, 1, 1)), Mat("B", 5m, 0m, DateTime.Today) };
        var updates = svc.PreviewProjectUpdates(project);

        var u = Assert.Single(updates);
        Assert.Equal("A", u.Codigo);
        Assert.Equal(20m, u.MatUfNuevo);
    }

    [Fact]
    public void ApplyBankPricesToProject_MutatesMaterialsInPlace()
    {
        var repo = new FakePriceBankStore();
        repo.Upsert(new PriceBankEntry { Codigo = "A", MatUf = 20m, MoUf = 2m, FechaPrecio = new DateTime(2026, 6, 1) });
        var svc = new PriceBankApplicationService(repo);

        var project = new List<MaterialRow> { Mat("A", 10m, 1m, new DateTime(2026, 1, 1)) };
        var result = svc.ApplyBankPricesToProject(project, new ProjectInfoRow(), valorUfProyecto: 39000m);

        Assert.Equal(PriceBankApplyStatus.Applied, result.Status);
        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(20m, project[0].MatUf);
        Assert.Equal(2m, project[0].MoUf);
        Assert.Equal("Banco de precios", project[0].OrigenPrecio);
        Assert.Equal(new DateTime(2026, 6, 1), project[0].FechaPrecio);
    }

    [Fact]
    public void ApplyBankPricesToProject_NoBankData_ChangesNothing()
    {
        var svc = new PriceBankApplicationService(new FakePriceBankStore());
        var project = new List<MaterialRow> { Mat("A", 10m, 1m, DateTime.Today) };
        var result = svc.ApplyBankPricesToProject(project, new ProjectInfoRow());

        Assert.Equal(PriceBankApplyStatus.NothingToApply, result.Status);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(10m, project[0].MatUf);
    }
}
