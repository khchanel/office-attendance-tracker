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
                Records = csv.GetRecords<AttendanceRecord>().ToList();
            }
            else
            {
                Records = [];
                Save();
            }
        }

        protected override void Save()
        {
            var directory = Path.GetDirectoryName(_dataFilePath)!;
            Directory.CreateDirectory(directory);
            
            // Use atomic write pattern: write to temp file, then replace
            // This prevents data loss if app crashes during write
            var tempFilePath = Path.Combine(directory, $"{Path.GetFileName(_dataFilePath)}.tmp");
            var backupFilePath = Path.Combine(directory, $"{Path.GetFileName(_dataFilePath)}.bak");
            
            try
            {
                // Write to temporary file first
                using (var writer = new StreamWriter(tempFilePath, false))
                using (var csv = new CsvWriter(writer, _config))
                {
                    csv.Context.RegisterClassMap<AttendanceRecordMap>();
                    csv.WriteRecords(Records);
                }
                
                // If original file exists, back it up
                if (File.Exists(_dataFilePath))
                {
                    File.Copy(_dataFilePath, backupFilePath, true);
                }
                
                // Atomic replace: move temp file to target
                File.Move(tempFilePath, _dataFilePath, true);
                
                // Clean up backup after successful write
                if (File.Exists(backupFilePath))
                {
                    File.Delete(backupFilePath);
                }
            }
            catch
            {
                // If write failed, restore from backup if available
                if (File.Exists(backupFilePath) && !File.Exists(_dataFilePath))
                {
                    File.Move(backupFilePath, _dataFilePath, true);
                }
                throw;
            }
            finally
            {
                // Clean up temp file if it still exists
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }
        }
    }
}
