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
        AbsolutelyFine,
        
        /// <summary>
        /// Meeting the rolling requirement (up to current business days)
        /// </summary>
        Compliant,
        
        /// <summary>
        /// Close to but below the rolling requirement
        /// </summary>
        Warning,
        
        /// <summary>
        /// Far below the rolling requirement
        /// </summary>
        Critical
    }
}
