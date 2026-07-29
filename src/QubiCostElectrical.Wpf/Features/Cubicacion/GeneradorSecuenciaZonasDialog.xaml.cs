using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

    public partial class GeneradorSecuenciaZonasDialog : Window
    {
        private bool _initialized;

        public GeneradorSecuenciaZonasDialog()
        {
            InitializeComponent();
            _initialized = true;
            UpdatePreview();
        }

        public List<string> ZonasGeneradas { get; private set; } = new();

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (PresetCombo?.SelectedItem is not ComboBoxItem item)
            return;

        var tag = item.Tag as string;
        bool twoLevel = tag == "torre-piso";
        if (TwoLevelCheck != null) TwoLevelCheck.IsChecked = twoLevel;
        if (TwoLevelPanel != null)
            TwoLevelPanel.Visibility = twoLevel ? Visibility.Visible : Visibility.Collapsed;

        switch (tag)
        {
            case "sectores":
                PrefixBox.Text = "S"; if (SeparatorCombo != null) SeparatorCombo.SelectedIndex = 0;
                StartBox.Text = "1"; EndBox.Text = "30"; StepBox.Text = "1"; SuffixBox.Text = "";
                break;
            case "pisos":
                PrefixBox.Text = "P"; if (SeparatorCombo != null) SeparatorCombo.SelectedIndex = 0;
                StartBox.Text = "1"; EndBox.Text = "20"; StepBox.Text = "1"; SuffixBox.Text = "";
                break;
            case "zonas":
                PrefixBox.Text = "Zona"; if (SeparatorCombo != null) SeparatorCombo.SelectedIndex = 3;
                StartBox.Text = "1"; EndBox.Text = "15"; StepBox.Text = "1"; SuffixBox.Text = "";
                break;
            case "niveles":
                PrefixBox.Text = "Nivel"; if (SeparatorCombo != null) SeparatorCombo.SelectedIndex = 3;
                StartBox.Text = "1"; EndBox.Text = "15"; StepBox.Text = "1"; SuffixBox.Text = "";
                break;
            case "locales":
                PrefixBox.Text = "Local"; if (SeparatorCombo != null) SeparatorCombo.SelectedIndex = 3;
                StartBox.Text = "1"; EndBox.Text = "6"; StepBox.Text = "1"; SuffixBox.Text = "";
                break;
            case "areas":
                PrefixBox.Text = "Área"; if (SeparatorCombo != null) SeparatorCombo.SelectedIndex = 3;
                StartBox.Text = "1"; EndBox.Text = "10"; StepBox.Text = "1"; SuffixBox.Text = "";
                break;
            case "torre-piso":
                PrefixBox.Text = ""; if (SeparatorCombo != null) SeparatorCombo.SelectedIndex = 0;
                StartBox.Text = "1"; EndBox.Text = "1"; StepBox.Text = "1"; SuffixBox.Text = "";
                Level1Prefix.Text = "T"; Level1End.Text = "3";
                Level2Prefix.Text = "P"; Level2End.Text = "10";
                break;
        }

        UpdatePreview();
    }

    private void OnFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        UpdatePreview();
    }

    private void OnTwoLevelChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        TwoLevelPanel.Visibility = TwoLevelCheck.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        ZonasGeneradas = GenerateZones();
        PreviewList.ItemsSource = null;
        PreviewList.ItemsSource = ZonasGeneradas;
    }

    private List<string> GenerateZones()
    {
        var result = new List<string>();

        if (TwoLevelCheck?.IsChecked == true)
        {
            if (Level1End == null || Level2End == null) return result;
            if (!int.TryParse(Level1End.Text, out int l1End) || l1End < 1) return result;
            if (!int.TryParse(Level2End.Text, out int l2End) || l2End < 1) return result;

            string l1Prefix = Level1Prefix?.Text?.Trim() ?? "";
            string l2Prefix = Level2Prefix?.Text?.Trim() ?? "";
            string separator = GetSeparator();

            for (int l1 = 1; l1 <= l1End; l1++)
            {
                string l1Name = l1Prefix;
                if (l1Prefix.Length > 0 && char.IsLetter(l1Prefix[^1]))
                {
                    char lastChar = l1Prefix[^1];
                    if (lastChar >= 'A' && lastChar <= 'Z' && (lastChar + l1 - 1) <= 'Z')
                        l1Name = l1Prefix[..^1] + (char)(lastChar + l1 - 1);
                    else if (lastChar >= 'a' && lastChar <= 'z' && (lastChar + l1 - 1) <= 'z')
                        l1Name = l1Prefix[..^1] + (char)(lastChar + l1 - 1);
                }

                for (int l2 = 1; l2 <= l2End; l2++)
                {
                    result.Add($"{l1Name}{separator}{l2Prefix}{l2}");
                }
            }
            return result;
        }

        if (StartBox == null || EndBox == null) return result;
        if (!int.TryParse(StartBox.Text, out int start)) return result;
        if (!int.TryParse(EndBox.Text, out int end)) return result;
        if (!int.TryParse(StepBox?.Text, out int step) || step < 1) step = 1;

        if (start > end) { int tmp = start; start = end; end = tmp; }

        string prefix = PrefixBox?.Text?.Trim() ?? "";
        string suffix = SuffixBox?.Text?.Trim() ?? "";
        string sep = GetSeparator();

        for (int n = start; n <= end; n += step)
        {
            string name = prefix;
            if (!string.IsNullOrEmpty(sep)) name += sep;
            name += n;
            if (!string.IsNullOrEmpty(suffix)) name += suffix;
            result.Add(name);
        }

        return result;
    }

    private string GetSeparator()
    {
        if (SeparatorCombo?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return tag;
        return "";
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        ZonasGeneradas = GenerateZones();
        if (ZonasGeneradas.Count == 0)
        {
            WpfDialogService.Info("Sin zonas", "Revise los valores ingresados. No se generaron zonas.");
            return;
        }
        DialogResult = true;
    }
}