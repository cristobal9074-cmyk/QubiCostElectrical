using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ContractAdministrationIdsTests
{
    [Fact]
    public void AdjustmentId_New_GeneratesNonEmptyId()
    {
        var id = AdjustmentId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void AdjustmentId_From_SameValue_AreEqual()
    {
        var id1 = AdjustmentId.From("abc123");
        var id2 = AdjustmentId.From("abc123");
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AdjustmentId_From_DifferentValue_AreNotEqual()
    {
        var id1 = AdjustmentId.From("abc");
        var id2 = AdjustmentId.From("def");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void AdjustmentId_From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AdjustmentId.From(null!));
    }

    [Fact]
    public void AdjustmentId_ToString_ReturnsValue()
    {
        var id = AdjustmentId.From("test-value");
        Assert.Equal("test-value", id.ToString());
    }

    [Fact]
    public void AdjustmentFormulaId_New_GeneratesNonEmptyId()
    {
        var id = AdjustmentFormulaId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void AdjustmentIndexId_New_GeneratesNonEmptyId()
    {
        var id = AdjustmentIndexId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void GuaranteeId_New_GeneratesNonEmptyId()
    {
        var id = GuaranteeId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void PenaltyId_New_GeneratesNonEmptyId()
    {
        var id = PenaltyId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void InvoiceId_New_GeneratesNonEmptyId()
    {
        var id = InvoiceId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void InvoiceLineId_New_GeneratesNonEmptyId()
    {
        var id = InvoiceLineId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }

    [Fact]
    public void ContractAdministrationAuditEventId_New_GeneratesNonEmptyId()
    {
        var id = ContractAdministrationAuditEventId.New();
        Assert.NotNull(id.Value);
        Assert.NotEmpty(id.Value);
    }
}
