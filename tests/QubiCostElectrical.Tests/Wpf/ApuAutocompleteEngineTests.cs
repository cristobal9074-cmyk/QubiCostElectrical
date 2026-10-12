using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ApuAutocompleteEngineTests
{
    private sealed class FakePriceBankStore : IPriceBankStore
    {
        private readonly List<PriceBankEntry> entries;

        public FakePriceBankStore(List<PriceBankEntry> entries)
        {
            this.entries = entries;
        }

        public IReadOnlyList<PriceBankEntry> GetAll() => entries;
        public PriceBankEntry? GetByCodigo(string codigo) => entries.Find(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<PriceBankEntry> Search(string text)
        {
            var tokens = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return entries.FindAll(e =>
            {
                var combined = (e.Codigo + " " + e.Descripcion).ToLowerInvariant();
                return tokens.All(t => combined.Contains(t));
            });
        }
        public void Upsert(PriceBankEntry entry) => entries.Add(entry);
        public int UpsertMany(IEnumerable<PriceBankEntry> newEntries) { int initial = entries.Count; entries.AddRange(newEntries); return entries.Count - initial; }
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> seedEntries) { if (entries.Count == 0) { entries.AddRange(seedEntries); return entries.Count; } return 0; }
        public bool Delete(string codigo) => entries.RemoveAll(e => e.Codigo == codigo) > 0;
        public int DeleteAll() { int c = entries.Count; entries.Clear(); return c; }
        public int Count() => entries.Count;
        public void Reorder(IReadOnlyList<string> orderedCodigos) { }
        public bool Exists(string codigo) => entries.Exists(e => e.Codigo == codigo);
    }

    [Fact]
    public async Task SearchAsync_WhenQueryLessThanTwoChars_ReturnsEmpty()
    {
        var session = new BudgetProjectSession();
        var searchService = new MaterialSearchService();
        var engine = new ApuAutocompleteEngine(session, searchService);

        var result = await engine.SearchAsync("a");

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_FindsMatchingMaterialInSession()
    {
        var session = new BudgetProjectSession();
        session.ValorUfClp = 38000m;
        session.Materiales.Add(new MaterialRow
        {
            Codigo = "CBL-LSHF-2.5",
            Descripcion = "Cable LSHF 2.5 mm2 Libre de Halogenos",
            Unidad = "m",
            MatClp = 1250m,
            Proveedor = "Covisa"
        });

        var searchService = new MaterialSearchService();
        var engine = new ApuAutocompleteEngine(session, searchService);

        var results = await engine.SearchAsync("lshf 2.5");

        Assert.NotEmpty(results);
        var first = results[0];
        Assert.Equal("CBL-LSHF-2.5", first.Codigo);
        Assert.Equal("m", first.Unidad);
        Assert.Equal(1250m, first.MatClp);
        Assert.True(first.MatUf > 0m);
        Assert.Equal("Covisa", first.Proveedor);
    }

    [Fact]
    public async Task SearchAsync_FindsMatchingMaterialInPriceBank()
    {
        var session = new BudgetProjectSession();
        session.ValorUfClp = 38000m;

        var bankEntries = new List<PriceBankEntry>
        {
            new()
            {
                Codigo = "CON-20",
                Descripcion = "Tuberia Conduit PVC 20mm Clase I",
                Unidad = "tira",
                MatClp = 3400m,
                Proveedor = "Tigre",
                Marca = "Tigre"
            },
            new()
            {
                Codigo = "DIS-1X16",
                Descripcion = "Disyuntor magnetotermico 1x16A Curva C",
                Unidad = "un",
                MatClp = 4500m,
                Proveedor = "Schneider"
            }
        };

        var fakeBank = new FakePriceBankStore(bankEntries);
        var searchService = new MaterialSearchService();
        var engine = new ApuAutocompleteEngine(session, searchService, fakeBank);

        var conduitResults = await engine.SearchAsync("conduit 20");
        Assert.NotEmpty(conduitResults);
        Assert.Equal("CON-20", conduitResults[0].Codigo);

        var disyuntorResults = await engine.SearchAsync("disyuntor 1x16");
        Assert.NotEmpty(disyuntorResults);
        Assert.Equal("DIS-1X16", disyuntorResults[0].Codigo);
        Assert.Equal("un", disyuntorResults[0].Unidad);
    }

    [Fact]
    public async Task SearchAsync_ApuResourceSuggestion_ExposesClpPriceAndDerivedUf()
    {
        var session = new BudgetProjectSession();
        session.ValorUfClp = 38000m;
        session.Data.Apus = new List<ApuRow>
        {
            new()
            {
                Descripcion = "APU de prueba",
                Unidad = "un",
                Recursos = new List<ApuRecursoRow>
                {
                    new()
                    {
                        Tipo = ApuTipoRecurso.MAT,
                        CodigoMaterial = "CBL-APU-16",
                        Descripcion = "Cable APU 16 mm2",
                        Unidad = "m",
                        Rendimiento = 1m,
                        // ApuRow.*Uf es la moneda base del motor: aquí el precio está en CLP.
                        PrecioUf = 5000m
                    }
                }
            }
        };

        var engine = new ApuAutocompleteEngine(session, new MaterialSearchService());

        var results = await engine.SearchAsync("CBL-APU-16");

        Assert.NotEmpty(results);
        var first = results[0];
        Assert.Equal(5000m, first.MatClp);
        Assert.Equal(PriceConversion.ClpToUf(5000m, 38000m), first.MatUf);
    }

    [Fact]
    public async Task SearchAsync_WhenProjectIsClp_SuggestionHidesUf()
    {
        var session = new BudgetProjectSession();
        session.ValorUfClp = 38000m;
        session.ProjectInfo.MonedaPresupuesto = "CLP";
        session.Materiales.Add(new MaterialRow
        {
            Codigo = "CBL-CLP-1",
            Descripcion = "Cable CLP 1 mm2",
            Unidad = "m",
            MatClp = 900m
        });

        var engine = new ApuAutocompleteEngine(session, new MaterialSearchService());

        var clpResults = await engine.SearchAsync("CBL-CLP-1");
        Assert.False(clpResults[0].MuestraUf);

        session.ProjectInfo.MonedaPresupuesto = "UF";
        var ufResults = await engine.SearchAsync("CBL-CLP-1");
        Assert.True(ufResults[0].MuestraUf);
    }

    [Fact]
    public void ApplyAutocompleteSuggestion_PopulatesRecursoRowProperly()
    {
        var session = new BudgetProjectSession();
        session.ValorUfClp = 38000m;

        var row = new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            Descripcion = "Placeholder",
            Rendimiento = 0m
        };
        var vm = new ApuRecursoVm(row);

        var suggestion = new ApuAutocompleteSuggestionVm(
            codigo: "PREN-M20",
            descripcion: "Prensacable M20 IP68 Poliamida",
            unidad: "un",
            matClp: 850m,
            matUf: 0.0224m,
            proveedor: "Legrand",
            matchReason: "Descripción",
            sourceBadge: "BANCO");

        vm.CodigoMaterial = suggestion.Codigo;
        vm.Descripcion = suggestion.Descripcion;
        vm.Unidad = suggestion.Unidad;
        vm.PrecioUf = suggestion.MatUf;
        vm.Rendimiento = 2.5m;
        vm.RefreshCalculated();

        Assert.Equal("PREN-M20", vm.CodigoMaterial);
        Assert.Equal("Prensacable M20 IP68 Poliamida", vm.Descripcion);
        Assert.Equal("un", vm.Unidad);
        Assert.Equal(0.0224m, vm.PrecioUf);
        Assert.Equal(2.5m, vm.Rendimiento);
        Assert.Equal(2.5m * 0.0224m, vm.CostoUnitUf);
        Assert.False(vm.HasIssue);
    }
}
