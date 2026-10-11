using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScResumenDialog : Window, INotifyPropertyChanged
{
    private readonly IReadOnlyList<CuadroCargaSCRow> _cuadrosGenerales;
    private readonly IReadOnlyList<CuadroCargaSCRow> _cuadrosDeptos;
    private readonly IReadOnlyList<ZonaProyecto> _zonas;
    private readonly CubicacionScConfig _config;
    private bool _isPerspectiveGenerales = true;
    private int _tab;
    private string _filtro = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsPerspectiveGenerales
    {
        get => _isPerspectiveGenerales;
        set
        {
            if (_isPerspectiveGenerales == value) return;
            _isPerspectiveGenerales = value;
            RecalculatePerspective();
        }
    }

    public bool IsPerspectiveDeptos => !_isPerspectiveGenerales;

    public string Eyebrow => IsPerspectiveGenerales ? "Servicios comunes" : "Departamentos";

    public string ViewTitle => _tab switch
    {
        1 => "Corte por tablero",
        2 => "Corte por piso",
        3 => "Artefactos por piso",
        _ => "Resumen de materiales"
    };

    public string PerspectiveSubtitle { get; private set; } = "";

    public string Hint => _tab switch
    {
        1 => "Un bloque por cuadro de carga.",
        2 => "Tubería y conductor por piso.",
        3 => "La misma cantidad del total, abierta por zona.",
        _ => "Agrupado por canalización, cajas y artefactos."
    };

    public bool IsTabTotal => _tab == 0;
    public bool IsTabTablero => _tab == 1;
    public bool IsTabPiso => _tab == 2;
    public bool IsTabArtefactos => _tab == 3;

    public string Filtro
    {
        get => _filtro;
        set
        {
            var next = value ?? "";
            if (_filtro == next) return;
            _filtro = next;
            ReconstruirListas();
            OnPropertyChanged();
        }
    }

    public CubicacionSCEngine.ResumenGrupoSC Total { get; private set; } = null!;
    public IReadOnlyList<CubicacionSCEngine.ResumenGrupoSC> PorTablero { get; private set; } = Array.Empty<CubicacionSCEngine.ResumenGrupoSC>();
    public IReadOnlyList<CubicacionSCEngine.ResumenGrupoSC> PorPiso { get; private set; } = Array.Empty<CubicacionSCEngine.ResumenGrupoSC>();
    public IReadOnlyList<CubicacionSCEngine.ZonaResumenSC> ArtefactosPorPiso { get; private set; } = Array.Empty<CubicacionSCEngine.ZonaResumenSC>();

    public IReadOnlyList<ResumenLineaVm> LineasTotal { get; private set; } = Array.Empty<ResumenLineaVm>();
    public IReadOnlyList<ResumenCorteVm> CortesTablero { get; private set; } = Array.Empty<ResumenCorteVm>();
    public IReadOnlyList<ResumenCorteVm> CortesPiso { get; private set; } = Array.Empty<ResumenCorteVm>();
    public IReadOnlyList<ResumenZonaVm> ZonasArtefactos { get; private set; } = Array.Empty<ResumenZonaVm>();

    public bool TotalSinFilas => LineasTotal.Count == 0;
    public bool TableroSinFilas => CortesTablero.Count == 0;
    public bool PisoSinFilas => CortesPiso.Count == 0;
    public bool ArtefactosSinFilas => ZonasArtefactos.Count == 0;

    public string TotalVacioTexto => string.IsNullOrWhiteSpace(Filtro)
        ? "Sin materiales cargados en esta vista."
        : "Ningún material coincide con el filtro.";

    public string PisoVacioTexto => string.IsNullOrWhiteSpace(Filtro)
        ? "Sin materiales por piso en esta vista.\nAsigna un piso a los circuitos en la columna Ubicación."
        : "Ningún material coincide con el filtro.";

    public string ArtefactosVacioTexto => string.IsNullOrWhiteSpace(Filtro)
        ? "Aún no hay artefactos cargados."
        : "Ningún artefacto coincide con el filtro.";

    public CubicacionScResumenDialog(
        IReadOnlyList<CuadroCargaSCRow> cuadros,
        IReadOnlyList<ZonaProyecto> zonas,
        CubicacionScConfig config,
        bool initialIsDeptos = false)
    {
        _config = config;
        _zonas = zonas;
        _cuadrosGenerales = cuadros.Where(c => !c.EsDepto).ToList();
        _cuadrosDeptos = cuadros.Where(c => c.EsDepto).ToList();
        _isPerspectiveGenerales = !initialIsDeptos;

        InitializeComponent();
        RecalculatePerspective();
        DataContext = this;
    }

    public CubicacionScResumenDialog(
        IReadOnlyList<CuadroCargaSCRow> cuadros,
        IReadOnlyList<ZonaProyecto> zonas,
        CubicacionScConfig config)
        : this(cuadros, zonas, config, false)
    {
    }

    private void RecalculatePerspective()
    {
        var cuadrosFiltrados = _isPerspectiveGenerales ? _cuadrosGenerales : _cuadrosDeptos;
        Total = CubicacionSCEngine.AggregateTotal(cuadrosFiltrados, _config);
        PorTablero = CubicacionSCEngine.AggregatePorTablero(cuadrosFiltrados, _config);
        PorPiso = CubicacionSCEngine.AggregateMaterialesPorPiso(cuadrosFiltrados, _config, _zonas);
        ArtefactosPorPiso = CubicacionSCEngine.AggregateArtefactosPorZona(cuadrosFiltrados, _zonas);

        var tableros = cuadrosFiltrados.Count;
        var circuitos = cuadrosFiltrados.Sum(t => t.Circuitos.Count(c => c.EsActivo || c.EsReservaEquipada));
        var otro = IsPerspectiveGenerales ? "departamentos" : "servicios comunes";
        PerspectiveSubtitle = $"Cálculo independiente del de {otro}. {Cuenta(tableros, "tablero", "tableros")} · {Cuenta(circuitos, "circuito", "circuitos")}.";

        ReconstruirListas();
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Eyebrow));
        OnPropertyChanged(nameof(PerspectiveSubtitle));
        OnPropertyChanged(nameof(IsPerspectiveGenerales));
        OnPropertyChanged(nameof(IsPerspectiveDeptos));
    }

    private void ReconstruirListas()
    {
        LineasTotal = ResumenScPresentacion.Agrupar(Total.Materiales, Filtro);
        CortesTablero = PorTablero
            .Select(ResumenScPresentacion.Corte)
            .Select(c => c.Filtrar(Filtro))
            .Where(c => c.Lineas.Count > 0)
            .ToList();
        CortesPiso = PorPiso
            .Select(ResumenScPresentacion.Corte)
            .Select(c => c.Filtrar(Filtro))
            .Where(c => c.Lineas.Count > 0)
            .ToList();
        ZonasArtefactos = ResumenScPresentacion.FiltrarZonas(ArtefactosPorPiso, Filtro);

        OnPropertyChanged(nameof(LineasTotal));
        OnPropertyChanged(nameof(CortesTablero));
        OnPropertyChanged(nameof(CortesPiso));
        OnPropertyChanged(nameof(ZonasArtefactos));
        OnPropertyChanged(nameof(TotalSinFilas));
        OnPropertyChanged(nameof(TableroSinFilas));
        OnPropertyChanged(nameof(PisoSinFilas));
        OnPropertyChanged(nameof(ArtefactosSinFilas));
        OnPropertyChanged(nameof(TotalVacioTexto));
        OnPropertyChanged(nameof(PisoVacioTexto));
        OnPropertyChanged(nameof(ArtefactosVacioTexto));
    }

    private void SelectTab(int tab)
    {
        if (_tab == tab) return;
        _tab = tab;
        OnPropertyChanged(nameof(ViewTitle));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(IsTabTotal));
        OnPropertyChanged(nameof(IsTabTablero));
        OnPropertyChanged(nameof(IsTabPiso));
        OnPropertyChanged(nameof(IsTabArtefactos));
    }

    private void OnSelectGeneralesClick(object sender, RoutedEventArgs e) => IsPerspectiveGenerales = true;
    private void OnSelectDeptosClick(object sender, RoutedEventArgs e) => IsPerspectiveGenerales = false;
    private void OnTabTotalClick(object sender, RoutedEventArgs e) => SelectTab(0);
    private void OnTabTableroClick(object sender, RoutedEventArgs e) => SelectTab(1);
    private void OnTabPisoClick(object sender, RoutedEventArgs e) => SelectTab(2);
    private void OnTabArtefactosClick(object sender, RoutedEventArgs e) => SelectTab(3);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }

        if (FiltroBox.IsKeyboardFocusWithin)
            return;

        switch (e.Key)
        {
            case Key.D1 or Key.NumPad1: SelectTab(0); e.Handled = true; break;
            case Key.D2 or Key.NumPad2: SelectTab(1); e.Handled = true; break;
            case Key.D3 or Key.NumPad3: SelectTab(2); e.Handled = true; break;
            case Key.D4 or Key.NumPad4: SelectTab(3); e.Handled = true; break;
            case Key.G: IsPerspectiveGenerales = true; e.Handled = true; break;
            case Key.D: IsPerspectiveGenerales = false; e.Handled = true; break;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    private static string Cuenta(int n, string uno, string varios) => n == 1 ? $"1 {uno}" : $"{n} {varios}";
}

internal static class ResumenScPresentacion
{
    public static IReadOnlyList<ResumenLineaVm> Agrupar(IEnumerable<CubicacionSCEngine.MaterialSC> materiales, string? filtro)
    {
        var q = (filtro ?? "").Trim();
        var items = materiales.Where(m =>
            q.Length == 0 || m.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        var lineas = new List<ResumenLineaVm>();
        foreach (var nombre in new[] { "Canalización", "Cajas", "Artefactos y montaje" })
        {
            var rows = items.Where(m => Familia(m.Nombre) == nombre).ToList();
            if (rows.Count == 0) continue;
            lineas.Add(new ResumenLineaVm { Nombre = nombre.ToUpperInvariant(), EsGrupo = true });
            foreach (var m in rows)
                lineas.Add(new ResumenLineaVm { Nombre = m.Nombre, Cantidad = Fmt(m.Cantidad), Unidad = m.Unidad });
            var unidades = rows.Where(r => r.Unidad == "UN").ToList();
            if (nombre != "Canalización" && unidades.Count > 0)
            {
                lineas.Add(new ResumenLineaVm
                {
                    Nombre = "Subtotal " + nombre.ToLowerInvariant(),
                    Cantidad = Fmt(unidades.Sum(r => r.Cantidad)),
                    Unidad = "UN",
                    EsSubtotal = true
                });
            }
        }
        return lineas;
    }

    public static ResumenCorteVm Corte(CubicacionSCEngine.ResumenGrupoSC grupo) => new()
    {
        Nombre = grupo.Nombre,
        Detalle = grupo.Detalle,
        Tuberia = Fmt(grupo.TuberiaMl),
        Conductor = Fmt(grupo.ConductorMl),
        Cajas = grupo.Cajas.ToString(CultureInfo.CurrentCulture),
        Artefactos = grupo.Artefactos.ToString(CultureInfo.CurrentCulture),
        Materiales = grupo.Materiales
    };

    public static IReadOnlyList<ResumenZonaVm> FiltrarZonas(IEnumerable<CubicacionSCEngine.ZonaResumenSC> zonas, string? filtro)
    {
        var q = (filtro ?? "").Trim();
        var resultado = new List<ResumenZonaVm>();
        foreach (var zona in zonas)
        {
            var filas = zona.Artefactos.Where(a =>
                q.Length == 0
                || a.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)
                || a.Categoria.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filas.Count == 0) continue;
            resultado.Add(new ResumenZonaVm
            {
                Nombre = zona.ZonaNombre.ToUpperInvariant(),
                Subtotal = Fmt(filas.Sum(a => a.Cantidad)),
                Filas = filas.Select(a => new ResumenLineaVm
                {
                    Nombre = a.Nombre,
                    Categoria = a.Categoria,
                    Cantidad = Fmt(a.Cantidad),
                    Unidad = "UN"
                }).ToList()
            });
        }
        return resultado;
    }

    private static string Familia(string nombre)
    {
        if (nombre.StartsWith("TUBERIA", StringComparison.OrdinalIgnoreCase) ||
            nombre.StartsWith("CONDUCTOR", StringComparison.OrdinalIgnoreCase))
            return "Canalización";
        if (nombre.StartsWith("CAJA", StringComparison.OrdinalIgnoreCase))
            return "Cajas";
        return "Artefactos y montaje";
    }

    public static string Fmt(decimal n) => n.ToString("#,##0.##", CultureInfo.CurrentCulture);
}

public sealed class ResumenLineaVm
{
    public string Nombre { get; init; } = "";
    public string Categoria { get; init; } = "";
    public string Cantidad { get; init; } = "";
    public string Unidad { get; init; } = "";
    public bool EsGrupo { get; init; }
    public bool EsSubtotal { get; init; }
}

public sealed class ResumenCorteVm
{
    public string Nombre { get; init; } = "";
    public string Detalle { get; init; } = "";
    public string Tuberia { get; init; } = "";
    public string Conductor { get; init; } = "";
    public string Cajas { get; init; } = "";
    public string Artefactos { get; init; } = "";
    public IReadOnlyList<CubicacionSCEngine.MaterialSC> Materiales { get; init; } = Array.Empty<CubicacionSCEngine.MaterialSC>();
    public IReadOnlyList<ResumenLineaVm> Lineas { get; init; } = Array.Empty<ResumenLineaVm>();

    public ResumenCorteVm Filtrar(string? filtro) => new()
    {
        Nombre = Nombre,
        Detalle = Detalle,
        Tuberia = Tuberia,
        Conductor = Conductor,
        Cajas = Cajas,
        Artefactos = Artefactos,
        Lineas = ResumenScPresentacion.Agrupar(Materiales, filtro)
    };
}

public sealed class ResumenZonaVm
{
    public string Nombre { get; init; } = "";
    public string Subtotal { get; init; } = "";
    public IReadOnlyList<ResumenLineaVm> Filas { get; init; } = Array.Empty<ResumenLineaVm>();
}
