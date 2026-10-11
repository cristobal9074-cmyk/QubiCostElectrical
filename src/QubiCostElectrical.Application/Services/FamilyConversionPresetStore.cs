using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QubiCostElectrical;

public sealed class FamilyConversionPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourcePrefix { get; set; } = "";
    public string TargetPrefix { get; set; } = "";
    public Dictionary<string, string> ItemOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsFactory { get; set; }

    public override string ToString() => Name;
}

public interface IFamilyConversionPresetStore
{
    IReadOnlyList<FamilyConversionPreset> GetAllPresets();
    void SavePreset(FamilyConversionPreset preset);
    bool DeletePreset(string presetId);
}

public sealed class FamilyConversionPresetStore : IFamilyConversionPresetStore
{
    private readonly string presetsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public FamilyConversionPresetStore(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            presetsFilePath = customFilePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "QubiCostElectrical", "Presets");
            presetsFilePath = Path.Combine(dir, "family_conversion_presets.json");
        }
    }

    public IReadOnlyList<FamilyConversionPreset> GetAllPresets()
    {
        var presets = LoadFromDisk();
        if (presets.Count == 0)
        {
            presets = GetFactoryPresets().ToList();
            SaveToDisk(presets);
        }
        return presets;
    }

    public void SavePreset(FamilyConversionPreset preset)
    {
        if (preset == null) return;
        var list = LoadFromDisk();
        var index = list.FindIndex(p => string.Equals(p.Id, preset.Id, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            list[index] = preset;
        }
        else
        {
            list.Add(preset);
        }
        SaveToDisk(list);
    }

    public bool DeletePreset(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId)) return false;
        var list = LoadFromDisk();
        int removed = list.RemoveAll(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase) && !p.IsFactory);
        if (removed > 0)
        {
            SaveToDisk(list);
            return true;
        }
        return false;
    }

    private List<FamilyConversionPreset> LoadFromDisk()
    {
        try
        {
            if (File.Exists(presetsFilePath))
            {
                string json = File.ReadAllText(presetsFilePath);
                var items = JsonSerializer.Deserialize<List<FamilyConversionPreset>>(json, JsonOptions);
                if (items != null && items.Count > 0)
                    return items;
            }
        }
        catch
        {
            // Silently fallback if corrupted or inaccessible
        }
        return new List<FamilyConversionPreset>();
    }

    private void SaveToDisk(List<FamilyConversionPreset> presets)
    {
        try
        {
            string? dir = Path.GetDirectoryName(presetsFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string json = JsonSerializer.Serialize(presets, JsonOptions);
            File.WriteAllText(presetsFilePath, json);
        }
        catch
        {
            // Fallback safe
        }
    }

    public static IEnumerable<FamilyConversionPreset> GetFactoryPresets()
    {
        yield return new FamilyConversionPreset
        {
            Id = "factory_magic_to_sinthesi",
            Name = "Bticino Magic ➔ Bticino Sinthesi",
            Description = "Convierte artefactos de la línea Magic a la moderna línea modular Sinthesi",
            SourcePrefix = "magic",
            TargetPrefix = "s179/",
            IsFactory = true,
            ItemOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["magic9/12"] = "s179/12",
                ["magic9/15"] = "s179/15",
                ["magic9/24"] = "s179/24",
                ["magices16"] = "s179/es16",
                ["magic503"] = "s179/503",
                ["magic504"] = "s179/504"
            }
        };

        yield return new FamilyConversionPreset
        {
            Id = "factory_pvc_to_clh",
            Name = "Canalización PVC ➔ Conduit Libre de Halógenos (RIC N°04)",
            Description = "Actualiza tuberías y curvas de PVC tradicional a Conduit Libre de Halógenos bajo normativa SEC",
            SourcePrefix = "PVC-",
            TargetPrefix = "CLH-",
            IsFactory = true,
            ItemOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PVC-16"] = "CLH-16",
                ["PVC-20"] = "CLH-20",
                ["PVC-25"] = "CLH-25",
                ["PVC-32"] = "CLH-32",
                ["PVC-40"] = "CLH-40",
                ["PVC-50"] = "CLH-50",
                ["CURVA-PVC-20"] = "CURVA-CLH-20",
                ["CURVA-PVC-25"] = "CURVA-CLH-25",
                ["UNION-PVC-20"] = "UNION-CLH-20",
                ["UNION-PVC-25"] = "UNION-CLH-25"
            }
        };

        yield return new FamilyConversionPreset
        {
            Id = "factory_eva_to_h07z1k",
            Name = "Conductor Tradicional EVA / NYA ➔ Cable H07Z1-K",
            Description = "Sustituye cables termoplásticos convencionales por conductores extra-deslizantes libres de halógenos",
            SourcePrefix = "EVA-",
            TargetPrefix = "H07Z1-K-",
            IsFactory = true,
            ItemOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EVA-1.5"] = "H07Z1-K-1.5",
                ["EVA-2.5"] = "H07Z1-K-2.5",
                ["EVA-4.0"] = "H07Z1-K-4.0",
                ["EVA-6.0"] = "H07Z1-K-6.0",
                ["EVA-10.0"] = "H07Z1-K-10.0",
                ["NYA-1.5"] = "H07Z1-K-1.5",
                ["NYA-2.5"] = "H07Z1-K-2.5"
            }
        };

        yield return new FamilyConversionPreset
        {
            Id = "factory_magic_to_orion",
            Name = "Bticino Magic ➔ Schneider Orion",
            Description = "Sustituye módulos tradicionales por la serie Schneider Orion",
            SourcePrefix = "magic",
            TargetPrefix = "S70",
            IsFactory = true,
            ItemOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["magic9/12"] = "S70110104",
                ["magic9/15"] = "S70110204",
                ["magices16"] = "S70111204"
            }
        };
    }
}
