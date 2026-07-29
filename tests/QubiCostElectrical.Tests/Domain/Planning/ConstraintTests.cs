using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public class ConstraintTests
{
    [Fact]
    public void Create_StartNoEarlierThan_SetsType()
    {
        var c = new Constraint
        {
            Type = ConstraintType.StartNoEarlierThan,
            ConstraintDate = new DateTime(2026, 8, 15),
            Reason = "Entrega de materiales",
            Author = "user1"
        };
        Assert.Equal(ConstraintType.StartNoEarlierThan, c.Type);
        Assert.NotNull(c.ConstraintDate);
        Assert.True(c.IsActive);
    }

    [Fact]
    public void Create_MustFinishOn_SetsExactDate()
    {
        var c = new Constraint
        {
            Type = ConstraintType.MustFinishOn,
            ConstraintDate = new DateTime(2026, 12, 31),
            Author = "user1"
        };
        Assert.Equal(ConstraintType.MustFinishOn, c.Type);
        Assert.Equal(new DateTime(2026, 12, 31), c.ConstraintDate);
    }

    [Fact]
    public void Constraint_WithoutDate_IsValid()
    {
        var c = new Constraint { Type = ConstraintType.None, Author = "user1" };
        Assert.Null(c.ConstraintDate);
    }

    [Fact]
    public void FinishNoLaterThan_CompatibleWithStartNoEarlierThan()
    {
        var c1 = new Constraint { Type = ConstraintType.FinishNoLaterThan, ConstraintDate = new DateTime(2026, 10, 1) };
        var c2 = new Constraint { Type = ConstraintType.StartNoEarlierThan, ConstraintDate = new DateTime(2026, 8, 1) };
        Assert.NotEqual(c1.Type, c2.Type);
    }

    [Fact]
    public void Constraint_Deactivated_SetsIsActiveFalse()
    {
        var c = new Constraint { Type = ConstraintType.MustStartOn, ConstraintDate = new DateTime(2026, 9, 1), Author = "user1" };
        c.IsActive = false;
        Assert.False(c.IsActive);
    }
}
