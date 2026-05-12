namespace OfficeAttendanceTracker.Core
{
    public static class DateTimeExtensions
    {
        public static bool IsWeekday(this DateTime date) =>
            date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }
}
