using System.Text.Json.Serialization;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Application settings model
    /// </summary>
    public class AppSettings
    {
        [JsonPropertyName("networks")]
        public List<string> Networks { get; set; } = [];

        [JsonPropertyName("pollIntervalMs")]
        public int PollIntervalMs { get; set; } = 1800000; // 30 minutes default

        [JsonPropertyName("enableBackgroundWorker")]
        public bool EnableBackgroundWorker { get; set; } = true;

        [JsonPropertyName("complianceThreshold")]
        public double ComplianceThreshold { get; set; } = 0.5; // 50%

        [JsonPropertyName("dataFilePath")]
        public string? DataFilePath { get; set; } = null;

        [JsonPropertyName("dataFileName")]
        public string DataFileName { get; set; } = "attendance.csv";

        /// <summary>
        /// Creates a deep copy of the settings
        /// </summary>
        public AppSettings Clone()
        {
            return new AppSettings
            {
                Networks = new List<string>(Networks),
                PollIntervalMs = PollIntervalMs,
                EnableBackgroundWorker = EnableBackgroundWorker,
                ComplianceThreshold = ComplianceThreshold,
                DataFilePath = DataFilePath,
                DataFileName = DataFileName
            };
        }
    }
}
