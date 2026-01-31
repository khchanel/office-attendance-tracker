using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Provides a file-based store for attendance records using a JSON file as the underlying data format.
    /// </summary>
    public class AttendanceRecordJsonFileStore : AttendanceRecordFileStore
    {
        public AttendanceRecordJsonFileStore(IConfiguration? config = null)
            : base(config, "attendance.json")
        {
        }

        public override void Load()
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

        protected override void Save()
        {
            string json = JsonSerializer.Serialize(_attendanceRecords, new JsonSerializerOptions { WriteIndented = true });

            Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
            File.WriteAllText(_dataFilePath, json);
        }
    }
}
