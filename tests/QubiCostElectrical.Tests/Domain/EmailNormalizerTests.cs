using Xunit;

namespace QubiCostElectrical.Tests;

public class EmailNormalizerTests
{
    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.Equal("", EmailNormalizer.Normalize(null));
    }

    [Fact]
    public void Normalize_Empty_ReturnsEmpty()
    {
        Assert.Equal("", EmailNormalizer.Normalize(""));
    }

    [Fact]
    public void Normalize_Lowercases_Email()
    {
        Assert.Equal("user@empresa.cl", EmailNormalizer.Normalize("User@Empresa.Cl"));
    }

    [Fact]
    public void Normalize_Trims_Whitespace()
    {
        Assert.Equal("user@empresa.cl", EmailNormalizer.Normalize("  user@empresa.cl  "));
    }

    [Fact]
    public void EqualsNormalized_SameEmail_ReturnsTrue()
    {
        Assert.True(EmailNormalizer.EqualsNormalized("User@Empresa.Cl", "user@empresa.cl"));
    }

    [Fact]
    public void EqualsNormalized_DifferentEmail_ReturnsFalse()
    {
        Assert.False(EmailNormalizer.EqualsNormalized("a@empresa.cl", "b@empresa.cl"));
    }

    [Fact]
    public void IsValid_Null_ReturnsFalse()
    {
        Assert.False(EmailNormalizer.IsValid(null));
    }

    [Fact]
    public void IsValid_Empty_ReturnsFalse()
    {
        Assert.False(EmailNormalizer.IsValid(""));
    }

    [Fact]
    public void IsValid_NoAtSign_ReturnsFalse()
    {
        Assert.False(EmailNormalizer.IsValid("notanemail"));
    }

    [Fact]
    public void IsValid_ValidEmail_ReturnsTrue()
    {
        Assert.True(EmailNormalizer.IsValid("user@empresa.cl"));
    }
}
