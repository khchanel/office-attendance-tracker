using System.Runtime.InteropServices;

namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Default implementation that returns the actual system date/time
    /// </summary>
    public class DefaultDateTimeProvider : IDateTimeProvider
    {
        public DateTime Today => DateTime.Today;

        /// <inheritdoc />
        public TimeSpan SuspendedTime
        {
            get
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(10))
                    return TimeSpan.Zero;

                // Biased interrupt time includes sleep and hibernation. Unbiased time excludes both.
                QueryUnbiasedInterruptTimePrecise(out var awakeTicks);
                QueryInterruptTimePrecise(out var totalTicks);
                return TimeSpan.FromTicks((long)totalTicks - (long)awakeTicks);
            }
        }

        [DllImport("api-ms-win-core-realtime-l1-1-1.dll", ExactSpelling = true)]
        private static extern void QueryInterruptTimePrecise(out ulong interruptTime);

        [DllImport("api-ms-win-core-realtime-l1-1-1.dll", ExactSpelling = true)]
        private static extern void QueryUnbiasedInterruptTimePrecise(out ulong unbiasedTime);
    }
}
