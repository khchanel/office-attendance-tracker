using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using OfficeAttendanceTracker.Service;
using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Test
{
    [TestClass]
    public class AttendanceServiceTest
    {
        [TestMethod]
        public void Constructor_Throws_WhenNetworksConfigMissing()
        {
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var config = new ConfigurationBuilder().Build(); // No Networks section
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            Assert.ThrowsExactly<ArgumentException>(() => new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object));
        }

        [TestMethod]
        public void Constructor_ParsesNetworksConfig()
        {
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void CheckAttendance_ReturnsTrue_WhenHostIpInNetwork()
        {
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var networkProviderMock = new Mock<INetworkInfoProvider>();
            networkProviderMock.Setup(p => p.GetHostName()).Returns("testhost");
            networkProviderMock.Setup(p => p.GetHostAddresses(It.IsAny<string>())).Returns(new[] { IPAddress.Parse("192.168.1.10") });
            networkProviderMock.Setup(p => p.GetAllNetworkInterfaces()).Returns(new List<NetworkInterface>());
            var storeMock = new Mock<IAttendanceRecordStore>();

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);
            Assert.IsTrue(service.CheckAttendance());
        }

        [TestMethod]
        public void CheckAttendance_ReturnsFalse_WhenNoIpInNetwork()
        {
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var networkProviderMock = new Mock<INetworkInfoProvider>();
            networkProviderMock.Setup(p => p.GetHostName()).Returns("testhost");
            networkProviderMock.Setup(p => p.GetHostAddresses(It.IsAny<string>())).Returns(new[] { IPAddress.Parse("10.0.0.1") });
            networkProviderMock.Setup(p => p.GetAllNetworkInterfaces()).Returns(new List<NetworkInterface>());
            var storeMock = new Mock<IAttendanceRecordStore>();

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);
            Assert.IsFalse(service.CheckAttendance());
        }
    }
}
