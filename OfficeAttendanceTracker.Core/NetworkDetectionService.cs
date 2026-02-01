using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OfficeAttendanceTracker.Core
{
    /// <summary>
    /// Default implementation of network detection service
    /// </summary>
    public class NetworkDetectionService : INetworkDetectionService
    {
        private readonly INetworkInfoProvider _networkInfoProvider;

        public NetworkDetectionService(INetworkInfoProvider networkInfoProvider)
        {
            _networkInfoProvider = networkInfoProvider;
        }

        /// <summary>
        /// Detects all active network configurations in CIDR notation by analyzing routing table
        /// </summary>
        /// <returns>List of network addresses in CIDR format (e.g., 10.8.1.0/24)</returns>
        public List<string> DetectCurrentNetworks()
        {
            var networks = new List<string>();
            
            // Get all network interfaces with their gateway information
            var interfacesWithGateways = GetInterfacesWithGateways();

            // If no interfaces have gateways, fall back to all active interfaces
            if (interfacesWithGateways.Count == 0)
            {
                interfacesWithGateways = GetAllActiveInterfaces();
            }

            foreach (var (networkInterface, ipProperties) in interfacesWithGateways)
            {
                foreach (var unicastAddress in ipProperties.UnicastAddresses)
                {
                    // Only process IPv4 addresses
                    if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    // Skip link-local addresses (169.254.x.x)
                    var addressBytes = unicastAddress.Address.GetAddressBytes();
                    if (addressBytes[0] == 169 && addressBytes[1] == 254)
                    {
                        continue;
                    }

                    // Get subnet mask
                    var subnetMask = unicastAddress.IPv4Mask;
                    if (subnetMask == null || IPAddress.IsLoopback(subnetMask))
                    {
                        continue;
                    }

                    // Calculate network address
                    var networkAddress = CalculateNetworkAddress(unicastAddress.Address, subnetMask);

                    // Calculate CIDR prefix length
                    int cidrPrefix = CalculateCidrPrefix(subnetMask.GetAddressBytes());

                    // Add network in CIDR notation
                    var cidrNotation = $"{networkAddress}/{cidrPrefix}";
                    if (!networks.Contains(cidrNotation))
                    {
                        networks.Add(cidrNotation);
                    }
                }
            }

            return networks;
        }

        /// <summary>
        /// Validates if a string is in valid CIDR notation (e.g., 192.168.1.0/24)
        /// </summary>
        /// <param name="cidr">The CIDR string to validate</param>
        /// <returns>True if valid CIDR format, false otherwise</returns>
        public bool IsValidCidr(string cidr)
        {
            if (string.IsNullOrWhiteSpace(cidr))
                return false;

            var parts = cidr.Split('/');
            if (parts.Length != 2)
                return false;

            // Validate IP address format (must be exactly 4 octets separated by dots)
            var ipPart = parts[0];
            var octets = ipPart.Split('.');
            if (octets.Length != 4)
                return false;

            // Validate each octet is a valid number between 0 and 255
            foreach (var octet in octets)
            {
                if (!byte.TryParse(octet, out byte value))
                    return false;
            }

            // Now validate with IPAddress.TryParse
            if (!IPAddress.TryParse(ipPart, out var ipAddress))
                return false;

            // Only support IPv4
            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
                return false;

            // Validate prefix length (0-32 for IPv4)
            if (!int.TryParse(parts[1], out int prefixLength))
                return false;

            if (prefixLength < 0 || prefixLength > 32)
                return false;

            return true;
        }

        /// <summary>
        /// Gets network interfaces that have gateway addresses (i.e., can route to external networks)
        /// Prioritizes interfaces with lower gateway metrics (primary routing interfaces)
        /// </summary>
        private List<(NetworkInterface Interface, IPInterfaceProperties Properties)> GetInterfacesWithGateways()
        {
            var interfacesWithGateways = new List<(NetworkInterface, IPInterfaceProperties, int Metric)>();

            foreach (var networkInterface in _networkInfoProvider.GetAllNetworkInterfaces())
            {
                // Skip non-operational interfaces and loopback
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var ipProperties = networkInterface.GetIPProperties();

                // Get IPv4 gateways
                var ipv4Gateways = ipProperties.GatewayAddresses
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork &&
                               !IPAddress.IsLoopback(g.Address))
                    .ToList();

                if (ipv4Gateways.Count == 0)
                {
                    continue;
                }

                // Get the IPv4 properties to access metrics
                var ipv4Properties = ipProperties.GetIPv4Properties();
                int metric = ipv4Properties?.Index ?? int.MaxValue;

                interfacesWithGateways.Add((networkInterface, ipProperties, metric));
            }

            // Sort by metric (lower is better - primary route)
            // Then return only the interfaces, not the metrics
            return interfacesWithGateways
                .OrderBy(x => x.Metric)
                .Select(x => (x.Item1, x.Item2))
                .ToList();
        }

        /// <summary>
        /// Fallback: Gets all active network interfaces with IPv4 addresses
        /// </summary>
        private List<(NetworkInterface Interface, IPInterfaceProperties Properties)> GetAllActiveInterfaces()
        {
            var activeInterfaces = new List<(NetworkInterface, IPInterfaceProperties)>();

            foreach (var networkInterface in _networkInfoProvider.GetAllNetworkInterfaces())
            {
                // Skip non-operational interfaces and loopback
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var ipProperties = networkInterface.GetIPProperties();

                // Check if it has any IPv4 unicast addresses
                var hasIPv4 = ipProperties.UnicastAddresses
                    .Any(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                               !IPAddress.IsLoopback(addr.Address));

                if (hasIPv4)
                {
                    activeInterfaces.Add((networkInterface, ipProperties));
                }
            }

            return activeInterfaces;
        }

        /// <summary>
        /// Calculates the network address from an IP address and subnet mask
        /// </summary>
        private IPAddress CalculateNetworkAddress(IPAddress ipAddress, IPAddress subnetMask)
        {
            var ipBytes = ipAddress.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            var networkBytes = new byte[4];

            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }

            return new IPAddress(networkBytes);
        }

        /// <summary>
        /// Calculates the CIDR prefix length from subnet mask bytes
        /// </summary>
        private int CalculateCidrPrefix(byte[] maskBytes)
        {
            int cidrPrefix = 0;
            foreach (byte b in maskBytes)
            {
                cidrPrefix += CountBits(b);
            }
            return cidrPrefix;
        }

        /// <summary>
        /// Counts the number of set bits in a byte
        /// </summary>
        private int CountBits(byte b)
        {
            int count = 0;
            while (b != 0)
            {
                count += b & 1;
                b >>= 1;
            }
            return count;
        }
    }
}


