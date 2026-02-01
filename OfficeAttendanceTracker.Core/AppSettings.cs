using System.Text.Json.Serialization;

namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Application settings model shared across Desktop and Service projects
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
        /// Creates default settings for Desktop application
        /// </summary>
        public static AppSettings CreateDesktopDefaults()
        {
            return new AppSettings
            {
                DataFilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };
        }

        /// <summary>
        /// Creates default settings for Windows Service
        /// </summary>
        public static AppSettings CreateServiceDefaults()
        {
            return new AppSettings
            {
                DataFilePath = null // Service uses application directory
            };
        }

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

