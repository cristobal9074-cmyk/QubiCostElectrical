using Xunit;

namespace QubiCostElectrical.Tests;

public class ResourceModelTests
{
    [Fact]
    public void CreateResource_ValidData_Succeeds()
    {
        var r = new Resource
        {
            Code = "CABLE-001",
            Description = "Cable de cobre 2.5mm",
            Type = ResourceType.Material,
            Unit = "ML",
            BasePrice = 1500m,
            Currency = "CLP",
            PriceDate = DateTime.UtcNow,
            CreatedBy = "user@test.cl"
        };

        Assert.NotNull(r.Id);
        Assert.Equal("CABLE-001", r.Code);
        Assert.True(r.IsActive);
        Assert.True(r.HasPrice);
        Assert.True(r.IsValidForUse);
    }

    [Fact]
    public void Resource_EffectivePrice_AppliesFactor()
    {
        var r = new Resource
        {
            Code = "R-001",
            Description = "Test",
            BasePrice = 1000m,
            AdjustmentFactor = 1.15m
        };

        Assert.Equal(1150m, r.EffectivePrice);
    }

    [Fact]
    public void Resource_DefaultFactor_IsOne()
    {
        var r = new Resource { Code = "R-001", Description = "Test" };
        Assert.Equal(1m, r.AdjustmentFactor);
    }

    [Fact]
    public void Resource_WithoutPrice_HasPriceIsFalse()
    {
        var r = new Resource { Code = "R-001", Description = "Test" };
        Assert.False(r.HasPrice);
    }

    [Fact]
    public void Resource_InvalidUnit_IsValidForUseFalse()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Unit = "INVALID" };
        Assert.False(r.IsValidForUse);
    }

    [Fact]
    public void Resource_Deactivated_IsValidForUseFalse()
    {
        var r = new Resource { Code = "R-001", Description = "Test", IsActive = false };
        Assert.False(r.IsValidForUse);
    }

    [Fact]
    public void Resource_EffectiveYield_UsesActualWhenPositive()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Yield = 1.5m };
        Assert.Equal(1.5m, r.EffectiveYield);
    }

    [Fact]
    public void Resource_EffectiveYield_FallbackToOneWhenZero()
    {
        var r = new Resource { Code = "R-001", Description = "Test", Yield = 0m };
        Assert.Equal(1m, r.EffectiveYield);
    }

    [Fact]
    public void ResourceType_ToApuTipo_MapsCorrectly()
    {
        Assert.Equal(ApuTipoRecurso.MAT, ResourceType.Material.ToApuTipo());
        Assert.Equal(ApuTipoRecurso.MO, ResourceType.Labor.ToApuTipo());
        Assert.Equal(ApuTipoRecurso.EQ, ResourceType.Equipment.ToApuTipo());
        Assert.Equal(ApuTipoRecurso.HERR, ResourceType.Tool.ToApuTipo());
        Assert.Equal(ApuTipoRecurso.SC, ResourceType.Subcontract.ToApuTipo());
        Assert.Equal(ApuTipoRecurso.TR, ResourceType.Transport.ToApuTipo());
        Assert.Equal(ApuTipoRecurso.HH, ResourceType.Auxiliary.ToApuTipo());
        Assert.Equal(ApuTipoRecurso.MAT, ResourceType.Other.ToApuTipo());
    }

    [Fact]
    public void ResourceType_FromApuTipo_RoundTrips()
    {
        foreach (ApuTipoRecurso apu in Enum.GetValues<ApuTipoRecurso>())
        {
            var rt = apu.FromApuTipo();
            Assert.Equal(apu, rt.ToApuTipo());
        }
    }
}
