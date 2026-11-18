namespace OfficeAttendanceTracker.Core
{
    public static class DateTimeExtensions
    {
        internal const string OffsetTimestampFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

        internal static DateTimeOffset TruncateToSecond(this DateTimeOffset value) =>
            value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));

        public static bool IsWeekday(this DateTime date) =>
            date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }
}
