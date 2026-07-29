using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class PaymentCertificateIdsTests
{
    [Fact]
    public void PaymentCertificateId_New_GeneratesNonEmptyId()
    {
        var id = PaymentCertificateId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void PaymentCertificateId_From_SameValue_AreEqual()
    {
        var id1 = PaymentCertificateId.From("abc123");
        var id2 = PaymentCertificateId.From("abc123");
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void PaymentCertificateId_From_DifferentValue_AreNotEqual()
    {
        var id1 = PaymentCertificateId.From("abc");
        var id2 = PaymentCertificateId.From("def");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void PaymentCertificateId_From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PaymentCertificateId.From(null!));
    }

    [Fact]
    public void PaymentCertificateId_ToString_ReturnsValue()
    {
        var id = PaymentCertificateId.From("test-value");
        Assert.Equal("test-value", id.ToString());
    }

    [Fact]
    public void PaymentCertificateLineId_New_GeneratesNonEmptyId()
    {
        var id = PaymentCertificateLineId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void ContractAdvanceId_New_GeneratesNonEmptyId()
    {
        var id = ContractAdvanceId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void ContractRetentionId_New_GeneratesNonEmptyId()
    {
        var id = ContractRetentionId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void PaymentObservationId_New_GeneratesNonEmptyId()
    {
        var id = PaymentObservationId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void PaymentCertificateRevisionId_New_GeneratesNonEmptyId()
    {
        var id = PaymentCertificateRevisionId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }
}
