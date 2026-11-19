namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Provider interface for date/time operations to enable testability
    /// </summary>
    public interface IDateTimeProvider
    {
        DateTime Today { get; }

        /// <summary>Gets the current time with the local UTC offset.</summary>
        DateTimeOffset Now => DateTimeOffset.Now;

        /// <summary>Gets the cumulative time the system has spent suspended, when available.</summary>
        TimeSpan SuspendedTime => TimeSpan.Zero;
    }
}
