namespace QubiCostElectrical;

public enum PlanningValidationCode
{
    ScheduleNotFound,
    InvalidTimeZone,
    InvalidWbsParent,
    WbsCycleDetected,
    DuplicateWbsCode,
    InvalidActivityDuration,
    MilestoneDurationNotZero,
    CalendarNotFound,
    CalendarOverlap,
    InvalidWorkingInterval,
    InvalidException,
    InvalidConstraint,
    InvalidResponsible,
    DisabledResponsible,
    InvalidBudgetLink,
    AllocationExceedsLimit,
    ConcurrencyConflict,
    ReadOnlySchedule,
    InvalidActivityStatusTransition,
    WbsNodeNotFound,
    ActivityNotFound,
    ScheduleAlreadyApproved,
    ResponsibleAlreadyAssigned
}
