using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class Calendar
{
    public CalendarId Id { get; private set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public CalendarType CalendarType { get; set; }
    public List<WeeklyWorkDay> WeeklyPattern { get; set; } = new();
    public List<CalendarException> Exceptions { get; set; } = new();

    public Calendar(CalendarId id, string code, string name, string timeZoneId, string author, CalendarType calendarType)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TimeZoneId = timeZoneId ?? throw new ArgumentNullException(nameof(timeZoneId));
        Author = author ?? throw new ArgumentNullException(nameof(author));
        CalendarType = calendarType;
        Description = string.Empty;
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
        RowVersion = Guid.NewGuid().ToString("N");
    }

    public Calendar() { }
}
