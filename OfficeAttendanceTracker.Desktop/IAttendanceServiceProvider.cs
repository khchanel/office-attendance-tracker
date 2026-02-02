using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Provides access to the current IAttendanceService instance
    /// </summary>
    public interface IAttendanceServiceProvider
    {
        IAttendanceService Current { get; }
        event EventHandler<IAttendanceService>? ServiceRecreated;
    }
}
