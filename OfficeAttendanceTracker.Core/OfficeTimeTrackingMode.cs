namespace OfficeAttendanceTracker.Core;

/// <summary>Determines how office detections qualify an attendance day.</summary>
public enum OfficeTimeTrackingMode
{
    /// <summary>Any office detection qualifies.</summary>
    Off,
    /// <summary>The elapsed time from the first to the last detection qualifies the day.</summary>
    FirstToLast,
    /// <summary>Only time between consecutive office detections qualifies the day.</summary>
    Accumulated
}
