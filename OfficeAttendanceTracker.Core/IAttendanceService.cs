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
        /// Calculates the number of business days (Mon-Fri) up to today in the current month
        /// </summary>
        int GetBusinessDaysUpToToday();
        
        /// <summary>
        /// Determines the compliance status based on rolling attendance (up to current date)
        /// </summary>
        ComplianceStatus GetComplianceStatus();
    }
}