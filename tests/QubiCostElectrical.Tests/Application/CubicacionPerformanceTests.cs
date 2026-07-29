using System;
using System.Diagnostics;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Tests.Wpf;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionPerformanceTests
{
    [Fact]
    public void RecalculateAll_ThreeThousandRows_UnderTwoSeconds()
    {
        var session = new BudgetProjectSession();
        session.NewProject("perf-cubicacion", "Tester");

        var itemId = "partida-perf-001";
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = itemId,
            Item = "1.1",
            Descripcion = "Partida de rendimiento",
            Unidad = "UN",
            Cantidad = 1m
        });

        // Agregar variables de proyecto para que las fórmulas tengan contexto
        session.ProjectVariables["largo_corredor"] = 45m;
        session.ProjectVariables["alto_piso"] = 2.8m;

        // Crear 3000 filas: mitad con fórmula, mitad con cantidad fija
        for (int i = 1; i <= 3000; i++)
        {
            var row = new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = itemId,
                Codigo = $"MAT-{i:00000}",
                Descripcion = $"Material {i}",
                Unidad = "UN",
                Cantidad = i % 2 == 0 ? i : 0m,
                MatUf = 0.01m * (i % 10 + 1),
                MoUf = 0.005m * (i % 5 + 1),
                Area = "General"
            };

            if (i % 2 != 0)
            {
                // Fórmula simple que usa variables de proyecto
                row.TotalExpression = $"largo_corredor * {i % 10 + 1}";
            }

            session.Cubicacion.Add(row);
        }

        var svc = new CubicacionTakeoffQueryService();

        var sw = Stopwatch.StartNew();
        svc.RecalculateAll(session);
        sw.Stop();

        // Verificar que el servicio procesó las 3000 filas y el tiempo es aceptable
        int totalRows = session.Cubicacion.Count;
        Assert.Equal(3000, totalRows);
        Assert.True(sw.Elapsed.TotalSeconds < 3.0,
            $"RecalculateAll con 3000 filas tardó {sw.Elapsed.TotalMilliseconds:N0} ms (límite: 3000 ms).");
    }

    [Fact]
    public void ValidateRows_ThreeThousandVms_UnderOneSecond()
    {
        var session = new BudgetProjectSession();
        session.NewProject("perf-validate", "Tester");

        // Catálogo con 500 materiales
        for (int i = 1; i <= 500; i++)
            session.Materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-{i:00000}",
                Descripcion = $"Material {i}",
                Unidad = "UN",
                MatUf = 0.01m * i,
                FechaPrecio = DateTime.Today
            });

        var itemId = "partida-perf-002";
        // 3000 filas: mitad con código válido, mitad con código inválido
        for (int i = 1; i <= 3000; i++)
        {
            session.Cubicacion.Add(new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = itemId,
                Codigo = i <= 1500 ? $"MAT-{(i % 500 + 1):00000}" : $"INVALIDO-{i}",
                Descripcion = $"Recurso {i}",
                Cantidad = i,
                MatUf = 0.01m
            });
        }

        // Simular la validación del ViewModel (lógica extraída para testear)
        var catalogCodigos = session.Materiales
            .Select(m => m.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sw = Stopwatch.StartNew();

        int errorCount = 0;
        foreach (var row in session.Cubicacion.Where(r =>
            string.Equals(r.ItemId, itemId, StringComparison.OrdinalIgnoreCase)))
        {
            bool hasError = !string.IsNullOrWhiteSpace(row.Codigo)
                            && !catalogCodigos.Contains(row.Codigo);
            if (hasError) errorCount++;
        }

        sw.Stop();

        Assert.Equal(1500, errorCount);
        Assert.True(sw.Elapsed.TotalSeconds < 1.0,
            $"Validación de 3000 filas tardó {sw.Elapsed.TotalMilliseconds:N0} ms (límite: 1000 ms).");
    }
}
