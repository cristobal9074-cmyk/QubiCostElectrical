using Xunit;

namespace QubiCostElectrical.Tests;

public class CurrencyTests
{
    [Fact]
    public void Catalog_ContainsCLP()
    {
        var clp = CurrencyCatalog.Find("CLP");
        Assert.NotNull(clp);
        Assert.Equal("CLP", clp!.IsoCode);
        Assert.Equal(0, clp.DecimalPlaces);
    }

    [Fact]
    public void Catalog_ContainsUSD()
    {
        var usd = CurrencyCatalog.Find("USD");
        Assert.NotNull(usd);
        Assert.Equal("US$", usd!.Symbol);
        Assert.Equal(2, usd.DecimalPlaces);
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        Assert.NotNull(CurrencyCatalog.Find("clp"));
        Assert.NotNull(CurrencyCatalog.Find("Usd"));
    }

    [Fact]
    public void Find_InvalidCode_ReturnsNull()
    {
        Assert.Null(CurrencyCatalog.Find("XYZ"));
        Assert.Null(CurrencyCatalog.Find(""));
        Assert.Null(CurrencyCatalog.Find(null));
    }

    [Fact]
    public void IsValid_ReturnsTrueForKnown()
    {
        Assert.True(CurrencyCatalog.IsValid("CLP"));
        Assert.True(CurrencyCatalog.IsValid("USD"));
        Assert.True(CurrencyCatalog.IsValid("EUR"));
        Assert.True(CurrencyCatalog.IsValid("UF"));
    }

    [Fact]
    public void IsValid_ReturnsFalseForUnknown()
    {
        Assert.False(CurrencyCatalog.IsValid("GBP"));
    }

    [Fact]
    public void GetDefault_ReturnsCLP()
    {
        Assert.Equal("CLP", CurrencyCatalog.GetDefault().IsoCode);
    }

    [Fact]
    public void ExchangeRate_RequiresPositiveRate()
    {
        var rate = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 900m };
        var result = CurrencyCatalog.Convert(100m, rate);
        Assert.Equal(90000m, result);
    }

    [Fact]
    public void ExchangeRate_NegativeRate_Throws()
    {
        var rate = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = -1m };
        Assert.Throws<ArgumentException>(() => CurrencyCatalog.Convert(100m, rate));
    }

    [Fact]
    public void ExchangeRate_ZeroRate_Throws()
    {
        var rate = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 0m };
        Assert.Throws<ArgumentException>(() => CurrencyCatalog.Convert(100m, rate));
    }

    [Fact]
    public void Convert_AppliesRateAndRounds()
    {
        var rate = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 935.12m };
        var result = CurrencyCatalog.Convert(100.50m, rate);
        Assert.Equal(93979.56m, result); // 100.50 * 935.12 = 93979.56
    }

    [Fact]
    public void All_ContainsAllCurrencies()
    {
        Assert.Equal(4, CurrencyCatalog.All.Count);
    }
}
