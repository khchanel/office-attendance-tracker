namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Service for detecting network configurations
    /// </summary>
    public interface INetworkDetectionService
    {
        /// <summary>
        /// Detects all active network configurations in CIDR notation
        /// </summary>
        /// <returns>List of network addresses in CIDR format (e.g., 10.8.1.0/24)</returns>
        List<string> DetectCurrentNetworks();

        /// <summary>
        /// Validates if a string is in valid CIDR notation (e.g., 192.168.1.0/24)
        /// </summary>
        /// <param name="cidr">The CIDR string to validate</param>
        /// <returns>True if valid CIDR format, false otherwise</returns>
        bool IsValidCidr(string cidr);
    }
}

