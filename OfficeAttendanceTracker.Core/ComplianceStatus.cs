namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Represents the compliance status based on rolling attendance (up to current date)
    /// </summary>
    public enum ComplianceStatus
    {
        /// <summary>
        /// Already met the entire month's requirement - no more attendance needed
        /// </summary>
        Secured,
        
        /// <summary>
        /// Meeting the rolling requirement (up to current business days)
        /// </summary>
        Compliant,
        
        /// <summary>
        /// Below rolling threshold but still achievable given remaining business days
        /// </summary>
        Warning,
        
        /// <summary>
        /// Impossible to meet compliance target for remainder of the month
        /// </summary>
        Critical
    }
}

