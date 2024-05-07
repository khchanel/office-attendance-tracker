

namespace OfficeAttendanceTracker.Service
{
    public interface IAttendanceRecordStore
    {
        /// <summary>
        /// Take attendance for <paramref name="date"/>
        /// </summary>
        /// <param name="isOffice"></param>
        /// <param name="date"></param>
        /// <returns>reference to the record stored</returns>
        AttendanceRecord Add(bool isOffice, DateTime date);

        /// <summary>
        /// Update <paramref name="isOffice"/> for <paramref name="date"/>
        /// </summary>
        /// <param name="isOffice"></param>
        /// <param name="date"></param>
        void Update(bool isOffice, DateTime date);

        /// <summary>
        /// Clear storage and reset
        /// </summary>
        void Clear();

        List<AttendanceRecord> GetAll();
        List<AttendanceRecord> GetAll(DateTime startDate, DateTime endDate);
        List<AttendanceRecord> GetMonth(DateTime? month = null);
        AttendanceRecord? GetToday();
        AttendanceRecord? GetDate(DateTime date);
    }
}