using System.Text.Json.Serialization;

namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Application settings model shared across Desktop and Service projects
    /// </summary>
    public class AppSettings
    {
        /// <summary>The default minimum duration for a tracked office day.</summary>
        public const decimal DefaultMinimumOfficeHours = 7.5m;

        /// <summary>Gets or sets how office time is measured.</summary>
        [JsonPropertyName("officeTimeTrackingMode")]
        [JsonConverter(typeof(JsonStringEnumConverter<OfficeTimeTrackingMode>))]
        public OfficeTimeTrackingMode OfficeTimeTrackingMode { get; set; }

        /// <summary>Gets or sets the hours required to qualify a tracked office day.</summary>
        [JsonPropertyName("minimumOfficeHours")]
        public decimal MinimumOfficeHours { get; set; } = DefaultMinimumOfficeHours;

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

        [JsonPropertyName("startWithWindows")]
        public bool StartWithWindows { get; set; } = false;

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
                OfficeTimeTrackingMode = OfficeTimeTrackingMode,
                MinimumOfficeHours = MinimumOfficeHours,
                DataFilePath = DataFilePath,
                DataFileName = DataFileName,
                StartWithWindows = StartWithWindows
            };
        }
    }
}

