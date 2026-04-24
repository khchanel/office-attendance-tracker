using System.Text.Json.Serialization;

namespace OfficeAttendanceTracker.Core
{
    public record AttendanceRecord
    {
        [JsonConverter(typeof(DateTimeConverter))]
        public required DateTime Date { get; set; }
        public required bool IsOffice { get; set; }
        public bool IsDayOff { get; set; }
    }

    public static class DateTimeExtensions
    {
        public static bool IsWeekday(this DateTime date) =>
            date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday;
    }
}
