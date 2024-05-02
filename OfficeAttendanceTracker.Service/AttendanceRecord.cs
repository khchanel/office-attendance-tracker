
namespace OfficeAttendanceTracker.Service
{
    public record AttendanceRecord
    {
        public required DateTime Date { get; set; }
        public required bool IsOffice { get; set; }
    }
}
