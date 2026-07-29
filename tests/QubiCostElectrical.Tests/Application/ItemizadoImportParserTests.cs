using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ItemizadoImportParserTests
{
    // ─── HeaderKey ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Descripción", "DESCRIPCION")]
    [InlineData("ÍTEM", "ITEM")]
    [InlineData("N° Ítem", "NITEM")]
    [InlineData("Ud.", "UD")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void HeaderKey_NormalizesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, ItemizadoImportParser.HeaderKey(input));
    }

    // ─── FindColumn ───────────────────────────────────────────────────────

    [Fact]
    public void FindColumn_FindsExactMatch()
    {
        var headers = new List<string> { "ITEM", "DESCRIPCION", "UNIDAD" };
        Assert.Equal(1, ItemizadoImportParser.FindColumn(headers, "DESCRIPCION"));
    }

    [Fact]
    public void FindColumn_FindsWithAccentVariant()
    {
        var headers = new List<string> { "Ítem", "Descripción", "Ud." };
        Assert.Equal(0, ItemizadoImportParser.FindColumn(headers, "ITEM", "ÍTEM"));
        Assert.Equal(1, ItemizadoImportParser.FindColumn(headers, "DESCRIPCION"));
    }

    [Fact]
    public void FindColumn_ReturnsMinusOneWhenNotFound()
    {
        var headers = new List<string> { "Código", "Descripción" };
        Assert.Equal(-1, ItemizadoImportParser.FindColumn(headers, "PRECIO", "MATUF"));
    }

    [Fact]
    public void FindColumn_DoesNotMatchItemInsideCompoundHeader()
    {
        var headers = new List<string> { "CANTIDADITEM", "DESCRIPCION" };
        Assert.Equal(-1, ItemizadoImportParser.FindColumn(headers, "ITEM"));
        Assert.Equal(0, ItemizadoImportParser.FindColumn(headers, "CANTIDAD", "CANTIDADITEM"));
    }

    [Fact]
    public void FindColumn_MatchesKnownCompoundAlias()
    {
        var headers = new List<string> { "N° Ítem", "Detalle", "Ud.", "Cant." };
        Assert.Equal(0, ItemizadoImportParser.FindColumn(headers, "ITEM", "NITEM"));
        Assert.Equal(1, ItemizadoImportParser.FindColumn(headers, "DESCRIPCION", "DETALLE"));
    }

    // ─── FindHeaderRow ────────────────────────────────────────────────────

    [Fact]
    public void FindHeaderRow_FindsRowWithDescAndItem()
    {
        var table = new List<List<string>>
        {
            new() { "Proyecto XYZ" },
            new() { "Ítem", "Descripción", "Unidad", "Cantidad" },
            new() { "1", "Tablero general", "GL", "1" }
        };
        Assert.Equal(1, ItemizadoImportParser.FindHeaderRow(table, "itemizado"));
    }

    [Fact]
    public void FindHeaderRow_ReturnsMinusOneWhenNoHeader()
    {
        var table = new List<List<string>>
        {
            new() { "1", "Tablero general", "GL" },
            new() { "1.1", "Cableado", "m" }
        };
        Assert.Equal(-1, ItemizadoImportParser.FindHeaderRow(table, "itemizado"));
    }

    // ─── IsImportedTitleCode ──────────────────────────────────────────────

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", true)]
    [InlineData("1.0", true)]
    [InlineData("3.0", true)]
    [InlineData("1,0", true)]   // coma → punto en la normalización
    [InlineData("1.1", false)]
    [InlineData("1.00001", false)]
    [InlineData("", false)]
    [InlineData("ABC", false)]
    public void IsImportedTitleCode_ClassifiesCorrectly(string code, bool expected)
    {
        Assert.Equal(expected, ItemizadoImportParser.IsImportedTitleCode(code));
    }

    // ─── BuildImportedTitleId ─────────────────────────────────────────────

    [Theory]
    [InlineData("1", 99, "1.0")]
    [InlineData("2.0", 99, "2.0")]
    [InlineData("", 3, "3.0")]
    [InlineData("ABC", 5, "5.0")]
    public void BuildImportedTitleId_BuildsCorrectly(string code, int fallback, string expected)
    {
        Assert.Equal(expected, ItemizadoImportParser.BuildImportedTitleId(code, fallback));
    }

    // ─── ParseDecimal ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", 1.5)]
    [InlineData("1.250", 1250)]       // formato chileno: X.000 con 3 dec. es miles (no decimal)
    [InlineData("1,250", 1250)]       // ídem con coma
    [InlineData("12.250", 12250)]     // >1 dígito entero → miles con punto
    [InlineData("12,250", 12250)]     // >1 dígito entero → miles con coma
    [InlineData("1.250,50", 1250.50)] // chileno
    [InlineData("1,250.50", 1250.50)] // inglés
    [InlineData("0.035", 0.035)]      // decimal con punto
    [InlineData("0,035", 0.035)]      // decimal con coma
    [InlineData("=A1+B1", 0)]         // fórmula sin evaluar
    [InlineData("=SUMA(A1:A4)", 0)]   // fórmula de Excel real
    [InlineData("1 250,75", 1250.75)] // separador no-break-space
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    public void ParseDecimal_ParsesFormats(string input, double expectedDouble)
    {
        decimal expected = (decimal)expectedDouble;
        Assert.Equal(expected, ItemizadoImportParser.ParseDecimal(input));
    }

    // ─── GetCell ─────────────────────────────────────────────────────────

    [Fact]
    public void GetCell_ReturnsSafeValue()
    {
        var row = new List<string> { "A", "B", "C" };
        Assert.Equal("A", ItemizadoImportParser.GetCell(row, 0));
        Assert.Equal("C", ItemizadoImportParser.GetCell(row, 2));
        Assert.Equal("", ItemizadoImportParser.GetCell(row, 5));
        Assert.Equal("", ItemizadoImportParser.GetCell(null!, 0));
    }
}
