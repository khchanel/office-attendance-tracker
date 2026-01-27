namespace OfficeAttendanceTracker.Service
{
    public interface IAttendanceService
    {
        bool CheckAttendance();
        int GetCurrentMonthAttendance();
    }
}