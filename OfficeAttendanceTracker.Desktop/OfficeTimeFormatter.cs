namespace OfficeAttendanceTracker.Desktop;

internal static class OfficeTimeFormatter
{
    public static string Format(decimal hours)
    {
        // Remove decimal division noise when hours originated from tracked seconds.
        var preciseMinutes = decimal.Round(hours * 60, 12);
        var totalMinutes = decimal.Round(preciseMinutes, 0, MidpointRounding.AwayFromZero);
        var wholeHours = decimal.Floor(totalMinutes / 60);
        var minutes = totalMinutes % 60;
        return $"{wholeHours:0}h {minutes:0}m";
    }
}
