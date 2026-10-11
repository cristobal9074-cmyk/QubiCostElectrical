using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class PrecioListaResumenVmTests
{
    private sealed class MockPriceBankStore : IPriceBankStore
    {
        private readonly List<PriceBankEntry> entries;

        public MockPriceBankStore(List<PriceBankEntry> entries) => this.entries = entries;

        public IReadOnlyList<PriceBankEntry> GetAll() => entries.ToList();
        public PriceBankEntry? GetByCodigo(string codigo) => entries.FirstOrDefault(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<PriceBankEntry> Search(string text) => entries.Where(e => (e.Codigo + e.Descripcion).Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        public void Upsert(PriceBankEntry entry)
        {
            var idx = entries.FindIndex(e => string.Equals(e.Codigo, entry.Codigo, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) entries[idx] = entry;
            else entries.Add(entry);
        }
        public bool Delete(string codigo) => entries.RemoveAll(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase)) > 0;
        public int DeleteAll() { var count = entries.Count; entries.Clear(); return count; }
        public int UpsertMany(IEnumerable<PriceBankEntry> newEntries) { foreach (var e in newEntries) Upsert(e); return newEntries.Count(); }
        public int Count() => entries.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> seed) => 0;
    }

    [Fact]
    public void PrecioListaMaterialVm_SinPrecio_CalculatesCorrectly()
    {
        var session = new BudgetProjectSession();
        var entrySinPrecio = new PriceBankEntry { Codigo = "MAT-01", Descripcion = "Material Sin Precio", MatClp = 0, MoClp = 0 };
        var vmSinPrecio = new PrecioListaMaterialVm(entrySinPrecio, session);

        Assert.True(vmSinPrecio.SinPrecio);
        Assert.Equal("Sin precio", vmSinPrecio.EstadoDisplay);
        Assert.Equal("#F59E0B", vmSinPrecio.EstadoDotBrush);

        // Actualizar precio
        vmSinPrecio.MatClp = 15000;
        Assert.False(vmSinPrecio.SinPrecio);
        Assert.Equal("Vigente", vmSinPrecio.EstadoDisplay);
        Assert.Equal("#10B981", vmSinPrecio.EstadoDotBrush);
    }

    [Fact]
    public void PrecioListaResumenVm_TotalCountsAndFilters_WorkAccurately()
    {
        var session = new BudgetProjectSession();
        var store = new MockPriceBankStore(new List<PriceBankEntry>
        {
            new() { Codigo = "MAT-01", Descripcion = "Interruptor 10A", MatClp = 2500, MoClp = 1000 },
            new() { Codigo = "MAT-02", Descripcion = "Enchufe Doble", MatClp = 3200, MoClp = 0 },
            new() { Codigo = "MAT-03", Descripcion = "Caja Derivación", MatClp = 0, MoClp = 0 },
        });

        var vm = new PrecioListaResumenVm(session, store);

        // Verify total metrics
        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(2, vm.ConPrecioCount);
        Assert.Equal(1, vm.SinPrecioCount);
        Assert.Equal(3, vm.FilteredCount);

        // Test Filter: Con precio
        vm.ActiveFilter = "Con precio";
        Assert.Equal(2, vm.FilteredCount);

        // Test Filter: Sin precio
        vm.ActiveFilter = "Sin precio";
        Assert.Equal(1, vm.FilteredCount);

        // Test Filter: Todos
        vm.ActiveFilter = "Todos";
        Assert.Equal(3, vm.FilteredCount);

        // Test Search
        vm.SearchText = "Enchufe";
        Assert.Equal(1, vm.FilteredCount);
    }
}
