namespace OfficeAttendanceTracker.Core
{
    public interface IAttendanceService
    {
        int GetCurrentMonthAttendance();
        bool CheckAttendance();
        bool TakeAttendance();
        void Reload();
        
        /// <summary>
        /// Indicates whether any office networks have been configured
        /// </summary>
        bool IsReady { get; }
        
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