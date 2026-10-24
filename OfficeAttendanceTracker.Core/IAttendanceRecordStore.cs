



namespace OfficeAttendanceTracker.Core
{
    public interface IAttendanceRecordStore : IDisposable
    {
        /// <summary>
        /// Initialize the store by loading data from storage.
        /// Must be called before using the store.
        /// </summary>
        void Initialize();

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
        /// Sets and immediately persists a date's day-off flag, preserving office attendance.
        /// Marking an unrecorded date creates a record without office attendance.
        /// Removing a day off from an unrecorded date has no effect.
        /// </summary>
        void SetDayOff(bool isDayOff, DateTime date);

        /// <summary>
        /// Clear storage and reset
        /// </summary>
        void Clear();

        /// <summary>
        /// Refresh data from storage, discarding any unsaved changes
        /// </summary>
        void Reload();

        /// <summary>
        /// Persist all pending changes to storage
        /// </summary>
        void SaveChanges();

        List<AttendanceRecord> GetAll();
        List<AttendanceRecord> GetAll(DateTime startDate, DateTime endDate);
        List<AttendanceRecord> GetMonth(DateTime? month = null);
        AttendanceRecord? GetToday();
        AttendanceRecord? GetDate(DateTime date);
    }
}
