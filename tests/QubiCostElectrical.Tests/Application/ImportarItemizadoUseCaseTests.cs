using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;


namespace QubiCostElectrical.Tests.Application;

public sealed class ImportarItemizadoUseCaseTests
{
    private readonly ImportarItemizadoUseCase _sut = new();

    // ─── Casos de error ───────────────────────────────────────────────────

    [Fact]
    public void Execute_NullTable_ReturnsError()
    {
        var result = _sut.Execute(null!);
        Assert.NotEmpty(result.Error);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Execute_EmptyTable_ReturnsError()
    {
        var result = _sut.Execute(new List<List<string>>());
        Assert.NotEmpty(result.Error);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Execute_TableWithoutRecognizableHeaders_ReturnsError()
    {
        var table = new List<List<string>>
        {
            new() { "1", "Cableado monofásico", "m" },
            new() { "2", "Tablero general", "GL" }
        };
        var result = _sut.Execute(table);
        Assert.NotEmpty(result.Error);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Execute_TableWithHeadersButNoDescColumn_ReturnsError()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "UNIDAD", "CANTIDAD" },
            new() { "1.1", "m", "10" }
        };
        var result = _sut.Execute(table);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public void Execute_HeadersOnlyNoDataRows_ReturnsError()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" }
        };
        var result = _sut.Execute(table);
        Assert.NotEmpty(result.Error);
    }

    // ─── Casos de éxito ───────────────────────────────────────────────────

    [Fact]
    public void Execute_ValidTable_ReturnsRows()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" },
            new() { "1", "TABLERO GENERAL", "GL", "1" },
            new() { "1.1", "Cableado monofásico", "m", "120" },
            new() { "1.2", "Disyuntores 2P 20A", "un", "8" }
        };

        var result = _sut.Execute(table);

        Assert.Empty(result.Error);
        Assert.Equal(3, result.Rows.Count);
    }

    [Fact]
    public void Execute_ValidTable_ClassifiesTitlesCorrectly()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" },
            new() { "1", "TABLERO GENERAL", "GL", "1" },
            new() { "1.1", "Cableado monofásico", "m", "120" },
            new() { "2", "ILUMINACION", "GL", "1" },
            new() { "2.1", "Luminarias LED", "un", "12" }
        };

        var result = _sut.Execute(table);

        Assert.True(result.Rows[0].EsTitulo);
        Assert.False(result.Rows[1].EsTitulo);
        Assert.True(result.Rows[2].EsTitulo);
        Assert.False(result.Rows[3].EsTitulo);
    }

    [Fact]
    public void Execute_ValidTable_AssignsHierarchicalIds()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD" },
            new() { "1", "TITULO UNO", "GL" },
            new() { "1.1", "Partida A", "un" },
            new() { "1.2", "Partida B", "m" },
            new() { "2", "TITULO DOS", "GL" },
            new() { "2.1", "Partida C", "m" }
        };

        var result = _sut.Execute(table);

        Assert.Equal("1.0", result.Rows[0].Id);
        Assert.StartsWith("1.0.", result.Rows[1].Id);
        Assert.StartsWith("1.0.", result.Rows[2].Id);
        Assert.Equal("2.0", result.Rows[3].Id);
        Assert.StartsWith("2.0.", result.Rows[4].Id);
    }

    [Fact]
    public void Execute_ValidTable_ParsesChileanDecimalQuantity()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" },
            new() { "1", "TITULO", "GL", "1" },
            new() { "1.1", "Cableado", "m", "1.250,50" }
        };

        var result = _sut.Execute(table);

        Assert.Equal(1250.50m, result.Rows[1].CantidadOriginal);
        Assert.Equal(0m, result.Rows[1].Cantidad);
    }

    [Fact]
    public void Execute_ValidTable_EmptyQuantityDefaultsToOne()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" },
            new() { "1", "TITULO", "GL", "" },
            new() { "1.1", "Partida", "m", "" }
        };

        var result = _sut.Execute(table);

        Assert.Equal(1m, result.Rows[1].CantidadOriginal);
        Assert.Equal(0m, result.Rows[1].Cantidad);
    }

    [Fact]
    public void Execute_clamps_zero_quantity_to_one_for_partidas()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" },
            new() { "1,0", "CAPITULO", "GL", "" },
            new() { "1,1", "Cableado", "m", "0" }
        };

        var result = _sut.Execute(table);

        Assert.Empty(result.Error);
        Assert.Equal(1m, result.Rows[1].CantidadOriginal);
        Assert.Equal(0m, result.Rows[1].Cantidad);
    }

    [Fact]
    public void Execute_ValidTable_EmptyUnitPreservedAsEmpty()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD" },
            new() { "1.1", "Partida sin unidad", "" }
        };

        var result = _sut.Execute(table);

        // Unidad vacía del Excel se preserva como "" (sin inventar "GL")
        Assert.Equal("", result.Rows[0].Unidad);
    }

    [Fact]
    public void Execute_HeadersWithAccents_FoundCorrectly()
    {
        var table = new List<List<string>>
        {
            new() { "Ítem", "Descripción", "Ud.", "Cantidad" },
            new() { "1", "TITULO", "GL", "1" },
            new() { "1.1", "Partida A", "un", "5" }
        };

        var result = _sut.Execute(table);

        Assert.Empty(result.Error);
        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Execute_RepeatedHeaderRowInData_IsIgnored()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD" },
            new() { "ITEM", "DESCRIPCION", "UNIDAD" },   // repetición al inicio de datos
            new() { "1", "TITULO", "GL" },
            new() { "1.1", "Partida", "m" }
        };

        var result = _sut.Execute(table);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("TITULO", result.Rows[0].Descripcion);
    }

    [Fact]
    public void Execute_HeaderFoundBelowFirstRow_StillWorks()
    {
        var table = new List<List<string>>
        {
            new() { "Proyecto ABC" },          // fila de metadata antes del encabezado
            new() { "Cliente: XYZ" },
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" },
            new() { "1", "TABLERO GENERAL", "GL", "1" },
            new() { "1.1", "Cableado", "m", "50" }
        };

        var result = _sut.Execute(table);

        Assert.Empty(result.Error);
        Assert.Equal(2, result.Rows.Count);
    }

    // ─── Diferencia con PegarItemizadoUseCase ────────────────────────────

    [Fact]
    public void Execute_AllCapsDescriptionWithoutTitleCode_IsNotTitle()
    {
        // A diferencia del clipboard, en Excel no usamos heurística all-caps
        // para detectar títulos — sólo el código (1, 1.0, etc.).
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD" },
            new() { "1.1", "DESCRIPCION EN MAYUSCULAS", "GL" }
        };

        var result = _sut.Execute(table);

        Assert.Single(result.Rows);
        Assert.False(result.Rows[0].EsTitulo);
    }

    [Fact]
    public void Execute_ItemAndDescriptionOnlyHeaders_InfersUnitAndQuantityColumns()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "", "" },
            new() { "", "", "", "" },
            new() { "2.8.3", "Suministro e instalación de cable", "ml", "251" }
        };

        var result = _sut.Execute(table);

        Assert.Empty(result.Error);
        Assert.Single(result.Rows);
        Assert.Equal("ml", result.Rows[0].Unidad);
        Assert.Equal(251m, result.Rows[0].CantidadOriginal);
        Assert.Equal(0m, result.Rows[0].Cantidad);
    }

    [Fact]
    public void Execute_SkipsEmptyRowsBeforeFirstDataRow()
    {
        var table = new List<List<string>>
        {
            new() { "ITEM", "DESCRIPCION", "UNIDAD", "CANTIDAD" },
            new() { "", "", "", "" },
            new() { "", "", "", "" },
            new() { "1.1", "Cableado monofásico", "m", "120" }
        };

        var result = _sut.Execute(table);

        Assert.Empty(result.Error);
        Assert.Single(result.Rows);
        Assert.Equal("Cableado monofásico", result.Rows[0].Descripcion);
    }

    [Fact]
    public void Execute_WithExplicitMapping_ReturnsMappedRows()
    {
        var table = new List<List<string>>
        {
            new() { "Ignorado 1", "Ignorado 2", "Ignorado 3" },
            new() { "MiCodigo", "MiDesc", "MiCant", "MiUnidad" },
            new() { "1.1", "Tendido de Bandeja", "20", "m" },
            new() { "1.2", "Puesta a tierra", "1", "GL" }
        };

        var result = _sut.Execute(table, 1, 0, 1, 3, 2, 2);

        Assert.Empty(result.Error);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("1.1", result.Rows[0].Item);
        Assert.Equal("Tendido de Bandeja", result.Rows[0].Descripcion);
        Assert.Equal("m", result.Rows[0].Unidad);
        Assert.Equal(20m, result.Rows[0].CantidadOriginal);
        Assert.Equal(0m, result.Rows[0].Cantidad);
    }

    /// <summary>
    /// Reproduce el formato exacto del Excel "ITEMIZADO PSIO 3 a cotizar":
    /// - Fila 0: ITEM en col 1, DESCRIPCION en col 2 (col 0 vacía)
    /// - Fila 1: UNID. en col 3, NUEVAS CANTIDADES en col 4
    /// - Fila 2+: datos
    /// </summary>
    [Fact]
    public void Execute_TwoRowHeader_PsioFormat_DetectsColumnsCorrectly()
    {
        var table = new List<List<string>>
        {
            // Fila Excel 3: encabezado principal
            new() { "", "ITEM", "DESCRIPCION", "", "", "" },
            // Fila Excel 4: encabezado secundario con unidad/cantidad
            new() { "", "", "", "UNID.", "NUEVAS CANTIDADES", "" },
            // Fila Excel 5: título de sección
            new() { "", "2", "ITEMIZADO ELÉCTRICO", "", "", "" },
            // Fila Excel 6: subsección con asterisco
            new() { "", "*", "PROYECTO ELECTRICO E ILUMINACION", "", "", "" },
            // Fila Excel 8: partida con datos
            new() { "", "2.1.11", "Suministro e Instalación Circuito 3x1,5mm²", "ml", "2.520", "" },
            // Fila Excel 9: otra partida
            new() { "", "2.1.14", "Cable unipolar 1x1,5mm² H07Z1-K", "ml", "1.530", "" },
        };

        var result = _sut.Execute(table);

        Assert.Empty(result.Error);
        // El título "2" y el título "*" deben ser títulos; 2 partidas deben ser partidas
        var titulos = result.Rows.Where(r => r.EsTitulo).ToList();
        var partidas = result.Rows.Where(r => !r.EsTitulo).ToList();
        Assert.Equal(2, titulos.Count);
        Assert.Equal(2, partidas.Count);
        // Verificar cantidad: "2.520" debe parsear como 2520
        Assert.Equal(2520m, partidas[0].CantidadOriginal);
        Assert.Equal(1530m, partidas[1].CantidadOriginal);
        // Unidad exacta del Excel
        Assert.Equal("ml", partidas[0].Unidad);
        Assert.Equal("ml", partidas[1].Unidad);
    }
}

