
using System.Text.Json;

namespace OfficeAttendanceTracker.Service
{
    public class AttendanceRecordFileStore : IAttendanceRecordStore
    {

        private readonly string _dataFilePath;
        private List<AttendanceRecord> _attendanceRecords;


        public AttendanceRecordFileStore()
        {
            var filename = "attendance.json";
            var filepath = AppDomain.CurrentDomain.BaseDirectory;
            _dataFilePath = Path.Combine(filepath, filename);
            _attendanceRecords = [];

            Load();
        }

        public AttendanceRecordFileStore(IConfiguration config)
        {
            var filename = config["DataFileName"] ?? "attendance.json";
            var filepath = string.IsNullOrEmpty(config["DataFilePath"]) ? AppDomain.CurrentDomain.BaseDirectory : config["DataFilePath"];
            _dataFilePath = Path.Combine(filepath, filename);
            _attendanceRecords = [];

            Load();
        }


        public void Add(bool isPresent, DateTime? date = null)
        {
            _attendanceRecords.Add(new AttendanceRecord
            {
                Date = date == null? DateTime.Today : date.Value.Date, // Use provided date or default to today's date (time part is truncated)
                IsOffice = isPresent
            });

            Save();
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
            if (month == null) month = DateTime.Today;

            return _attendanceRecords.FindAll(record => record.Date.Year == month.Value.Year && record.Date.Month == month.Value.Month);
        }


        public AttendanceRecord GetToday()
        {
            return _attendanceRecords.Find(record =>
                record.Date.Year == DateTime.Today.Year &&
                record.Date.Month == DateTime.Today.Month &&
                record.Date.Day == DateTime.Today.Day);
        }


        public void Clear()
        {
            _attendanceRecords = [];
            Save();
        }


        private void Load()
        {
            if (File.Exists(_dataFilePath))
            {
                string json = File.ReadAllText(_dataFilePath);
                _attendanceRecords = JsonSerializer.Deserialize<List<AttendanceRecord>>(json);
            }
            else
            {
                Save();
            }
        }


        private void Save()
        {
            string json = JsonSerializer.Serialize(_attendanceRecords);
            File.WriteAllText(_dataFilePath, json);
        }
    }
}
