using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

// Una zona en el diálogo (traída de ZonaProyecto, o sintética "Sin zona"/"General" para no
// perder artefactos sin zona asignada). Conteo se refresca al agregar/quitar para mostrar el
// total de unidades por zona en la pestaña — el control por piso que pidió el usuario.
public sealed class ZonaArtefactoUi : ViewModelBase
{
    public string Id { get; init; } = "";
    public string Nombre { get; init; } = "";
    private int conteo;
    public int Conteo { get => conteo; set { conteo = value; OnPropertyChanged(); OnPropertyChanged(nameof(Etiqueta)); } }
    public string Etiqueta => Conteo > 0 ? $"{Nombre}  ({Conteo})" : Nombre;
}

// Fila editable de un artefacto asignado a una zona.
public sealed class ArtefactoZonaItem : ViewModelBase
{
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "Otro";
    public string ZonaId { get; set; } = "";
    private decimal cantidad = 1;
    public decimal Cantidad { get => cantidad; set { cantidad = value; OnPropertyChanged(); } }
}

public sealed class ArtefactoSugerencia
{
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "Otro";
    public string Detalle { get; set; } = "";
    public bool EsNuevo { get; set; }
}

public partial class CubicacionScArtefactosDialog : Window
{
    private readonly CircuitoSCRow circuito;
    private readonly CubicacionScConfig config;

    // Todos los artefactos (todas las zonas) en edición; se filtran a la zona activa en Visibles.
    private readonly ObservableCollection<ArtefactoZonaItem> todos = new();
    // Artefactos de la zona activa (lo que muestra la lista central).
    public ObservableCollection<ArtefactoZonaItem> Visibles { get; } = new();
    public ObservableCollection<ZonaArtefactoUi> Zonas { get; } = new();
    public ObservableCollection<ArtefactoSugerencia> Sugerencias { get; } = new();
    public ObservableCollection<ArtefactoSugerencia> Frecuentes { get; } = new();

    private readonly HashSet<string> nombresNuevos = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> NombresNuevos => nombresNuevos;

    private ZonaArtefactoUi? zonaActiva;

    public CubicacionScArtefactosDialog(CircuitoSCRow circuito, IReadOnlyList<ZonaProyecto> zonasProyecto, CubicacionScConfig config)
    {
        this.circuito = circuito;
        this.config = config;
        InitializeComponent();
        DataContext = this;

        foreach (var a in circuito.Artefactos)
            todos.Add(new ArtefactoZonaItem { Nombre = a.Nombre, Categoria = a.Categoria, ZonaId = a.ZonaId ?? "", Cantidad = a.Cantidad });

        BuildZonas(zonasProyecto);
        BuildFrecuentes();

        Titulo.Text = $"Artefactos · Circuito {circuito.Numero}";
        Subtitulo.Text = zonasProyecto.Count == 0
            ? "Este proyecto aún no tiene zonas: se usa una zona general. Defina zonas en Cubicación para el desglose por piso."
            : "Escribe y Enter para agregar · \"6 foco\" agrega 6 · F1–F6 frecuentes · Ctrl+←/→ cambia zona";

        SelectZona(Zonas.FirstOrDefault());
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void BuildZonas(IReadOnlyList<ZonaProyecto> zonasProyecto)
    {
        var targetIds = new HashSet<string>(circuito.PisosTramos.Select(t => t.ZonaId), StringComparer.Ordinal);
        if (targetIds.Count == 0 && !string.IsNullOrWhiteSpace(circuito.Ubicacion))
        {
            var singleMatch = zonasProyecto.FirstOrDefault(z => 
                string.Equals(z.Nombre, circuito.Ubicacion, StringComparison.OrdinalIgnoreCase) || 
                z.Nombre.Contains(circuito.Ubicacion, StringComparison.OrdinalIgnoreCase));
            if (singleMatch != null)
                targetIds.Add(singleMatch.Id);
        }

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var sortedZonas = zonasProyecto
            .Where(z => z.Activa)
            .OrderBy(z => targetIds.Contains(z.Id) ? 0 : 1)
            .ThenBy(z => z.Orden);

        foreach (var z in sortedZonas)
        {
            if (vistos.Add(z.Id))
            {
                string suffix = targetIds.Contains(z.Id) ? " ⭐" : "";
                Zonas.Add(new ZonaArtefactoUi { Id = z.Id, Nombre = z.Nombre + suffix });
            }
        }

        // Zonas huérfanas: artefactos con ZonaId que ya no está entre las zonas del proyecto
        // (o vacío) — se agrupan en "Sin zona" para no perderlos.
        bool hayHuerfanos = todos.Any(a => !vistos.Contains(a.ZonaId));
        if (hayHuerfanos || Zonas.Count == 0)
        {
            string nombre = Zonas.Count == 0 ? "General" : "Sin zona";
            if (vistos.Add(""))
                Zonas.Insert(0, new ZonaArtefactoUi { Id = "", Nombre = nombre });
            // Reasigna cualquier ZonaId desconocido a "" para que caiga en el bucket sin zona.
            foreach (var a in todos.Where(a => !zonasProyecto.Any(z => z.Id == a.ZonaId)))
                a.ZonaId = "";
        }

        RefreshConteos();
    }

    private void BuildFrecuentes()
    {
        foreach (var a in config.Artefactos.Take(6))
            Frecuentes.Add(new ArtefactoSugerencia { Nombre = a.Nombre, Categoria = a.Categoria });
    }

    private void RefreshConteos()
    {
        foreach (var z in Zonas)
            z.Conteo = (int)Math.Round(todos.Where(a => a.ZonaId == z.Id && a.Cantidad > 0).Sum(a => a.Cantidad));
        TotalCircuito.Text = ((int)Math.Round(todos.Where(a => a.Cantidad > 0).Sum(a => a.Cantidad))).ToString();
    }

    private void RefreshVisibles()
    {
        Visibles.Clear();
        if (zonaActiva == null) return;
        foreach (var a in todos.Where(a => a.ZonaId == zonaActiva.Id).OrderBy(a => a.Categoria).ThenBy(a => a.Nombre))
            Visibles.Add(a);
        VacioMsg.Visibility = Visibles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectZona(ZonaArtefactoUi? zona)
    {
        if (zona == null) return;
        zonaActiva = zona;
        ZonasList.SelectedItem = zona;
        RefreshVisibles();
    }

    private void OnZonaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ZonasList.SelectedItem is ZonaArtefactoUi z && z != zonaActiva)
        {
            zonaActiva = z;
            RefreshVisibles();
            SearchBox.Focus();
        }
    }

    private void ShiftZona(int delta)
    {
        if (Zonas.Count == 0) return;
        int i = zonaActiva == null ? 0 : Zonas.IndexOf(zonaActiva);
        i = (i + delta + Zonas.Count) % Zonas.Count;
        SelectZona(Zonas[i]);
        SearchBox.Focus();
    }

    // Parsea "6 foco embutido" → (6, "foco embutido"). Sin número inicial → (1, texto).
    private static (decimal qty, string text) ParseEntrada(string raw)
    {
        raw = (raw ?? "").Trim();
        int sp = raw.IndexOf(' ');
        if (sp > 0 && decimal.TryParse(raw[..sp], out var q) && q > 0)
            return (Math.Truncate(q), raw[(sp + 1)..].Trim());
        return (1m, raw);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var (_, text) = ParseEntrada(SearchBox.Text);
        Sugerencias.Clear();
        var q = text.Trim();
        var matches = config.Artefactos
            .Where(a => string.IsNullOrEmpty(q) || a.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(8);
        foreach (var a in matches)
            Sugerencias.Add(new ArtefactoSugerencia { Nombre = a.Nombre, Categoria = a.Categoria, Detalle = a.Categoria });

        bool existe = config.Artefactos.Any(a => string.Equals(a.Nombre, q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q) && !existe)
            Sugerencias.Add(new ArtefactoSugerencia { Nombre = q, Categoria = "Otro", Detalle = "crear nuevo", EsNuevo = true });

        SugerenciasPopup.IsOpen = Sugerencias.Count > 0 && SearchBox.IsKeyboardFocusWithin;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitSearch();
                e.Handled = true;
                break;
            case Key.Down when Sugerencias.Count > 0:
                SugerenciasList.SelectedIndex = 0;
                SugerenciasList.Focus();
                (SugerenciasList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                if (SugerenciasPopup.IsOpen) { SugerenciasPopup.IsOpen = false; e.Handled = true; }
                break;
        }
    }

    private void CommitSearch()
    {
        var (qty, text) = ParseEntrada(SearchBox.Text);
        if (string.IsNullOrWhiteSpace(text)) return;

        // Si la lista de sugerencias tiene selección la usamos; si no, el primer match; si no, crear.
        var elegido = SugerenciasList.SelectedItem as ArtefactoSugerencia
                      ?? Sugerencias.FirstOrDefault(s => !s.EsNuevo)
                      ?? new ArtefactoSugerencia { Nombre = text, Categoria = "Otro", EsNuevo = true };
        AddArtefacto(elegido.Nombre, elegido.Categoria, qty, elegido.EsNuevo || !config.Artefactos.Any(a => string.Equals(a.Nombre, elegido.Nombre, StringComparison.OrdinalIgnoreCase)));
    }

    private void OnSugerenciaClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArtefactoSugerencia s)
        {
            var (qty, _) = ParseEntrada(SearchBox.Text);
            AddArtefacto(s.Nombre, s.Categoria, qty, s.EsNuevo);
        }
    }

    private void OnSugerenciaKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SugerenciasList.SelectedItem is ArtefactoSugerencia s)
        {
            var (qty, _) = ParseEntrada(SearchBox.Text);
            AddArtefacto(s.Nombre, s.Categoria, qty, s.EsNuevo);
            e.Handled = true;
        }
    }

    private void OnFrecuenteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArtefactoSugerencia s)
            AddArtefacto(s.Nombre, s.Categoria, 1, false);
    }

    private void AddArtefacto(string nombre, string categoria, decimal qty, bool esNuevo)
    {
        nombre = (nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre) || zonaActiva == null) return;
        if (qty <= 0) qty = 1;

        var existente = todos.FirstOrDefault(a =>
            a.ZonaId == zonaActiva.Id && string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        if (existente != null)
            existente.Cantidad += qty;
        else
            todos.Add(new ArtefactoZonaItem { Nombre = nombre, Categoria = string.IsNullOrWhiteSpace(categoria) ? "Otro" : categoria, ZonaId = zonaActiva.Id, Cantidad = qty });

        if (esNuevo)
            nombresNuevos.Add(nombre);

        SearchBox.Text = "";
        SugerenciasPopup.IsOpen = false;
        RefreshVisibles();
        RefreshConteos();
        SearchBox.Focus();
    }

    private void OnMasClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArtefactoZonaItem it)
        { it.Cantidad += 1; RefreshConteos(); }
    }

    private void OnMenosClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArtefactoZonaItem it)
        {
            it.Cantidad -= 1;
            if (it.Cantidad <= 0) Remove(it);
            RefreshConteos();
        }
    }

    private void OnQuitarClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArtefactoZonaItem it)
            Remove(it);
    }

    private void Remove(ArtefactoZonaItem it)
    {
        todos.Remove(it);
        Visibles.Remove(it);
        VacioMsg.Visibility = Visibles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshConteos();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (SugerenciasPopup.IsOpen) return; // Escape cierra el popup primero (lo maneja el TextBox)
            DialogResult = false;
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Right) { ShiftZona(1); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Left) { ShiftZona(-1); e.Handled = true; return; }
        if (e.Key is >= Key.F1 and <= Key.F6)
        {
            int idx = e.Key - Key.F1;
            if (idx < Frecuentes.Count) { AddArtefacto(Frecuentes[idx].Nombre, Frecuentes[idx].Categoria, 1, false); e.Handled = true; }
        }
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        circuito.Artefactos.Clear();
        foreach (var a in todos.Where(a => a.Cantidad > 0))
            circuito.Artefactos.Add(new ArtefactoSCRow { Nombre = a.Nombre.Trim(), Categoria = a.Categoria, Cantidad = a.Cantidad, ZonaId = a.ZonaId ?? "" });
        DialogResult = true;
    }
}
