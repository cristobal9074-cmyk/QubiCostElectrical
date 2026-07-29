using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoImportPasteResolverTests
{
    private static PegarItemizadoUseCase Paste => new();

    [Fact]
    public void Resolve_empty_clipboard_fails()
    {
        var result = Paste.Execute("");
        var resolved = ItemizadoImportPasteResolver.Resolve(result, null, showPreview: true);

        Assert.Equal(ItemizadoImportResolveAction.Failed, resolved.Action);
        Assert.Contains("portapapeles", resolved.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_clean_small_paste_accepts_directly()
    {
        string text = "Ítem\tDescripción\tUnidad\tCantidad\n1.1\tCableado\tm\t10\n";
        var paste = Paste.Execute(text);
        var plan = new ItemizadoImportPlan(paste.Rows, 1, 0, false, 0, "Pegar itemizado");

        var resolved = ItemizadoImportPasteResolver.Resolve(paste, plan, showPreview: false);

        Assert.Equal(ItemizadoImportResolveAction.AcceptDirectly, resolved.Action);
        Assert.Single(resolved.Rows);
    }

    [Fact]
    public void ResolveImportPlan_small_clean_plan_accepts_directly()
    {
        var rows = new[]
        {
            new ItemizadoRow { Id = "1.0", Item = "1", Descripcion = "Cap", EsTitulo = true },
            new ItemizadoRow { Id = "1.0.00001", Item = "1.1", Descripcion = "P1", Unidad = "UN", Cantidad = 1m }
        };
        var plan = new ItemizadoImportPlan(rows, 1, 1, false, 0, "Importar");

        var resolved = ItemizadoImportPasteResolver.ResolveImportPlan(plan);

        Assert.Equal(ItemizadoImportResolveAction.AcceptDirectly, resolved.Action);
        Assert.Equal(2, resolved.Rows.Count);
    }

    [Fact]
    public void Resolve_duplicate_codes_needs_preview_when_showPreview_true()
    {
        var rows = new[]
        {
            new ItemizadoRow { Id = "1.0.00001", Item = "1,1", Descripcion = "A", Unidad = "UN", Cantidad = 1m },
            new ItemizadoRow { Id = "1.0.00002", Item = "1,1", Descripcion = "B", Unidad = "UN", Cantidad = 1m }
        };
        var plan = new ItemizadoImportPlan(rows, 2, 0, false, 0, "Pegar");
        var paste = new PegarItemizadoResult(rows, "", "");

        var resolved = ItemizadoImportPasteResolver.Resolve(paste, plan, showPreview: true);

        Assert.Equal(ItemizadoImportResolveAction.NeedsUiPreview, resolved.Action);
    }
}
