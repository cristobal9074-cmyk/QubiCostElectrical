using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ZonaArtefactoUi : ViewModelBase
{
    public string Id { get; init; } = "";
    public string Nombre { get; init; } = "";
    private int conteo;
    public int Conteo { get => conteo; set { conteo = value; OnPropertyChanged(); OnPropertyChanged(nameof(Etiqueta)); } }
    public string Etiqueta => Conteo > 0 ? $"{Nombre}  {Conteo}" : Nombre;
}

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
    private readonly ObservableCollection<ArtefactoZonaItem> todos = new();
    public ObservableCollection<ArtefactoZonaItem> Visibles { get; } = new();
    public ObservableCollection<ZonaArtefactoUi> Zonas { get; } = new();
    public ObservableCollection<ArtefactoSugerencia> Sugerencias { get; } = new();
    private readonly HashSet<string> nombresNuevos = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> NombresNuevos => nombresNuevos;

    private ZonaArtefactoUi? zonaActiva;
    private int sugerenciaIndex;
    private int cargadoIndex = -1;
    private bool enCantidad;
    private bool syncingSelection;
    private ArtefactoSugerencia? pendiente;

    public CubicacionScArtefactosDialog(CircuitoSCRow circuito, IReadOnlyList<ZonaProyecto> zonasProyecto, CubicacionScConfig config)
    {
        this.circuito = circuito;
        this.config = config;
        InitializeComponent();
        DataContext = this;

        foreach (var a in circuito.Artefactos)
            todos.Add(new ArtefactoZonaItem { Nombre = a.Nombre, Categoria = a.Categoria, ZonaId = a.ZonaId ?? "", Cantidad = a.Cantidad });

        foreach (var piso in ResolverPisos(circuito, zonasProyecto, todos.Select(a => a.ZonaId)))
            Zonas.Add(new ZonaArtefactoUi { Id = piso.Id, Nombre = piso.Nombre });

        var ids = Zonas.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var a in todos.Where(a => !ids.Contains(a.ZonaId ?? "")))
            a.ZonaId = "";

        Titulo.Text = $"Artefactos · Circuito {circuito.Numero}";
        FloorsHost.Visibility = Zonas.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        Subtitulo.Text = Zonas.Count > 1
            ? "Pisos de este circuito"
            : $"Ubicación: {Zonas[0].Nombre}";

        SelectZona(Zonas[0]);
        RefreshSugerencias();
        Loaded += (_, _) => SearchBox.Focus();
    }

    /// <summary>
    /// Pisos que muestra Alt+A: los del circuito (PisosTramos o la ubicación única).
    /// Si hay artefactos en una zona que ya no está, se conserva un grupo "Sin zona".
    /// </summary>
    public static IReadOnlyList<(string Id, string Nombre)> ResolverPisos(
        CircuitoSCRow circuito,
        IReadOnlyList<ZonaProyecto> zonasProyecto,
        IEnumerable<string?> artefactZonaIds)
    {
        var result = new List<(string Id, string Nombre)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (circuito.PisosTramos.Count > 0)
        {
            foreach (var tramo in circuito.PisosTramos)
            {
                var id = tramo.ZonaId ?? "";
                if (!seen.Add(id)) continue;
                var nombre = string.IsNullOrWhiteSpace(tramo.ZonaNombre)
                    ? zonasProyecto.FirstOrDefault(z => z.Id == id)?.Nombre
                    : tramo.ZonaNombre;
                result.Add((id, string.IsNullOrWhiteSpace(nombre) ? "Piso" : nombre));
            }
        }
        else
        {
            var ubic = (circuito.Ubicacion ?? "").Trim();
            if (ubic.Length > 0)
            {
                var match = zonasProyecto.FirstOrDefault(z =>
                    string.Equals(z.Nombre, ubic, StringComparison.OrdinalIgnoreCase));
                if (match != null && seen.Add(match.Id))
                    result.Add((match.Id, match.Nombre));
                else if (seen.Add(""))
                    result.Add(("", ubic));
            }
        }

        var ids = result.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        if (artefactZonaIds.Any(id => !ids.Contains(id ?? "")))
        {
            if (seen.Add(""))
                result.Add(("", result.Count == 0 ? "General" : "Sin zona"));
        }

        if (result.Count == 0)
            result.Add(("", "General"));

        return result;
    }

    private void RefreshConteos()
    {
        foreach (var z in Zonas)
            z.Conteo = (int)Math.Round(todos.Where(a => a.ZonaId == z.Id && a.Cantidad > 0).Sum(a => a.Cantidad));
        var total = (int)Math.Round(todos.Where(a => a.Cantidad > 0).Sum(a => a.Cantidad));
        TotalCircuito.Text = $"Total circuito: {total}";
    }

    private void RefreshVisibles()
    {
        Visibles.Clear();
        if (zonaActiva == null) return;
        LoadedTitle.Text = (Zonas.Count > 1 ? $"EN {zonaActiva.Nombre}" : "EN ESTE CIRCUITO").ToUpperInvariant();
        foreach (var a in todos.Where(a => a.ZonaId == zonaActiva.Id).OrderBy(a => a.Categoria).ThenBy(a => a.Nombre))
            Visibles.Add(a);
        VacioMsg.Visibility = Visibles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (cargadoIndex >= Visibles.Count) cargadoIndex = -1;
    }

    private void SelectZona(ZonaArtefactoUi? zona)
    {
        if (zona == null) return;
        zonaActiva = zona;
        syncingSelection = true;
        ZonasList.SelectedItem = zona;
        syncingSelection = false;
        cargadoIndex = -1;
        RefreshVisibles();
        SyncLists();
        RefreshConteos();
    }

    private void OnZonaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncingSelection) return;
        if (ZonasList.SelectedItem is ZonaArtefactoUi z && z != zonaActiva)
        {
            CerrarCantidad();
            SelectZona(z);
            SearchBox.Focus();
        }
    }

    private void ShiftZona(int delta)
    {
        if (Zonas.Count < 2 || enCantidad) return;
        int i = zonaActiva == null ? 0 : Zonas.IndexOf(zonaActiva);
        i = (i + delta + Zonas.Count) % Zonas.Count;
        SelectZona(Zonas[i]);
        SearchBox.Focus();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (enCantidad) return;
        cargadoIndex = -1;
        sugerenciaIndex = 0;
        RefreshSugerencias();
    }

    private void RefreshSugerencias()
    {
        var q = (SearchBox.Text ?? "").Trim();
        Sugerencias.Clear();
        foreach (var a in config.Artefactos.Where(a => q.Length == 0 || a.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(8))
            Sugerencias.Add(new ArtefactoSugerencia { Nombre = a.Nombre, Categoria = a.Categoria, Detalle = a.Categoria });

        bool existe = config.Artefactos.Any(a => string.Equals(a.Nombre, q, StringComparison.OrdinalIgnoreCase));
        if (q.Length > 0 && !existe)
            Sugerencias.Add(new ArtefactoSugerencia { Nombre = q, Categoria = "Otro", Detalle = "crear nuevo", EsNuevo = true });

        if (sugerenciaIndex >= Sugerencias.Count) sugerenciaIndex = 0;
        SyncLists();
    }

    private void SyncLists()
    {
        syncingSelection = true;
        SugerenciasList.SelectedIndex = cargadoIndex >= 0 || Sugerencias.Count == 0 ? -1 : sugerenciaIndex;
        VisiblesList.SelectedIndex = cargadoIndex;
        syncingSelection = false;
        if (SugerenciasList.SelectedItem != null)
            SugerenciasList.ScrollIntoView(SugerenciasList.SelectedItem);
        if (VisiblesList.SelectedItem != null)
            VisiblesList.ScrollIntoView(VisiblesList.SelectedItem);
    }

    private void MoveHighlight(int delta)
    {
        if (delta > 0)
        {
            if (cargadoIndex >= 0)
                cargadoIndex = Math.Min(Visibles.Count - 1, cargadoIndex + 1);
            else if (sugerenciaIndex < Sugerencias.Count - 1)
                sugerenciaIndex++;
            else if (string.IsNullOrEmpty(SearchBox.Text) && Visibles.Count > 0)
                cargadoIndex = 0;
        }
        else if (cargadoIndex > 0)
            cargadoIndex--;
        else if (cargadoIndex == 0)
            cargadoIndex = -1;
        else
            sugerenciaIndex = Math.Max(0, sugerenciaIndex - 1);
        SyncLists();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            GuardarYCerrar();
            e.Handled = true;
            return;
        }

        if (enCantidad)
        {
            if (e.Key == Key.Enter) { ConfirmarCantidad(); e.Handled = true; }
            return;
        }

        if (Keyboard.FocusedElement is Button)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Right) { ShiftZona(1); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Left) { ShiftZona(-1); e.Handled = true; return; }

        if (e.Key == Key.Down) { MoveHighlight(1); e.Handled = true; return; }
        if (e.Key == Key.Up) { MoveHighlight(-1); e.Handled = true; return; }
        if (e.Key == Key.Enter) { AbrirCantidadDesdeSeleccion(); e.Handled = true; return; }
        if (e.Key == Key.Delete && cargadoIndex >= 0 && string.IsNullOrEmpty(SearchBox.Text))
        {
            QuitarCargado(cargadoIndex);
            e.Handled = true;
        }
    }

    private void OnSugerenciaClick(object sender, MouseButtonEventArgs e)
    {
        if (SugerenciasList.SelectedItem is not ArtefactoSugerencia s) return;
        sugerenciaIndex = Sugerencias.IndexOf(s);
        cargadoIndex = -1;
        AbrirCantidad(s);
    }

    private void OnCargadoClick(object sender, MouseButtonEventArgs e)
    {
        if (VisiblesList.SelectedItem is not ArtefactoZonaItem it) return;
        cargadoIndex = Visibles.IndexOf(it);
        AbrirCantidad(new ArtefactoSugerencia { Nombre = it.Nombre, Categoria = it.Categoria, Detalle = it.Categoria });
    }

    private void AbrirCantidadDesdeSeleccion()
    {
        if (cargadoIndex >= 0 && cargadoIndex < Visibles.Count)
        {
            var it = Visibles[cargadoIndex];
            AbrirCantidad(new ArtefactoSugerencia { Nombre = it.Nombre, Categoria = it.Categoria, Detalle = it.Categoria });
            return;
        }

        if (sugerenciaIndex >= 0 && sugerenciaIndex < Sugerencias.Count)
        {
            AbrirCantidad(Sugerencias[sugerenciaIndex]);
            return;
        }

        var text = (SearchBox.Text ?? "").Trim();
        if (text.Length > 0)
            AbrirCantidad(new ArtefactoSugerencia { Nombre = text, Categoria = "Otro", Detalle = "crear nuevo", EsNuevo = true });
    }

    private void AbrirCantidad(ArtefactoSugerencia sugerencia)
    {
        if (zonaActiva == null || string.IsNullOrWhiteSpace(sugerencia.Nombre)) return;
        pendiente = sugerencia;
        enCantidad = true;
        var existente = BuscarEnZona(sugerencia.Nombre);
        QtyNombre.Text = sugerencia.Nombre;
        QtyCategoria.Text = sugerencia.Categoria;
        QtyPregunta.Text = existente != null
            ? $"Ya hay {FormatQty(existente.Cantidad)} en {zonaActiva.Nombre}. ¿Cuántos quedan?"
            : $"¿Cuántos van en {zonaActiva.Nombre}?";
        QtyBox.Text = FormatQty(existente?.Cantidad ?? 1);
        SearchBox.Visibility = Visibility.Collapsed;
        SugerenciasList.Visibility = Visibility.Collapsed;
        QtyPanel.Visibility = Visibility.Visible;
        QtyBox.Focus();
        QtyBox.SelectAll();
    }

    private void CerrarCantidad()
    {
        enCantidad = false;
        pendiente = null;
        QtyPanel.Visibility = Visibility.Collapsed;
        SearchBox.Visibility = Visibility.Visible;
        SugerenciasList.Visibility = Visibility.Visible;
        SearchBox.Focus();
    }

    private void ConfirmarCantidad()
    {
        if (pendiente == null || zonaActiva == null) return;
        if (!TryParseQty(QtyBox.Text, out var qty))
        {
            QtyBox.SelectAll();
            QtyBox.Focus();
            return;
        }

        AplicarCantidad(pendiente, qty);

        SearchBox.Text = "";
        cargadoIndex = -1;
        sugerenciaIndex = 0;
        CerrarCantidad();
        RefreshVisibles();
        RefreshConteos();
        RefreshSugerencias();
        SearchBox.Focus();
    }

    private void AplicarCantidad(ArtefactoSugerencia sugerencia, decimal qty)
    {
        if (zonaActiva == null) return;
        var nombre = sugerencia.Nombre.Trim();
        var categoria = string.IsNullOrWhiteSpace(sugerencia.Categoria) ? "Otro" : sugerencia.Categoria;
        bool esNuevo = sugerencia.EsNuevo || !config.Artefactos.Any(a => string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        var existente = BuscarEnZona(nombre);

        if (qty <= 0)
        {
            if (existente != null) todos.Remove(existente);
        }
        else if (existente != null)
        {
            existente.Cantidad = qty;
            existente.Categoria = categoria;
        }
        else
        {
            todos.Add(new ArtefactoZonaItem { Nombre = nombre, Categoria = categoria, ZonaId = zonaActiva.Id, Cantidad = qty });
        }

        if (esNuevo && qty > 0)
            nombresNuevos.Add(nombre);
    }

    private ArtefactoZonaItem? BuscarEnZona(string nombre) =>
        zonaActiva == null ? null : todos.FirstOrDefault(a =>
            a.ZonaId == zonaActiva.Id && string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    /// <summary>Cantidad plana o cuenta: 12, 13+5+6+5, 2*4, (10-1)/3.</summary>
    public static bool TryEvaluarCantidad(string? text, out decimal qty)
    {
        var result = QuantityFormulaService.Evaluate(text);
        qty = result.Success ? result.Value : 0m;
        return result.Success;
    }

    private static bool TryParseQty(string? text, out decimal qty) => TryEvaluarCantidad(text, out qty);

    private static string FormatQty(decimal qty) =>
        qty == decimal.Truncate(qty) ? decimal.Truncate(qty).ToString(CultureInfo.InvariantCulture) : qty.ToString(CultureInfo.InvariantCulture);

    private void QuitarCargado(int index)
    {
        if (index < 0 || index >= Visibles.Count) return;
        todos.Remove(Visibles[index]);
        cargadoIndex = -1;
        RefreshVisibles();
        RefreshConteos();
        SyncLists();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void GuardarYCerrar()
    {
        if (enCantidad && !TryAplicarCantidadPendiente())
            return;
        OnAcceptClick(this, new RoutedEventArgs());
    }

    private bool TryAplicarCantidadPendiente()
    {
        if (pendiente == null || zonaActiva == null) return true;
        if (!TryParseQty(QtyBox.Text, out var qty))
        {
            QtyBox.SelectAll();
            QtyBox.Focus();
            return false;
        }

        AplicarCantidad(pendiente, qty);
        enCantidad = false;
        pendiente = null;
        return true;
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (enCantidad && !TryAplicarCantidadPendiente())
            return;

        circuito.Artefactos.Clear();
        foreach (var a in todos.Where(a => a.Cantidad > 0))
            circuito.Artefactos.Add(new ArtefactoSCRow { Nombre = a.Nombre.Trim(), Categoria = a.Categoria, Cantidad = a.Cantidad, ZonaId = a.ZonaId ?? "" });
        DialogResult = true;
    }
}
