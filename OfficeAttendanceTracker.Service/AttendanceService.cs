using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Service
{
    public class AttendanceService : IAttendanceService
    {
        private sealed class Network
        {
            public string? Mask { get; set; }
            public string? Address { get; set; }

            public IPAddress IPAddress => IPAddress.Parse(Address!);
            public IPAddress SubnetMask => IPAddress.Parse(Mask!);

        }


        private readonly ILogger<AttendanceService> _logger;
        private readonly List<Network> _networks;
        private readonly Guid _instanceId;


        public AttendanceService(ILogger<AttendanceService> logger,
            IConfiguration config)
        {
            _logger = logger;
            _instanceId = Guid.NewGuid();

            var networks = config.GetSection("Networks").Get<List<Network>>() 
                ?? throw new ArgumentNullException("Networks", "config missing Networks");
            _networks = [.. networks];

        }

        public bool CheckAttendance()
        {
            _logger.LogInformation("instance id: {Instance}", _instanceId);

            bool isHostResolveToOffice = CheckUsingHostName();
            bool isNicAddressInOffice = CheckUsingNicIP();

            return isHostResolveToOffice && isNicAddressInOffice;

        }


        private bool CheckUsingHostName()
        {
            var hostname = Dns.GetHostName();
            _logger.LogDebug("Hostname: {Host}", hostname);

            IPHostEntry ipEntry = Dns.GetHostEntry(hostname);
            IPAddress[] addr = ipEntry.AddressList;

            for (int i = 0; i < addr.Length; i++)
            {
                _logger.LogDebug("Hostname resolved IP Address {0}: {1} ", i, addr[i].ToString());

                bool isInSubnet = IsIPv4AddressInSubnet(addr[i], _networks);
                if (isInSubnet)
                {
                    return true;
                }
            }
            return false;
        }


        private bool CheckUsingNicIP()
        {

            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var networkInterface in networkInterfaces)
            {
                // Filter out loopback and disconnected interfaces
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    _logger.LogDebug("Interface: {Name}", networkInterface.Name);


                    foreach (var ipAddress in ipProperties.UnicastAddresses)
                    {
                        _logger.LogDebug("Interface: {Name} IP Address: {Address}", networkInterface.Name, ipAddress.Address);

                        bool isInSubnet = IsIPv4AddressInSubnet(ipAddress.Address, _networks);
                        if (isInSubnet)
                        {
                            return true;
                        }

                    }

                }
            }


            return false;
        }

        private static bool IsIPv4AddressInSubnet(IPAddress ipAddress, List<Network> subnet)
        {
            if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            foreach (var network in subnet)
            {
                byte[] ipBytes = ipAddress.GetAddressBytes();
                byte[] subnetBytes = network.IPAddress.GetAddressBytes();
                byte[] subnetMaskBytes = network.SubnetMask.GetAddressBytes();

                if (ipBytes.Length != subnetBytes.Length || subnetBytes.Length != subnetMaskBytes.Length)
                {
                    throw new ArgumentException("IP address, subnet address, and subnet mask must be IPv4 addresses of the same size.");
                }

                for (int i = 0; i < ipBytes.Length; i++)
                {
                    byte subnetByte = (byte)(subnetBytes[i] & subnetMaskBytes[i]);
                    byte ipByte = (byte)(ipBytes[i] & subnetMaskBytes[i]);

                    if (subnetByte != ipByte)
                    {
                        return false;
                    }
                }

            }

            return true;
        }


    }
}
