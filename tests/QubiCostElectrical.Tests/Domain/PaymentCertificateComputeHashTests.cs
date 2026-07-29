using System.Text.Json;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class PaymentCertificateComputeHashTests
{
    [Fact]
    public void ComputeHash_SameJson_ProducesSameHash()
    {
        var json = JsonSerializer.Serialize(new { Id = "test", Value = 123 });
        var hash1 = PaymentCertificate.ComputeHash(json);
        var hash2 = PaymentCertificate.ComputeHash(json);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentJson_ProducesDifferentHash()
    {
        var json1 = JsonSerializer.Serialize(new { Id = "test1", Value = 123 });
        var json2 = JsonSerializer.Serialize(new { Id = "test2", Value = 456 });
        var hash1 = PaymentCertificate.ComputeHash(json1);
        var hash2 = PaymentCertificate.ComputeHash(json2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_Returns64CharHexString()
    {
        var json = "{}";
        var hash = PaymentCertificate.ComputeHash(json);

        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHex()
    {
        var json = "{}";
        var hash = PaymentCertificate.ComputeHash(json);

        foreach (var c in hash)
        {
            Assert.True(char.IsAsciiHexDigit(c));
            if (char.IsLetter(c))
                Assert.True(char.IsLower(c));
        }
    }

    [Fact]
    public void ComputeHash_EmptyString_ProducesHash()
    {
        var hash = PaymentCertificate.ComputeHash("");
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void ComputeHash_NullInput_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => PaymentCertificate.ComputeHash(null!));
    }
}
