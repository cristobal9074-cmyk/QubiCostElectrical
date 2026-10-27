using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class FamilyBatchReplaceServiceTests
{
    private sealed class FakePriceBankStore : IPriceBankStore
    {
        private readonly List<PriceBankEntry> items = new();

        public FakePriceBankStore(IEnumerable<PriceBankEntry>? initial = null)
        {
            if (initial != null) items.AddRange(initial);
        }

        public IReadOnlyList<PriceBankEntry> GetAll() => items;
        public PriceBankEntry? GetByCodigo(string codigo) =>
            items.FirstOrDefault(i => string.Equals(i.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<PriceBankEntry> Search(string text) =>
            items.Where(i => (i.Descripcion?.Contains(text, StringComparison.OrdinalIgnoreCase) == true) ||
                             (i.Codigo?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)).ToList();

        public void Upsert(PriceBankEntry entry)
        {
            var idx = items.FindIndex(i => string.Equals(i.Codigo, entry.Codigo, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) items[idx] = entry;
            else items.Add(entry);
        }

        public bool Delete(string codigo) =>
            items.RemoveAll(i => string.Equals(i.Codigo, codigo, StringComparison.OrdinalIgnoreCase)) > 0;

        public int DeleteAll() { int c = items.Count; items.Clear(); return c; }
        public int UpsertMany(IEnumerable<PriceBankEntry> entries) { int n = 0; foreach (var e in entries) { Upsert(e); n++; } return n; }
        public int Count() => items.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> entries) => 0;
    }

    private static BudgetProjectSession CreateTestSession()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.ValorUfClp = 38000m;

        // Partidas
        session.Itemizado.Add(new ItemizadoRow { Id = "p1", Item = "1.1", Descripcion = "Alumbrado oficinas", Cantidad = 10m, EsTitulo = false });
        session.Itemizado.Add(new ItemizadoRow { Id = "p2", Item = "1.2", Descripcion = "Enchufes fuerza", Cantidad = 5m, EsTitulo = false });

        // Cubicación
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "c1",
            ItemId = "p1",
            Codigo = "magic9/12",
            Descripcion = "Interruptor simple Magic 9/12",
            Unidad = "un",
            Cantidad = 25m,
            MatClp = 2500m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "c2",
            ItemId = "p1",
            Codigo = "magic9/15",
            Descripcion = "Interruptor combinación Magic 9/15",
            Unidad = "un",
            Cantidad = 15m,
            MatClp = 3200m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "c3",
            ItemId = "p2",
            Codigo = "magices16",
            Descripcion = "Enchufe doble 10/16A Magic",
            Unidad = "un",
            Cantidad = 40m,
            MatClp = 4800m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "c4",
            ItemId = "p2",
            Codigo = "PVC-20",
            Descripcion = "Tubo PVC Conduit 20mm",
            Unidad = "m",
            Cantidad = 120m,
            MatClp = 850m
        });

        // APUs
        var apu1 = new ApuRow
        {
            Id = "a1",
            ItemId = "p1",
            Descripcion = "Punto de alumbrado",
            Recursos = new List<ApuRecursoRow>
            {
                new() { CodigoMaterial = "magic9/12", Descripcion = "Interruptor Magic 9/12", Unidad = "un", Rendimiento = 1.0m, PrecioUf = 0.065m }
            }
        };
        session.Apus.Add(apu1);

        return session;
    }

    [Fact]
    public void ScanMatchingMaterials_WithPrefix_FindsAllOccurrences()
    {
        var session = CreateTestSession();
        var service = new FamilyBatchReplaceService();

        var scanned = service.ScanMatchingMaterials(session, "magic");

        Assert.Equal(3, scanned.Count);
        Assert.Contains(scanned, m => m.SourceCodigo == "magic9/12");
        Assert.Contains(scanned, m => m.SourceCodigo == "magic9/15");
        Assert.Contains(scanned, m => m.SourceCodigo == "magices16");

        var magic12 = scanned.First(m => m.SourceCodigo == "magic9/12");
        // En cubicacion son 25 + en APU p1 (cant 10 * rend 1) = 35 total
        Assert.Equal(35m, magic12.CantidadTotal);
        Assert.Equal(2, magic12.OccurrencesCount);
    }

    [Fact]
    public void AutoMapWithPriceBank_ReplacesPrefixAndResolvesBankEntries()
    {
        var session = CreateTestSession();
        var service = new FamilyBatchReplaceService();

        var bank = new FakePriceBankStore(new[]
        {
            new PriceBankEntry { Codigo = "s179/12", Descripcion = "Interruptor 9/12 Sinthesi Bticino", Unidad = "un", MatClp = 3100m },
            new PriceBankEntry { Codigo = "s179/15", Descripcion = "Interruptor 9/15 Sinthesi Bticino", Unidad = "un", MatClp = 3900m },
            new PriceBankEntry { Codigo = "s179/es16", Descripcion = "Enchufe doble Sinthesi 16A", Unidad = "un", MatClp = 5400m }
        });

        var scanned = service.ScanMatchingMaterials(session, "magic");

        service.AutoMapWithPriceBank(
            scanned,
            sourcePrefix: "magic",
            targetPrefix: "s179/",
            priceBankStore: bank,
            explicitOverrides: new Dictionary<string, string>
            {
                ["magic9/12"] = "s179/12",
                ["magic9/15"] = "s179/15",
                ["magices16"] = "s179/es16"
            });

        var m12 = scanned.First(m => m.SourceCodigo == "magic9/12");
        Assert.Equal("s179/12", m12.TargetCodigo);
        Assert.Equal("Interruptor 9/12 Sinthesi Bticino", m12.TargetDescripcion);
        Assert.Equal(3100m, m12.NuevoPrecioClp);
        Assert.Equal(FamilyMappingStatus.InBank, m12.Status);
    }

    [Fact]
    public void CalculatePreview_ComputesAccurateDeltasAcrossPartidas()
    {
        var session = CreateTestSession();
        var service = new FamilyBatchReplaceService();

        var bank = new FakePriceBankStore(new[]
        {
            new PriceBankEntry { Codigo = "s179/12", Descripcion = "Interruptor 9/12 Sinthesi", Unidad = "un", MatClp = 3000m } // +500 CLP diff
        });

        var scanned = service.ScanMatchingMaterials(session, "magic9/12");
        service.AutoMapWithPriceBank(scanned, "magic", "s179/", bank, explicitOverrides: new Dictionary<string, string> { ["magic9/12"] = "s179/12" });

        var preview = service.CalculatePreview(session, scanned, replaceCubicacion: true, replaceApu: false);

        Assert.Equal(1, preview.PartidasAfectadasCount);
        Assert.Equal(1, preview.TotalOcurrenciasCount);
        // 25 un * 2500 = 62,500 actual; 25 un * 3000 = 75,000 nuevo; delta = 12,500
        Assert.Equal(62500m, preview.CostoTotalAnteriorClp);
        Assert.Equal(75000m, preview.CostoTotalNuevoClp);
        Assert.Equal(12500m, preview.DeltaTotalClp);
        Assert.Equal(20m, preview.VariacionPorcentual);
    }

    [Fact]
    public void ExecuteReplacement_UpdatesCubicacionAndApuPreservingFormulas()
    {
        var session = CreateTestSession();
        var service = new FamilyBatchReplaceService();

        var bank = new FakePriceBankStore(new[]
        {
            new PriceBankEntry { Codigo = "s179/12", Descripcion = "Interruptor 9/12 Sinthesi", Unidad = "un", MatClp = 3100m },
            new PriceBankEntry { Codigo = "s179/15", Descripcion = "Interruptor 9/15 Sinthesi", Unidad = "un", MatClp = 3900m },
            new PriceBankEntry { Codigo = "s179/es16", Descripcion = "Enchufe doble Sinthesi 16A", Unidad = "un", MatClp = 5400m }
        });

        var scanned = service.ScanMatchingMaterials(session, "magic");
        service.AutoMapWithPriceBank(
            scanned,
            "magic",
            "s179/",
            bank,
            explicitOverrides: new Dictionary<string, string>
            {
                ["magic9/12"] = "s179/12",
                ["magic9/15"] = "s179/15",
                ["magices16"] = "s179/es16"
            });

        string? undoRecorded = null;
        var result = service.ExecuteReplacement(
            session,
            (s, desc) => undoRecorded = desc,
            scanned,
            replaceCubicacion: true,
            replaceApu: true);

        Assert.Equal(3, result.ItemsReemplazados);
        Assert.Equal(2, result.PartidasActualizadas);
        Assert.Equal(3, result.FilasCubicacionModificadas);
        Assert.Equal(1, result.FilasApuModificadas);
        Assert.NotNull(undoRecorded);

        // Verificar que las filas de cubicación tienen el nuevo código y precio pero mantienen la cantidad intacta
        var c1 = session.Cubicacion.First(c => c.Id == "c1");
        Assert.Equal("s179/12", c1.Codigo);
        Assert.Equal("Interruptor 9/12 Sinthesi", c1.Descripcion);
        Assert.Equal(25m, c1.Cantidad); // Metraje intacto
        Assert.Equal(3100m, c1.MatClp);

        // Verificar que APU tiene el nuevo código
        var apu1Rec = session.Apus.First(a => a.Id == "a1").Recursos.First();
        Assert.Equal("s179/12", apu1Rec.CodigoMaterial);
        Assert.Equal(1.0m, apu1Rec.Rendimiento); // Rendimiento intacto
    }

    [Fact]
    public void PresetStore_SaveAndLoad_PreservesCustomPresetAndDefaults()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"presets_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new FamilyConversionPresetStore(tempFile);
            var presets = store.GetAllPresets();

            Assert.NotEmpty(presets);
            Assert.Contains(presets, p => p.IsFactory && p.SourcePrefix == "magic");

            // Guardar uno personalizado
            var custom = new FamilyConversionPreset
            {
                Id = "custom_test_1",
                Name = "Mi Regla Personalizada",
                SourcePrefix = "ABC-",
                TargetPrefix = "XYZ-",
                ItemOverrides = new Dictionary<string, string> { ["ABC-1"] = "XYZ-1" }
            };
            store.SavePreset(custom);

            var reloaded = store.GetAllPresets();
            var found = reloaded.FirstOrDefault(p => p.Id == "custom_test_1");
            Assert.NotNull(found);
            Assert.Equal("Mi Regla Personalizada", found.Name);
            Assert.Equal("XYZ-1", found.ItemOverrides["ABC-1"]);

            // Eliminar
            bool deleted = store.DeletePreset("custom_test_1");
            Assert.True(deleted);
            Assert.DoesNotContain(store.GetAllPresets(), p => p.Id == "custom_test_1");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ScanMatchingMaterials_WithScanCubicacionOnly_ScansOnlyCubicacion()
    {
        var session = CreateTestSession();
        var service = new FamilyBatchReplaceService();

        // Con scanCubicacionOnly = true, solo debe considerar la cubicacion (25) y no el APU (10)
        var scanned = service.ScanMatchingMaterials(session, "magic", scanCubicacionOnly: true);

        Assert.Equal(3, scanned.Count);
        var magic12 = scanned.First(m => m.SourceCodigo == "magic9/12");
        Assert.Equal(25m, magic12.CantidadTotal);
        Assert.Equal(1, magic12.OccurrencesCount);
    }

    [Fact]
    public void AutoMapWithPriceBank_WithSessionCubicacion_ResolvesFromProjectCubicacionFirst()
    {
        var session = CreateTestSession();
        // Agregar un item destino a la cubicacion directamente
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "c_dest",
            ItemId = "p1",
            Codigo = "s179/12",
            Descripcion = "Interruptor 9/12 Sinthesi en Proyecto",
            Unidad = "un",
            Cantidad = 5m,
            MatClp = 3300m
        });

        var service = new FamilyBatchReplaceService();
        var emptyBank = new FakePriceBankStore();

        var scanned = service.ScanMatchingMaterials(session, "magic", scanCubicacionOnly: true);

        service.AutoMapWithPriceBank(
            scanned,
            sourcePrefix: "magic",
            targetPrefix: "s179/",
            priceBankStore: emptyBank,
            explicitOverrides: new Dictionary<string, string> { ["magic9/12"] = "s179/12" },
            session: session);

        var m12 = scanned.First(m => m.SourceCodigo == "magic9/12");
        Assert.Equal("s179/12", m12.TargetCodigo);
        Assert.Equal("Interruptor 9/12 Sinthesi en Proyecto", m12.TargetDescripcion);
        Assert.Equal(3300m, m12.NuevoPrecioClp);
        Assert.Equal(FamilyMappingStatus.InCubicacion, m12.Status);
    }

    [Fact]
    public void AutoMapWithPriceBank_MagicToMatix_FallbackPreservesPriceAndSetsManualStatus()
    {
        var session = CreateTestSession();
        var service = new FamilyBatchReplaceService();
        var emptyBank = new FakePriceBankStore();

        var scanned = service.ScanMatchingMaterials(session, "magic");
        var preset = FamilyConversionPresetStore.GetFactoryPresets().First(p => p.Id == "factory_magic_to_matix");

        service.AutoMapWithPriceBank(
            scanned,
            sourcePrefix: preset.SourcePrefix,
            targetPrefix: preset.TargetPrefix,
            priceBankStore: emptyBank,
            explicitOverrides: preset.ItemOverrides,
            session: session);

        var m12 = scanned.First(m => m.SourceCodigo == "magic9/12");
        Assert.Equal("matix9/12", m12.TargetCodigo);
        // Cuando no está en banco ni cubicación, se genera propuesta y se mantiene el precio base sin quedar en 
        Assert.Equal(2500m, m12.NuevoPrecioClp);
        Assert.Equal(FamilyMappingStatus.Manual, m12.Status);
    }

    [Fact]
    public void ExecuteReplacement_UpdatesCubicacionSC_PvcToPvclhAndArtefactos()
    {
        var session = CreateTestSession();
        var cuadro = new CuadroCargaSCRow
        {
            Id = "tab_1",
            Nombre = "TDA-1",
            Circuitos = new List<CircuitoSCRow>
            {
                new CircuitoSCRow
                {
                    Numero = 1,
                    TipoTuberia = "PVC",
                    TipoTuberia2 = "PVC",
                    MetrosTuberia = 30m,
                    MetrosTuberia2 = 10m,
                    Artefactos = new List<ArtefactoSCRow>
                    {
                        new() { Nombre = "magic9/12", Cantidad = 4m }
                    }
                }
            }
        };
        session.CubicacionSC.Add(cuadro);

        var service = new FamilyBatchReplaceService();
        var scanned = service.ScanMatchingMaterials(session, "PVC");
        Assert.NotEmpty(scanned);

        var mappings = new List<FamilyItemMapping>
        {
            new FamilyItemMapping
            {
                SourceCodigo = "PVC",
                SourceDescripcion = "PVC",
                TargetCodigo = "PVCLH",
                TargetDescripcion = "PVCLH",
                NuevoPrecioClp = 1200m,
                IsSelected = true
            },
            new FamilyItemMapping
            {
                SourceCodigo = "magic9/12",
                SourceDescripcion = "Interruptor simple Magic 9/12",
                TargetCodigo = "matix9/12",
                TargetDescripcion = "Interruptor simple Matix 9/12",
                NuevoPrecioClp = 2900m,
                IsSelected = true
            }
        };

        var result = service.ExecuteReplacement(session, null, mappings, replaceCubicacion: true, replaceApu: false);

        var circuito = session.CubicacionSC[0].Circuitos[0];
        Assert.Equal("PVCLH", circuito.TipoTuberia);
        Assert.Equal("PVCLH", circuito.TipoTuberia2);
        Assert.Equal("Interruptor simple Matix 9/12", circuito.Artefactos[0].Nombre);
    }

    [Fact]
    public void FamilyItemMapping_PropertyChanged_RecalculatesDeltasLive()
    {
        var mapping = new FamilyItemMapping
        {
            SourceCodigo = "PVC-20",
            CantidadTotal = 100m,
            PrecioActualClp = 1000m
        };

        var changedProps = new List<string>();
        mapping.PropertyChanged += (s, e) => { if (e.PropertyName != null) changedProps.Add(e.PropertyName); };

        Assert.Equal(100000m, mapping.SubtotalActualClp);
        Assert.Equal(0m, mapping.NuevoSubtotalClp);

        // Cambiar precio nuevo en vivo
        mapping.NuevoPrecioClp = 1500m;

        Assert.Equal(150000m, mapping.NuevoSubtotalClp);
        Assert.Equal(50000m, mapping.DeltaClp);
        Assert.Equal(500m, mapping.DeltaUnitarioClp);
        Assert.Contains("NuevoPrecioClp", changedProps);
        Assert.Contains("NuevoSubtotalClp", changedProps);
        Assert.Contains("DeltaClp", changedProps);
        Assert.Contains("DeltaUnitarioClp", changedProps);
    }
}
