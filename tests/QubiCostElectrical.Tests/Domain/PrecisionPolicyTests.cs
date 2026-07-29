using Xunit;

namespace QubiCostElectrical.Tests;

public class PrecisionPolicyTests
{
    [Fact]
    public void RoundCantidad_Uses4Decimals()
    {
        Assert.Equal(1.1234m, PrecisionPolicy.RoundCantidad(1.12345m)); // .12345 → .1234 (ToEven)
        Assert.Equal(1.1235m, PrecisionPolicy.RoundCantidad(1.123451m)); // .123451 → .1235 (>5)
    }

    [Fact]
    public void RoundPrecioUnitario_Uses4Decimals()
    {
        Assert.Equal(10.1234m, PrecisionPolicy.RoundPrecioUnitario(10.12345m)); // .12345 → .1234 (ToEven)
    }

    [Fact]
    public void RoundImporte_Uses2Decimals()
    {
        Assert.Equal(100.12m, PrecisionPolicy.RoundImporte(100.123m));
        Assert.Equal(100.12m, PrecisionPolicy.RoundImporte(100.125m)); // ToEven: .125 → .12
        Assert.Equal(100.12m, PrecisionPolicy.RoundImporte(100.1249m));
    }

    [Fact]
    public void RoundPorcentaje_Uses2Decimals()
    {
        Assert.Equal(15.12m, PrecisionPolicy.RoundPorcentaje(15.123m));
    }

    [Fact]
    public void RoundFactor_Uses6Decimals()
    {
        Assert.Equal(1.123457m, PrecisionPolicy.RoundFactor(1.1234567m));
    }

    [Fact]
    public void RoundTasaCambio_Uses6Decimals()
    {
        Assert.Equal(850.123457m, PrecisionPolicy.RoundTasaCambio(850.1234567m));
    }

    [Fact]
    public void BankerRounding_MidpointToEven()
    {
        // 1.995 → 2 decimals: .995, digit before is 9 (odd) → rounds up to 2.00
        Assert.Equal(2.00m, PrecisionPolicy.RoundImporte(1.995m));
        // 1.005 → 2 decimals: .005, digit before is 0 (even) → stays at 1.00
        Assert.Equal(1.00m, PrecisionPolicy.RoundImporte(1.005m));
    }

    [Fact]
    public void Apply_RoundsToSpecifiedDecimals()
    {
        Assert.Equal(3.142m, PrecisionPolicy.Apply(3.14159m, 3));
    }

    [Fact]
    public void CantidadDecimals_Constant()
    {
        Assert.Equal(4, PrecisionPolicy.CantidadDecimals);
    }

    [Fact]
    public void ImporteDecimals_Constant()
    {
        Assert.Equal(2, PrecisionPolicy.ImporteDecimals);
    }

    [Fact]
    public void FactorDecimals_Constant()
    {
        Assert.Equal(6, PrecisionPolicy.FactorDecimals);
    }

    [Fact]
    public void PrecioUnitarioDecimals_Constant()
    {
        Assert.Equal(4, PrecisionPolicy.PrecioUnitarioDecimals);
    }
}
