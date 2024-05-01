

namespace OfficeAttendanceTracker.Service
{
    public interface IAttendanceRecordStore
    {
        /// <summary>
        /// Take attendance for date.
        /// if <paramref name="date"/> is not defined then default to Today
        /// </summary>
        /// <param name="isPresent"></param>
        /// <param name="date"></param>
        void Add(bool isPresent, DateTime? date = null);

        /// <summary>
        /// Clear storage and reset
        /// </summary>
        void Clear();

        List<AttendanceRecord> GetAll();
        List<AttendanceRecord> GetAll(DateTime startDate, DateTime endDate);
        List<AttendanceRecord> GetMonth(DateTime? month = null);
        AttendanceRecord GetToday();
    }
}