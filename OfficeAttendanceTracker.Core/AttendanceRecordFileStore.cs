using Microsoft.Extensions.Configuration;

namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Abstract base class for file-based attendance record storage.
    /// Implements common logic while allowing subclasses to define format-specific Load/Save behavior.
    /// Uses explicit initialization and auto-save pattern.
    /// </summary>
    public abstract class AttendanceRecordFileStore : IAttendanceRecordStore
    {

        protected List<AttendanceRecord> Records
        {
            get {
                if (!_isInitialized)
                    throw new InvalidOperationException("Store not initialized. Call Initialize() before using the store.");

                return _attendanceRecords;
            }
            set {
                _attendanceRecords = value;
            }
        }

        protected readonly string _dataFilePath;
        private List<AttendanceRecord> _attendanceRecords;
        private bool _isDirty;
        private bool _isInitialized;
        private Timer? _autoSaveTimer;
        private readonly object _lock = new object();

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

        }

        public void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized)
                    return;

                Load();
                _isInitialized = true;

                // Start auto-save timer - saves every minute if dirty
                var autoSaveInterval = TimeSpan.FromMinutes(1);
                _autoSaveTimer = new Timer(_ =>
                {
                    if (_isDirty)
                        SaveChanges();
                }, null, autoSaveInterval, autoSaveInterval);
            }
        }


        public AttendanceRecord Add(bool isOffice, DateTime date)
        {

            lock (_lock)
            {
                var record = new AttendanceRecord
                {
                    Date = date.Date, // time part is truncated
                    IsOffice = isOffice
                };

                Records.Add(record);
                _isDirty = true;

                return record;
            }
        }

        public void Update(bool isOffice, DateTime date)
        {

            lock (_lock)
            {
                var record = GetDate(date);

                if (record != null && record.IsOffice != isOffice)
                {
                    record.IsOffice = isOffice;
                    _isDirty = true;
                }
            }
        }

        public List<AttendanceRecord> GetAll(DateTime startDate, DateTime endDate)
        {

            lock (_lock)
            {
                return Records
                    .FindAll(record => record.Date >= startDate.Date && record.Date <= endDate.Date);
            }
        }

        public List<AttendanceRecord> GetAll()
        {

            lock (_lock)
            {
                return Records.ToList();
            }
        }

        public List<AttendanceRecord> GetMonth(DateTime? month = null)
        {

            if (month == null) month = DateTime.Today.Date;

            lock (_lock)
            {
                return Records.FindAll(record => 
                    record.Date.Year == month.Value.Year && record.Date.Month == month.Value.Month);
            }
        }

        public AttendanceRecord? GetDate(DateTime date)
        {

            lock (_lock)
            {
                var record = Records.Find(r => r.Date == date.Date);
                return record;
            }
        }

        public AttendanceRecord? GetToday()
        {
            return GetDate(DateTime.Today);
        }

        public void Clear()
        {

            lock (_lock)
            {
                Records = [];
                _isDirty = true;
                SaveChanges();
            }
        }

        public void Reload()
        {

            lock (_lock)
            {
                Load();
                _isDirty = false;
            }
        }

        public void SaveChanges()
        {

            lock (_lock)
            {
                if (_isDirty)
                {
                    Save();
                    _isDirty = false;
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _autoSaveTimer?.Dispose();
                
                // Final save on disposal if dirty
                if (_isDirty && _isInitialized)
                {
                    try
                    {
                        Save();
                        _isDirty = false;
                    }
                    catch
                    {
                        // Suppress exceptions during disposal
                        // but could log them if logging is available
                    }
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Load attendance records from file. Must be implemented by subclasses.
        /// Called by Initialize() and Reload().
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Save attendance records to file. Must be implemented by subclasses.
        /// Called by SaveChanges() when data is dirty.
        /// </summary>
        protected abstract void Save();
    }
}

