namespace OfficeAttendanceTracker.Core
{
    public interface IAttendanceService
    {
        int GetCurrentMonthAttendance();
        bool CheckAttendance();
        bool TakeAttendance();
        void Reload();

        /// <summary>Gets a detached monthly report using the current compliance threshold.</summary>
        MonthlyAttendanceReport GetMonthlyReport(DateTime month);

        /// <summary>Reloads records and saves a day-off change without altering office attendance.</summary>
        void SetDayOff(DateTime date, bool isDayOff);

        /// <summary>Reloads records and saves office attendance, preserving any day-off flag.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Office attendance is being added for a future date.</exception>
        void SetOfficeAttendance(DateTime date, bool isOffice);
        
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
