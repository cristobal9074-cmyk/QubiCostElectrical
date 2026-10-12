using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class CurrencyConversionServiceTests
{
    private readonly CurrencyConversionService _service = new();

    [Theory]
    [InlineData("UF", "UF")]
    [InlineData("CLP", "CLP")]
    [InlineData("USD", "USD")]
    [InlineData("PESOS", "CLP")]
    [InlineData("DOLAR", "USD")]
    [InlineData("DÓLAR", "USD")]
    [InlineData("us$", "USD")]
    [InlineData("", "UF")]
    [InlineData(null, "UF")]
    [InlineData("XXX", "UF")]
    public void NormalizeCurrency_Various(string? input, string expected)
    {
        Assert.Equal(expected, _service.NormalizeCurrency(input));
    }

    [Theory]
    [InlineData("UF", "N3")]
    [InlineData("CLP", "N0")]
    [InlineData("PESOS", "N0")]
    [InlineData("USD", "N2")]
    public void GetNumberFormat_Various(string currency, string expected)
    {
        Assert.Equal(expected, _service.GetNumberFormat(currency));
    }

    [Fact]
    public void ConvertUfToClp_Multiplies()
    {
        Assert.Equal(36000m, _service.ConvertUfToClp(10m, 3600m));
    }

    [Fact]
    public void ConvertUfToClp_ZeroRate_ReturnsUf()
    {
        Assert.Equal(10m, _service.ConvertUfToClp(10m, 0m));
    }

    [Fact]
    public void ConvertClpToUf_Divides()
    {
        Assert.Equal(10m, _service.ConvertClpToUf(36000m, 3600m));
    }

    [Fact]
    public void ConvertUfToUsd_Converts()
    {
        decimal result = _service.ConvertUfToUsd(10m, 3600m, 900m);
        Assert.Equal(40m, result);
    }

    [Fact]
    public void ConvertUsdToUf_Converts()
    {
        decimal result = _service.ConvertUsdToUf(40m, 3600m, 900m);
        Assert.Equal(10m, result);
    }

    [Fact]
    public void ConvertUfToCurrency_Uf_ReturnsSame()
    {
        Assert.Equal(10m, _service.ConvertUfToCurrency(10m, "UF", 3600m, 900m));
    }

    [Fact]
    public void ConvertCurrencyToUf_Uf_ReturnsSame()
    {
        Assert.Equal(10m, _service.ConvertCurrencyToUf(10m, "UF", 3600m, 900m));
    }

    [Fact]
    public void FormatMoneyFromUf_Uf_FormatsN3()
    {
        string result = _service.FormatMoneyFromUf(10.5m, "UF", 3600m, 900m);
        Assert.Contains("UF", result);
        Assert.Contains("10,500", result);
    }

    [Fact]
    public void FormatMoneyFromUf_Clp_FormatsPesos()
    {
        string result = _service.FormatMoneyFromUf(10m, "CLP", 3600m, 900m);
        Assert.Contains("CLP", result);
        Assert.Contains("$", result);
    }

    [Fact]
    public void FormatMoneyFromUf_Usd_FormatsDollars()
    {
        string result = _service.FormatMoneyFromUf(10m, "USD", 3600m, 900m);
        Assert.Contains("US$", result);
    }

    [Fact]
    public void FormatMoneyFromUfCompact_Shorter()
    {
        string result = _service.FormatMoneyFromUfCompact(10m, "UF", 3600m, 900m);
        Assert.Contains("UF", result);
        Assert.DoesNotContain("CLP", result);
    }

    [Fact]
    public void FormatMoneyPairFromClp_Clp_HasNoSecondary_SoNeverShowsUf()
    {
        var (primary, secondary) = _service.FormatMoneyPairFromClp(1_000_000m, "CLP", 3600m, 900m);
        Assert.StartsWith("$", primary);
        Assert.DoesNotContain("UF", primary);
        Assert.Equal("", secondary);
    }

    [Fact]
    public void FormatMoneyPairFromClp_Uf_ShowsUfPrimaryAndClpSecondary()
    {
        var (primary, secondary) = _service.FormatMoneyPairFromClp(36_000m, "UF", 3600m, 900m);
        Assert.Contains("UF", primary);
        Assert.StartsWith("$", secondary);
        Assert.DoesNotContain("UF", secondary);
    }

    [Fact]
    public void FormatMoneyPairFromClp_Usd_ShowsUsdPrimaryAndClpSecondary()
    {
        var (primary, secondary) = _service.FormatMoneyPairFromClp(90_000m, "USD", 3600m, 900m);
        Assert.StartsWith("US$", primary);
        Assert.StartsWith("$", secondary);
    }
}
