

namespace OfficeAttendanceTracker.Service
{
    public interface IAttendanceRecordStore
    {
        void Add(string employeeId, bool isPresent, DateTime? date = null);
        void Clear();
        List<AttendanceRecord> GetAll();
        List<AttendanceRecord> GetAll(string employeeId, DateTime startDate, DateTime endDate);
        List<AttendanceRecord> GetMonth(DateTime? month = null);
        List<AttendanceRecord> GetMonth(string employeeId, DateTime? month = null);
        List<AttendanceRecord> GetToday(string employeeId);
    }
}