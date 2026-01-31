using Microsoft.Extensions.Configuration;

namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Abstract base class for file-based attendance record storage.
    /// </summary>
    public abstract class AttendanceRecordFileStore : IAttendanceRecordStore
    {
        protected readonly string _dataFilePath;
        protected List<AttendanceRecord> _attendanceRecords;

        protected AttendanceRecordFileStore(IConfiguration? config, string defaultFileName)
        {
            var filename = config?["DataFileName"] ?? defaultFileName;
            var filepath = string.IsNullOrEmpty(config?["DataFilePath"])
                ? AppDomain.CurrentDomain.BaseDirectory
                : config["DataFilePath"];
            
            // Ensure filepath is not null before using Path.Combine
            filepath ??= AppDomain.CurrentDomain.BaseDirectory;
            _dataFilePath = Path.Combine(filepath, filename);
            _attendanceRecords = [];
            Load();
        }

        public AttendanceRecord Add(bool isOffice, DateTime date)
        {
            var record = new AttendanceRecord
            {
                Date = date.Date, // time part is truncated
                IsOffice = isOffice
            };

            _attendanceRecords.Add(record);
            Save();

            return record;
        }

        public void Update(bool isOffice, DateTime date)
        {
            var record = GetDate(date);

            if (record != null && record.IsOffice != isOffice)
            {
                record.IsOffice = isOffice;
                Save();
            }
        }

        public List<AttendanceRecord> GetAll(DateTime startDate, DateTime endDate)
        {
            return _attendanceRecords
                .FindAll(record => record.Date >= startDate.Date && record.Date <= endDate.Date);
        }

        public List<AttendanceRecord> GetAll()
        {
            return _attendanceRecords;
        }

        public List<AttendanceRecord> GetMonth(DateTime? month = null)
        {
            if (month == null) month = DateTime.Today.Date;

            return _attendanceRecords.FindAll(record => 
                record.Date.Year == month.Value.Year && record.Date.Month == month.Value.Month);
        }

        public AttendanceRecord? GetDate(DateTime date)
        {
            var record = _attendanceRecords.Find(r => r.Date == date.Date);
            return record;
        }

        public AttendanceRecord? GetToday()
        {
            return GetDate(DateTime.Today);
        }

        public void Clear()
        {
            _attendanceRecords = [];
            Save();
        }

        /// <summary>
        /// Load attendance records from file. Must be implemented by subclasses.
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Save attendance records to file. Must be implemented by subclasses.
        /// </summary>
        protected abstract void Save();
    }
}
