using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class BudgetVersionComparisonTests
{
    [Fact]
    public void Compare_DetectsAddedDeletedAndModifiedItemsWithSpecificResourceChanges()
    {
        var service = new BudgetVersionComparisonService();

        // 1. Setup Baseline Version Data
        var baseline = new BudgetVersionData
        {
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "cap1", Item = "1", Descripcion = "Capítulo 1", EsTitulo = true, NivelJerarquico = 0 },
                new() { Id = "part1", Item = "1.1", Descripcion = "Partida 1.1", EsTitulo = false, NivelJerarquico = 1, Cantidad = 10m },
                new() { Id = "part2", Item = "1.2", Descripcion = "Partida 1.2", EsTitulo = false, NivelJerarquico = 1, Cantidad = 5m }
            },
            Apus = new List<ApuRow>
            {
                new()
                {
                    ItemId = "part1",
                    HerramientasPct = 0m,
                    ImprevistosAlzaPct = 0m,
                    Recursos = new List<ApuRecursoRow>
                    {
                        new() { Tipo = ApuTipoRecurso.MAT, Codigo = "MAT01", Rendimiento = 1m, PrecioUf = 2m },
                        new() { Tipo = ApuTipoRecurso.MO, Codigo = "MO01", Rendimiento = 2m, PrecioUf = 3m }
                    }
                },
                new()
                {
                    ItemId = "part2",
                    HerramientasPct = 0m,
                    ImprevistosAlzaPct = 0m,
                    Recursos = new List<ApuRecursoRow>
                    {
                        new() { Tipo = ApuTipoRecurso.MAT, Codigo = "MAT02", Rendimiento = 1m, PrecioUf = 5m }
                    }
                }
            }
        };

        // 2. Setup Current Version Data (ProjectFileData)
        var current = new ProjectFileData
        {
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "cap1", Item = "1", Descripcion = "Capítulo 1", EsTitulo = true, NivelJerarquico = 0 },
                new() { Id = "part1", Item = "1.1", Descripcion = "Partida 1.1", EsTitulo = false, NivelJerarquico = 1, Cantidad = 10m }, // Sin cambios
                new() { Id = "part3", Item = "1.3", Descripcion = "Partida 1.3 Nueva", EsTitulo = false, NivelJerarquico = 1, Cantidad = 8m } // Nueva partida
            },
            Apus = new List<ApuRow>
            {
                new()
                {
                    ItemId = "part1",
                    HerramientasPct = 0m,
                    ImprevistosAlzaPct = 0m,
                    Recursos = new List<ApuRecursoRow>
                    {
                        // Modificamos el precio de MO de 3m a 4m (Cambio de Precio / Mano de Obra)
                        new() { Tipo = ApuTipoRecurso.MAT, Codigo = "MAT01", Rendimiento = 1m, PrecioUf = 2m },
                        new() { Tipo = ApuTipoRecurso.MO, Codigo = "MO01", Rendimiento = 2m, PrecioUf = 4m }
                    }
                },
                new()
                {
                    ItemId = "part3",
                    HerramientasPct = 0m,
                    ImprevistosAlzaPct = 0m,
                    Recursos = new List<ApuRecursoRow>
                    {
                        new() { Tipo = ApuTipoRecurso.MAT, Codigo = "MAT03", Rendimiento = 1m, PrecioUf = 10m }
                    }
                }
            }
        };

        // 3. Perform Comparison
        var summary = service.Compare(baseline, current, "Base", "Actual");

        // 4. Assert Summary Cost Metrics
        // Baseline:
        // part1: Qty=10, Price = 1*2 + 2*3 = 8 -> Total = 80
        // part2: Qty=5, Price = 1*5 = 5 -> Total = 25
        // cap1: Total = part1 + part2 = 80 + 25 = 105
        // BaselineUf (non-titles sum) = 105
        Assert.Equal(105m, summary.BaselineUf);

        // Current:
        // part1: Qty=10, Price = 1*2 + 2*4 = 10 -> Total = 100 (Precio aumentó de 8 a 10, Delta = +20)
        // part2: (Eliminada) -> Delta = -25
        // part3: Qty=8, Price = 1*10 = 10 -> Total = 80 (Nueva, Delta = +80)
        // cap1: Total = part1 + part3 = 100 + 80 = 180
        // CurrentUf (non-titles sum) = 180
        Assert.Equal(180m, summary.CurrentUf);
        Assert.Equal(75m, summary.DeltaUf); // 180 - 105 = 75

        // Added Cost = +20 (part1 mod) + 80 (part3 new) = 100
        Assert.Equal(100m, summary.CostoAgregadoUf);
        // Removed Cost = 25 (part2 deleted)
        Assert.Equal(25m, summary.CostoEliminadoUf);

        // Counts
        Assert.Equal(1, summary.AddedCount);
        Assert.Equal(1, summary.RemovedCount);
        Assert.Equal(1, summary.ChangedCount); // part1 is modified

        // 5. Assert Row Level Details
        var rowCap1 = summary.Rows.First(x => x.ItemId == "cap1");
        Assert.True(rowCap1.EsTitulo);
        Assert.Equal(105m, rowCap1.BaselineUf);
        Assert.Equal(180m, rowCap1.CurrentUf);
        Assert.Equal(75m, rowCap1.DeltaUf);

        var rowPart1 = summary.Rows.First(x => x.ItemId == "part1");
        Assert.Equal("Aumentó", rowPart1.Estado);
        Assert.False(rowPart1.CambióCantidad);
        Assert.True(rowPart1.CambióPrecio);
        Assert.True(rowPart1.CambióManoObra);
        Assert.False(rowPart1.CambióMateriales);
        Assert.Contains("Precio", rowPart1.DetalleCambios);
        Assert.Contains("Mano de Obra", rowPart1.DetalleCambios);

        var rowPart2 = summary.Rows.First(x => x.ItemId == "part2");
        Assert.Equal("Eliminado", rowPart2.Estado);

        var rowPart3 = summary.Rows.First(x => x.ItemId == "part3");
        Assert.Equal("Nuevo", rowPart3.Estado);
    }
}
