using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

// Wrapper mutable para listas de un solo texto (Conductores, Secciones, Tipos de tubería,
// Diámetros, Bandejas, Cajas): un DataGridTextColumn no puede editar un string suelto por
// Binding="{Binding}" (los string son inmutables), necesita una propiedad settable.
public sealed class CatalogTextItem : INotifyPropertyChanged
{
    private string value = "";

    public string Value
    {
        get => value;
        set
        {
            if (this.value == value) return;
            this.value = value ?? "";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class CubicacionScCatalogoDialog : Window
{
    public CubicacionScConfig? Result { get; private set; }

    // "Tipos" (abreviaciones A/E/CL/C/F de Sist.) y "TiposRecorrido" (Tubería/Bandeja) no se
    // editan desde este catálogo — no forman parte de las 9 categorías expuestas acá. Se
    // conservan del config original para no perderlos al guardar.
    private readonly System.Collections.Generic.List<string> tiposOriginal;
    private readonly System.Collections.Generic.List<string> tiposRecorridoOriginal;

    private readonly ObservableCollection<CatalogTextItem> conductores = new();
    private readonly ObservableCollection<CatalogTextItem> secciones = new();
    private readonly ObservableCollection<CatalogTextItem> tiposTuberia = new();
    private readonly ObservableCollection<CatalogTextItem> diametros = new();
    private readonly ObservableCollection<CatalogTextItem> bandejas = new();
    private readonly ObservableCollection<CatalogTextItem> cajas = new();
    private readonly ObservableCollection<SistemaCircuitoSC> sistemasCircuito = new();
    private readonly ObservableCollection<SistemaSC> sistemas = new();
    private readonly ObservableCollection<FlexibleMapSC> flexibles = new();
    private readonly ObservableCollection<ArtefactoCatalogoItem> artefactos = new();

    private string activeTag = "Conductores";
    private ICollectionView? activeView;

    public CubicacionScCatalogoDialog(CubicacionScConfig config, string? projectName = null)
    {
        InitializeComponent();

        tiposOriginal = config.Tipos;
        tiposRecorridoOriginal = config.TiposRecorrido;

        FillSimple(conductores, config.TiposConductor);
        FillSimple(secciones, config.SeccionesConductor);
        FillSimple(tiposTuberia, config.TiposTuberia);
        FillSimple(diametros, config.DiametrosTuberia);
        FillSimple(bandejas, config.TiposBandeja);
        FillSimple(cajas, config.TiposCaja);
        var scSource = config.SistemasCircuito != null && config.SistemasCircuito.Count > 0
            ? config.SistemasCircuito
            : CubicacionScConfig.Default().SistemasCircuito;
        foreach (var sc in scSource)
            sistemasCircuito.Add(new SistemaCircuitoSC { Codigo = sc.Codigo, Nombre = sc.Nombre });
        foreach (var s in config.Sistemas)
            sistemas.Add(new SistemaSC { Nombre = s.Nombre, NumConductores = s.NumConductores, MermaPct = s.MermaPct });
        foreach (var f in config.Flexibles)
            flexibles.Add(new FlexibleMapSC { Diametro = f.Diametro, TipoFlexible = f.TipoFlexible, MetrosPorCaja = f.MetrosPorCaja });
        foreach (var a in config.Artefactos)
            artefactos.Add(new ArtefactoCatalogoItem { Nombre = a.Nombre, Categoria = a.Categoria });

        ProyectoNombreRun.Text = string.IsNullOrWhiteSpace(projectName) ? "—" : projectName.Trim();

        HookCollection(conductores);
        HookCollection(secciones);
        HookCollection(tiposTuberia);
        HookCollection(diametros);
        HookCollection(bandejas);
        HookCollection(cajas);
        HookCollection(sistemasCircuito);
        HookCollection(sistemas);
        HookCollection(flexibles);
        HookCollection(artefactos);

        RefreshCounts();
        CategoriaList.SelectedIndex = 0;
    }

    private void SimpleGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is DataGrid grid
            && DataGridExcelNavigation.TryBeginEditFromChar(grid, e.Text))
        {
            e.Handled = true;
        }
    }

    private void HookCollection(INotifyCollectionChanged collection) =>
        collection.CollectionChanged += (_, _) => RefreshCounts();

    private static void FillSimple(ObservableCollection<CatalogTextItem> target, System.Collections.Generic.List<string> source)
    {
        foreach (var value in source)
            target.Add(new CatalogTextItem { Value = value });
    }

    private void RefreshCounts()
    {
        CountConductores.Text = conductores.Count.ToString();
        CountSecciones.Text = secciones.Count.ToString();
        CountTiposTuberia.Text = tiposTuberia.Count.ToString();
        CountDiametros.Text = diametros.Count.ToString();
        CountBandejas.Text = bandejas.Count.ToString();
        CountCajas.Text = cajas.Count.ToString();
        CountSistemasCircuito.Text = sistemasCircuito.Count.ToString();
        CountSistemas.Text = sistemas.Count.ToString();
        CountFlexibles.Text = flexibles.Count.ToString();
        CountArtefactos.Text = artefactos.Count.ToString();
        TotalItemsRun.Text = (
            conductores.Count + secciones.Count + tiposTuberia.Count + diametros.Count +
            bandejas.Count + cajas.Count + sistemasCircuito.Count + sistemas.Count + flexibles.Count + artefactos.Count
        ).ToString();
    }

    private void OnCategoriaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoriaList.SelectedItem is not ListBoxItem item || item.Tag is not string tag)
            return;

        activeTag = tag;
        SearchBox.Text = "";

        SimpleGrid.Visibility = Visibility.Collapsed;
        SistemasCircuitoGrid.Visibility = Visibility.Collapsed;
        SistemasGrid.Visibility = Visibility.Collapsed;
        FlexiblesGrid.Visibility = Visibility.Collapsed;
        ArtefactosGrid.Visibility = Visibility.Collapsed;

        switch (tag)
        {
            case "Conductores":
                SetCategory("Conductores", "Tipos de cable disponibles en la planilla de circuitos", SimpleGrid, conductores);
                break;
            case "Secciones":
                SetCategory("Secciones de conductor", "Secciones normalizadas (mm²) para tramos bandeja / PVC", SimpleGrid, secciones);
                break;
            case "TiposTuberia":
                SetCategory("Tipos de tubería", "Materiales de ducto usados en tramos PVC / conduit", SimpleGrid, tiposTuberia);
                break;
            case "Diametros":
                SetCategory("Diámetros de tubería", "Diámetros disponibles para ductos", SimpleGrid, diametros);
                break;
            case "Bandejas":
                SetCategory("Tipos de bandeja", "Bandejas portaconductores en medidas de entrada", SimpleGrid, bandejas);
                break;
            case "Cajas":
                SetCategory("Cajas", "Tipos de caja de derivación / registro", SimpleGrid, cajas);
                break;
            case "SistemasCircuito":
                SetCategory("Sistemas de circuitos (SIST)", "Códigos de sistema y destino (A = Alumbrado, E = Enchufe, CL = Clima, CO = Computación...)", SistemasCircuitoGrid, sistemasCircuito);
                break;
            case "Sistemas":
                SetCategory("Alimentación (ALIM)", "Códigos de alimentación y merma por fase (M, T, EM)", SistemasGrid, sistemas);
                break;
            case "Flexibles":
                SetCategory("Terminales flexibles", "Relación diámetro ducto → flexible y metros/caja", FlexiblesGrid, flexibles);
                break;
            case "Artefactos":
                SetCategory("Artefactos", "Artefactos por zona en la planilla SC", ArtefactosGrid, artefactos);
                break;
        }
    }

    private void SetCategory(string title, string subtitle, DataGrid grid, IEnumerable itemsSource)
    {
        CategoriaTitulo.Text = title;
        CategoriaSubtitulo.Text = subtitle;
        grid.ItemsSource = itemsSource;
        grid.Visibility = Visibility.Visible;
        activeView = CollectionViewSource.GetDefaultView(itemsSource);
        activeView.Filter = FilterActiveItem;
        activeView.Refresh();
    }

    private bool FilterActiveItem(object obj)
    {
        string q = (SearchBox.Text ?? "").Trim();
        if (q.Length == 0)
            return true;

        return obj switch
        {
            CatalogTextItem t => Contains(t.Value, q),
            SistemaCircuitoSC sc => Contains(sc.Codigo, q) || Contains(sc.Nombre, q),
            SistemaSC s => Contains(s.Nombre, q) || Contains(s.NumConductores.ToString(), q) || Contains(s.MermaPct.ToString(), q),
            FlexibleMapSC f => Contains(f.Diametro, q) || Contains(f.TipoFlexible, q) || Contains(f.MetrosPorCaja.ToString(), q),
            ArtefactoCatalogoItem a => Contains(a.Nombre, q) || Contains(a.Categoria, q),
            _ => true
        };
    }

    private static bool Contains(string? haystack, string needle) =>
        (haystack ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        activeView?.Refresh();
    }

    private DataGrid? ActiveGrid()
    {
        if (SimpleGrid.Visibility == Visibility.Visible) return SimpleGrid;
        if (SistemasCircuitoGrid.Visibility == Visibility.Visible) return SistemasCircuitoGrid;
        if (SistemasGrid.Visibility == Visibility.Visible) return SistemasGrid;
        if (FlexiblesGrid.Visibility == Visibility.Visible) return FlexiblesGrid;
        if (ArtefactosGrid.Visibility == Visibility.Visible) return ArtefactosGrid;
        return null;
    }

    private void OnAgregarFilaClick(object sender, RoutedEventArgs e)
    {
        switch (activeTag)
        {
            case "Conductores":
                conductores.Add(new CatalogTextItem { Value = "NUEVO" });
                break;
            case "Secciones":
                secciones.Add(new CatalogTextItem { Value = "" });
                break;
            case "TiposTuberia":
                tiposTuberia.Add(new CatalogTextItem { Value = "" });
                break;
            case "Diametros":
                diametros.Add(new CatalogTextItem { Value = "" });
                break;
            case "Bandejas":
                bandejas.Add(new CatalogTextItem { Value = "" });
                break;
            case "Cajas":
                cajas.Add(new CatalogTextItem { Value = "" });
                break;
            case "SistemasCircuito":
                sistemasCircuito.Add(new SistemaCircuitoSC { Codigo = "", Nombre = "" });
                break;
            case "Sistemas":
                sistemas.Add(new SistemaSC { Nombre = "" });
                break;
            case "Flexibles":
                flexibles.Add(new FlexibleMapSC { Diametro = "" });
                break;
            case "Artefactos":
                artefactos.Add(new ArtefactoCatalogoItem { Nombre = "" });
                break;
        }

        activeView?.Refresh();
        var grid = ActiveGrid();
        if (grid != null && grid.Items.Count > 0)
        {
            grid.SelectedIndex = grid.Items.Count - 1;
            grid.ScrollIntoView(grid.SelectedItem);
            grid.Focus();
        }
    }

    private void OnQuitarClick(object sender, RoutedEventArgs e) => RemoveSelectedRows();

    private void OnDuplicarClick(object sender, RoutedEventArgs e)
    {
        var grid = ActiveGrid();
        if (grid == null || grid.SelectedItems.Count == 0)
            return;

        var selected = grid.SelectedItems.Cast<object>().ToList();
        foreach (var item in selected)
        {
            switch (item)
            {
                case CatalogTextItem t:
                    GetActiveSimpleCollection()?.Add(new CatalogTextItem { Value = t.Value });
                    break;
                case SistemaCircuitoSC sc:
                    sistemasCircuito.Add(new SistemaCircuitoSC { Codigo = sc.Codigo, Nombre = sc.Nombre });
                    break;
                case SistemaSC s:
                    sistemas.Add(new SistemaSC { Nombre = s.Nombre, NumConductores = s.NumConductores, MermaPct = s.MermaPct });
                    break;
                case FlexibleMapSC f:
                    flexibles.Add(new FlexibleMapSC { Diametro = f.Diametro, TipoFlexible = f.TipoFlexible, MetrosPorCaja = f.MetrosPorCaja });
                    break;
                case ArtefactoCatalogoItem a:
                    artefactos.Add(new ArtefactoCatalogoItem { Nombre = a.Nombre, Categoria = a.Categoria });
                    break;
            }
        }

        activeView?.Refresh();
    }

    private ObservableCollection<CatalogTextItem>? GetActiveSimpleCollection() => activeTag switch
    {
        "Conductores" => conductores,
        "Secciones" => secciones,
        "TiposTuberia" => tiposTuberia,
        "Diametros" => diametros,
        "Bandejas" => bandejas,
        "Cajas" => cajas,
        _ => null
    };

    private void RemoveSelectedRows()
    {
        var grid = ActiveGrid();
        if (grid == null || grid.SelectedItems.Count == 0)
            return;

        var selected = grid.SelectedItems.Cast<object>().ToList();
        foreach (var item in selected)
        {
            switch (item)
            {
                case CatalogTextItem t:
                    GetActiveSimpleCollection()?.Remove(t);
                    break;
                case SistemaCircuitoSC sc:
                    sistemasCircuito.Remove(sc);
                    break;
                case SistemaSC s:
                    sistemas.Remove(s);
                    break;
                case FlexibleMapSC f:
                    flexibles.Remove(f);
                    break;
                case ArtefactoCatalogoItem a:
                    artefactos.Remove(a);
                    break;
            }
        }

        activeView?.Refresh();
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        var validSistemasCircuito = sistemasCircuito.Where(s => !string.IsNullOrWhiteSpace(s.Codigo)).ToList();
        Result = new CubicacionScConfig
        {
            SistemasCircuito = validSistemasCircuito,
            Tipos = validSistemasCircuito.Select(s => s.Codigo).ToList(),
            TiposRecorrido = tiposRecorridoOriginal,
            TiposBandeja = ToStrings(bandejas),
            TiposTuberia = ToStrings(tiposTuberia),
            DiametrosTuberia = ToStrings(diametros),
            TiposConductor = ToStrings(conductores),
            SeccionesConductor = ToStrings(secciones),
            TiposCaja = ToStrings(cajas),
            Sistemas = sistemas.Where(s => !string.IsNullOrWhiteSpace(s.Nombre)).ToList(),
            Flexibles = flexibles.Where(f => !string.IsNullOrWhiteSpace(f.Diametro)).ToList(),
            Artefactos = artefactos.Where(a => !string.IsNullOrWhiteSpace(a.Nombre)).ToList(),
        };
        DialogResult = true;
    }

    private static System.Collections.Generic.List<string> ToStrings(ObservableCollection<CatalogTextItem> items) =>
        items.Select(i => i.Value?.Trim() ?? "").Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Oem2 || (e.Key == Key.Divide) || (e.Key == Key.D7 && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift))
        {
            // "/" enfoca búsqueda (como en el HTML), salvo si ya se escribe en un TextBox/DataGrid
            if (Keyboard.FocusedElement is not TextBox)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OnAcceptClick(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
        {
            RemoveSelectedRows();
            e.Handled = true;
        }
    }
}
