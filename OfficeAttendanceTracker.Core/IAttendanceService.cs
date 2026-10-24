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
        /// Calculates the required office days for the supplied business-day count,
        /// rounded up using the configured compliance threshold.
        /// </summary>
        /// <param name="businessDays">The number of eligible business days.</param>
        int CalculateRequiredOfficeDays(int businessDays);
        
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
