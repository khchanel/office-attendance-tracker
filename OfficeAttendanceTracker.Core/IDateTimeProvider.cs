namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Provider interface for date/time operations to enable testability
    /// </summary>
    public interface IDateTimeProvider
    {
        DateTime Today { get; }
    }
}
