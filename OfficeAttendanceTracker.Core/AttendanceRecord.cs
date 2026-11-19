using System.Text.Json.Serialization;

namespace OfficeAttendanceTracker.Core
{
    public record AttendanceRecord
    {
        [JsonConverter(typeof(DateTimeConverter))]
        public required DateTime Date { get; set; }
        public required bool IsOffice { get; set; }
        public bool IsDayOff { get; set; }

        /// <summary>Gets or sets the first tracked office detection for this date.</summary>
        [JsonConverter(typeof(OfficeTimestampJsonConverter))]
        public DateTimeOffset? FirstOfficeDetectionAt { get; set; }

        /// <summary>Gets or sets the most recent tracked office detection for this date.</summary>
        [JsonConverter(typeof(OfficeTimestampJsonConverter))]
        public DateTimeOffset? LastOfficeDetectionAt { get; set; }

        /// <summary>Gets or sets credited office seconds, or null when the visit was untracked.</summary>
        public decimal? TrackedOfficeSeconds { get; set; }
    }

}
