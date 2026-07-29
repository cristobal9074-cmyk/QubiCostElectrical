using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ProjectPriceFreezeServiceTests
{
    private static readonly ProjectPriceFreezeService Svc = new();

    private static MaterialRow Mat(string codigo, decimal matUf, decimal moUf = 0m)
        => new()
        {
            Codigo = codigo,
            Descripcion = "Cable",
            MatUf = matUf,
            MoUf = moUf,
            Proveedor = "Proveedor A",
            MonedaPrecio = Moneda.UF,
            FechaPrecio = new DateTime(2026, 3, 1),
            UfUsadaClp = 39000m
        };

    [Fact]
    public void Freeze_SnapshotsMaterialsAndProjectFx()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                ValorUfClp = 39500m,
                TipoCambioUsdClp = 920m,
                FechaTipoCambio = new DateTime(2026, 6, 1)
            },
            Materiales = { Mat("A", 1.2m, 0.3m) }
        };

        Svc.Freeze(data, "Cristóbal");

        Assert.True(data.Proyecto.PreciosCongelados);
        Assert.Equal(39500m, data.Proyecto.ValorUfClpCongelado);
        Assert.Equal(920m, data.Proyecto.TipoCambioUsdClpCongelado);
        Assert.NotNull(data.Materiales[0].PrecioCongelado);
        // Fase 5: el snapshot guarda CLP como fuente; MatUf queda derivado.
        // UfUsadaClp del material (39000) manda sobre ValorUfClp del proyecto (39500).
        Assert.Equal(46800m, data.Materiales[0].PrecioCongelado!.MatClp); // 1.2 × 39000
        Assert.Equal(11700m, data.Materiales[0].PrecioCongelado!.MoClp);  // 0.3 × 39000
        Assert.Equal(1.2m, data.Materiales[0].PrecioCongelado!.MatUf);
        Assert.Equal(Moneda.CLP, data.Materiales[0].PrecioCongelado!.MonedaPrecio);
        Assert.Equal("Proveedor A", data.Materiales[0].PrecioCongelado!.Proveedor);
        Assert.Equal("Cristóbal", data.Proyecto.UsuarioCongelamientoPrecios);
    }

    [Fact]
    public void ApplyBankPrices_BlockedWhenFrozen_UnlessExplicit()
    {
        var repo = new PriceBankApplicationServiceTests.FakePriceBankRepository();
        repo.Upsert(new PriceBankEntry
        {
            Codigo = "A",
            MatUf = 20m,
            MoUf = 2m,
            FechaPrecio = new DateTime(2026, 6, 1)
        });
        var bank = new PriceBankApplicationService(repo);
        var proyecto = new ProjectInfoRow { PreciosCongelados = true };
        var materials = new List<MaterialRow> { Mat("A", 10m) };

        var blocked = bank.ApplyBankPricesToProject(materials, proyecto);
        Assert.Equal(PriceBankApplyStatus.BlockedByFrozenPrices, blocked.Status);
        Assert.Equal(10m, materials[0].MatUf);

        var applied = bank.ApplyBankPricesToProject(materials, proyecto, allowWhenFrozen: true);
        Assert.Equal(PriceBankApplyStatus.Applied, applied.Status);
        Assert.Equal(20m, materials[0].MatUf);
        Assert.Equal("Banco de precios", materials[0].OrigenPrecio);
        Assert.NotNull(materials[0].PrecioCongelado);
        Assert.Equal(20m, materials[0].PrecioCongelado!.MatUf);
        Assert.True(materials[0].PrecioCongelado!.MatClp > 0m);
    }
}
