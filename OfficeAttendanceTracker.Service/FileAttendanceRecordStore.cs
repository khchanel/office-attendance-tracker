
using System.Text.Json;

namespace OfficeAttendanceTracker.Service
{
    public class FileAttendanceRecordStore : IAttedanceRecordStore
    {

        private readonly string _dataFilePath;
        private List<AttendanceRecord> _attendanceRecords;


        public FileAttendanceRecordStore()
        {
            var filename = "attendance.json";
            var filepath = AppDomain.CurrentDomain.BaseDirectory;
            _dataFilePath = Path.Combine(filepath, filename);
            _attendanceRecords = [];

            Load();
        }

        public FileAttendanceRecordStore(IConfiguration config)
        {
            var filename = config["DataFileName"] ?? "attendance.json";
            var filepath = config["DataFilePath"] ?? AppDomain.CurrentDomain.BaseDirectory;
            _dataFilePath = Path.Combine(filepath, filename);
            _attendanceRecords = [];

            Load();
        }


        public void Add(string employeeId, bool isPresent, DateTime? date = null)
        {
            _attendanceRecords.Add(new AttendanceRecord
            {
                EmployeeId = employeeId,
                Date = date == null? DateTime.Today : date.Value.Date, // Use provided date or default to today's date (time part is truncated)
                IsPresent = isPresent
            });

            Save();
        }


        public List<AttendanceRecord> GetAll(string employeeId, DateTime startDate, DateTime endDate)
        {
            return _attendanceRecords
                .FindAll(record => record.EmployeeId == employeeId && record.Date >= startDate.Date && record.Date <= endDate.Date);
        }

        public List<AttendanceRecord> GetAll()
        {
            return _attendanceRecords;
        }

        public List<AttendanceRecord> GetMonth(DateTime? month = null)
        {
            if (month == null) month = DateTime.Today;

            return _attendanceRecords.FindAll(x => x.Date.Year == month.Value.Year && x.Date.Month == month.Value.Month);
        }

        public List<AttendanceRecord> GetMonth(string employeeId, DateTime? month = null)
        {
            if (month == null) month = DateTime.Today;

            return _attendanceRecords.FindAll(x => x.EmployeeId == employeeId && x.Date.Year == month.Value.Year && x.Date.Month == month.Value.Month);
        }


        public List<AttendanceRecord> GetToday(string employeeId)
        {
            return _attendanceRecords.FindAll(x => x.EmployeeId == employeeId &&
                x.Date.Year == DateTime.Today.Year &&
                x.Date.Month == DateTime.Today.Month &&
                x.Date.Day == DateTime.Today.Day);
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
