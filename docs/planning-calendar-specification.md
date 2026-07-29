# Planning Calendar Specification

This document details the behavior, data models, and logic for calendars, shifts, work patterns, and duration computations in Qubi Cost Electrical.

## Data Structures

### Calendar
- **Id**: `CalendarId`
- **TimeZoneId**: e.g., "America/Santiago"
- **WeeklyPattern**: List of `WeeklyWorkDay` (Monday to Sunday) containing active `WorkShift` intervals.
- **Exceptions**: List of non-working dates or custom work periods (e.g., half-days, local holidays).

### PlanningDuration
Supports working time representations:
- Minutes, Hours, Days, Weeks.
- Supports working vs. elapsed time difference (Working duration follows calendar pattern; Elapsed duration ignores holidays/weekends).

## Calculation Logic

The `CalendarCalculationService` exposes:
1. `IsWorkingTime(DateTime instant)`: Verifies if a given timestamp falls in working shifts and doesn't overlap exceptions.
2. `AddWorkingTime(DateTime start, PlanningDuration duration)`: Walks the timeline forward to find the correct ending timestamp.
3. `SubtractWorkingTime(DateTime start, PlanningDuration duration)`: Walks the timeline backward.
4. `GetWorkingDuration(DateTime start, DateTime end)`: Computes actual working hours/days elapsed between two dates.
