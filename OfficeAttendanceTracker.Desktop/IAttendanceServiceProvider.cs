using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Manage AttendanceService instances lifecycle
    /// </summary>
    public interface IAttendanceServiceProvider
    {
        IAttendanceService Current { get; }
        event EventHandler<IAttendanceService>? ServiceRecreated;
    }
}
