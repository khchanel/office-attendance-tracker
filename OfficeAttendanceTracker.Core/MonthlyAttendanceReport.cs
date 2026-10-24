namespace OfficeAttendanceTracker.Core;

/// <summary>A snapshot of attendance and target progress for one calendar month.</summary>
public sealed class MonthlyAttendanceReport
{
    /// <summary>Gets the first day of the reported month.</summary>
    public required DateTime Month { get; init; }

    /// <summary>Gets a detached copy of each persisted record in date order.</summary>
    public required IReadOnlyList<AttendanceRecord> Records { get; init; }

    /// <summary>Gets office days, including weekend visits and excluding days off.</summary>
    public required int OfficeDays { get; init; }

    /// <summary>Gets weekdays excluding recorded days off.</summary>
    public required int BusinessDays { get; init; }

    /// <summary>Gets the required office days rounded up at the configured threshold.</summary>
    public required int RequiredOfficeDays { get; init; }

    /// <summary>Gets eligible weekdays with no record up to today.</summary>
    public required int UnrecordedBusinessDays { get; init; }

    /// <summary>Gets weekday days off excluded from the monthly denominator.</summary>
    public required int DaysOff { get; init; }

    /// <summary>Gets the configured target percentage as a fraction.</summary>
    public required double Threshold { get; init; }

    /// <summary>Gets monthly office attendance divided by eligible business days, or null when none exist.</summary>
    public double? AttendanceRate => BusinessDays == 0 ? null : (double)OfficeDays / BusinessDays;
}
