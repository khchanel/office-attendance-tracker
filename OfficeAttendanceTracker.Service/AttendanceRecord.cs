
namespace OfficeAttendanceTracker.Service
{
    public class AttendanceRecord
    {
        public required string EmployeeId { get; set; }
        public DateTime Date { get; set; }
        public bool IsPresent { get; set; }
    }
}
