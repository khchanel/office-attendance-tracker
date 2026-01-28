using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;


namespace OfficeAttendanceTracker.Service
{
    public class AttendanceRecordCsvFileStore : IAttendanceRecordStore
    {

        private readonly string _dataFilePath;
        private List<AttendanceRecord> _attendanceRecords;
        private CsvConfiguration _config;



        public AttendanceRecordCsvFileStore(IConfiguration? config = null)
        {
            var filename = config?["DataFileName"] ?? "attendance.csv";
            var filepath = string.IsNullOrEmpty(config?["DataFilePath"]) ? AppDomain.CurrentDomain.BaseDirectory : config["DataFilePath"];
            // Ensure filepath is not null
            filepath ??= AppDomain.CurrentDomain.BaseDirectory;
            _dataFilePath = Path.Combine(filepath, filename);
            _attendanceRecords = [];

            _config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            };

            Load();
        }


        public AttendanceRecord Add(bool isOffice, DateTime date)
        {
            // Check if record already exists for this date
            var existingRecord = GetDate(date);
            if (existingRecord != null)
            {
                // Update existing record instead of adding duplicate
                existingRecord.IsOffice = isOffice;
                Save();
                return existingRecord;
            }

            // Add new record
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
                .FindAll(record => record.Date >= startDate.Date && record.Date <= endDate.Date)
                .GroupBy(r => r.Date)
                .Select(g => g.Last())
                .OrderBy(r => r.Date)
                .ToList();
        }

        public List<AttendanceRecord> GetAll()
        {
            return _attendanceRecords
                .GroupBy(r => r.Date)
                .Select(g => g.Last())
                .OrderBy(r => r.Date)
                .ToList();
        }

        public List<AttendanceRecord> GetMonth(DateTime? month = null)
        {
            if (month == null) month = DateTime.Today.Date;

            return _attendanceRecords
                .FindAll(record => record.Date.Year == month.Value.Year && record.Date.Month == month.Value.Month)
                .GroupBy(r => r.Date)
                .Select(g => g.Last())
                .OrderBy(r => r.Date)
                .ToList();
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
                using var reader = new StreamReader(_dataFilePath);
                using var csv = new CsvReader(reader, _config);
                csv.Context.RegisterClassMap<AttendanceRecordMap>();
                var records = csv.GetRecords<AttendanceRecord>().ToList();
                
                // Deduplicate by date - keep the last occurrence (most recent in file)
                _attendanceRecords = records
                    .GroupBy(r => r.Date)
                    .Select(g => g.Last())
                    .OrderBy(r => r.Date)
                    .ToList();
            }
            else
            {
                _attendanceRecords = [];
                Save();
            }
        }


        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
            using var writer = new StreamWriter(_dataFilePath, false); // overwrite
            using var csv = new CsvWriter(writer, _config);
            csv.Context.RegisterClassMap<AttendanceRecordMap>();
            csv.WriteRecords(_attendanceRecords);
        }
    }
}
