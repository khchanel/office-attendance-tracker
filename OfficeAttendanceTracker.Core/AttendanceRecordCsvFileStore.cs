using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;


namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Provides a file-based store for attendance records using the CSV (Comma-Separated Values) format.
    /// </summary>
    public class AttendanceRecordCsvFileStore : AttendanceRecordFileStore
    {
        private readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        public AttendanceRecordCsvFileStore(IConfiguration? config = null)
            : base(config, "attendance.csv")
        {
        }

        public override void Load()
        {
            if (File.Exists(_dataFilePath))
            {
                using var reader = new StreamReader(_dataFilePath);
                using var csv = new CsvReader(reader, _config);
                csv.Context.RegisterClassMap<AttendanceRecordMap>();
                _attendanceRecords = csv.GetRecords<AttendanceRecord>().ToList();
            }
            else
            {
                _attendanceRecords = [];
                Save();
            }
        }

        protected override void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
            using var writer = new StreamWriter(_dataFilePath, false); // overwrite
            using var csv = new CsvWriter(writer, _config);
            csv.Context.RegisterClassMap<AttendanceRecordMap>();
            csv.WriteRecords(_attendanceRecords);
        }
    }
}
