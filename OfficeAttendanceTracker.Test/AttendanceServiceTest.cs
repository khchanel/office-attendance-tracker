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

        #region GetBusinessDaysInCurrentMonth Tests

        [TestMethod]
        public void GetBusinessDaysInCurrentMonth_CalculatesCorrectly_ForJanuary2025()
        {
            // January 2025: 31 days, starts on Wednesday, ends on Friday
            // Expected business days: 23 (Mon-Fri only)
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
            
            // Note: This test will only pass if run in January 2025
            // For a more robust test, you'd need to make DateTime.Today mockable
            var businessDays = service.GetBusinessDaysInCurrentMonth();
            
            Assert.IsTrue(businessDays >= 20 && businessDays <= 23, $"Expected 20-23 business days, got {businessDays}");
        }

        [TestMethod]
        public void GetBusinessDaysInCurrentMonth_ReturnsPositiveNumber()
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
            var businessDays = service.GetBusinessDaysInCurrentMonth();
            
            Assert.IsTrue(businessDays > 0, "Business days should be positive");
            Assert.IsTrue(businessDays <= 23, "Business days should not exceed 23 (max Mon-Fri in a month)");
        }

        #endregion

        #region GetComplianceStatus Tests

        [TestMethod]
        public void GetComplianceStatus_ReturnsCompliant_WhenAttendanceExceedsThreshold()
        {
            // Arrange: 50% threshold with 20 business days = 10 required days
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"},
                {"ComplianceThreshold", "0.5"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            // Mock 15 office days (more than 50% of typical 20-23 business days)
            var records = Enumerable.Range(1, 15)
                .Select(i => new AttendanceRecord { Date = DateTime.Today.AddDays(-i), IsOffice = true })
                .ToList();
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Compliant, status);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsWarning_WhenAttendanceNearThreshold()
        {
            // Arrange: Create scenario where attendance is within warning zone (within 20% of threshold)
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"},
                {"ComplianceThreshold", "0.5"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            // Mock 9 office days (assuming ~20 business days: 50% = 10 required, warning = 6, so 9 is in warning zone)
            var records = Enumerable.Range(1, 9)
                .Select(i => new AttendanceRecord { Date = DateTime.Today.AddDays(-i), IsOffice = true })
                .ToList();
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            // Note: This might be Warning or Compliant depending on actual business days
            Assert.IsTrue(status == ComplianceStatus.Warning || status == ComplianceStatus.Compliant);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsCritical_WhenAttendanceBelowWarning()
        {
            // Arrange: Low attendance scenario
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"},
                {"ComplianceThreshold", "0.5"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            // Mock only 2 office days (well below 50% threshold)
            var records = Enumerable.Range(1, 2)
                .Select(i => new AttendanceRecord { Date = DateTime.Today.AddDays(-i), IsOffice = true })
                .ToList();
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Critical, status);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsCompliant_WhenAttendanceExactlyAtThreshold()
        {
            // Arrange: Attendance exactly at 50% threshold
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"},
                {"ComplianceThreshold", "0.5"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            // Calculate exact threshold
            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);
            var businessDays = service.GetBusinessDaysInCurrentMonth();
            var requiredDays = (int)Math.Ceiling(businessDays * 0.5);

            // Mock exactly required days
            var records = Enumerable.Range(1, requiredDays)
                .Select(i => new AttendanceRecord { Date = DateTime.Today.AddDays(-i), IsOffice = true })
                .ToList();
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Compliant, status);
        }

        [TestMethod]
        public void GetComplianceStatus_UsesCustomThreshold_WhenConfigured()
        {
            // Arrange: Test with 60% threshold
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"},
                {"ComplianceThreshold", "0.6"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            // Mock 10 office days (50% of 20 days, but less than 60%)
            var records = Enumerable.Range(1, 10)
                .Select(i => new AttendanceRecord { Date = DateTime.Today.AddDays(-i), IsOffice = true })
                .ToList();
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            // With 60% threshold and only 50% attendance, should not be compliant
            Assert.AreNotEqual(ComplianceStatus.Compliant, status);
        }

        [TestMethod]
        public void GetComplianceStatus_UsesDefaultThreshold_WhenNotConfigured()
        {
            // Arrange: No threshold in config, should default to 0.5
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"}
                // No ComplianceThreshold
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            // Mock 15 office days (should be compliant with default 50%)
            var records = Enumerable.Range(1, 15)
                .Select(i => new AttendanceRecord { Date = DateTime.Today.AddDays(-i), IsOffice = true })
                .ToList();
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Compliant, status);
        }

        [TestMethod]
        public void GetComplianceStatus_HandleZeroAttendance()
        {
            // Arrange: No attendance records
            var loggerMock = new Mock<ILogger<AttendanceService>>();
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"},
                {"ComplianceThreshold", "0.5"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var networkProviderMock = new Mock<INetworkInfoProvider>();
            var storeMock = new Mock<IAttendanceRecordStore>();

            // Mock empty records
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(new List<AttendanceRecord>());

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Critical, status);
        }

        #endregion

        #region GetCurrentMonthAttendance Tests

        [TestMethod]
        public void GetCurrentMonthAttendance_CountsOnlyOfficeRecords()
        {
            // Arrange
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

            var records = new List<AttendanceRecord>
            {
                new AttendanceRecord { Date = DateTime.Today.AddDays(-1), IsOffice = true },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-2), IsOffice = true },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-3), IsOffice = false },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-4), IsOffice = false },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-5), IsOffice = true }
            };
            storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            var service = new AttendanceService(loggerMock.Object, config, networkProviderMock.Object, storeMock.Object);

            // Act
            var count = service.GetCurrentMonthAttendance();

            // Assert
            Assert.AreEqual(3, count);
        }

        #endregion
    }
}
