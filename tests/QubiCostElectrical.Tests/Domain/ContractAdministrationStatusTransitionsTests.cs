using System;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class AdjustmentStatusTransitionsTests
{
    [Fact]
    public void CanTransition_DraftToCalculated_ReturnsTrue()
    {
        Assert.True(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Draft, AdjustmentStatus.Calculated));
    }

    [Fact]
    public void CanTransition_DraftToClosed_ReturnsTrue()
    {
        Assert.True(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Draft, AdjustmentStatus.Closed));
    }

    [Fact]
    public void CanTransition_DraftToApplied_ReturnsFalse()
    {
        Assert.False(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Draft, AdjustmentStatus.Applied));
    }

    [Fact]
    public void CanTransition_CalculatedToApplied_ReturnsTrue()
    {
        Assert.True(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Calculated, AdjustmentStatus.Applied));
    }

    [Fact]
    public void CanTransition_CalculatedToRejected_ReturnsTrue()
    {
        Assert.True(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Calculated, AdjustmentStatus.Rejected));
    }

    [Fact]
    public void CanTransition_AppliedToClosed_ReturnsTrue()
    {
        Assert.True(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Applied, AdjustmentStatus.Closed));
    }

    [Fact]
    public void CanTransition_RejectedToClosed_ReturnsTrue()
    {
        Assert.True(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Rejected, AdjustmentStatus.Closed));
    }

    [Fact]
    public void CanTransition_Closed_ReturnsEmptySet()
    {
        Assert.False(AdjustmentStatusTransitions.CanTransition(
            AdjustmentStatus.Closed, AdjustmentStatus.Draft));
    }

    [Fact]
    public void EnsureCanTransition_Valid_DoesNotThrow()
    {
        AdjustmentStatusTransitions.EnsureCanTransition(
            AdjustmentStatus.Draft, AdjustmentStatus.Calculated);
    }

    [Fact]
    public void EnsureCanTransition_Invalid_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AdjustmentStatusTransitions.EnsureCanTransition(
                AdjustmentStatus.Draft, AdjustmentStatus.Applied));
    }

    [Fact]
    public void GetAllowedNext_Draft_ReturnsExpected()
    {
        var allowed = AdjustmentStatusTransitions.GetAllowedNext(AdjustmentStatus.Draft);
        Assert.Contains(AdjustmentStatus.Calculated, allowed);
        Assert.Contains(AdjustmentStatus.Closed, allowed);
        Assert.Equal(2, allowed.Count);
    }

    [Fact]
    public void GetAllowedNext_Closed_ReturnsEmpty()
    {
        var allowed = AdjustmentStatusTransitions.GetAllowedNext(AdjustmentStatus.Closed);
        Assert.Empty(allowed);
    }
}

public sealed class GuaranteeStatusTransitionsTests
{
    [Fact]
    public void CanTransition_DraftToActive_ReturnsTrue()
    {
        Assert.True(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Draft, GuaranteeStatus.Active));
    }

    [Fact]
    public void CanTransition_DraftToCancelled_ReturnsTrue()
    {
        Assert.True(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Draft, GuaranteeStatus.Cancelled));
    }

    [Fact]
    public void CanTransition_ActiveToRenewed_ReturnsTrue()
    {
        Assert.True(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Active, GuaranteeStatus.Renewed));
    }

    [Fact]
    public void CanTransition_ActiveToReleased_ReturnsTrue()
    {
        Assert.True(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Active, GuaranteeStatus.Released));
    }

    [Fact]
    public void CanTransition_ActiveToExecuted_ReturnsTrue()
    {
        Assert.True(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Active, GuaranteeStatus.Executed));
    }

    [Fact]
    public void CanTransition_ReleasedToExpired_ReturnsTrue()
    {
        Assert.True(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Released, GuaranteeStatus.Expired));
    }

    [Fact]
    public void CanTransition_ExpiredToCancelled_ReturnsTrue()
    {
        Assert.True(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Expired, GuaranteeStatus.Cancelled));
    }

    [Fact]
    public void CanTransition_Executed_ReturnsEmptySet()
    {
        Assert.False(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Executed, GuaranteeStatus.Active));
        Assert.False(GuaranteeStatusTransitions.CanTransition(
            GuaranteeStatus.Executed, GuaranteeStatus.Cancelled));
    }

    [Fact]
    public void CanTransition_Cancelled_ReturnsEmptySet()
    {
        var allowed = GuaranteeStatusTransitions.GetAllowedNext(GuaranteeStatus.Cancelled);
        Assert.Empty(allowed);
    }

    [Fact]
    public void EnsureCanTransition_Valid_DoesNotThrow()
    {
        GuaranteeStatusTransitions.EnsureCanTransition(
            GuaranteeStatus.Draft, GuaranteeStatus.Active);
    }

    [Fact]
    public void GetAllowedNext_Draft_ReturnsExpected()
    {
        var allowed = GuaranteeStatusTransitions.GetAllowedNext(GuaranteeStatus.Draft);
        Assert.Contains(GuaranteeStatus.Active, allowed);
        Assert.Contains(GuaranteeStatus.Cancelled, allowed);
    }
}

public sealed class PenaltyStatusTransitionsTests
{
    [Fact]
    public void CanTransition_DraftToProposed_ReturnsTrue()
    {
        Assert.True(PenaltyStatusTransitions.CanTransition(
            PenaltyStatus.Draft, PenaltyStatus.Proposed));
    }

    [Fact]
    public void CanTransition_DraftToClosed_ReturnsTrue()
    {
        Assert.True(PenaltyStatusTransitions.CanTransition(
            PenaltyStatus.Draft, PenaltyStatus.Closed));
    }

    [Fact]
    public void CanTransition_UnderReviewToApproved_ReturnsTrue()
    {
        Assert.True(PenaltyStatusTransitions.CanTransition(
            PenaltyStatus.UnderReview, PenaltyStatus.Approved));
    }

    [Fact]
    public void CanTransition_UnderReviewToRejected_ReturnsTrue()
    {
        Assert.True(PenaltyStatusTransitions.CanTransition(
            PenaltyStatus.UnderReview, PenaltyStatus.Rejected));
    }

    [Fact]
    public void CanTransition_ApprovedToApplied_ReturnsTrue()
    {
        Assert.True(PenaltyStatusTransitions.CanTransition(
            PenaltyStatus.Approved, PenaltyStatus.Applied));
    }

    [Fact]
    public void CanTransition_AppliedToClosed_ReturnsTrue()
    {
        Assert.True(PenaltyStatusTransitions.CanTransition(
            PenaltyStatus.Applied, PenaltyStatus.Closed));
    }

    [Fact]
    public void CanTransition_ClosedToAny_ReturnsFalse()
    {
        Assert.False(PenaltyStatusTransitions.CanTransition(
            PenaltyStatus.Closed, PenaltyStatus.Draft));
    }

    [Fact]
    public void GetAllowedNext_Draft_ReturnsExpected()
    {
        var allowed = PenaltyStatusTransitions.GetAllowedNext(PenaltyStatus.Draft);
        Assert.Contains(PenaltyStatus.Proposed, allowed);
        Assert.Contains(PenaltyStatus.Closed, allowed);
    }

    [Fact]
    public void GetAllowedNext_Closed_ReturnsEmpty()
    {
        var allowed = PenaltyStatusTransitions.GetAllowedNext(PenaltyStatus.Closed);
        Assert.Empty(allowed);
    }
}

public sealed class InvoiceStatusTransitionsTests
{
    [Fact]
    public void CanTransition_DraftToIssued_ReturnsTrue()
    {
        Assert.True(InvoiceStatusTransitions.CanTransition(
            InvoiceStatus.Draft, InvoiceStatus.Issued));
    }

    [Fact]
    public void CanTransition_DraftToCancelled_ReturnsTrue()
    {
        Assert.True(InvoiceStatusTransitions.CanTransition(
            InvoiceStatus.Draft, InvoiceStatus.Cancelled));
    }

    [Fact]
    public void CanTransition_IssuedToPaid_ReturnsTrue()
    {
        Assert.True(InvoiceStatusTransitions.CanTransition(
            InvoiceStatus.Issued, InvoiceStatus.Paid));
    }

    [Fact]
    public void CanTransition_IssuedToPartiallyPaid_ReturnsTrue()
    {
        Assert.True(InvoiceStatusTransitions.CanTransition(
            InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid));
    }

    [Fact]
    public void CanTransition_PartiallyPaidToPaid_ReturnsTrue()
    {
        Assert.True(InvoiceStatusTransitions.CanTransition(
            InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid));
    }

    [Fact]
    public void CanTransition_PaidToCancelled_ReturnsTrue()
    {
        Assert.True(InvoiceStatusTransitions.CanTransition(
            InvoiceStatus.Paid, InvoiceStatus.Cancelled));
    }

    [Fact]
    public void CanTransition_CancelledToAny_ReturnsFalse()
    {
        Assert.False(InvoiceStatusTransitions.CanTransition(
            InvoiceStatus.Cancelled, InvoiceStatus.Draft));
    }

    [Fact]
    public void GetAllowedNext_Draft_ReturnsExpected()
    {
        var allowed = InvoiceStatusTransitions.GetAllowedNext(InvoiceStatus.Draft);
        Assert.Contains(InvoiceStatus.Issued, allowed);
        Assert.Contains(InvoiceStatus.Cancelled, allowed);
    }

    [Fact]
    public void GetAllowedNext_Cancelled_ReturnsEmpty()
    {
        var allowed = InvoiceStatusTransitions.GetAllowedNext(InvoiceStatus.Cancelled);
        Assert.Empty(allowed);
    }
}
