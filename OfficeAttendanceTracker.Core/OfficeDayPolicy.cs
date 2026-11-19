namespace OfficeAttendanceTracker.Core;

internal readonly record struct OfficeDayPolicy(OfficeTimeTrackingMode Mode, decimal MinimumHours)
{
    public bool CountsAsOfficeDay(AttendanceRecord record) =>
        record.IsOffice && !record.IsDayOff &&
        (GetOfficeHours(record) is not { } hours || hours >= MinimumHours);

    public decimal? GetOfficeHours(AttendanceRecord record)
    {
        if (record.TrackedOfficeSeconds is not { } seconds)
            return null;

        return Mode switch
        {
            OfficeTimeTrackingMode.Accumulated => seconds / 3600m,
            OfficeTimeTrackingMode.FirstToLast when record.FirstOfficeDetectionAt is { } first &&
                record.LastOfficeDetectionAt is { } last =>
                Math.Max(0, (last - first).Ticks) / (decimal)TimeSpan.TicksPerHour,
            _ => null
        };
    }
}
