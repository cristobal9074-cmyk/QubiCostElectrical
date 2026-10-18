using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record ObservationChipPreset(
    string Label,
    string Icon,
    string Text,
    string Tooltip);

/// <summary>
/// Almacén inteligente de memoria de observaciones y presets rápidos de justificación.
/// Guarda en memoria de usuario las frases más frecuentes y recientes (MRU) para autocompletado instantáneo.
/// </summary>
public static class ObservationMemoryStore
{
    private static readonly List<ObservationChipPreset> DefaultChips = new()
    {
        new("Cía. Eléctrica", "⚡", "Por compañía eléctrica", "Justificación: Por compañía distribuidora (Enel/CGE/Chilquinta)"),
        new("Mandante", "🏢", "Suministrado por Mandante", "Justificación: Suministro o provisión por cliente/mandante"),
        new("Proforma", "📄", "Valor proforma", "Justificación: Costo estimado / valor proforma a regularizar"),
        new("No Aplica", "🚫", "No considerado en bases técnicas", "Justificación: Exclusión formal no solicitada en bases"),
        new("Opcional", "⏳", "Alternativa / Opcional", "Justificación: Partida cotizada como opción voluntaria"),
        new("Incluido", "🔗", "Se incluye en ítem", "Justificación: Costo absorbido en otra partida del presupuesto")
    };

    public static IReadOnlyList<ObservationChipPreset> StandardChips => DefaultChips;

    public static IReadOnlyList<string> GetSuggestions(UserPreferences? prefs, string? prefix = null)
    {
        var list = new List<string>();

        // 1. Agregar chips estándar primero
        foreach (var chip in DefaultChips)
        {
            if (!list.Contains(chip.Text, StringComparer.OrdinalIgnoreCase))
                list.Add(chip.Text);
        }

        // 2. Agregar recientes del usuario
        if (prefs?.RecentObservations != null)
        {
            foreach (var obs in prefs.RecentObservations)
            {
                if (!string.IsNullOrWhiteSpace(obs) && !list.Contains(obs, StringComparer.OrdinalIgnoreCase))
                    list.Add(obs.Trim());
            }
        }

        if (string.IsNullOrWhiteSpace(prefix))
            return list;

        string p = prefix.Trim();
        return list
            .Where(s => s.Contains(p, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static void Remember(UserPreferences? prefs, string? observation, IUserPreferencesStore? store = null)
    {
        if (prefs == null || string.IsNullOrWhiteSpace(observation))
            return;

        string clean = observation.Trim();
        if (clean.Length < 3)
            return;

        prefs.RecentObservations ??= new List<string>();

        // Mover al principio (MRU)
        prefs.RecentObservations.RemoveAll(s => string.Equals(s, clean, StringComparison.OrdinalIgnoreCase));
        prefs.RecentObservations.Insert(0, clean);

        // Limitar a máximo 20 elementos recientes
        if (prefs.RecentObservations.Count > 20)
        {
            prefs.RecentObservations = prefs.RecentObservations.Take(20).ToList();
        }

        if (store != null)
        {
            store.Save(prefs);
        }
    }
}
