using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Service
{
    public interface INetworkInfoProvider
    {
        string GetHostName();
        IPAddress[] GetHostAddresses(string hostName);
        IEnumerable<NetworkInterface> GetAllNetworkInterfaces();
    }
}
