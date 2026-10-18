using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Shell;

public partial class KeyboardShortcutsDialog : Window
{
    private readonly List<ShortcutGroupVm> allGroups = new();
    public ObservableCollection<ShortcutGroupVm> FilteredGroups { get; } = new();

    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
        DataContext = this;
        LoadShortcuts();
        FilterShortcuts(string.Empty);
    }

    public static void Show(Window owner)
    {
        if (!ShellWindow.CanShowModalDialog)
            return;

        var dialog = new KeyboardShortcutsDialog { Owner = owner };
        dialog.ShowDialog();
    }

    private void LoadShortcuts()
    {
        allGroups.Clear();

        // ── 1. Globales y Edición ──
        allGroups.Add(new ShortcutGroupVm
        {
            Title = "GLOBALES Y EDICIÓN",
            Items = new List<ShortcutEntryVm>
            {
                CreateEntry("Ctrl+K", "Paleta de comandos inteligente (búsqueda universal)", "Global"),
                CreateEntry("Ctrl+J", "Lanzador Spotlight (salto directo a partidas)", "Global"),
                CreateEntry("Ctrl+S", "Guardar presupuesto activo", "Global"),
                CreateEntry("Ctrl+Shift+S", "Guardar como copia nueva del proyecto", "Global"),
                CreateEntry("Ctrl+Z", "Deshacer última acción", "Global"),
                CreateEntry("Ctrl+Y", "Rehacer acción deshecha", "Global"),
                CreateEntry("Ctrl+N", "Crear nuevo presupuesto eléctrico", "Global"),
                CreateEntry("Ctrl+O", "Abrir archivo de presupuesto (.qce)", "Global"),
                CreateEntry("F1", "Abrir este centro de atajos de teclado", "Global"),
                CreateEntry("F5", "Recargar y sincronizar datos de la vista", "Global"),
                CreateEntry("F8", "Abrir panel de revisión final de coherencia", "Global"),
            }
        });

        // ── 2. Navegación entre Módulos ──
        allGroups.Add(new ShortcutGroupVm
        {
            Title = "NAVEGACIÓN ENTRE MÓDULOS",
            Items = new List<ShortcutEntryVm>
            {
                CreateEntry("Alt+Left", "Ir a la vista anterior (Historial de navegación)", "Navegación"),
                CreateEntry("Alt+Right", "Ir a la vista siguiente (Historial de navegación)", "Navegación"),
                CreateEntry("Alt+1", "Dashboard ejecutivo y KPIs de obra", "Navegación"),
                CreateEntry("Alt+2", "Itemizado general de partidas", "Navegación"),
                CreateEntry("Alt+3", "Cubicación técnica y metrajes", "Navegación"),
                CreateEntry("Alt+Shift+3", "Cubicación SC (Servicios Comunes)", "Navegación"),
                CreateEntry("Alt+4", "Análisis de Precios Unitarios (APU)", "Navegación"),
                CreateEntry("Alt+6", "Banco de precios y catálogo", "Navegación"),
                CreateEntry("Alt+9", "Revisión técnica y auditoría", "Navegación"),
                CreateEntry("Alt+O", "Oferta comercial, márgenes y cierre", "Navegación"),
            }
        });

        // ── 3. Planillas e Itemizado ──
        allGroups.Add(new ShortcutGroupVm
        {
            Title = "ITEMIZADO Y ESTRUCTURA DE OBRA",
            Items = new List<ShortcutEntryVm>
            {
                CreateEntry("Ins", "Insertar nueva partida en el capítulo activo", "Itemizado"),
                CreateEntry("Alt+Up", "Mover partida o capítulo hacia arriba", "Itemizado"),
                CreateEntry("Alt+Down", "Mover partida o capítulo hacia abajo", "Itemizado"),
                CreateEntry("Delete", "Eliminar fila o partida seleccionada", "Itemizado"),
                CreateEntry("Ctrl+V", "Pegar partidas completas desde Excel", "Itemizado"),
                CreateEntry("Ctrl+Shift+C", "Abrir y cubicar directamente la partida seleccionada", "Itemizado"),
            }
        });

        // ── 4. Cubicación Viva (Sincronizada con Preferencias) ──
        var prefs = WpfAppHost.Current?.UserPreferencesStore?.Load() ?? new UserPreferences();
        var cubicacionItems = new List<ShortcutEntryVm>();

        foreach (var def in CubicacionShortcutCatalog.All)
        {
            var effective = KeyboardShortcutPreferences.GetEffective(def.Id, prefs);
            var display = KeyboardShortcutGesture.TryParse(effective, out var gesture) && gesture != null
                ? gesture.ToDisplayString().Replace(" + ", "+", StringComparison.Ordinal)
                : effective;
            if (string.IsNullOrWhiteSpace(display))
                display = def.DefaultGesture;

            cubicacionItems.Add(CreateEntry(display, def.Label, "Cubicación"));
        }

        // Agregar además atajos de edición de celda
        cubicacionItems.Insert(0, CreateEntry("F2", "Editar celda o fórmula directa in-situ", "Cubicacion"));
        cubicacionItems.Insert(1, CreateEntry("Enter", "Confirmar edición y bajar a la siguiente celda", "Cubicacion"));
        cubicacionItems.Insert(2, CreateEntry("Tab", "Confirmar y avanzar a la celda de la derecha", "Cubicacion"));
        cubicacionItems.Insert(3, CreateEntry("Esc", "Cancelar edición o limpiar filtros de búsqueda", "Cubicacion"));

        allGroups.Add(new ShortcutGroupVm
        {
            Title = "CUBICACIÓN VIVA (REFLEJA PREFERENCIAS)",
            Items = cubicacionItems
        });

        // ── 5. Análisis de Precios (APU) ──
        allGroups.Add(new ShortcutGroupVm
        {
            Title = "ANÁLISIS DE PRECIOS UNITARIOS (APU)",
            Items = new List<ShortcutEntryVm>
            {
                CreateEntry("Enter", "Editar coeficiente o rendimiento de insumo", "APU"),
                CreateEntry("Delete", "Quitar recurso o insumo de la partida", "APU"),
                CreateEntry("Ctrl+V", "Pegar recursos directos en la matriz", "APU"),
            }
        });
    }

    private static ShortcutEntryVm CreateEntry(string gesture, string description, string category)
    {
        var tokens = new List<ShortcutKeyToken>();
        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                tokens.Add(new ShortcutKeyToken { Text = "+", IsPlus = true });

            var key = parts[i] switch
            {
                "Up" => "↑",
                "Down" => "↓",
                "Left" => "←",
                "Right" => "→",
                "Plus" => "+",
                "Minus" => "-",
                "Delete" => "Supr",
                "Insert" => "Ins",
                "Escape" => "Esc",
                _ => parts[i]
            };
            tokens.Add(new ShortcutKeyToken { Text = key, IsPlus = false });
        }

        return new ShortcutEntryVm
        {
            Tokens = tokens,
            Description = description,
            Category = category
        };
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = (sender as TextBox)?.Text?.Trim() ?? string.Empty;
        FilterShortcuts(query);
    }

    private void FilterShortcuts(string query)
    {
        FilteredGroups.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var g in allGroups)
                FilteredGroups.Add(g);
            return;
        }

        var lower = query.ToLowerInvariant();
        foreach (var g in allGroups)
        {
            var matching = g.Items.Where(it =>
                it.Description.ToLowerInvariant().Contains(lower) ||
                string.Join("+", it.Tokens.Where(t => !t.IsPlus).Select(t => t.Text)).ToLowerInvariant().Contains(lower)).ToList();

            if (matching.Count > 0)
            {
                FilteredGroups.Add(new ShortcutGroupVm
                {
                    Title = g.Title,
                    Items = matching
                });
            }
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

public sealed class ShortcutKeyToken
{
    public string Text { get; init; } = "";
    public bool IsPlus { get; init; }
}

public sealed class ShortcutEntryVm
{
    public IReadOnlyList<ShortcutKeyToken> Tokens { get; init; } = Array.Empty<ShortcutKeyToken>();
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
}

public sealed class ShortcutGroupVm
{
    public string Title { get; init; } = "";
    public IReadOnlyList<ShortcutEntryVm> Items { get; init; } = Array.Empty<ShortcutEntryVm>();
}
