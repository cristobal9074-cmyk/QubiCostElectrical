using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        // Inicializar con la primera torre por defecto
        ActualizarTorres(1);
    }

    public List<string> ZonasGeneradas { get; } = new();

    private void OnComboTorresSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboTorres == null || GridPrefijo == null || GridSubComun == null || GridPisosComunes == null)
            return;

        var selectedItem = (ComboBoxItem)ComboTorres.SelectedItem;
        int torres = int.Parse(selectedItem.Tag.ToString()!);

        if (torres > 1)
        {
            GridPrefijo.Visibility = Visibility.Visible;
            GridSubComun.Visibility = Visibility.Visible;
            GridPisosComunes.Visibility = Visibility.Visible;
        }
        else
        {
            GridPrefijo.Visibility = Visibility.Collapsed;
            GridSubComun.Visibility = Visibility.Collapsed;
            GridPisosComunes.Visibility = Visibility.Collapsed;
        }

        ActualizarTorres(torres);
    }

    private void OnComboPrefijoSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboTorres == null || ComboPrefijo == null) return;
        var selectedItem = (ComboBoxItem)ComboTorres.SelectedItem;
        int torres = int.Parse(selectedItem.Tag.ToString()!);
        ActualizarTorres(torres);
    }

    private void ActualizarTorres(int count)
    {
        string prefijoTipo = ComboPrefijo != null && ComboPrefijo.SelectedItem != null
            ? ((ComboBoxItem)ComboPrefijo.SelectedItem).Tag.ToString()!
            : "LETRAS";

        // Guardar valores ingresados previamente para no sobreescribirlos si el usuario sube/baja de torres
        var prevValues = new Dictionary<string, (string Pisos, string Cubiertas)>();
        foreach (var t in torreConfigs)
        {
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

            torreConfigs.Add(new TorreConfigVm(key, nombre, defaultPisos, defaultCubiertas));
        }
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
        string prefijoTipo = ((ComboBoxItem)ComboPrefijo.SelectedItem).Tag.ToString()!;

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
