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
        var factory = GetFactoryPresets().ToList();
        if (presets.Count == 0)
        {
            presets = factory;
            SaveToDisk(presets);
        }
        else
        {
            bool updated = false;
            foreach (var f in factory)
            {
                if (!presets.Any(p => string.Equals(p.Id, f.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    presets.Add(f);
                    updated = true;
                }
            }
            if (updated)
            {
                SaveToDisk(presets);
            }
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
            Id = "factory_magic_to_matix",
            Name = "Bticino Magic ➔ Bticino Matix",
            Description = "Sustituye la línea clásica Magic por la serie modular Matix de Bticino",
            SourcePrefix = "magic",
            TargetPrefix = "matix",
            IsFactory = true,
            ItemOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["magic9/12"] = "matix9/12",
                ["magic9/15"] = "matix9/15",
                ["magic9/24"] = "matix9/24",
                ["magices16"] = "matixes16",
                ["magic503"] = "matix503",
                ["magic504"] = "matix504",
                ["INT 9/12 MAGIC"] = "INT 9/12 MATIX",
                ["INT 9/15 MAGIC"] = "INT 9/15 MATIX",
                ["INT 9/24 MAGIC"] = "INT 9/24 MATIX",
                ["ENCHUFE MAGIC"] = "ENCHUFE MATIX",
                ["PLACA MAGIC"] = "PLACA MATIX"
            }
        };

        yield return new FamilyConversionPreset
        {
            Id = "factory_pvc_to_pvclh",
            Name = "Canalización PVC ➔ Conduit PVCLH",
            Description = "Reemplaza tuberías y accesorios de PVC tradicional por Conduit PVCLH",
            SourcePrefix = "PVC",
            TargetPrefix = "PVCLH",
            IsFactory = true,
            ItemOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PVC-16"] = "PVCLH-16",
                ["PVC-20"] = "PVCLH-20",
                ["PVC-25"] = "PVCLH-25",
                ["PVC-32"] = "PVCLH-32",
                ["PVC-40"] = "PVCLH-40",
                ["PVC-50"] = "PVCLH-50",
                ["PVC 16MM"] = "PVCLH 16MM",
                ["PVC 20MM"] = "PVCLH 20MM",
                ["PVC 25MM"] = "PVCLH 25MM",
                ["PVC 32MM"] = "PVCLH 32MM",
                ["PVC 40MM"] = "PVCLH 40MM",
                ["PVC 50MM"] = "PVCLH 50MM",
                ["TUBERIA PVC 16MM"] = "TUBERIA PVCLH 16MM",
                ["TUBERIA PVC 20MM"] = "TUBERIA PVCLH 20MM",
                ["TUBERIA PVC 25MM"] = "TUBERIA PVCLH 25MM",
                ["TUBERIA PVC 32MM"] = "TUBERIA PVCLH 32MM",
                ["CURVA-PVC-20"] = "CURVA-PVCLH-20",
                ["CURVA-PVC-25"] = "CURVA-PVCLH-25",
                ["UNION-PVC-20"] = "UNION-PVCLH-20",
                ["UNION-PVC-25"] = "UNION-PVCLH-25",
                ["PVC"] = "PVCLH"
            }
        };

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
