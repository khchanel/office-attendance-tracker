namespace OfficeAttendanceTracker.Core
{
    public interface IAttendanceService
    {
        bool CheckAttendance();
        int GetCurrentMonthAttendance();
        void TakeAttendance();
        void Reload();
        
        /// <summary>
        /// Calculates the number of business days (Mon-Fri) in the current month
        /// </summary>
        int GetBusinessDaysInCurrentMonth();
        
        /// <summary>
        /// Determines the compliance status based on current attendance
        /// </summary>
        ComplianceStatus GetComplianceStatus();
    }
}