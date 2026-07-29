namespace QubiCostElectrical;

public enum OutOfSequenceKind
{
    SuccessorStartedBeforeFsPredecessorComplete,
    SuccessorStartedBeforeSsPredecessorStart,
    FinishBeforeFfPredecessorFinish,
    StartedBeforeConstraint,
    WorkOnNonWorkingDay,
    MilestoneReachedBeforeDependency
}
