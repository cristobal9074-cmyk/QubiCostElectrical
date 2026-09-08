using System;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class PriceConversionTests
{
    [Fact]
    public void ClpToUf_DivideYRedondeaA4Decimales()
    {
        Assert.Equal(0.1282m, PriceConversion.ClpToUf(5000m, 39000m));
    }

    [Fact]
    public void ClpToUf_ValorUfCero_UsaValorUfPorDefecto()
    {
        Assert.Equal(0.1282m, PriceConversion.ClpToUf(5000m, 0m));
    }

    [Fact]
    public void UfToClp_MultiplicaYRedondeaAEntero()
    {
        Assert.Equal(5000m, PriceConversion.UfToClp(0.1282051282m, 39000m));
    }

    [Fact]
    public void UfToClp_ValorUfCero_RetornaCero()
    {
        Assert.Equal(0m, PriceConversion.UfToClp(1.2m, 0m));
    }

    [Fact]
    public void ClpToUf_ClpCero_RetornaCero()
    {
        Assert.Equal(0m, PriceConversion.ClpToUf(0m, 39000m));
    }

    // ── MaterialRow.RecalcularUf / RequiereRevisionPrecio ──────────────────────

    [Fact]
    public void MaterialRow_RecalcularUf_DerivaMatUfYMoUfDesdeClp()
    {
        var m = new MaterialRow { MatClp = 5000m, MoClp = 2000m };

        m.RecalcularUf(39000m);

        Assert.Equal(0.1282m, m.MatUf);
        Assert.Equal(0.0513m, m.MoUf);
        Assert.Equal(39000m, m.UfUsadaClp);
    }

    [Fact]
    public void MaterialRow_RecalcularUf_ConValorUfCero_NoActualizaUfUsadaClp()
    {
        var m = new MaterialRow { MatClp = 5000m, UfUsadaClp = 38000m };

        m.RecalcularUf(0m);

        Assert.Equal(0.1282m, m.MatUf);
        Assert.Equal(38000m, m.UfUsadaClp); // conserva el último valor válido
    }

    [Fact]
    public void MaterialRow_RequiereRevisionPrecio_MenosDe6Meses_EsFalse()
    {
        var m = new MaterialRow { FechaPrecio = DateTime.Today.AddDays(-30) };
        Assert.False(m.RequiereRevisionPrecio);
    }

    [Fact]
    public void MaterialRow_RequiereRevisionPrecio_6MesesOMas_EsTrue()
    {
        var m = new MaterialRow { FechaPrecio = DateTime.Today.AddDays(-181) };
        Assert.True(m.RequiereRevisionPrecio);
    }

    // ── PriceBankEntry.RecalcularUf / RequiereRevisionPrecio ───────────────────

    [Fact]
    public void PriceBankEntry_RecalcularUf_DerivaMatUfYMoUfDesdeClp()
    {
        var e = new PriceBankEntry { MatClp = 10000m, MoClp = 3000m };

        e.RecalcularUf(39000m);

        Assert.Equal(0.2564m, e.MatUf);
        Assert.Equal(0.0769m, e.MoUf);
    }

    [Fact]
    public void PriceBankEntry_RequiereRevisionPrecio_6MesesOMas_EsTrue()
    {
        var e = new PriceBankEntry { FechaPrecio = DateTime.Today.AddDays(-180) };
        Assert.True(e.RequiereRevisionPrecio);
    }
}
