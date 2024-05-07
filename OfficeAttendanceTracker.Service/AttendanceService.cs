﻿using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Service
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ILogger<AttendanceService> _logger;
        private readonly IPAddress _officeSubnetMask;
        private readonly IPAddress _officeAddress;
        private readonly Guid _instanceId;


        public AttendanceService(ILogger<AttendanceService> logger,
            IConfiguration config)
        {
            _logger = logger;
            _instanceId = Guid.NewGuid();

            var mask = config["OfficeNetwork:SubnetMask"];
            var addr = config["OfficeNetwork:Address"];

            if (mask == null)
            {
                throw new ArgumentNullException("mask", "Config missing OfficeNetwork:SubnetMask");
            }

            if (addr == null)
            {
                throw new ArgumentNullException("addr", "config missing OfficeNetwork:Address");
            }


            _officeSubnetMask = IPAddress.Parse(mask);
            _officeAddress = IPAddress.Parse(addr);

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

                bool isInSubnet = IsIPv4Address(addr[i]) && IsIPv4AddressInSubnet(addr[i], _officeAddress, _officeSubnetMask);
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

                        bool isInSubnet = IsIPv4Address(ipAddress.Address) && IsIPv4AddressInSubnet(ipAddress.Address, _officeAddress, _officeSubnetMask);
                        if (isInSubnet)
                        {
                            return true;
                        }

                    }

                }
            }


            return false;
        }

        private static bool IsIPv4AddressInSubnet(IPAddress ipAddress, IPAddress subnetAddress, IPAddress subnetMask)
        {
            byte[] ipBytes = ipAddress.GetAddressBytes();
            byte[] subnetBytes = subnetAddress.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

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

            return true;
        }

        private static bool IsIPv4Address(IPAddress ipAddress)
        {
            return ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }



    }
}
