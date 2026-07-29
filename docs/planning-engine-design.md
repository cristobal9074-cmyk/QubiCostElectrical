# Planning Engine Design Specification

This document details the architecture and design of the Qubi Cost Electrical project planning engine (Fase 7A).

## Core Architecture

The planning engine is designed around three main domains:
1. **Schedule**: Represents the root configuration of a project timeline.
2. **WBS (Work Breakdown Structure)**: Represents the hierarchical organization of tasks.
3. **Activities & Milestones**: Represent the leaf tasks with durations, states, and constraints.
4. **Calendars**: Represent time slots, shifts, working patterns, and exceptions.

```mermaid
classDiagram
    class Schedule {
        +ScheduleId Id
        +string Name
        +string TimeZoneId
        +CalendarId DefaultCalendarId
        +ScheduleStatus Status
        +DateTime? ProjectStartDate
        +DateTime? TargetEndDate
    }
    class WbsPlanningNode {
        +WbsPlanningNodeId Id
        +ScheduleId ScheduleId
        +WbsPlanningNodeId? ParentId
        +string WbsCode
        +string Name
        +WbsNodeType NodeType
        +int SortOrder
    }
    class Activity {
        +ActivityId Id
        +ScheduleId ScheduleId
        +WbsPlanningNodeId WbsNodeId
        +string Code
        +string Name
        +ActivityType Type
        +PlanningDuration Duration
        +ActivityStatus Status
        +Responsible Responsible
        +List~BudgetLink~ BudgetLinks
    }
    class Calendar {
        +CalendarId Id
        +string Code
        +string Name
        +string TimeZoneId
        +List~WeeklyWorkDay~ WeeklyPattern
        +List~CalendarException~ Exceptions
    }

    Schedule "1" *-- "many" WbsPlanningNode
    Schedule "1" *-- "many" Activity
    Activity "many" --> "1" WbsPlanningNode
    Activity "many" --> "0..1" Calendar
```

## Security & Verification

All mutations are checked against `AuthorizationGuard` for:
- Session context (roles ESTIMATOR, ADMINISTRATOR, etc.)
- Strict read-only state checks (Approved, Archived, or Obsolete schedules)
- Transition rules of activity states.
