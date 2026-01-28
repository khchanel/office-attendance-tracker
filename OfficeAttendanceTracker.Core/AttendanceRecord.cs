using System.Text.Json.Serialization;

namespace OfficeAttendanceTracker.Service
{
    public record AttendanceRecord
    {
        [JsonConverter(typeof(DateTimeConverter))]
        public required DateTime Date { get; set; }
        public required bool IsOffice { get; set; }
        public bool IsDayOff { get; set; }
    }
}
