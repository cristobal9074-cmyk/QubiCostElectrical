using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QubiCostElectrical;

/// <summary>
/// Preset de estructura de zonas y sectores para proyectos eléctricos por industria o personalizados.
/// </summary>
public sealed class ZonaStructurePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Personalizado";
    public string Icon { get; set; } = "⭐";
    public string Description { get; set; } = string.Empty;
    public List<string> Zonas { get; set; } = new();
    public bool IsFactory { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public override string ToString() => Name;
}

public interface IZonaStructurePresetStore
{
    IReadOnlyList<ZonaStructurePreset> GetAllPresets();
    IReadOnlyList<ZonaStructurePreset> GetPresetsByCategory(string category);
    void SavePreset(ZonaStructurePreset preset);
    bool DeletePreset(string presetId);
}

public sealed class ZonaStructurePresetStore : IZonaStructurePresetStore
{
    private readonly string presetsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ZonaStructurePresetStore(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            presetsFilePath = customFilePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "QubiCostElectrical", "Presets");
            presetsFilePath = Path.Combine(dir, "zona_structure_presets.json");
        }
    }

    public IReadOnlyList<ZonaStructurePreset> GetAllPresets()
    {
        var presets = LoadFromDisk();
        if (presets.Count == 0)
        {
            presets = GetFactoryPresets().ToList();
            SaveToDisk(presets);
        }
        else
        {
            // Aseguramos que siempre existan los factory presets aunque el archivo haya sido creado previamente
            var factory = GetFactoryPresets().ToList();
            bool changed = false;
            foreach (var f in factory)
            {
                if (!presets.Any(p => string.Equals(p.Id, f.Id, StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(p.Name, f.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    presets.Insert(0, f);
                    changed = true;
                }
            }
            if (changed)
            {
                SaveToDisk(presets);
            }
        }
        return presets;
    }

    public IReadOnlyList<ZonaStructurePreset> GetPresetsByCategory(string category)
    {
        var all = GetAllPresets();
        if (string.IsNullOrWhiteSpace(category) || category.Equals("Todos", StringComparison.OrdinalIgnoreCase))
            return all;

        return all.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public void SavePreset(ZonaStructurePreset preset)
    {
        if (preset == null) return;
        var list = LoadFromDisk();
        var index = list.FindIndex(p => string.Equals(p.Id, preset.Id, StringComparison.OrdinalIgnoreCase) ||
                                       (!p.IsFactory && string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase)));
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

    private List<ZonaStructurePreset> LoadFromDisk()
    {
        try
        {
            if (File.Exists(presetsFilePath))
            {
                string json = File.ReadAllText(presetsFilePath);
                var items = JsonSerializer.Deserialize<List<ZonaStructurePreset>>(json, JsonOptions);
                if (items != null)
                    return items;
            }
        }
        catch
        {
            // Ignoramos errores de lectura o formato corrupto y recurrimos a factory
        }
        return new List<ZonaStructurePreset>();
    }

    private void SaveToDisk(List<ZonaStructurePreset> presets)
    {
        try
        {
            string? dir = Path.GetDirectoryName(presetsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(presets, JsonOptions);
            File.WriteAllText(presetsFilePath, json);
        }
        catch
        {
            // Log o captura silenciosa para no romper la UX
        }
    }

    public static IEnumerable<ZonaStructurePreset> GetFactoryPresets()
    {
        // 1. Minería — Planta Concentradora
        yield return new ZonaStructurePreset
        {
            Id = "factory_mineria_concentradora",
            Name = "Planta Minera Concentradora",
            Category = "Minería",
            Icon = "⛏️",
            Description = "Estructura WBS estándar para faenas mineras con molienda SAG, flotación, espesadores y salas eléctricas E-House.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Área 100 - Chancado Primario",
                "Área 110 - Stockpile & Correas",
                "Área 200 - Molienda SAG & Bolas",
                "Área 210 - Sala Eléctrica Molienda (E-House)",
                "Área 300 - Flotación Rougher & Reactivos",
                "Área 400 - Espesadores & Bombas Relave",
                "Área 800 - Truck Shop & Talleres",
                "S/E Principal Faena 110/23 kV"
            }
        };

        // 2. Minería — Planta Lixiviación SX-EW
        yield return new ZonaStructurePreset
        {
            Id = "factory_mineria_lixiviacion",
            Name = "Planta Hidrometalúrgica (SX-EW)",
            Category = "Minería",
            Icon = "⛏️",
            Description = "Zonas para plantas de lixiviación, piscinas de soluciones, extracción por solventes y nave de electroobtención.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Área Pilas de Lixiviación & Bombas",
                "Área Piscinas PLS / ILF",
                "Área Extracción por Solventes (SX)",
                "Nave Electroobtención (EW) & Celdas",
                "Patio de Cátodos & Despacho",
                "Sala Rectificadores CC & Transformadores",
                "Subestación Eléctrica Principal"
            }
        };

        // 3. Solar — Parque Fotovoltaico PMGD (3 a 9 MW)
        yield return new ZonaStructurePreset
        {
            Id = "factory_solar_pmgd",
            Name = "Parque Solar Utility / PMGD (3-9 MW)",
            Category = "Solar",
            Icon = "☀️",
            Description = "Estructura para parques solares sobre suelo con seguidores (trackers), centros de transformación, SCADA y BESS.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Campo Solar - Strings Bloque 1",
                "Campo Solar - Strings Bloque 2",
                "Centro de Inversores & CT 01",
                "Centro de Inversores & CT 02",
                "Zanja Media Tensión & Red de Tierras",
                "Centro de Transformación & Enlace MT",
                "Sala de Control, SCADA & BESS",
                "Punto de Conexión a Red (Empalme MT)"
            }
        };

        // 4. Solar — Techo Comercial e Industrial C&I
        yield return new ZonaStructurePreset
        {
            Id = "factory_solar_techo_ci",
            Name = "Techo Solar C&I (Autoconsumo / Net-Billing)",
            Category = "Solar",
            Icon = "☀️",
            Description = "Planta fotovoltaica en cubiertas industriales, bajada DC/AC, tablero de inyección general y monitoreo.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Arreglo Paneles - Cubierta Nave A",
                "Arreglo Paneles - Cubierta Nave B",
                "Inversores de Techo & Cajas String",
                "Canalización Troncal Bajada DC / AC",
                "Tablero General Inyección Solar (TGIS)",
                "Sistema Monitoreo & Medidor Bidireccional"
            }
        };

        // 5. Industrial — Centro de Distribución y Bodega Logística
        yield return new ZonaStructurePreset
        {
            Id = "factory_industrial_logistica",
            Name = "Centro Logístico & Bodegas",
            Category = "Industrial",
            Icon = "🏭",
            Description = "Almacenes con racks, andenes de carga, pasarelas técnicas aéreas, sala de carga de baterías y bombas RCI.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Nave Almacenamiento & Racks",
                "Zona Muelles & Andenes de Carga",
                "Pasarelas Técnicas & Alumbrado Alto",
                "Sala Carga Baterías (Grúas)",
                "Sala Bombas RCI & Estanque",
                "Subestación & Grupo Electrógeno",
                "Oficinas de Despacho & Control"
            }
        };

        // 6. Industrial — Planta de Procesos y Manufactura
        yield return new ZonaStructurePreset
        {
            Id = "factory_industrial_manufactura",
            Name = "Planta de Procesos & Manufactura",
            Category = "Industrial",
            Icon = "🏭",
            Description = "Instalaciones fabriles con líneas de envasado, cámaras de frío, salas de fuerza CCM, compresores y calderas.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Recepción Materias Primas",
                "Línea de Proceso 1 (Elaboración)",
                "Línea de Proceso 2 (Envasado)",
                "Cámaras de Frío & Refrigeración",
                "Sala de Fuerza & CCM Principal",
                "Sala de Compresores & Calderas",
                "Taller de Mantención Eléctrica"
            }
        };

        // 7. Edificación — Torre Residencial Estándar
        yield return new ZonaStructurePreset
        {
            Id = "factory_edificio_residencial",
            Name = "Edificio Residencial (2 Sub + 12 Pisos)",
            Category = "Edificación",
            Icon = "🏢",
            Description = "Torre habitacional con subterráneos, hall de acceso, pisos tipo habitacionales, azotea y sala técnica.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Subterráneo 2",
                "Subterráneo 1",
                "Piso 1 - Hall & Accesos",
                "Piso 2 al 6 (Pisos Bajos)",
                "Piso 7 al 12 (Pisos Altos)",
                "Azotea, Calderas & Bombas",
                "Sala Tableros Generales & Grupo"
            }
        };

        // 8. Hospitalario — Centro Médico de Alta Complejidad
        yield return new ZonaStructurePreset
        {
            Id = "factory_hospital_clinica",
            Name = "Hospital & Centro de Alta Complejidad",
            Category = "Hospitalario",
            Icon = "🏥",
            Description = "Distribución hospitalaria con pabellones quirúrgicos (red aislada IT), UCI crítica, urgencias y central técnica.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Urgencias & Diagnóstico por Imágenes",
                "Pabellones Quirúrgicos (Zona Crítica IT)",
                "UCI / UTI Adulto & Pediátrico (UPS)",
                "Hospitalización & Recuperación",
                "Laboratorios & Farmacia Central",
                "Central Térmica & S/E Emergencia"
            }
        };

        // 9. Subestaciones — S/E Eléctrica MT/AT 66/23 kV
        yield return new ZonaStructurePreset
        {
            Id = "factory_subestacion_mt_at",
            Name = "Subestación MT/AT (66/23 kV)",
            Category = "Subestaciones",
            Icon = "⚡",
            Description = "Patio de alta tensión, transformadores de poder, sala de celdas switchgear MT, protecciones SCADA y banco de baterías.",
            IsFactory = true,
            Zonas = new List<string>
            {
                "Patio de Conexión AT 66 kV",
                "Transformador de Poder Principal",
                "Sala de Celdas MT (Switchgear 23 kV)",
                "Sala de Control, Protecciones & SCADA",
                "Banco Baterías & Servicios Auxiliares CC",
                "Malla de Puesta a Tierra Principal"
            }
        };
    }
}
