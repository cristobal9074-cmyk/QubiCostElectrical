using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoImportSummaryTests
{
    [Fact]
    public void CountPreviewRows_balances_imported_plus_rejected_against_non_titles()
    {
        var source = new[]
        {
            new ItemizadoRow { Id = "1.0", Item = "1", Descripcion = "Capítulo", Cantidad = 1m, EsTitulo = true },
            new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "OK A", Unidad = "UN", Cantidad = 2m },
            new ItemizadoRow { Id = "1.0.00002", Item = "1,2", Descripcion = "", Unidad = "UN", Cantidad = 1m },
            new ItemizadoRow { Id = "1.0.00003", Item = "1,2", Descripcion = "Dup B", Unidad = "UN", Cantidad = 1m },
            new ItemizadoRow { Id = "1.0.00004", Item = "", Descripcion = "Sin código", Unidad = "UN", Cantidad = 1m }
        };

        var previews = ItemizadoImportPreviewService.CreateValidatedPreviewRows(source);
        var (imported, rejected, warnings) = ItemizadoImportSummary.CountPreviewRows(previews);

        int nonTitles = previews.Count(p => !p.EsTitulo);
        Assert.Equal(nonTitles, imported + rejected);
        int acceptedNonTitles = ItemizadoImportPreviewService.AcceptRows(previews).Count(r => !r.EsTitulo);
        Assert.Equal(acceptedNonTitles, imported);
        Assert.True(warnings >= 1);
        Assert.True(rejected >= 2);
    }

    [Fact]
    public void FormatStatus_includes_all_counters()
    {
        var text = ItemizadoImportSummary.FormatStatus("Importación", 12, 3, 2);
        Assert.Contains("12", text);
        Assert.Contains("3", text);
        Assert.Contains("2", text);
        Assert.StartsWith("Importación completada:", text);
    }
}
