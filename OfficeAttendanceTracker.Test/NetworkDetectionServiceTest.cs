using Moq;
using OfficeAttendanceTracker.Core;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OfficeAttendanceTracker.Test
{
    [TestClass]
    public class NetworkDetectionServiceTest
    {
        private Mock<INetworkInfoProvider> _networkProviderMock = null!;
        private NetworkDetectionService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _networkProviderMock = new Mock<INetworkInfoProvider>();
            _service = new NetworkDetectionService(_networkProviderMock.Object);
        }

        #region Helper Methods

        /// <summary>
        /// Creates a mock network interface with specified properties
        /// </summary>
        private Mock<NetworkInterface> CreateMockNetworkInterface(
            string name,
            string description,
            OperationalStatus status,
            NetworkInterfaceType type)
        {
            var mockInterface = new Mock<NetworkInterface>();
            mockInterface.Setup(i => i.Name).Returns(name);
            mockInterface.Setup(i => i.Description).Returns(description);
            mockInterface.Setup(i => i.OperationalStatus).Returns(status);
            mockInterface.Setup(i => i.NetworkInterfaceType).Returns(type);
            return mockInterface;
        }

        /// <summary>
        /// Creates mock IP properties with unicast addresses and optional gateways
        /// </summary>
        private Mock<IPInterfaceProperties> CreateMockIPProperties(
            List<(IPAddress Address, IPAddress Mask)> unicastAddresses,
            List<IPAddress>? gatewayAddresses = null,
            int interfaceIndex = 1)
        {
            var mockIPProperties = new Mock<IPInterfaceProperties>();

            // Setup unicast addresses
            var unicastCollection = new List<UnicastIPAddressInformation>();
            foreach (var (address, mask) in unicastAddresses)
            {
                var mockUnicast = new Mock<UnicastIPAddressInformation>();
                mockUnicast.Setup(u => u.Address).Returns(address);
                mockUnicast.Setup(u => u.IPv4Mask).Returns(mask);
                unicastCollection.Add(mockUnicast.Object);
            }
            mockIPProperties.Setup(p => p.UnicastAddresses)
                .Returns(new MockUnicastIPAddressInformationCollection(unicastCollection));

            // Setup gateway addresses
            var gatewayCollection = new List<GatewayIPAddressInformation>();
            if (gatewayAddresses != null)
            {
                foreach (var gatewayAddress in gatewayAddresses)
                {
                    var mockGateway = new Mock<GatewayIPAddressInformation>();
                    mockGateway.Setup(g => g.Address).Returns(gatewayAddress);
                    gatewayCollection.Add(mockGateway.Object);
                }
            }
            mockIPProperties.Setup(p => p.GatewayAddresses)
                .Returns(new MockGatewayIPAddressInformationCollection(gatewayCollection));

            // Setup IPv4 properties for metric
            var mockIPv4Properties = new Mock<IPv4InterfaceProperties>();
            mockIPv4Properties.Setup(p => p.Index).Returns(interfaceIndex);
            mockIPProperties.Setup(p => p.GetIPv4Properties()).Returns(mockIPv4Properties.Object);

            return mockIPProperties;
        }

        #endregion

        #region Basic Functionality Tests

        [TestMethod]
        public void DetectCurrentNetworks_WithSingleNetworkAndGateway_ReturnsCorrectCIDR()
        {
            // Arrange
            var mockInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Realtek PCIe GbE Family Controller",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0"))
            };
            var gatewayAddresses = new List<IPAddress> { IPAddress.Parse("192.168.1.1") };

            var mockIPProperties = CreateMockIPProperties(unicastAddresses, gatewayAddresses);
            mockInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { mockInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("192.168.1.0/24", result[0]);
        }

        [TestMethod]
        public void DetectCurrentNetworks_WithMultipleNetworksAndGateways_ReturnsSortedByMetric()
        {
            // Arrange - WiFi with lower metric (primary)
            var wifiInterface = CreateMockNetworkInterface(
                "WiFi",
                "Intel Wireless",
                OperationalStatus.Up,
                NetworkInterfaceType.Wireless80211);

            var wifiUnicast = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("10.0.0.50"), IPAddress.Parse("255.255.255.0"))
            };
            var wifiGateway = new List<IPAddress> { IPAddress.Parse("10.0.0.1") };
            var wifiIPProperties = CreateMockIPProperties(wifiUnicast, wifiGateway, interfaceIndex: 5);
            wifiInterface.Setup(i => i.GetIPProperties()).Returns(wifiIPProperties.Object);

            // Arrange - Ethernet with higher metric (secondary)
            var ethernetInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Realtek Ethernet",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var ethernetUnicast = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0"))
            };
            var ethernetGateway = new List<IPAddress> { IPAddress.Parse("192.168.1.1") };
            var ethernetIPProperties = CreateMockIPProperties(ethernetUnicast, ethernetGateway, interfaceIndex: 10);
            ethernetInterface.Setup(i => i.GetIPProperties()).Returns(ethernetIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { ethernetInterface.Object, wifiInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("10.0.0.0/24", result[0]); // WiFi first (lower metric)
            Assert.AreEqual("192.168.1.0/24", result[1]); // Ethernet second
        }

        [TestMethod]
        public void DetectCurrentNetworks_WithDifferentSubnetMasks_CalculatesCorrectCIDR()
        {
            // Arrange - Test various subnet masks
            var mockInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Test Adapter",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("10.8.1.50"), IPAddress.Parse("255.255.255.0")),     // /24
                (IPAddress.Parse("172.16.0.50"), IPAddress.Parse("255.255.0.0")),      // /16
                (IPAddress.Parse("192.168.100.50"), IPAddress.Parse("255.255.255.128")) // /25
            };
            var gatewayAddresses = new List<IPAddress> { IPAddress.Parse("10.8.1.1") };

            var mockIPProperties = CreateMockIPProperties(unicastAddresses, gatewayAddresses);
            mockInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { mockInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.Contains("10.8.1.0/24"));
            Assert.IsTrue(result.Contains("172.16.0.0/16"));
            Assert.IsTrue(result.Contains("192.168.100.0/25"));
        }

        #endregion

        #region Filtering Tests

        [TestMethod]
        public void DetectCurrentNetworks_SkipsLoopbackInterface()
        {
            // Arrange
            var loopbackInterface = CreateMockNetworkInterface(
                "Loopback",
                "Software Loopback Interface",
                OperationalStatus.Up,
                NetworkInterfaceType.Loopback);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("127.0.0.1"), IPAddress.Parse("255.0.0.0"))
            };
            var mockIPProperties = CreateMockIPProperties(unicastAddresses, new List<IPAddress>());
            loopbackInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { loopbackInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetectCurrentNetworks_SkipsTunnelInterface()
        {
            // Arrange
            var tunnelInterface = CreateMockNetworkInterface(
                "Tunnel",
                "VPN Tunnel",
                OperationalStatus.Up,
                NetworkInterfaceType.Tunnel);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("10.0.0.1"), IPAddress.Parse("255.255.255.0"))
            };
            var gatewayAddresses = new List<IPAddress> { IPAddress.Parse("10.0.0.254") };
            var mockIPProperties = CreateMockIPProperties(unicastAddresses, gatewayAddresses);
            tunnelInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { tunnelInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetectCurrentNetworks_SkipsNonOperationalInterface()
        {
            // Arrange
            var downInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Disabled Adapter",
                OperationalStatus.Down,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0"))
            };
            var mockIPProperties = CreateMockIPProperties(unicastAddresses, new List<IPAddress>());
            downInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { downInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetectCurrentNetworks_SkipsLinkLocalAddresses()
        {
            // Arrange
            var mockInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Test Adapter",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("169.254.1.1"), IPAddress.Parse("255.255.0.0")), // Link-local
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0")) // Valid
            };
            var gatewayAddresses = new List<IPAddress> { IPAddress.Parse("192.168.1.1") };

            var mockIPProperties = CreateMockIPProperties(unicastAddresses, gatewayAddresses);
            mockInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { mockInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("192.168.1.0/24", result[0]);
        }

        [TestMethod]
        public void DetectCurrentNetworks_SkipsInterfacesWithoutGateway()
        {
            // Arrange - Interface without gateway
            var interfaceWithoutGateway = CreateMockNetworkInterface(
                "Ethernet1",
                "Test Adapter Without Gateway",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses1 = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0"))
            };
            var mockIPProperties1 = CreateMockIPProperties(unicastAddresses1, new List<IPAddress>());
            interfaceWithoutGateway.Setup(i => i.GetIPProperties()).Returns(mockIPProperties1.Object);

            // Arrange - Interface with gateway (should be detected)
            var interfaceWithGateway = CreateMockNetworkInterface(
                "Ethernet2",
                "Test Adapter With Gateway",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses2 = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("10.0.0.100"), IPAddress.Parse("255.255.255.0"))
            };
            var gatewayAddresses = new List<IPAddress> { IPAddress.Parse("10.0.0.1") };
            var mockIPProperties2 = CreateMockIPProperties(unicastAddresses2, gatewayAddresses);
            interfaceWithGateway.Setup(i => i.GetIPProperties()).Returns(mockIPProperties2.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { interfaceWithoutGateway.Object, interfaceWithGateway.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert - Only interface with gateway should be detected
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("10.0.0.0/24", result[0]);
        }

        [TestMethod]
        public void DetectCurrentNetworks_SkipsIPv6Addresses()
        {
            // Arrange
            var mockInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Test Adapter",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var mockIPProperties = new Mock<IPInterfaceProperties>();

            // Setup IPv6 unicast address
            var unicastCollection = new List<UnicastIPAddressInformation>();
            var mockUnicast = new Mock<UnicastIPAddressInformation>();
            mockUnicast.Setup(u => u.Address).Returns(IPAddress.Parse("fe80::1"));
            mockUnicast.Setup(u => u.IPv4Mask).Returns((IPAddress)null!);
            unicastCollection.Add(mockUnicast.Object);

            mockIPProperties.Setup(p => p.UnicastAddresses)
                .Returns(new MockUnicastIPAddressInformationCollection(unicastCollection));

            var gatewayCollection = new List<GatewayIPAddressInformation>();
            var mockGateway = new Mock<GatewayIPAddressInformation>();
            mockGateway.Setup(g => g.Address).Returns(IPAddress.Parse("192.168.1.1"));
            gatewayCollection.Add(mockGateway.Object);

            mockIPProperties.Setup(p => p.GatewayAddresses)
                .Returns(new MockGatewayIPAddressInformationCollection(gatewayCollection));

            var mockIPv4Properties = new Mock<IPv4InterfaceProperties>();
            mockIPv4Properties.Setup(p => p.Index).Returns(1);
            mockIPProperties.Setup(p => p.GetIPv4Properties()).Returns(mockIPv4Properties.Object);

            mockInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { mockInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        #endregion

        #region Fallback Tests

        [TestMethod]
        public void DetectCurrentNetworks_FallbackToActiveInterfaces_WhenNoGateways()
        {
            // Arrange - Interface without gateway (should trigger fallback)
            var mockInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Test Adapter",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0"))
            };
            // No gateways - should trigger fallback to active interfaces
            var mockIPProperties = CreateMockIPProperties(unicastAddresses, new List<IPAddress>());
            mockInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { mockInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert - fallback should return active interfaces even without gateways
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("192.168.1.0/24", result[0]);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void DetectCurrentNetworks_WithNoInterfaces_ReturnsEmptyList()
        {
            // Arrange
            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(Array.Empty<NetworkInterface>());

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetectCurrentNetworks_WithDuplicateNetworks_ReturnsUniqueEntries()
        {
            // Arrange
            var mockInterface = CreateMockNetworkInterface(
                "Ethernet",
                "Test Adapter",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("192.168.1.50"), IPAddress.Parse("255.255.255.0")),
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0")) // Same network
            };
            var gatewayAddresses = new List<IPAddress> { IPAddress.Parse("192.168.1.1") };

            var mockIPProperties = CreateMockIPProperties(unicastAddresses, gatewayAddresses);
            mockInterface.Setup(i => i.GetIPProperties()).Returns(mockIPProperties.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { mockInterface.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("192.168.1.0/24", result[0]);
        }

        [TestMethod]
        public void DetectCurrentNetworks_WithLoopbackGateway_SkipsInterface()
        {
            // Arrange - Interface with loopback gateway
            var interfaceWithLoopbackGateway = CreateMockNetworkInterface(
                "Ethernet1",
                "Test Adapter With Loopback Gateway",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses1 = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("192.168.1.100"), IPAddress.Parse("255.255.255.0"))
            };
            var loopbackGatewayAddresses = new List<IPAddress> { IPAddress.Parse("127.0.0.1") }; // Loopback gateway
            var mockIPProperties1 = CreateMockIPProperties(unicastAddresses1, loopbackGatewayAddresses);
            interfaceWithLoopbackGateway.Setup(i => i.GetIPProperties()).Returns(mockIPProperties1.Object);

            // Arrange - Interface with valid gateway (should be detected)
            var interfaceWithValidGateway = CreateMockNetworkInterface(
                "Ethernet2",
                "Test Adapter With Valid Gateway",
                OperationalStatus.Up,
                NetworkInterfaceType.Ethernet);

            var unicastAddresses2 = new List<(IPAddress, IPAddress)>
            {
                (IPAddress.Parse("10.0.0.100"), IPAddress.Parse("255.255.255.0"))
            };
            var validGatewayAddresses = new List<IPAddress> { IPAddress.Parse("10.0.0.1") };
            var mockIPProperties2 = CreateMockIPProperties(unicastAddresses2, validGatewayAddresses);
            interfaceWithValidGateway.Setup(i => i.GetIPProperties()).Returns(mockIPProperties2.Object);

            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces())
                .Returns(new[] { interfaceWithLoopbackGateway.Object, interfaceWithValidGateway.Object });

            // Act
            var result = _service.DetectCurrentNetworks();

            // Assert - Only interface with valid gateway should be detected
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("10.0.0.0/24", result[0]);
        }

        #endregion

        #region CIDR Validation Tests

        [TestMethod]
        public void IsValidCidr_WithValidCidr_ReturnsTrue()
        {
            // Arrange
            var validCidrs = new[]
            {
                "192.168.1.0/24",
                "10.0.0.0/8",
                "172.16.0.0/12",
                "192.168.0.0/16",
                "10.8.1.0/24",
                "0.0.0.0/0",
                "255.255.255.255/32"
            };

            // Act & Assert
            foreach (var cidr in validCidrs)
            {
                Assert.IsTrue(_service.IsValidCidr(cidr), $"Expected '{cidr}' to be valid");
            }
        }

        [TestMethod]
        public void IsValidCidr_WithInvalidFormat_ReturnsFalse()
        {
            // Arrange
            var invalidCidrs = new[]
            {
                "192.168.1.0",          // Missing prefix
                "192.168.1.0/",         // Missing prefix length
                "/24",                  // Missing IP
                "192.168.1.0/24/32",    // Extra slash
                "192.168.1.0 / 24",     // Spaces
                "",                     // Empty
                null!,                  // Null
                "   ",                  // Whitespace
            };

            // Act & Assert
            foreach (var cidr in invalidCidrs)
            {
                Assert.IsFalse(_service.IsValidCidr(cidr), $"Expected '{cidr}' to be invalid");
            }
        }

        [TestMethod]
        public void IsValidCidr_WithInvalidIPAddress_ReturnsFalse()
        {
            // Arrange
            var invalidCidrs = new[]
            {
                "256.1.1.1/24",         // Octet > 255
                "192.168.1.999/24",     // Octet > 255
                "192.168.1/24",         // Incomplete IP
                "192.168.1.1.1/24",     // Too many octets
                "not.an.ip/24",         // Non-numeric
                "192.168.-1.0/24",      // Negative octet
                "a.b.c.d/24",           // Letters
            };

            // Act & Assert
            foreach (var cidr in invalidCidrs)
            {
                Assert.IsFalse(_service.IsValidCidr(cidr), $"Expected '{cidr}' to be invalid");
            }
        }

        [TestMethod]
        public void IsValidCidr_WithInvalidPrefixLength_ReturnsFalse()
        {
            // Arrange
            var invalidCidrs = new[]
            {
                "192.168.1.0/-1",       // Negative prefix
                "192.168.1.0/33",       // Prefix > 32
                "192.168.1.0/100",      // Way too large
                "192.168.1.0/abc",      // Non-numeric prefix
                "192.168.1.0/24.5",     // Decimal prefix
                "192.168.1.0/",         // Empty prefix
            };

            // Act & Assert
            foreach (var cidr in invalidCidrs)
            {
                Assert.IsFalse(_service.IsValidCidr(cidr), $"Expected '{cidr}' to be invalid");
            }
        }

        [TestMethod]
        public void IsValidCidr_WithIPv6_ReturnsFalse()
        {
            // Arrange
            var ipv6Cidrs = new[]
            {
                "2001:db8::/32",
                "fe80::1/64",
                "::1/128",
                "2001:0db8:85a3:0000:0000:8a2e:0370:7334/64"
            };

            // Act & Assert
            foreach (var cidr in ipv6Cidrs)
            {
                Assert.IsFalse(_service.IsValidCidr(cidr), $"Expected IPv6 '{cidr}' to be invalid (IPv4 only)");
            }
        }

        [TestMethod]
        public void IsValidCidr_WithBoundaryPrefixLengths_ReturnsCorrectResult()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(_service.IsValidCidr("192.168.1.0/0"), "Prefix /0 should be valid");
            Assert.IsTrue(_service.IsValidCidr("192.168.1.0/32"), "Prefix /32 should be valid");
            Assert.IsFalse(_service.IsValidCidr("192.168.1.0/33"), "Prefix /33 should be invalid");
            Assert.IsFalse(_service.IsValidCidr("192.168.1.0/-1"), "Prefix /-1 should be invalid");
        }

        [TestMethod]
        public void IsValidCidr_WithCommonNetworkRanges_ReturnsTrue()
        {
            // Arrange
            var commonRanges = new[]
            {
                "10.0.0.0/8",           // Class A private
                "172.16.0.0/12",        // Class B private
                "192.168.0.0/16",       // Class C private
                "169.254.0.0/16",       // Link-local
                "127.0.0.0/8",          // Loopback
            };

            // Act & Assert
            foreach (var cidr in commonRanges)
            {
                Assert.IsTrue(_service.IsValidCidr(cidr), $"Expected common range '{cidr}' to be valid");
            }
        }

        [TestMethod]
        public void IsValidCidr_WithVariousSubnetSizes_ReturnsTrue()
        {
            // Arrange
            var variousSubnets = new[]
            {
                "192.168.1.0/30",       // /30 - 4 IPs
                "192.168.1.0/29",       // /29 - 8 IPs
                "192.168.1.0/28",       // /28 - 16 IPs
                "192.168.1.0/27",       // /27 - 32 IPs
                "192.168.1.0/26",       // /26 - 64 IPs
                "192.168.1.0/25",       // /25 - 128 IPs
                "192.168.1.0/24",       // /24 - 256 IPs
            };

            // Act & Assert
            foreach (var cidr in variousSubnets)
            {
                Assert.IsTrue(_service.IsValidCidr(cidr), $"Expected subnet '{cidr}' to be valid");
            }
        }

        #endregion

        #region Mock Collection Classes

        private class MockUnicastIPAddressInformationCollection : UnicastIPAddressInformationCollection
        {
            private readonly List<UnicastIPAddressInformation> _items;

            public MockUnicastIPAddressInformationCollection(List<UnicastIPAddressInformation> items)
            {
                _items = items;
            }

            public override int Count => _items.Count;

            public override UnicastIPAddressInformation this[int index] => _items[index];

            public override IEnumerator<UnicastIPAddressInformation> GetEnumerator()
            {
                return _items.GetEnumerator();
            }
        }

        private class MockGatewayIPAddressInformationCollection : GatewayIPAddressInformationCollection
        {
            private readonly List<GatewayIPAddressInformation> _items;

            public MockGatewayIPAddressInformationCollection(List<GatewayIPAddressInformation> items)
            {
                _items = items;
            }

            public override int Count => _items.Count;

            public override GatewayIPAddressInformation this[int index] => _items[index];

            public override IEnumerator<GatewayIPAddressInformation> GetEnumerator()
            {
                return _items.GetEnumerator();
            }
        }

        #endregion
    }
}
