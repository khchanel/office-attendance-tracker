using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Test;

[TestClass]
public class MonthlyAttendanceReportTest
{
    private readonly Mock<IAttendanceRecordStore> _store = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    private AttendanceService CreateService(List<AttendanceRecord>? records = null, double threshold = 0.5)
    {
        _clock.Setup(clock => clock.Today).Returns(new DateTime(2025, 1, 15));
        _store.Setup(store => store.GetMonth(It.IsAny<DateTime>())).Returns(records ?? []);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ComplianceThreshold"] = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        return new AttendanceService(Mock.Of<ILogger<AttendanceService>>(), config,
            Mock.Of<INetworkInfoProvider>(), _store.Object, _clock.Object);
    }

    [TestMethod]
    public void GetMonthlyReport_RequestsSelectedMonthRatherThanCurrentMonth()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.GetMonthlyReport(new DateTime(2024, 2, 20));

        // Assert
        _store.Verify(store => store.GetMonth(new DateTime(2024, 2, 1)), Times.Once);
    }

    [TestMethod]
    [DataRow(2024, 2, 21)]
    [DataRow(2025, 1, 23)]
    [DataRow(2025, 2, 20)]
    public void GetMonthlyReport_CountsWeekdaysForSelectedMonth(int year, int month, int expected)
    {
        // Arrange
        var service = CreateService();

        // Act
        var report = service.GetMonthlyReport(new DateTime(year, month, 1));

        // Assert
        Assert.AreEqual(expected, report.BusinessDays);
    }

    [TestMethod]
    public void GetMonthlyReport_ExcludesWeekdayDaysOffFromDenominator()
    {
        // Arrange
        var service = CreateService([
            new() { Date = new DateTime(2025, 1, 6, 12, 0, 0), IsOffice = true, IsDayOff = true },
            new() { Date = new DateTime(2025, 1, 11), IsOffice = false, IsDayOff = true }
        ]);

        // Act
        var report = service.GetMonthlyReport(new DateTime(2025, 1, 1));

        // Assert
        Assert.AreEqual(22, report.BusinessDays);
    }

    [TestMethod]
    public void GetMonthlyReport_CountsWeekendOfficeVisitsButExcludesDaysOff()
    {
        // Arrange
        var service = CreateService([
            new() { Date = new DateTime(2025, 1, 6), IsOffice = true, IsDayOff = true },
            new() { Date = new DateTime(2025, 1, 11), IsOffice = true },
            new() { Date = new DateTime(2025, 1, 13), IsOffice = false }
        ]);

        // Act
        var report = service.GetMonthlyReport(new DateTime(2025, 1, 1));

        // Assert
        Assert.AreEqual(1, report.OfficeDays);
    }

    [TestMethod]
    public void GetMonthlyReport_RoundsUpConfiguredTarget()
    {
        // Arrange
        var service = CreateService(threshold: 0.6);

        // Act
        var report = service.GetMonthlyReport(new DateTime(2025, 1, 1));

        // Assert
        Assert.AreEqual(14, report.RequiredOfficeDays);
    }

    [TestMethod]
    public void GetMonthlyReport_CountsMissingWeekdaysOnlyThroughToday()
    {
        // Arrange: 11 weekdays through January 15, with two recorded weekdays.
        var service = CreateService([
            new() { Date = new DateTime(2025, 1, 6), IsOffice = false, IsDayOff = true },
            new() { Date = new DateTime(2025, 1, 7, 15, 0, 0), IsOffice = false }
        ]);

        // Act
        var report = service.GetMonthlyReport(new DateTime(2025, 1, 1));

        // Assert
        Assert.AreEqual(9, report.UnrecordedBusinessDays);
    }

    [TestMethod]
    public void GetMonthlyReport_FutureMonthHasNoMissingWeekdays()
    {
        // Arrange
        var service = CreateService();

        // Act
        var report = service.GetMonthlyReport(new DateTime(2025, 2, 1));

        // Assert
        Assert.AreEqual(0, report.UnrecordedBusinessDays);
    }

    [TestMethod]
    public void GetMonthlyReport_HistoricalEmptyMonthHasAllWeekdaysMissing()
    {
        // Arrange
        var service = CreateService();

        // Act
        var report = service.GetMonthlyReport(new DateTime(2024, 2, 1));

        // Assert
        Assert.AreEqual(21, report.UnrecordedBusinessDays);
    }

    [TestMethod]
    public void GetMonthlyReport_AllDaysOffHasNoAttendanceRate()
    {
        // Arrange
        var service = CreateService(Enumerable.Range(1, 31).Select(day => new AttendanceRecord
        {
            Date = new DateTime(2025, 1, day), IsOffice = false, IsDayOff = true
        }).ToList());

        // Act
        var report = service.GetMonthlyReport(new DateTime(2025, 1, 1));

        // Assert
        Assert.IsNull(report.AttendanceRate);
    }

    [TestMethod]
    public void GetMonthlyReport_DetachesRecordsFromStore()
    {
        // Arrange
        var record = new AttendanceRecord { Date = new DateTime(2025, 1, 6), IsOffice = true };
        var service = CreateService([record]);

        // Act
        var report = service.GetMonthlyReport(new DateTime(2025, 1, 1));
        report.Records[0].IsOffice = false;

        // Assert
        Assert.IsTrue(record.IsOffice);
    }

    [TestMethod]
    public void GetMonthlyReport_DoesNotWriteToStore()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.GetMonthlyReport(new DateTime(2025, 1, 1));

        // Assert
        _store.Verify(store => store.SaveChanges(), Times.Never);
    }
}
