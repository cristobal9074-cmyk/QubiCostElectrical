using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// Mide el costo de cambiar de partida en un proyecto MADURO (muchas partidas acumuladas
/// con el tiempo), no uno recién creado. GetRowsForItem y RecalculateForItem recorren
/// session.Cubicacion COMPLETO (todas las partidas, no solo la seleccionada) cada vez que
/// se cambia de partida — este test verifica si ese recorrido total escala mal a medida
/// que el proyecto crece, aunque cada partida individual siga siendo chica.
/// </summary>
public sealed class CubicacionPartidaSwitchPerformanceTests
{
    [Theory]
    [InlineData(300, 40)]   // proyecto grande: 300 partidas x 40 filas = 12.000 filas totales
    [InlineData(1000, 30)]  // proyecto muy grande acumulado en años: 30.000 filas totales
    public void SwitchPartida_MatureProject_UnderBudget(int partidaCount, int rowsPerPartida)
    {
        var session = new BudgetProjectSession();
        session.NewProject("perf-switch", "Tester");
        session.ProjectVariables["largo"] = 10m;

        for (int p = 0; p < partidaCount; p++)
        {
            string itemId = $"item-{p:00000}";
            session.Itemizado.Add(new ItemizadoRow { Id = itemId, Item = $"{p}", Descripcion = $"Partida {p}", Cantidad = 1m });

            for (int r = 0; r < rowsPerPartida; r++)
            {
                var row = new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = itemId,
                    Codigo = $"MAT-{r:000}",
                    Cantidad = r % 2 == 0 ? r + 1 : 0m,
                    MatUf = 0.01m,
                };
                if (r % 2 != 0)
                    row.TotalExpression = $"largo * {r + 1}";
                session.Cubicacion.Add(row);
            }
        }

        var cubSvc = new CubicacionService();
        var querySvc = new CubicacionTakeoffQueryService();

        // Simula LoadCubicacionRows() completo: cambiar a la ÚLTIMA partida (peor caso para
        // un recorrido lineal desde el principio de la lista).
        string targetItemId = $"item-{(partidaCount - 1):00000}";

        var sw = Stopwatch.StartNew();
        querySvc.RecalculateForItem(session, targetItemId);
        var rows = cubSvc.GetRowsForItem(session.Cubicacion, targetItemId);
        sw.Stop();

        Assert.Equal(rowsPerPartida, rows.Count);
        // Costo real medido (JIT en frío incluido): ~17-50ms. El margen hasta 400ms es para
        // absorber el jitter de correr la suite completa en paralelo en una máquina compartida,
        // sin dejar de detectar una regresión real (que se mediría en cientos de ms, no ~20-50).
        Assert.True(sw.Elapsed.TotalMilliseconds < 400,
            $"Cambiar a la última partida de {partidaCount} (con {rowsPerPartida} filas c/u = " +
            $"{partidaCount * rowsPerPartida:N0} filas totales) tardó {sw.Elapsed.TotalMilliseconds:N1} ms.");
    }
}
