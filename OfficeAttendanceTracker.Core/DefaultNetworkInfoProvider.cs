using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Service
{
    public class DefaultNetworkInfoProvider : INetworkInfoProvider
    {
        public string GetHostName() => Dns.GetHostName();
        public IPAddress[] GetHostAddresses(string hostName) => Dns.GetHostEntry(hostName).AddressList;
        public IEnumerable<NetworkInterface> GetAllNetworkInterfaces() => NetworkInterface.GetAllNetworkInterfaces();
    }
}
