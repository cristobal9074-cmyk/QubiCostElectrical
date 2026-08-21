using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Lógica del banco de precios reutilizable: normalización, fusión por antigüedad,
/// consolidación y propuesta de actualizaciones a un proyecto.
/// </summary>
public sealed class PriceBankServiceTests
{
    private static readonly PriceBankService Svc = new();

    private static MaterialRow Mat(string codigo, decimal matUf, decimal moUf, DateTime fecha)
        => new() { Codigo = codigo, Descripcion = "d", Unidad = "UN", MatUf = matUf, MoUf = moUf, FechaPrecio = fecha };

    [Theory]
    [InlineData(" thhn-2.5 ", "THHN-2.5")]
    [InlineData("pvc20", "PVC20")]
    public void NormalizeCode_TrimsAndUppercases(string input, string expected)
        => Assert.Equal(expected, Svc.NormalizeCode(input));

    [Fact]
    public void IsNewer_OnlyWhenStrictlyMoreRecent()
    {
        var older = new PriceBankEntry { FechaPrecio = new DateTime(2026, 1, 1) };
        var newer = new PriceBankEntry { FechaPrecio = new DateTime(2026, 6, 1) };

        Assert.True(Svc.IsNewer(newer, older));
        Assert.False(Svc.IsNewer(older, newer));
        Assert.False(Svc.IsNewer(older, older)); // empate conserva el banco
        Assert.True(Svc.IsNewer(newer, null));   // banco vacío acepta cualquiera
    }

    [Fact]
    public void Merge_KeepsExisting_WhenIncomingNotNewer()
    {
        var existing = new PriceBankEntry { Codigo = "A", MatUf = 10m, FechaPrecio = new DateTime(2026, 6, 1) };
        var incoming = new PriceBankEntry { Codigo = "A", MatUf = 99m, FechaPrecio = new DateTime(2026, 1, 1) };

        var result = Svc.Merge(existing, incoming);

        Assert.Equal(10m, result.MatUf); // gana el banco, no el precio viejo entrante
    }

    [Fact]
    public void Merge_FillsMissingMetadataFromExisting()
    {
        var existing = new PriceBankEntry { Codigo = "A", Descripcion = "Cable THHN", Categoria = "Conductores", FechaPrecio = new DateTime(2026, 1, 1) };
        var incoming = new PriceBankEntry { Codigo = "A", Descripcion = "", Categoria = "", MatUf = 5m, FechaPrecio = new DateTime(2026, 6, 1) };

        var result = Svc.Merge(existing, incoming);

        Assert.Equal(5m, result.MatUf);                 // precio nuevo
        Assert.Equal("Cable THHN", result.Descripcion); // descripción rescatada del banco
        Assert.Equal("Conductores", result.Categoria);
    }

    [Fact]
    public void Consolidate_DedupesByCode_KeepingNewestPrice()
    {
        var materiales = new[]
        {
            Mat("X1", 10m, 1m, new DateTime(2026, 1, 1)),
            Mat("x1", 20m, 2m, new DateTime(2026, 6, 1)), // mismo código, más nuevo
            Mat("X2", 5m, 0m, new DateTime(2026, 3, 1))
        };

        var bank = Svc.Consolidate(materiales).ToDictionary(e => e.Codigo);

        Assert.Equal(2, bank.Count);
        Assert.Equal(20m, bank["X1"].MatUf); // gana el de junio
        Assert.Equal(5m, bank["X2"].MatUf);
    }

    [Fact]
    public void Consolidate_IgnoresBlankCodes()
    {
        var materiales = new[] { Mat("", 10m, 0m, DateTime.Today), Mat("   ", 20m, 0m, DateTime.Today) };
        Assert.Empty(Svc.Consolidate(materiales));
    }

    [Fact]
    public void BuildProjectUpdates_ProposesOnlyNewerAndChangedPrices()
    {
        var bank = new Dictionary<string, PriceBankEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new() { Codigo = "A", MatUf = 12m, MoUf = 1m, FechaPrecio = new DateTime(2026, 6, 1) },
            ["B"] = new() { Codigo = "B", MatUf = 9m, MoUf = 0m, FechaPrecio = new DateTime(2026, 6, 1) },
            ["C"] = new() { Codigo = "C", MatUf = 7m, MoUf = 0m, FechaPrecio = new DateTime(2026, 1, 1) }
        };
        var project = new[]
        {
            Mat("A", 10m, 1m, new DateTime(2026, 1, 1)), // banco más nuevo y distinto → actualiza
            Mat("B", 9m, 0m, new DateTime(2026, 1, 1)),  // banco más nuevo pero igual precio → no
            Mat("C", 5m, 0m, new DateTime(2026, 3, 1)),  // banco más viejo → no
            Mat("Z", 1m, 0m, new DateTime(2026, 1, 1))   // no está en banco → no
        };

        var updates = Svc.BuildProjectUpdates(project, bank);

        var u = Assert.Single(updates);
        Assert.Equal("A", u.Codigo);
        Assert.Equal(10m, u.MatUfAnterior);
        Assert.Equal(12m, u.MatUfNuevo);
    }
}
