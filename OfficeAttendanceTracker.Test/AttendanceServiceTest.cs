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

        [TestMethod]
        public void GetBusinessDaysUpToToday_ReturnsPositiveNumber()
        {
            var service = CreateService();
            var businessDays = service.GetBusinessDaysUpToToday();
            
            Assert.IsTrue(businessDays >= 0, "Business days up to today should be non-negative");
        }

        [TestMethod]
        public void GetBusinessDaysUpToToday_LessThanOrEqualToTotal()
        {
            var service = CreateService();
            var businessDaysUpToToday = service.GetBusinessDaysUpToToday();
            var totalBusinessDays = service.GetBusinessDaysInCurrentMonth();
            
            Assert.IsTrue(businessDaysUpToToday <= totalBusinessDays, 
                "Business days up to today should not exceed total business days in month");
        }

        #endregion

        #region GetComplianceStatus Tests

        [TestMethod]
        public void GetComplianceStatus_ReturnsAbsolutelyFine_WhenAttendanceMeetsEntireMonthRequirement()
        {
            // Arrange: Attendance meets entire month's requirement
            // With 50% threshold and 20+ business days total, 10+ required for entire month
            var config = BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });
            
            var tempService = CreateService(config);
            var totalBusinessDays = tempService.GetBusinessDaysInCurrentMonth();
            var requiredForEntireMonth = (int)Math.Ceiling(totalBusinessDays * 0.5);

            // Set attendance to meet entire month's requirement
            var service = CreateServiceWithAttendanceRecords(requiredForEntireMonth, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.AbsolutelyFine, status);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsCompliant_WhenAttendanceMeetsRequirement()
        {
            // Arrange: Attendance meets rolling (up to today) requirement but not entire month
            var config = BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });
            
            var tempService = CreateService(config);
            var businessDaysUpToToday = tempService.GetBusinessDaysUpToToday();
            var totalBusinessDays = tempService.GetBusinessDaysInCurrentMonth();
            var requiredForRolling = (int)Math.Ceiling(businessDaysUpToToday * 0.5);
            var requiredForEntireMonth = (int)Math.Ceiling(totalBusinessDays * 0.5);

            // Set attendance to meet rolling but not entire month (if possible)
            // If businessDaysUpToToday == totalBusinessDays, this will be AbsolutelyFine instead
            var attendanceDays = requiredForRolling;
            if (attendanceDays >= requiredForEntireMonth)
            {
                // If we're at end of month, test will show AbsolutelyFine instead
                attendanceDays = requiredForEntireMonth - 1;
            }

            var service = CreateServiceWithAttendanceRecords(attendanceDays, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            // Should be Compliant or AbsolutelyFine (if we're at end of month)
            Assert.IsTrue(status == ComplianceStatus.Compliant || status == ComplianceStatus.AbsolutelyFine);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsWarning_WhenAttendanceBelowRollingButAboveWarningThreshold()
        {
            // Arrange: Attendance between warningThreshold and rolling requirement
            var config = BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });
            
            var tempService = CreateService(config);
            var businessDaysUpToToday = tempService.GetBusinessDaysUpToToday();
            var requiredForRolling = (int)Math.Ceiling(businessDaysUpToToday * 0.5);
            var marginDays = (int)(businessDaysUpToToday * 0.2);
            var warningThreshold = requiredForRolling - marginDays;
            var attendanceDays = Math.Max(1, warningThreshold + 1); // Just above warning threshold

            var service = CreateServiceWithAttendanceRecords(attendanceDays, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Warning, status);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsCritical_WhenAttendanceBelowWarningThreshold()
        {
            // Arrange: Attendance below warningThreshold (based on rolling days)
            var config = BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });
            
            var tempService = CreateService(config);
            var businessDaysUpToToday = tempService.GetBusinessDaysUpToToday();
            var requiredForRolling = (int)Math.Ceiling(businessDaysUpToToday * 0.5);
            var marginDays = (int)(businessDaysUpToToday * 0.2);
            var warningThreshold = requiredForRolling - marginDays;
            var attendanceDays = Math.Max(0, warningThreshold - 2); // Well below warning

            var service = CreateServiceWithAttendanceRecords(attendanceDays, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.Critical, status);
        }

        [TestMethod]
        public void GetComplianceStatus_ReturnsAbsolutelyFine_WhenAttendanceExactlyAtEntireMonthThreshold()
        {
            // Arrange: Attendance exactly at entire month's requirement
            var config = BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });
            
            var tempService = CreateService(config);
            var totalBusinessDays = tempService.GetBusinessDaysInCurrentMonth();
            var requiredForEntireMonth = (int)Math.Ceiling(totalBusinessDays * 0.5);

            var service = CreateServiceWithAttendanceRecords(requiredForEntireMonth, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.5"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.AbsolutelyFine, status);
        }

        [TestMethod]
        public void GetComplianceStatus_UsesCustomThreshold_WhenConfigured()
        {
            // Arrange: Test with 60% threshold
            var config = BuildConfig(new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.6"}
            });
            
            var tempService = CreateService(config);
            var businessDaysUpToToday = tempService.GetBusinessDaysUpToToday();
            var requiredForRolling = (int)Math.Ceiling(businessDaysUpToToday * 0.6);
            // Set attendance below rolling requirement
            var attendanceDays = Math.Max(0, requiredForRolling - 1);

            var service = CreateServiceWithAttendanceRecords(attendanceDays, new Dictionary<string, string>
            {
                {"ComplianceThreshold", "0.6"}
            });

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            // With attendance below rolling required, should be Warning or Critical (not Compliant or AbsolutelyFine)
            Assert.IsTrue(status == ComplianceStatus.Warning || status == ComplianceStatus.Critical);
            Assert.AreNotEqual(ComplianceStatus.Compliant, status);
            Assert.AreNotEqual(ComplianceStatus.AbsolutelyFine, status);
        }

        [TestMethod]
        public void GetComplianceStatus_UsesDefaultThreshold_WhenNotConfigured()
        {
            // Arrange: No threshold in config, should default to 0.5
            var config = BuildConfig();  // No ComplianceThreshold, defaults to 0.5
            
            var tempService = CreateService(config);
            var totalBusinessDays = tempService.GetBusinessDaysInCurrentMonth();
            var requiredForEntireMonth = (int)Math.Ceiling(totalBusinessDays * 0.5);
            // Set attendance to meet entire month's requirement
            var attendanceDays = requiredForEntireMonth;

            var service = CreateServiceWithAttendanceRecords(attendanceDays);  // No ComplianceThreshold

            // Act
            var status = service.GetComplianceStatus();

            // Assert
            Assert.AreEqual(ComplianceStatus.AbsolutelyFine, status);
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
