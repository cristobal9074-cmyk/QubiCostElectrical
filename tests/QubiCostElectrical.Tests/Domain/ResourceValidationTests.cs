using Xunit;

namespace QubiCostElectrical.Tests;

public class ResourceValidationTests
{
    [Fact]
    public void Validate_ValidResource_ReturnsValid()
    {
        var r = new Resource
        {
            Code = "CABLE-001",
            Description = "Cable de cobre",
            Unit = "ML",
            BasePrice = 1000m,
            Currency = "CLP",
            PriceDate = DateTime.UtcNow
        };

        var result = ResourceValidator.Validate(r);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyCode_HasError()
    {
        var r = new Resource { Code = "", Description = "Test", Unit = "UN" };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("código"));
    }

    [Fact]
    public void Validate_EmptyDescription_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "", Unit = "UN" };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("descripción"));
    }

    [Fact]
    public void Validate_InvalidUnit_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "INVALID" };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unidad"));
    }

    [Fact]
    public void Validate_NegativePrice_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", BasePrice = -1m };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("precio"));
    }

    [Fact]
    public void Validate_ZeroYield_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", Yield = 0m };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("rendimiento"));
    }

    [Fact]
    public void Validate_NegativeYield_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", Yield = -1m };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("rendimiento"));
    }

    [Fact]
    public void Validate_NegativeWaste_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", Waste = -1m };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("desperdicio"));
    }

    [Fact]
    public void Validate_NegativeAdjustment_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", AdjustmentFactor = 0m };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("factor"));
    }

    [Fact]
    public void Validate_InvalidCurrency_HasError()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", Currency = "XYZ" };
        var result = ResourceValidator.Validate(r);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("moneda"));
    }

    [Fact]
    public void WasteOver100_ProducesWarning()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", Waste = 101m };
        var result = ResourceValidator.Validate(r);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("desperdicio"));
    }

    [Fact]
    public void PriceWithoutDate_ProducesWarning()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "UN", BasePrice = 1000m, PriceDate = null };
        var result = ResourceValidator.Validate(r);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("fecha"));
    }

    [Fact]
    public void ValidatePriceHistory_Valid_ReturnsValid()
    {
        var entry = new ResourcePriceHistoryEntry
        {
            ResourceId = "r1",
            Price = 1000m,
            Currency = "CLP",
            EffectiveDate = DateTime.UtcNow
        };
        var result = ResourceValidator.ValidatePriceHistory(entry);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePriceHistory_NegativePrice_HasError()
    {
        var entry = new ResourcePriceHistoryEntry
        {
            ResourceId = "r1",
            Price = -1m,
            Currency = "CLP",
            EffectiveDate = DateTime.UtcNow
        };
        var result = ResourceValidator.ValidatePriceHistory(entry);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("precio"));
    }

    [Fact]
    public void ValidateExchangeRate_Valid_ReturnsValid()
    {
        var rate = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 900m };
        var result = ResourceValidator.ValidateExchangeRate(rate);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateExchangeRate_ZeroRate_HasError()
    {
        var rate = new ExchangeRate { SourceCurrency = "USD", TargetCurrency = "CLP", Rate = 0m };
        var result = ResourceValidator.ValidateExchangeRate(rate);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("positiva"));
    }

    [Fact]
    public void ValidateExchangeRate_SameCurrency_Warning()
    {
        var rate = new ExchangeRate { SourceCurrency = "CLP", TargetCurrency = "CLP", Rate = 1m };
        var result = ResourceValidator.ValidateExchangeRate(rate);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("moneda origen"));
    }
}
