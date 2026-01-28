namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Default implementation that returns the actual system date/time
    /// </summary>
    public class DefaultDateTimeProvider : IDateTimeProvider
    {
        public DateTime Today => DateTime.Today;
    }
}
