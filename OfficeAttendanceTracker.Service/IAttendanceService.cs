namespace OfficeAttendanceTracker.Service
{
    public interface IAttendanceService
    {
        bool CheckAttendance();
        int GetCurrentMonthAttendance();
        void TakeAttendance();
        void Reload();
    }
}