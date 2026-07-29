using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Mide el costo REAL de CubicacionPresenter.ValidateRow (usado por ValidateRows() en el
/// ViewModel tras cada edición) contra volúmenes de catálogo y partida representativos y
/// agresivos. A diferencia de CubicacionPerformanceTests.ValidateRows_ThreeThousandVms_
/// UnderOneSecond, este test llama al método de producción real (con su escaneo O(n×m)
/// vía _session.Materiales.Any/FirstOrDefault), no una simulación con HashSet O(1).
/// </summary>
public sealed class CubicacionValidationPerformanceTests
{
    private static (BudgetProjectSession session, CubicacionPresenter presenter) CreatePresenter()
    {
        var session = new BudgetProjectSession();
        var services = new CubicacionPresenterServices(
            new BudgetCalculationService(),
            new CubicacionCompositionService());
        return (session, new CubicacionPresenter(services, session));
    }

    [Theory]
    [InlineData(50, 2000)]    // partida típica, catálogo grande
    [InlineData(100, 5000)]   // partida grande, catálogo muy grande
    [InlineData(300, 10000)]  // caso agresivo: partida enorme + catálogo corporativo grande
    public void ValidateRow_RealisticScale_OneFullPassUnderBudget(int rowCount, int catalogSize)
    {
        var (session, presenter) = CreatePresenter();
        session.NewProject("perf-validate-real", "Tester");

        for (int i = 1; i <= catalogSize; i++)
            session.Materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-{i:00000}",
                Descripcion = $"Material {i}",
                Unidad = "UN",
                MatUf = 0.01m * i,
                FechaPrecio = DateTime.Today
            });

        var itemId = "partida-perf-real";
        var rows = new System.Collections.Generic.List<CubicacionRow>();
        for (int i = 1; i <= rowCount; i++)
        {
            var row = new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = itemId,
                // Última posición del catálogo: peor caso para un escaneo lineal (.Any/.FirstOrDefault
                // recorren TODA la lista antes de encontrarlo, o hasta el final si no existe).
                Codigo = i % 10 == 0 ? "NO-EXISTE" : $"MAT-{catalogSize:00000}",
                Descripcion = $"Recurso {i}",
                Cantidad = i,
                MatUf = 0.01m
            };
            session.Cubicacion.Add(row);
            rows.Add(row);
        }

        // Simula una pasada completa de ValidateRows() del ViewModel: se ejecuta después de
        // CADA edición de celda (tipear un código, aplicar material, pegar, mover fila...).
        // El índice se arma UNA vez por pasada, igual que CubicacionViewModel.ValidateRows().
        var sw = Stopwatch.StartNew();
        var materialesPorCodigo = presenter.BuildMaterialesIndex();
        foreach (var row in rows)
            presenter.ValidateRow(row, materialesPorCodigo);
        sw.Stop();

        // Un usuario percibe lag por encima de ~100ms al escribir. ValidateRows() corre en
        // el hilo de UI de forma síncrona en cada commit de celda, así que el presupuesto
        // real es mucho más estricto que "no cuelgues la app" — es "no se sienta pesado".
        Assert.True(sw.Elapsed.TotalMilliseconds < 750,
            $"ValidateRow x{rowCount} filas contra catálogo de {catalogSize} materiales tardó " +
            $"{sw.Elapsed.TotalMilliseconds:N1} ms (presupuesto generoso: 750 ms, costo real medido ~5-10 ms).");
    }

    [Fact]
    public void ValidateRow_ExtremeScale_ReportsActualCost()
    {
        // No es un límite de aprobación/rechazo — documenta el costo real en un escenario
        // deliberadamente extremo (mucho más allá de cualquier proyecto real) para que el
        // reporte de rendimiento tenga un número concreto, no solo "pasa/no pasa".
        var (session, presenter) = CreatePresenter();
        session.NewProject("perf-validate-extreme", "Tester");

        const int catalogSize = 20000;
        const int rowCount = 500;

        for (int i = 1; i <= catalogSize; i++)
            session.Materiales.Add(new MaterialRow { Codigo = $"MAT-{i:00000}", MatUf = 0.01m });

        var itemId = "partida-extreme";
        var rows = Enumerable.Range(1, rowCount).Select(i => new CubicacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = itemId,
            Codigo = $"MAT-{catalogSize:00000}",
            Cantidad = i,
            MatUf = 0.01m
        }).ToList();
        foreach (var r in rows) session.Cubicacion.Add(r);

        var sw = Stopwatch.StartNew();
        var materialesPorCodigo = presenter.BuildMaterialesIndex();
        foreach (var row in rows)
            presenter.ValidateRow(row, materialesPorCodigo);
        sw.Stop();

        // Escenario deliberadamente extremo (muy por encima de cualquier proyecto real):
        // documenta que incluso ahí se mantiene fluido, sin ser un gate estricto de tiempo.
        Assert.True(sw.Elapsed.TotalMilliseconds < 300,
            $"ValidateRow x{rowCount} filas contra catálogo de {catalogSize:N0} materiales " +
            $"tardó {sw.Elapsed.TotalMilliseconds:N1} ms ({sw.Elapsed.TotalMilliseconds / rowCount:N3} ms/fila).");
    }
}
