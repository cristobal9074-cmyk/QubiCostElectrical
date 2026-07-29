using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class PegarItemizadoUseCaseTests
{
    private readonly PegarItemizadoUseCase _sut = new();

    // ─── Casos base ───────────────────────────────────────────────────────

    [Fact]
    public void Execute_EmptyText_ReturnsNoRows()
    {
        var result = _sut.Execute("");
        Assert.Empty(result.Rows);
        Assert.NotEmpty(result.Warning);
    }

    [Fact]
    public void Execute_Whitespace_ReturnsNoRows()
    {
        var result = _sut.Execute("   \n\t\n  ");
        Assert.Empty(result.Rows);
    }

    // ─── Con encabezados ─────────────────────────────────────────────────

    [Fact]
    public void Execute_WithHeaders_DetectsColumnsAndBuildsRows()
    {
        string text =
            "Ítem\tDescripción\tUnidad\tCantidad\n" +
            "1\tTABLERO GENERAL\tGL\t1\n" +
            "1.1\tCableado monofásico\tm\t120\n" +
            "1.2\tDisyuntores 2P 20A\tun\t8\n";

        var result = _sut.Execute(text);

        Assert.Equal(3, result.Rows.Count);
        Assert.True(result.Rows[0].EsTitulo);
        Assert.Equal("TABLERO GENERAL", result.Rows[0].Descripcion);
        Assert.False(result.Rows[1].EsTitulo);
        Assert.Equal("Cableado monofásico", result.Rows[1].Descripcion);
        Assert.Equal("m", result.Rows[1].Unidad);
        Assert.Equal(120m, result.Rows[1].CantidadOriginal);
        Assert.Equal(0m, result.Rows[1].Cantidad);
    }

    [Fact]
    public void Execute_WithHeaders_FilaEncabezadoRepetidaIgnorada()
    {
        string text =
            "ITEM\tDESCRIPCION\tUNIDAD\n" +
            "ITEM\tDESCRIPCION\tUNIDAD\n" +   // encabezado repetido en medio
            "1\tTABLERO\tGL\n" +
            "1.1\tCableado\tm\n";

        var result = _sut.Execute(text);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("TABLERO", result.Rows[0].Descripcion);
    }

    // ─── Sin encabezados (detección posicional) ───────────────────────────

    [Fact]
    public void Execute_WithoutHeaders_DetectsColumnsHeuristically()
    {
        string text =
            "1\tTABLERO GENERAL\tGL\n" +
            "1.1\tCableado monofásico\tm\n" +
            "1.2\tDisyuntores 2P 20A\tun\n";

        var result = _sut.Execute(text);

        Assert.Equal(3, result.Rows.Count);
        Assert.True(result.Rows[0].EsTitulo);
    }

    [Fact]
    public void Execute_SingleColumn_EachLineIsPartida()
    {
        string text =
            "Instalación de tablero general\n" +
            "Cableado circuitos monofásicos\n" +
            "Disyuntores termomagnéticos\n";

        var result = _sut.Execute(text);

        Assert.Equal(3, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.False(r.EsTitulo));
        Assert.Equal("Instalación de tablero general", result.Rows[0].Descripcion);
    }

    // ─── Clasificación de títulos ─────────────────────────────────────────

    [Fact]
    public void Execute_AllCapsDescription_ClassifiedAsTitle()
    {
        string text = "1\tINSTALACIONES ELECTRICAS\tGL\n1.1\tCableado\tm\n";
        var result = _sut.Execute(text);

        Assert.True(result.Rows[0].EsTitulo);
        Assert.False(result.Rows[1].EsTitulo);
    }

    [Fact]
    public void Execute_TitleCodeNormalizesId()
    {
        string text = "2\tSEGUNDA SECCION\tGL\n2.1\tPartida A\tun\n";
        var result = _sut.Execute(text);

        Assert.Equal("2.0", result.Rows[0].Id);
        Assert.StartsWith("2.0.", result.Rows[1].Id);
    }

    // ─── IDs asignados correctamente ─────────────────────────────────────

    [Fact]
    public void Execute_AssignsWbsCodeAndParentId_like_excel_import()
    {
        string text =
            "1\tTitulo uno\tGL\n" +
            "1.1\tPartida 1\tun\n" +
            "2\tTitulo dos\tGL\n" +
            "2.1\tPartida 3\tm\n";

        var result = _sut.Execute(text);

        Assert.Equal("1.0", result.Rows[0].Id);
        Assert.Equal("1", result.Rows[0].WBSCode);
        Assert.Equal("", result.Rows[0].ParentId);
        Assert.True(result.Rows[0].EsTitulo);
        Assert.Equal(1, result.Rows[0].NivelJerarquico);

        var partida = result.Rows[1];
        Assert.StartsWith("1.0.", partida.Id);
        Assert.Equal("1.1", partida.WBSCode);
        Assert.Equal("1.0", partida.ParentId);
        Assert.False(partida.EsTitulo);
        Assert.Equal(2, partida.NivelJerarquico);

        Assert.Equal("2", result.Rows[2].WBSCode);
        Assert.Equal("2.0", result.Rows[3].ParentId);
    }

    [Fact]
    public void Execute_AssignsHierarchicalIds()
    {
        string text =
            "1\tTitulo uno\tGL\n" +
            "1.1\tPartida 1\tun\n" +
            "1.2\tPartida 2\tun\n" +
            "2\tTitulo dos\tGL\n" +
            "2.1\tPartida 3\tm\n";

        var result = _sut.Execute(text);

        Assert.Equal("1.0", result.Rows[0].Id);
        Assert.StartsWith("1.0.", result.Rows[1].Id);
        Assert.StartsWith("1.0.", result.Rows[2].Id);
        Assert.Equal("2.0", result.Rows[3].Id);
        Assert.StartsWith("2.0.", result.Rows[4].Id);
    }

    // ─── Cantidad y unidad ────────────────────────────────────────────────

    [Fact]
    public void Execute_ParsesChileanDecimalQuantity()
    {
        string text = "ITEM\tDESCRIPCION\tUNIDAD\tCANTIDAD\n1.1\tCableado\tm\t1.250,50\n";
        var result = _sut.Execute(text);

        Assert.Single(result.Rows);
        Assert.Equal(1250.50m, result.Rows[0].CantidadOriginal);
        Assert.Equal(0m, result.Rows[0].Cantidad);
    }

    [Fact]
    public void Execute_EmptyQuantityDefaultsToOne()
    {
        string text = "ITEM\tDESCRIPCION\tUNIDAD\tCANTIDAD\n1.1\tCableado\tm\t\n";
        var result = _sut.Execute(text);

        Assert.Single(result.Rows);
        Assert.Equal(1m, result.Rows[0].CantidadOriginal);
        Assert.Equal(0m, result.Rows[0].Cantidad);
    }

    [Fact]
    public void Execute_EmptyUnitPreservedAsEmpty()
    {
        string text = "ITEM\tDESCRIPCION\tUNIDAD\n1.1\tCableado en metro lineal\t\n";
        var result = _sut.Execute(text);

        Assert.Single(result.Rows);
        // Unidad vacía del portapapeles se preserva como "" (sin adivinar)
        Assert.Equal("", result.Rows[0].Unidad);
    }

    // ─── ParseTable ───────────────────────────────────────────────────────

    [Fact]
    public void ParseTable_SkipsBlankLines()
    {
        string text = "A\tB\n\nC\tD\n\n";
        var table = PegarItemizadoUseCase.ParseTable(text);
        Assert.Equal(2, table.Count);
    }

    [Fact]
    public void ParseTable_HandlesCrLf()
    {
        string text = "A\tB\r\nC\tD\r\n";
        var table = PegarItemizadoUseCase.ParseTable(text);
        Assert.Equal(2, table.Count);
        Assert.Equal("A", table[0][0]);
        Assert.Equal("B", table[0][1]);
    }

    [Fact]
    public void Execute_AllCapsDescriptionWithItemCode_IsPartidaNotTitle()
    {
        string text =
            "Ítem\tDescripción\tUnidad\tCantidad\n" +
            "1.1\tINSTALACIONES ELÉCTRICAS\tUN\t5\n";

        var result = _sut.Execute(text);

        Assert.Single(result.Rows);
        Assert.False(result.Rows[0].EsTitulo);
        Assert.Equal("INSTALACIONES ELÉCTRICAS", result.Rows[0].Descripcion);
    }

    // ─── IsLikelyTitle ───────────────────────────────────────────────────

    [Theory]
    [InlineData("INSTALACIONES ELÉCTRICAS", true)]
    [InlineData("I. Instalaciones", true)]
    [InlineData("IV. Montaje", true)]
    [InlineData("Cableado monofásico", false)]
    [InlineData("", false)]
    public void IsLikelyTitle_ClassifiesCorrectly(string desc, bool expected)
    {
        Assert.Equal(expected, PegarItemizadoUseCase.IsLikelyTitle(desc));
    }
}
