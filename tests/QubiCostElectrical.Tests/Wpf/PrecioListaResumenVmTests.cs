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

    // ── Alta rápida: código automático, familias y M.O. por fórmula ─────────

    [Fact]
    public void SugerirCodigo_YaExisteEnElBanco_DevuelveElSiguiente()
    {
        var session = new BudgetProjectSession();
        var store = new MockPriceBankStore(new List<PriceBankEntry>
        {
            new() { Codigo = "TAB-001", Descripcion = "Tablero 6 ctos", Categoria = "Tableros" },
        });
        var vm = new PrecioListaResumenVm(session, store);

        Assert.Equal("TAB-002", vm.SugerirCodigo("Tableros", ""));
    }

    [Fact]
    public void SugerirCodigo_ConsideraTambienLosMaterialesDelProyecto()
    {
        var session = new BudgetProjectSession();
        session.Materiales.Add(new MaterialRow { Codigo = "TAB-005", Categoria = "Tableros" });
        var vm = new PrecioListaResumenVm(session, new MockPriceBankStore([]));

        Assert.Equal("TAB-006", vm.SugerirCodigo("Tableros", ""));
    }

    [Fact]
    public void CalcularMoPorcentaje_UsaElFaDelProyecto()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.IndiceMoFa = 50m;
        var vm = new PrecioListaResumenVm(session, new MockPriceBankStore([]));

        // 1.000 × 2,5% × 50 = 1.250
        Assert.Equal(1250m, vm.CalcularMoPorcentaje(1000m, 2.5m));
        Assert.Equal(50m, vm.IndiceMoFa);
    }

    [Fact]
    public void FamiliasDisponibles_UneBancoYMateriales_SinRepetir()
    {
        var session = new BudgetProjectSession();
        session.Materiales.Add(new MaterialRow { Codigo = "L1", Categoria = "Luminarias" });
        var store = new MockPriceBankStore(new List<PriceBankEntry>
        {
            new() { Codigo = "TAB-001", Categoria = "Tableros" },
            new() { Codigo = "TAB-002", Categoria = "tableros" }, // duplicado sin distinguir mayúsculas
            new() { Codigo = "CON-001", Categoria = "Conductores" },
            new() { Codigo = "LUM-001", Categoria = "Luminarias" },
        });
        var vm = new PrecioListaResumenVm(session, store);

        Assert.Contains("Conductores", vm.FamiliasDisponibles);
        Assert.Contains("Luminarias", vm.FamiliasDisponibles);
        Assert.Contains("Tableros", vm.FamiliasDisponibles);
        Assert.Contains("Transformadores", vm.FamiliasDisponibles);
        Assert.Contains("Grupo electrógeno", vm.FamiliasDisponibles);
        Assert.Contains("Ducto barra", vm.FamiliasDisponibles);
        Assert.Contains("Media tensión", vm.FamiliasDisponibles);
        Assert.Equal(1, vm.FamiliasDisponibles.Count(f => string.Equals(f, "Tableros", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AgregarMaterial_ConFormulaPorcentaje_GuardaFormulaYMoCalculada()
    {
        var session = new BudgetProjectSession();
        var entry = new PriceBankEntry { Codigo = "EXISTE-01" };
        var store = new MockPriceBankStore(new List<PriceBankEntry> { entry });
        var vm = new PrecioListaResumenVm(session, store);

        vm.AgregarMaterial("TAB-001", "Tablero eléctrico 12 ctos", "un", "Tableros", 100000m, 0m, "", DateTime.Today,
            MaterialMoResolver.MoFormulaPorcentaje, 2.5m);

        // El material es del proyecto: no se escribe en el banco de precios global.
        Assert.Null(store.GetByCodigo("TAB-001"));

        var material = session.Materiales.Single(m => m.Codigo == "TAB-001");
        Assert.Equal(MaterialMoResolver.MoFormulaPorcentaje, material.MoFormulaTipo);
        Assert.Equal(2.5m, material.MoPorcentajeMaterial);
        // Proyecto sin FA calibrado: 100.000 × 2,5% × 1 = 2.500 (factor neutro). Al mover el FA se recalcula.
        Assert.Equal(2_500m, material.MoClp);
        Assert.Equal("Tableros", material.Categoria);

        // Y queda visible en la lista (una VM nueva la reconstruye desde el catálogo del proyecto).
        Assert.Contains(vm.Items!.Cast<PrecioListaMaterialVm>(), i => i.Codigo == "TAB-001");
        var reabierta = new PrecioListaResumenVm(session, store);
        Assert.Contains(reabierta.Items!.Cast<PrecioListaMaterialVm>(), i => i.Codigo == "TAB-001");
    }

    /// <summary>Con el FA ya calibrado en el proyecto, el alta rápida guarda la M.O. de la fórmula.</summary>
    [Fact]
    public void AgregarMaterial_ConFaCalibrado_GuardaLaMoDeLaFormula()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.IndiceMoFa = 62.80m;
        var entry = new PriceBankEntry { Codigo = "EXISTE-01" };
        var store = new MockPriceBankStore(new List<PriceBankEntry> { entry });
        var vm = new PrecioListaResumenVm(session, store);

        vm.AgregarMaterial("TAB-002", "Tablero eléctrico 24 ctos", "un", "Tableros", 100000m, 0m, "", DateTime.Today,
            MaterialMoResolver.MoFormulaPorcentaje, 2.5m);

        // 100.000 × 2,5% × 62,80 = 157.000 — y sin tocar el banco de precios global.
        Assert.Equal(157000m, session.Materiales.Single(m => m.Codigo == "TAB-002").MoClp);
        Assert.Null(store.GetByCodigo("TAB-002"));
    }

    [Fact]
    public void AgregarMaterial_SinFormula_ConservaLaMoDirecta()
    {
        var session = new BudgetProjectSession();
        var store = new MockPriceBankStore(new List<PriceBankEntry>());
        var vm = new PrecioListaResumenVm(session, store);

        vm.AgregarMaterial("MAT-01", "Interruptor 10A", "un", "Protecciones", 2500m, 1000m, "", DateTime.Today);

        var material = session.Materiales.Single(m => m.Codigo == "MAT-01");
        Assert.Equal("", material.MoFormulaTipo);
        Assert.Equal(1000m, material.MoClp);
        Assert.Null(store.GetByCodigo("MAT-01"));
    }

    // ── Alta rápida desde la lista de precios (Alt+V → Ctrl++) ─────────────────────
    // El material queda SOLO en el catálogo del proyecto (no en el banco global, compartido entre
    // proyectos); la familia nueva se crea como categoría y la casilla de fórmula decide si lleva
    // M.O. Material × % × FA.

    [Fact]
    public void AgregarMaterial_FamiliaNueva_QuedaComoCategoria()
    {
        var session = new BudgetProjectSession();
        var store = new MockPriceBankStore(new List<PriceBankEntry>());
        var vm = new PrecioListaResumenVm(session, store);

        vm.AgregarMaterial("TAB-001", "Tablero general", "un", "Tableros nuevos", 250000m, 0m, "", DateTime.Today,
            MaterialMoResolver.MoFormulaPorcentaje, 2.5m);

        Assert.Equal("Tableros nuevos", session.Materiales.Single(m => m.Codigo == "TAB-001").Categoria);
        Assert.Contains("Tableros nuevos", vm.FamiliasDisponibles);
        Assert.Empty(store.GetAll());
    }

    /// <summary>La casilla de fórmula desmarcada guarda el material sin M.O. (solo material).</summary>
    [Fact]
    public void AgregarMaterial_SinUsarFormula_NoGuardaMoNiFormula()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.IndiceMoFa = 62.80m;
        var store = new MockPriceBankStore(new List<PriceBankEntry>());
        var vm = new PrecioListaResumenVm(session, store);

        vm.AgregarMaterial("ART-001", "Enchufe doble", "un", "Artefactos", 3200m, 0m, "", DateTime.Today, "", 0m);

        var material = session.Materiales.Single(m => m.Codigo == "ART-001");
        Assert.Equal("", material.MoFormulaTipo);
        Assert.Equal(0m, material.MoClp);
        Assert.Empty(store.GetAll());
    }

    /// <summary>Un código que ya existe en el banco o en el proyecto no se duplica.</summary>
    [Fact]
    public void AgregarMaterial_CodigoDuplicado_NoCreaNada()
    {
        var session = new BudgetProjectSession();
        var store = new MockPriceBankStore(new List<PriceBankEntry>
        {
            new() { Codigo = "MAT-01", Descripcion = "Interruptor 10A" },
        });
        var vm = new PrecioListaResumenVm(session, store);

        Assert.False(vm.AgregarMaterial("MAT-01", "Otro interruptor", "un", "Protecciones", 2500m, 0m, "", DateTime.Today));
        Assert.False(vm.AgregarMaterial("MAT-01", "Otro interruptor", "un", "Protecciones", 2500m, 0m, "", DateTime.Today));
        Assert.Empty(session.Materiales);
    }

    /// <summary>El código lo sugiere el alta rápida: familia nueva incluida, sin colisionar con el banco.</summary>
    [Fact]
    public void SugerirCodigo_FamiliaNueva_UsaSuPrefijoYElSiguienteCorrelativo()
    {
        var session = new BudgetProjectSession();
        var store = new MockPriceBankStore(new List<PriceBankEntry>
        {
            new() { Codigo = "LUM-001", Descripcion = "Luminaria existente", Categoria = "Luminarias" },
        });
        var vm = new PrecioListaResumenVm(session, store);

        Assert.Equal("LUM-002", vm.SugerirCodigo("Luminarias", "Luminaria LED 20W"));
    }
}
