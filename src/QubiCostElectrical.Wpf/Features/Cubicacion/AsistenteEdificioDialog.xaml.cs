using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class AsistenteEdificioDialog : Window
{
    private readonly ObservableCollection<TorreConfigVm> torreConfigs = new();

    public AsistenteEdificioDialog()
    {
        InitializeComponent();
        ItemsTorres.ItemsSource = torreConfigs;
        ActualizarTorres(1);
        Loaded += (_, _) => Refresh3DModel();
    }

    public List<string> ZonasGeneradas { get; } = new();

    private void OnComboTorresSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboTorres == null || PanelTorresMultiples == null)
            return;

        var selectedItem = (ComboBoxItem)ComboTorres.SelectedItem;
        int torres = int.Parse(selectedItem.Tag.ToString()!);

        PanelTorresMultiples.Visibility = torres > 1 ? Visibility.Visible : Visibility.Collapsed;

        ActualizarTorres(torres);
        Refresh3DModel();
    }

    private void OnComboPrefijoSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboTorres == null || ComboPrefijo == null) return;
        var selectedItem = (ComboBoxItem)ComboTorres.SelectedItem;
        int torres = int.Parse(selectedItem.Tag.ToString()!);
        ActualizarTorres(torres);
        Refresh3DModel();
    }

    private void ActualizarTorres(int count)
    {
        string prefijoTipo = ComboPrefijo != null && ComboPrefijo.SelectedItem != null
            ? ((ComboBoxItem)ComboPrefijo.SelectedItem).Tag.ToString()!
            : "LETRAS";

        var prevValues = new Dictionary<string, (string Pisos, string Cubiertas)>();
        foreach (var t in torreConfigs)
        {
            t.PropertyChanged -= OnTorrePropertyChanged;
            prevValues[t.Key] = (t.Pisos, t.Cubiertas);
        }

        torreConfigs.Clear();
        for (int i = 0; i < count; i++)
        {
            string nombre = count == 1 ? "Edificio Principal" : $"Torre {(prefijoTipo == "LETRAS" ? ((char)('A' + i)).ToString() : (i + 1).ToString())}";
            string key = $"T{i}";

            string defaultPisos = "20";
            string defaultCubiertas = "1";

            if (prevValues.TryGetValue(key, out var prev))
            {
                defaultPisos = prev.Pisos;
                defaultCubiertas = prev.Cubiertas;
            }

            var vm = new TorreConfigVm(key, nombre, defaultPisos, defaultCubiertas);
            vm.PropertyChanged += OnTorrePropertyChanged;
            torreConfigs.Add(vm);
        }
    }

    private void OnTorrePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ChkCompartirTodosPisos?.IsChecked == true)
        {
            SyncPisosComunesWithMax();
        }
        Refresh3DModel();
    }

    private void OnCompartirTodosPisosChanged(object sender, RoutedEventArgs e)
    {
        bool allShared = ChkCompartirTodosPisos?.IsChecked == true;
        if (TxtPisosComunes != null)
        {
            TxtPisosComunes.IsEnabled = !allShared;
        }

        if (allShared)
        {
            SyncPisosComunesWithMax();
        }
        Refresh3DModel();
    }

    private void SyncPisosComunesWithMax()
    {
        int maxP = 0;
        foreach (var t in torreConfigs)
        {
            if (int.TryParse(t.Pisos, out int p) && p > maxP)
                maxP = p;
        }
        if (maxP > 0 && TxtPisosComunes != null)
        {
            TxtPisosComunes.Text = maxP.ToString();
        }
    }

    private void OnSubterraneosMinusClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtSubterraneos.Text, out int val) && val > 0)
        {
            TxtSubterraneos.Text = (val - 1).ToString();
        }
    }

    private void OnSubterraneosPlusClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtSubterraneos.Text, out int val) && val < BuildingZoneGenerator.MaxSubterraneos)
        {
            TxtSubterraneos.Text = (val + 1).ToString();
        }
    }

    private void OnPisosComunesMinusClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtPisosComunes.Text, out int val) && val > 0)
        {
            TxtPisosComunes.Text = (val - 1).ToString();
        }
    }

    private void OnPisosComunesPlusClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtPisosComunes.Text, out int val) && val < BuildingZoneGenerator.MaxPisosComunes)
        {
            TxtPisosComunes.Text = (val + 1).ToString();
        }
    }

    private void OnSubterraneosTextChanged(object sender, TextChangedEventArgs e) => Refresh3DModel();

    private void OnPisosComunesTextChanged(object sender, TextChangedEventArgs e) => Refresh3DModel();

    private void OnSubComunChanged(object sender, RoutedEventArgs e) => Refresh3DModel();

    private void Refresh3DModel()
    {
        if (Viewport3D == null || ComboTorres == null)
            return;

        int.TryParse(TxtSubterraneos.Text, out int subterraneos);
        int torresCount = int.Parse(((ComboBoxItem)ComboTorres.SelectedItem).Tag.ToString()!);
        int.TryParse(TxtPisosComunes?.Text ?? "0", out int pisosComunes);
        bool subComun = ChkSubComun?.IsChecked == true;

        var torresConfig = new List<TorreZonaConfig>();
        foreach (var t in torreConfigs)
        {
            int.TryParse(t.Pisos, out int p);
            int.TryParse(t.Cubiertas, out int c);
            torresConfig.Add(new TorreZonaConfig { Nombre = t.Nombre, Pisos = p, Cubiertas = c });
        }

        string prefijoTipo = ComboPrefijo?.SelectedItem != null
            ? ((ComboBoxItem)ComboPrefijo.SelectedItem).Tag.ToString()!
            : "LETRAS";

        int totalZonas = 0;
        try
        {
            var simulatedZones = BuildingZoneGenerator.GenerateZones(subterraneos, torresCount, pisosComunes, subComun, prefijoTipo, torresConfig);
            totalZonas = simulatedZones.Count;
        }
        catch
        {
            // Ignorar errores parciales mientras el usuario tipea
        }

        Viewport3D.UpdateModel(subterraneos, torresCount, pisosComunes, subComun, torresConfig, totalZonas);
    }

    private void OnGenerarClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtSubterraneos.Text, out int subterraneos))
        {
            WpfDialogService.Info("Valor incorrecto", $"Ingrese una cantidad válida de subterráneos (0 - {BuildingZoneGenerator.MaxSubterraneos}).");
            return;
        }

        int torresCount = int.Parse(((ComboBoxItem)ComboTorres.SelectedItem).Tag.ToString()!);
        int pisosComunes = 0;

        if (torresCount > 1 && !int.TryParse(TxtPisosComunes.Text, out pisosComunes))
        {
            WpfDialogService.Info("Valor incorrecto", $"Ingrese una cantidad válida de pisos comunes (0 - {BuildingZoneGenerator.MaxPisosComunes}).");
            return;
        }

        var torresConfig = new List<TorreZonaConfig>();
        foreach (var t in torreConfigs)
        {
            if (!int.TryParse(t.Pisos, out int p))
            {
                WpfDialogService.Info("Valor incorrecto", $"Ingrese una cantidad válida de pisos para la {t.Nombre} (0 - {BuildingZoneGenerator.MaxPisosTorre}).");
                return;
            }
            if (!int.TryParse(t.Cubiertas, out int c))
            {
                WpfDialogService.Info("Valor incorrecto", $"Ingrese una cantidad válida de cubiertas para la {t.Nombre} (0 - {BuildingZoneGenerator.MaxCubiertas}).");
                return;
            }
            torresConfig.Add(new TorreZonaConfig { Nombre = t.Nombre, Pisos = p, Cubiertas = c });
        }

        string? validationError = BuildingZoneGenerator.Validate(subterraneos, torresCount, pisosComunes, torresConfig);
        if (validationError != null)
        {
            WpfDialogService.Info("Valor incorrecto", validationError);
            return;
        }

        bool subComun = ChkSubComun.IsChecked == true;
        string prefijoTipo = ComboPrefijo?.SelectedItem != null
            ? ((ComboBoxItem)ComboPrefijo.SelectedItem).Tag.ToString()!
            : "LETRAS";

        ZonasGeneradas.Clear();
        ZonasGeneradas.AddRange(BuildingZoneGenerator.GenerateZones(subterraneos, torresCount, pisosComunes, subComun, prefijoTipo, torresConfig));

        DialogResult = true;
    }

    public class TorreConfigVm : INotifyPropertyChanged
    {
        private string pisos;
        private string cubiertas;

        public TorreConfigVm(string key, string nombre, string pisos, string cubiertas)
        {
            Key = key;
            Nombre = nombre;
            this.pisos = pisos;
            this.cubiertas = cubiertas;
        }

        public string Key { get; }
        public string Nombre { get; }

        public string Pisos
        {
            get => pisos;
            set { pisos = value; Raise(nameof(Pisos)); }
        }

        public string Cubiertas
        {
            get => cubiertas;
            set { cubiertas = value; Raise(nameof(Cubiertas)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
