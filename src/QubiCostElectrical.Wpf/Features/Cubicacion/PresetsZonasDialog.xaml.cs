using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class PresetsZonasDialog : Window
{
    private readonly IZonaStructurePresetStore _store;
    private readonly List<ZonaStructurePreset> _allPresets = new();
    private readonly ObservableCollection<ZonaStructurePreset> _filteredPresets = new();
    private string _selectedCategory = "Todos";

    public List<string> ZonasSeleccionadas { get; } = new();
    public bool ModoReemplazar { get; private set; } = true;

    public PresetsZonasDialog(IZonaStructurePresetStore? store = null)
    {
        InitializeComponent();
        _store = store ?? new ZonaStructurePresetStore();

        PresetsList.ItemsSource = _filteredPresets;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ReloadPresets();
        BuildCategoryChips();
        if (_filteredPresets.Count > 0)
        {
            PresetsList.SelectedIndex = 0;
        }
        SearchBox.Focus();
    }

    private void ReloadPresets()
    {
        _allPresets.Clear();
        _allPresets.AddRange(_store.GetAllPresets());

        TxtTotalPresets.Text = _allPresets.Count.ToString();
        TxtCustomPresets.Text = _allPresets.Count(p => !p.IsFactory).ToString();

        ApplyFilter();
    }

    private void BuildCategoryChips()
    {
        CategoriesPanel.Children.Clear();
        var categories = new[]
        {
            "Todos",
            "⭐ Mis Presets",
            "⛏️ Minería",
            "☀️ Solar",
            "🏭 Industrial",
            "🏢 Edificación",
            "🏥 Hospitalario",
            "⚡ Subestaciones"
        };

        foreach (var cat in categories)
        {
            var btn = new Button
            {
                Content = cat,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(12, 5, 12, 5),
                Cursor = Cursors.Hand,
                Tag = cat
            };

            UpdateChipStyle(btn, cat == _selectedCategory);
            btn.Click += OnCategoryChipClick;
            CategoriesPanel.Children.Add(btn);
        }
    }

    private void UpdateChipStyle(Button btn, bool isSelected)
    {
        if (isSelected)
        {
            btn.Background = (Brush)FindResource("Brush.BlueDark");
            btn.Foreground = Brushes.White;
            btn.BorderBrush = (Brush)FindResource("Brush.BlueDark");
            btn.FontWeight = FontWeights.Bold;
        }
        else
        {
            btn.Background = (Brush)FindResource("Brush.Surface");
            btn.Foreground = (Brush)FindResource("Brush.Ink");
            btn.BorderBrush = (Brush)FindResource("Brush.Line");
            btn.FontWeight = FontWeights.Normal;
        }
    }

    private void OnCategoryChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cat)
        {
            _selectedCategory = cat;
            foreach (var child in CategoriesPanel.Children.OfType<Button>())
            {
                UpdateChipStyle(child, (string)child.Tag == _selectedCategory);
            }
            ApplyFilter();
        }
    }

    private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim();
        var filtered = _allPresets.AsEnumerable();

        if (_selectedCategory == "⭐ Mis Presets")
        {
            filtered = filtered.Where(p => !p.IsFactory);
        }
        else if (_selectedCategory != "Todos")
        {
            string cleanCat = _selectedCategory.Replace("⛏️ ", "").Replace("☀️ ", "").Replace("🏭 ", "")
                                               .Replace("🏢 ", "").Replace("🏥 ", "").Replace("⚡ ", "").Trim();
            filtered = filtered.Where(p => string.Equals(p.Category, cleanCat, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(p =>
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Zonas.Any(z => z.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        _filteredPresets.Clear();
        foreach (var p in filtered)
        {
            _filteredPresets.Add(p);
        }

        if (_filteredPresets.Count > 0)
        {
            PresetsList.SelectedIndex = 0;
        }
        else
        {
            PreviewZonasItems.ItemsSource = null;
            TxtPreviewCount.Text = "0 zonas";
            BtnDeletePreset.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPresetsListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetsList.SelectedItem is ZonaStructurePreset preset)
        {
            var previewList = preset.Zonas.Select((z, i) => new
            {
                Numero = (i + 1) < 10 ? $"0{i + 1}" : (i + 1).ToString(),
                Nombre = z
            }).ToList();

            PreviewZonasItems.ItemsSource = previewList;
            TxtPreviewCount.Text = $"{preset.Zonas.Count} zonas";
            BtnDeletePreset.Visibility = !preset.IsFactory ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            PreviewZonasItems.ItemsSource = null;
            TxtPreviewCount.Text = "0 zonas";
            BtnDeletePreset.Visibility = Visibility.Collapsed;
        }
    }

    private void OnDeletePresetClick(object sender, RoutedEventArgs e)
    {
        if (PresetsList.SelectedItem is ZonaStructurePreset preset && !preset.IsFactory)
        {
            bool ok = WpfDialogService.Confirm("Eliminar Preset Personalizado",
                $"¿Está seguro de eliminar el preset \"{preset.Name}\"? Esta acción no se puede deshacer.");
            if (ok)
            {
                _store.DeletePreset(preset.Id);
                ReloadPresets();
            }
        }
    }

    private void OnCrearNuevoPresetClick(object sender, RoutedEventArgs e)
    {
        string? nombre = WpfDialogService.PromptText("Nuevo Preset de Estructura", "Ingrese el nombre para la nueva plantilla:", "");
        if (string.IsNullOrWhiteSpace(nombre))
            return;

        string? zonasRaw = WpfDialogService.PromptText("Zonas de la Estructura",
            "Ingrese los nombres de las zonas separados por comas o punto y coma:\nEj: Nivel 1, Nivel 2, Cubierta", "");
        if (string.IsNullOrWhiteSpace(zonasRaw))
            return;

        var zonasList = zonasRaw
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (zonasList.Count == 0)
        {
            WpfDialogService.Info("Sin zonas válidas", "Debe ingresar al menos una zona para crear el preset.");
            return;
        }

        var newPreset = new ZonaStructurePreset
        {
            Name = nombre.Trim(),
            Category = "Personalizado",
            Icon = "⭐",
            Description = $"Preset definido por el usuario ({zonasList.Count} zonas).",
            Zonas = zonasList,
            IsFactory = false
        };

        _store.SavePreset(newPreset);
        ReloadPresets();

        // Seleccionamos el nuevo preset creado
        var found = _filteredPresets.FirstOrDefault(p => p.Id == newPreset.Id);
        if (found != null)
        {
            PresetsList.SelectedItem = found;
            PresetsList.ScrollIntoView(found);
        }
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        if (PresetsList.SelectedItem is not ZonaStructurePreset preset)
        {
            WpfDialogService.Info("Seleccionar Preset", "Por favor seleccione un preset de la lista para continuar.");
            return;
        }

        ZonasSeleccionadas.Clear();
        ZonasSeleccionadas.AddRange(preset.Zonas);
        ModoReemplazar = true;
        DialogResult = true;
    }

    private void OnAppendClick(object sender, RoutedEventArgs e)
    {
        if (PresetsList.SelectedItem is not ZonaStructurePreset preset)
        {
            WpfDialogService.Info("Seleccionar Preset", "Por favor seleccione un preset de la lista para continuar.");
            return;
        }

        ZonasSeleccionadas.Clear();
        ZonasSeleccionadas.AddRange(preset.Zonas);
        ModoReemplazar = false;
        DialogResult = true;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.B || e.Key == Key.F) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
                PresetsList.Focus();
                e.Handled = true;
                return;
            }
        }
    }
}
