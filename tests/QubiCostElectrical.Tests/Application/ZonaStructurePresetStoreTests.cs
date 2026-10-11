using System;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ZonaStructurePresetStoreTests : IDisposable
{
    private readonly string tempFilePath;

    public ZonaStructurePresetStoreTests()
    {
        tempFilePath = Path.Combine(Path.GetTempPath(), $"zona_presets_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(tempFilePath))
        {
            try { File.Delete(tempFilePath); } catch { }
        }
    }

    [Fact]
    public void GetAllPresets_InitializesFactoryPresetsWhenEmpty()
    {
        var store = new ZonaStructurePresetStore(tempFilePath);
        var presets = store.GetAllPresets();

        Assert.NotEmpty(presets);
        Assert.True(presets.Count >= 9);
        Assert.Contains(presets, p => p.Category == "Minería");
        Assert.Contains(presets, p => p.Category == "Solar");
        Assert.Contains(presets, p => p.Category == "Industrial");
        Assert.Contains(presets, p => p.Category == "Edificación");
        Assert.Contains(presets, p => p.Category == "Hospitalario");
        Assert.Contains(presets, p => p.Category == "Subestaciones");
        Assert.True(File.Exists(tempFilePath));
    }

    [Fact]
    public void SavePreset_AddsCustomPresetAndPersistsToDisk()
    {
        var store = new ZonaStructurePresetStore(tempFilePath);
        var custom = new ZonaStructurePreset
        {
            Name = "Parque Eólico Troncal",
            Category = "Renovables",
            Icon = "💨",
            Description = "Zonas de aerogeneradores y línea subterránea",
            Zonas = new() { "Circuito WTG 1-5", "Circuito WTG 6-10", "Subestación Elevadora" },
            IsFactory = false
        };

        store.SavePreset(custom);

        var store2 = new ZonaStructurePresetStore(tempFilePath);
        var all = store2.GetAllPresets();
        var found = all.FirstOrDefault(p => p.Name == "Parque Eólico Troncal");

        Assert.NotNull(found);
        Assert.Equal("Renovables", found.Category);
        Assert.Equal(3, found.Zonas.Count);
        Assert.False(found.IsFactory);
    }

    [Fact]
    public void DeletePreset_RemovesCustomPreset_AndProtectsFactoryPresets()
    {
        var store = new ZonaStructurePresetStore(tempFilePath);
        var custom = new ZonaStructurePreset
        {
            Id = "custom_test_123",
            Name = "Proyecto Temporal",
            IsFactory = false,
            Zonas = new() { "Zona A" }
        };
        store.SavePreset(custom);

        // No debe permitir borrar un factory preset
        bool factoryDeleteResult = store.DeletePreset("factory_mineria_concentradora");
        Assert.False(factoryDeleteResult);

        // Sí debe permitir borrar el custom preset
        bool customDeleteResult = store.DeletePreset("custom_test_123");
        Assert.True(customDeleteResult);

        var all = store.GetAllPresets();
        Assert.DoesNotContain(all, p => p.Id == "custom_test_123");
    }

    [Fact]
    public void GetPresetsByCategory_FiltersCorrectly()
    {
        var store = new ZonaStructurePresetStore(tempFilePath);
        var mineria = store.GetPresetsByCategory("Minería");
        Assert.All(mineria, p => Assert.Equal("Minería", p.Category));

        var solar = store.GetPresetsByCategory("Solar");
        Assert.All(solar, p => Assert.Equal("Solar", p.Category));
    }
}
