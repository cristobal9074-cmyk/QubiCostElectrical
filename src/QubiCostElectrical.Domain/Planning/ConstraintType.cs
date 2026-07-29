namespace QubiCostElectrical;

public enum ConstraintType
{
    None,
    StartNoEarlierThan,
    StartNoLaterThan,
    FinishNoEarlierThan,
    FinishNoLaterThan,
    MustStartOn,
    MustFinishOn
}
