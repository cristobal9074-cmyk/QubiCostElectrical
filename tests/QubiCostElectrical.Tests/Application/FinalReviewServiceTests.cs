using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class FinalReviewServiceTests
{
    private readonly FinalReviewService _service = new();

    private static ItemizadoRow Partida(string id, string item, string desc, bool esTitulo = false)
        => new() { Id = id, Item = item, Descripcion = desc, EsTitulo = esTitulo, Cantidad = 1 };

    private static CubicacionRow Cub(string itemId, string codigo, string desc, decimal cantidad, decimal matUf = 1m,
        decimal moUf = 0m, string area = "Piso 1")
        => new()
        {
            ItemId = itemId,
            Codigo = codigo,
            Descripcion = desc,
            Unidad = "UN",
            Cantidad = cantidad,
            MatUf = matUf,
            MoUf = moUf,
            Area = area
        };

    private static MaterialRow Mat(string codigo, string desc, decimal matUf = 1m, decimal moUf = 0m)
        => new() { Codigo = codigo, Descripcion = desc, Unidad = "UN", MatUf = matUf, MoUf = moUf };

    private static ApuRow Apu(string itemId, string desc, List<ApuRecursoRow>? recursos = null)
        => new() { ItemId = itemId, Descripcion = desc, Recursos = recursos ?? new List<ApuRecursoRow>() };

    private static ApuRecursoRow Recurso(string codigo, decimal precio = 1m, decimal rendimiento = 1m,
        ApuTipoRecurso tipo = ApuTipoRecurso.MAT)
        => new()
        {
            CodigoMaterial = codigo,
            Descripcion = codigo,
            PrecioUf = precio,
            Rendimiento = rendimiento,
            Tipo = tipo
        };

    [Fact]
    public void Build_EmptyPartidas_AreOneProgressWarning_NotABlockerEach()
    {
        var itemizado = new[]
        {
            Partida("P1", "1.1", "Canalización"),
            Partida("P2", "1.2", "Conductores"),
            Partida("P3", "1.3", "Tableros")
        };
        var result = _service.Build(itemizado, null, null, null);

        Assert.Equal(3, result.EmptyPartidas);
        var issue = Assert.Single(result.Issues, i => i.Id == "EMPTY|SUMMARY");
        Assert.Equal(FinalReviewSeverity.Warning, issue.Severity);
        Assert.Equal(3, issue.Count);
        Assert.DoesNotContain(result.Issues, i => i.Category == FinalReviewCategory.EmptyPartida && i.Severity == FinalReviewSeverity.Blocker);
        Assert.Contains(result.Issues, i => i.Id == "TOTAL|ZERO");
    }

    [Fact]
    public void Build_ExcluidoCubicacion_DoesNotCountAsEmptyOrMissingApu()
    {
        var itemizado = new[]
        {
            new ItemizadoRow
            {
                Id = "P1",
                Item = "1.1",
                Descripcion = "Fuera de alcance",
                Cantidad = 1,
                ExcluidoCubicacion = true
            }
        };
        var result = _service.Build(itemizado, null, null, null);

        Assert.Equal(0, result.EmptyPartidas);
        Assert.DoesNotContain(result.Issues, i => i.Category == FinalReviewCategory.EmptyPartida);
        Assert.DoesNotContain(result.Issues, i => i.Category == FinalReviewCategory.MissingApu);
    }

    [Fact]
    public void Build_PartidaWithCubicacion_NoEmptyPartidaIssues()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var result = _service.Build(itemizado, null, cubicacion, null);

        Assert.Equal(0, result.EmptyPartidas);
    }

    [Fact]
    public void Build_ZeroQuantity_ReturnsBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 0m) };
        var result = _service.Build(itemizado, null, cubicacion, null);

        Assert.True(result.Issues.Count(i => i.Category == FinalReviewCategory.ZeroQuantity) > 0);
    }

    [Fact]
    public void Build_MaterialWithoutPrice_ReturnsBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m, matUf: 0m, moUf: 0m) };
        var result = _service.Build(itemizado, null, cubicacion, null);

        Assert.True(result.MaterialsWithoutPrice > 0);
    }

    [Fact]
    public void Build_MaterialWithPriceInList_NoPriceIssue()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m, matUf: 0m, moUf: 0m) };
        var materiales = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var result = _service.Build(itemizado, materiales, cubicacion, null);

        Assert.Equal(0, result.MaterialsWithoutPrice);
    }

    [Fact]
    public void Build_AreaNotAssigned_ReturnsWarning()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m, area: "General") };
        var result = _service.Build(itemizado, null, cubicacion, null);

        Assert.True(result.AreasNotAssigned > 0);
    }

    [Fact]
    public void Build_AreaSpecific_NoAreaWarning()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m, area: "Piso 2") };
        var result = _service.Build(itemizado, null, cubicacion, null);

        Assert.Equal(0, result.AreasNotAssigned);
    }

    [Fact]
    public void Build_TakeoffWithoutApu_DoesNotRaiseAnIssuePerPartida()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var result = _service.Build(itemizado, null, cubicacion, null);

        Assert.Equal(0, result.MissingApus);
    }

    [Fact]
    public void Build_CompleteApu_NoMissingApu()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var apus = new[] { Apu("P1", "Canalización", new List<ApuRecursoRow>
        {
            Recurso("PVC20", 0.35m, 1m)
        }) };
        var materiales = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var result = _service.Build(itemizado, materiales, cubicacion, apus);

        Assert.Equal(0, result.MissingApus);
    }

    [Fact]
    public void Build_ApuWithMissingPrice_ReturnsBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var apus = new[] { Apu("P1", "Canalización", new List<ApuRecursoRow>
        {
            Recurso("PVC20", 0m, 1m)
        }) };
        var materiales = new[] { Mat("PVC20", "PVC 20mm", 0m) };
        var result = _service.Build(itemizado, materiales, cubicacion, apus);

        var issue = Assert.Single(result.Issues, i => i.Category == FinalReviewCategory.ApuIncomplete);
        Assert.Equal(FinalReviewSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Build_CompleteBudget_ReadyToExport()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var materiales = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var apus = new[] { Apu("P1", "Canalización", new List<ApuRecursoRow>
        {
            Recurso("PVC20", 0.35m, 1m)
        }) };
        var settings = BudgetSettings.FromValues(10m, 15m, 0m);
        var result = _service.Build(itemizado, materiales, cubicacion, apus, new ProjectInfoRow(), settings);

        Assert.True(result.ReadyToExport);
    }

    [Fact]
    public void Build_ZeroTotalSale_ReturnsBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var result = _service.Build(itemizado, null, Enumerable.Empty<CubicacionRow>(), null);

        Assert.True(result.Issues.Any(i => i.Category == FinalReviewCategory.ZeroTotalSale));
    }

    [Fact]
    public void Build_IvaOutOfRange_ReturnsBlocker()
    {
        var settings = new BudgetSettings();
        settings.Markup = new MarkupPolicy { IvaPct = 99m };
        var result = _service.Build(
            Enumerable.Empty<ItemizadoRow>(), null,
            Enumerable.Empty<CubicacionRow>(), null,
            new ProjectInfoRow(), settings);

        Assert.True(result.Issues.Any(i => i.Category == FinalReviewCategory.IvaInconsistent));
    }

    [Fact]
    public void Build_NegativeMargin_ReturnsBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m, matUf: 0.35m) };
        var materiales = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var settings = BudgetSettings.FromValues(50m, -80m, 0m);
        settings.Markup.DescuentoPct = 0m;
        settings.Markup.IncluirIvaEnTotal = false;
        var result = _service.Build(itemizado, materiales, cubicacion, null, new ProjectInfoRow(), settings);

        var hasMarkupError = result.Issues.Any(i => i.Category == FinalReviewCategory.MarkupInvalid);
        Assert.True(hasMarkupError);
    }

    [Fact]
    public void Build_MarkupOver100_ReturnsBlocker()
    {
        var settings = new BudgetSettings();
        settings.Markup = new MarkupPolicy { GastosGeneralesPct = 200m };
        var result = _service.Build(
            Enumerable.Empty<ItemizadoRow>(), null,
            Enumerable.Empty<CubicacionRow>(), null,
            new ProjectInfoRow(), settings);

        Assert.True(result.Issues.Any(i => i.Category == FinalReviewCategory.MarkupInvalid));
        Assert.False(result.ReadyToExport);
    }

    [Fact]
    public void Build_ZeroIvaIsValid()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var materiales = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var apus = new[] { Apu("P1", "Canalización", new List<ApuRecursoRow>
        {
            Recurso("PVC20", 0.35m, 1m)
        }) };
        var settings = BudgetSettings.FromValues(10m, 15m, 0m);
        settings.Markup.IvaPct = 0m;
        var result = _service.Build(itemizado, materiales, cubicacion, apus, new ProjectInfoRow(), settings);

        Assert.True(result.ReadyToExport);
    }

    [Fact]
    public void IsAreaNotAssigned_VariousInputs()
    {
        Assert.True(_service.IsAreaNotAssigned(null));
        Assert.True(_service.IsAreaNotAssigned(""));
        Assert.True(_service.IsAreaNotAssigned("General"));
        Assert.True(_service.IsAreaNotAssigned("general"));
        Assert.True(_service.IsAreaNotAssigned("SIN AREA"));
        Assert.True(_service.IsAreaNotAssigned("NO ASIGNADA"));
        Assert.True(_service.IsAreaNotAssigned("N/A"));
        Assert.True(_service.IsAreaNotAssigned("NA"));
        Assert.False(_service.IsAreaNotAssigned("Piso 1"));
        Assert.False(_service.IsAreaNotAssigned("Servicios Comunes"));
    }

    [Fact]
    public void Build_PartidaTitle_SkipsEmptyChecks()
    {
        var itemizado = new[] { Partida("P1", "1", "Título", esTitulo: true) };
        var result = _service.Build(itemizado, null, null, null);

        Assert.Equal(0, result.EmptyPartidas);
    }

    [Fact]
    public void ConfidencePct_DecreasesWithIssues()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var result = _service.Build(itemizado, null, null, null);

        Assert.True(result.ConfidencePct < 100m);
        Assert.True(result.ConfidencePct >= 0m);
    }

    [Fact]
    public void Build_MultiplePartidas_DetectsAllIssues()
    {
        var itemizado = new[]
        {
            Partida("P1", "1.1", "Canalización"),
            Partida("P2", "1.2", "Conductores")
        };
        var cubicacion = new[]
        {
            Cub("P1", "PVC20", "PVC 20mm", 50m, matUf: 0m, moUf: 0m),
            Cub("P2", "THHN10", "THHN 10mm²", 0m)
        };
        var result = _service.Build(itemizado, null, cubicacion, null);

        Assert.True(result.TotalIssues >= 2);
        Assert.False(result.ReadyToExport);
    }

    [Fact]
    public void Build_PriceTraceability_DetectsStalePrices()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var materiales = new[] { Mat("PVC20", "PVC 20mm", 0.35m) };
        var apus = new[] { Apu("P1", "Canalización", new List<ApuRecursoRow>
        {
            new() { CodigoMaterial = "PVC20", Descripcion = "PVC 20mm", PrecioUf = 0.35m,
                Rendimiento = 1m, Tipo = ApuTipoRecurso.MAT, FechaPrecio = DateTime.Today.AddMonths(-7) }
        }) };
        var settings = BudgetSettings.FromValues(10m, 15m, 0m);
        var result = _service.Build(itemizado, materiales, cubicacion, apus, new ProjectInfoRow(), settings);

        Assert.True(result.Issues.Any(i => i.Category == FinalReviewCategory.PriceTraceability));
    }

    [Fact]
    public void Build_NullApuList_NoMissingApuBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var settings = new BudgetSettings { ApuMode = BudgetApuMode.CubicacionManda };
        var result = _service.Build(itemizado, null, cubicacion, null, new ProjectInfoRow(), settings);

        Assert.DoesNotContain(result.Issues, i => i.Category == FinalReviewCategory.MissingApu && i.Severity == FinalReviewSeverity.Blocker);
    }

    [Fact]
    public void Build_BrokenFormulaInTotalExpression_ReturnsBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var row = Cub("P1", "PVC20", "PVC 20mm", 50m);
        row.TotalExpression = "=10+*5"; // Frmula rota / sintaxis invlida
        var result = _service.Build(itemizado, null, new[] { row }, null);

        Assert.True(result.FormulaErrors > 0);
        Assert.False(result.ReadyToExport);
        var issue = Assert.Single(result.Issues, i => i.Category == FinalReviewCategory.FormulaSyntaxError);
        Assert.Equal(FinalReviewSeverity.Blocker, issue.Severity);
        Assert.Contains("f\u00f3rmula de total", issue.Problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_BrokenFormulaInQtyExpressionsCasilla_ReturnsBlocker()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var row = Cub("P1", "PVC20", "PVC 20mm", 50m);
        row.QtyExpressions = new string[35];
        row.QtyExpressions[2] = "=5+*2"; // Frmula rota con sintaxis invlida en casilla 3
        var result = _service.Build(itemizado, null, new[] { row }, null);

        Assert.True(result.FormulaErrors > 0);
        var issue = Assert.Single(result.Issues, i => i.Category == FinalReviewCategory.FormulaSyntaxError);
        Assert.Equal(FinalReviewSeverity.Blocker, issue.Severity);
        Assert.Contains("casilla #3", issue.Problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ValidFormula_DoesNotTriggerFormulaSyntaxError()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var row = Cub("P1", "PVC20", "PVC 20mm", 50m);
        row.TotalExpression = "=10+2*5";
        row.QtyExpressions = new string[35];
        row.QtyExpressions[0] = "=(4+2)*3";
        row.QtyExpressions[1] = "15.5";
        var result = _service.Build(itemizado, null, new[] { row }, null);

        Assert.Equal(0, result.FormulaErrors);
        Assert.DoesNotContain(result.Issues, i => i.Category == FinalReviewCategory.FormulaSyntaxError);
    }

    [Fact]
    public void Build_ApuMandaMode_DoesNotRequireAnApu()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var settings = new BudgetSettings { ApuMode = BudgetApuMode.ApuManda };

        var result = _service.Build(itemizado, null, cubicacion, null, settings: settings);

        Assert.DoesNotContain(result.Issues, i => i.Category == FinalReviewCategory.MissingApu);
        Assert.DoesNotContain(result.Issues, i => i.Severity == FinalReviewSeverity.Blocker && i.Category == FinalReviewCategory.ApuIncomplete);
    }

    [Fact]
    public void Build_CubicacionMandaMode_DoesNotProduceMissingApuIssues()
    {
        var itemizado = new[] { Partida("P1", "1.1", "Canalización") };
        var cubicacion = new[] { Cub("P1", "PVC20", "PVC 20mm", 50m) };
        var settings = new BudgetSettings { ApuMode = BudgetApuMode.CubicacionManda };

        var result = _service.Build(itemizado, null, cubicacion, null, settings: settings);

        Assert.Equal(0, result.MissingApus);
        Assert.DoesNotContain(result.Issues, i => i.Category == FinalReviewCategory.MissingApu);
    }
}
