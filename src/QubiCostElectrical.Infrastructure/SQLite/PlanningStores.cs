using System;

namespace QubiCostElectrical;

internal static class SqlitePlanningTable
{
    internal const string Schedules = "project_schedules";
    internal const string WbsNodes = "planning_wbs_nodes";
    internal const string Activities = "planning_activities";
    internal const string Calendars = "planning_calendars";
    internal const string CalendarWeek = "planning_calendar_week";
    internal const string CalendarExceptions = "planning_calendar_exceptions";
    internal const string Constraints = "planning_constraints";
    internal const string Responsibles = "planning_responsibles";
    internal const string BudgetLinks = "planning_budget_links";
    internal const string Audit = "planning_audit";
}
