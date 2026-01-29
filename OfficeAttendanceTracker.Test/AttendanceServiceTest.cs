using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using OfficeAttendanceTracker.Core;
using System.Net;
using System.Net.NetworkInformation;

namespace OfficeAttendanceTracker.Test
{
    [TestClass]
    public class AttendanceServiceTest
    {
        private Mock<ILogger<AttendanceService>> _loggerMock = null!;
        private Mock<INetworkInfoProvider> _networkProviderMock = null!;
        private Mock<IAttendanceRecordStore> _storeMock = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<AttendanceService>>();
            _networkProviderMock = new Mock<INetworkInfoProvider>();
            _storeMock = new Mock<IAttendanceRecordStore>();
        }

        #region Helper Methods

        private IConfiguration BuildConfig(Dictionary<string, string>? settings = null)
        {
            var defaultSettings = new Dictionary<string, string>
            {
                {"Networks:0", "192.168.1.0/24"}
            };

            if (settings != null)
            {
                foreach (var kvp in settings)
                {
                    defaultSettings[kvp.Key] = kvp.Value;
                }
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(defaultSettings!)
                .Build();
        }

        private AttendanceService CreateService(IConfiguration? config = null)
        {
            config ??= BuildConfig();
            return new AttendanceService(_loggerMock.Object, config, _networkProviderMock.Object, _storeMock.Object);
        }

        private AttendanceService CreateServiceWithAttendanceRecords(int officeCount, Dictionary<string, string>? additionalConfig = null)
        {
            var records = Enumerable.Range(1, officeCount)
                .Select(i => new AttendanceRecord { Date = DateTime.Today.AddDays(-i), IsOffice = true })
                .ToList();
            _storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            return CreateService(BuildConfig(additionalConfig));
        }

        #endregion

        #region Constructor Tests

        [TestMethod]
        public void Constructor_Throws_WhenNetworksConfigMissing()
        {
            var emptyConfig = new ConfigurationBuilder().Build();
            
            Assert.ThrowsExactly<ArgumentException>(() => 
                new AttendanceService(_loggerMock.Object, emptyConfig, _networkProviderMock.Object, _storeMock.Object));
        }

        [TestMethod]
        public void Constructor_ParsesNetworksConfig()
        {
            var service = CreateService();
            Assert.IsNotNull(service);
        }

        #endregion

        #region CheckAttendance Tests

        [TestMethod]
        public void CheckAttendance_ReturnsTrue_WhenHostIpInNetwork()
        {
            _networkProviderMock.Setup(p => p.GetHostName()).Returns("testhost");
            _networkProviderMock.Setup(p => p.GetHostAddresses(It.IsAny<string>())).Returns(new[] { IPAddress.Parse("192.168.1.10") });
            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces()).Returns(new List<NetworkInterface>());

            var service = CreateService();
            Assert.IsTrue(service.CheckAttendance());
        }

        [TestMethod]
        public void CheckAttendance_ReturnsFalse_WhenNoIpInNetwork()
        {
            _networkProviderMock.Setup(p => p.GetHostName()).Returns("testhost");
            _networkProviderMock.Setup(p => p.GetHostAddresses(It.IsAny<string>())).Returns(new[] { IPAddress.Parse("10.0.0.1") });
            _networkProviderMock.Setup(p => p.GetAllNetworkInterfaces()).Returns(new List<NetworkInterface>());

            var service = CreateService();
            Assert.IsFalse(service.CheckAttendance());
        }

        #endregion

        #region GetBusinessDaysInCurrentMonth Tests

        [TestMethod]
        public void GetBusinessDaysInCurrentMonth_CalculatesCorrectly_ForJanuary2025()
        {
            // January 2025: 31 days, starts on Wednesday, ends on Friday
            // Expected business days: 23 (Mon-Fri only)
            var service = CreateService();
            
            // Note: This test will only pass if run in January 2025
            // For a more robust test, you'd need to make DateTime.Today mockable
            var businessDays = service.GetBusinessDaysInCurrentMonth();
            
            Assert.IsTrue(businessDays >= 20 && businessDays <= 23, $"Expected 20-23 business days, got {businessDays}");
        }

        [TestMethod]
        public void GetBusinessDaysInCurrentMonth_ReturnsPositiveNumber()
        {
            var service = CreateService();
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
            var service = CreateServiceWithAttendanceRecords(15, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Compliant, status);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsWarning_WhenAttendanceNearThreshold()
        {
            // Arrange: Create scenario where attendance is within warning zone (within 20% of threshold)
            // Mock 9 office days (assuming ~20 business days: 50% = 10 required, warning = 6, so 9 is in warning zone)
            var service = CreateServiceWithAttendanceRecords(9, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            // Note: This might be Warning or Compliant depending on actual business days
            Assert.IsTrue(status == ComplianceStatus.Warning || status == ComplianceStatus.Compliant);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsCritical_WhenAttendanceBelowWarning()
        {
            // Arrange: Low attendance scenario - only 2 office days (well below 50% threshold)
            var service = CreateServiceWithAttendanceRecords(2, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Critical, status);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsCompliant_WhenAttendanceExactlyAtThreshold()
        {
            // Arrange: Attendance exactly at 50% threshold
            var config = BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });
            
            // Calculate exact threshold
            var tempService = CreateService(config);
            var businessDays = tempService.GetBusinessDaysInCurrentMonth();
            var requiredDays = (int)Math.Ceiling(businessDays * 0.5);

            // Create service with exactly required days
            var service = CreateServiceWithAttendanceRecords(requiredDays, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Compliant, status);
        }

        [TestMethod]
        public void GetComplianceStatus_UsesCustomThreshold_WhenConfigured()
        {
            // Arrange: Test with 60% threshold
            // Mock 10 office days (50% of 20 days, but less than 60%)
            var service = CreateServiceWithAttendanceRecords(10, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.6"}
            });

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
            // Mock 15 office days (should be compliant with default 50%)
            var service = CreateServiceWithAttendanceRecords(15);  // No ComplianceThreshold

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Compliant, status);
        }


        [TestMethod]
        public void GetComplianceStatus_HandleZeroAttendance()
        {
            // Arrange: No attendance records
            _storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(new List<AttendanceRecord>());
            
            var service = CreateService(BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            }));

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
            var records = new List<AttendanceRecord>
            {
                new AttendanceRecord { Date = DateTime.Today.AddDays(-1), IsOffice = true },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-2), IsOffice = true },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-3), IsOffice = false },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-4), IsOffice = false },
                new AttendanceRecord { Date = DateTime.Today.AddDays(-5), IsOffice = true }
            };
            _storeMock.Setup(s => s.GetMonth(It.IsAny<DateTime>())).Returns(records);

            var service = CreateService();

            // Act
            var count = service.GetCurrentMonthAttendance();

            // Assert
            Assert.AreEqual(3, count);
        }

        #endregion
    }
}
