using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace OfficeAttendanceTracker.Service
{
    public class AttendanceRecordJsonFileStore : IAttendanceRecordStore
    {

        private readonly string _dataFilePath;
        private List<AttendanceRecord> _attendanceRecords;



        public AttendanceRecordJsonFileStore(IConfiguration? config = null)
        {
            var filename = config?["DataFileName"] ?? "attendance.json";
            var filepath = string.IsNullOrEmpty(config?["DataFilePath"]) ? AppDomain.CurrentDomain.BaseDirectory : config["DataFilePath"];
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

            return _attendanceRecords.FindAll(record => record.Date.Year == month.Value.Year && record.Date.Month == month.Value.Month);
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


        public void Load()
        {
            if (File.Exists(_dataFilePath))
            {
                string json = File.ReadAllText(_dataFilePath);
                _attendanceRecords = JsonSerializer.Deserialize<List<AttendanceRecord>>(json)!;
            }
            else
            {
                Save();
            }
        }


        private void Save()
        {
            string json = JsonSerializer.Serialize(_attendanceRecords, new JsonSerializerOptions { WriteIndented = true});

            Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
            File.WriteAllText(_dataFilePath, json);
        }
    }
}
