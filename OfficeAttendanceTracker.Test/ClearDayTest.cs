using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Test;

[TestClass]
public class ClearDayTest
{
    private static AttendanceService CreateService(IAttendanceRecordStore store)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(provider => provider.Today).Returns(new DateTime(2025, 1, 15));
        return new AttendanceService(Mock.Of<ILogger<AttendanceService>>(), new ConfigurationBuilder().Build(),
            Mock.Of<INetworkInfoProvider>(), store, clock.Object);
    }

    [TestMethod]
    public void ClearDay_ReloadsBeforeDeletingNormalizedDate()
    {
        // Arrange
        var date = new DateTime(2025, 1, 16, 15, 30, 0);
        var sequence = new MockSequence();
        var store = new Mock<IAttendanceRecordStore>(MockBehavior.Strict);
        store.InSequence(sequence).Setup(records => records.Reload());
        store.InSequence(sequence).Setup(records => records.ClearDay(date.Date));
        var service = CreateService(store.Object);

        // Act
        service.ClearDay(date);

        // Assert
        store.VerifyAll();
    }

    [TestMethod]
    public void ClearDay_PropagatesSaveFailure()
    {
        // Arrange
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(records => records.ClearDay(It.IsAny<DateTime>())).Throws(new IOException("Cannot save"));
        var service = CreateService(store.Object);

        // Act / Assert
        Assert.ThrowsExactly<IOException>(() => service.ClearDay(new DateTime(2025, 1, 6)));
    }

    [TestMethod]
    public void ClearDay_DoesNotDeleteWhenReloadFails()
    {
        // Arrange
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(records => records.Reload()).Throws(new IOException("Cannot load"));
        var service = CreateService(store.Object);

        // Act
        try { service.ClearDay(new DateTime(2025, 1, 6)); }
        catch (IOException) { }

        // Assert
        store.Verify(records => records.ClearDay(It.IsAny<DateTime>()), Times.Never);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void StoreClearDay_PersistsDeletionAndPreservesOtherDates(bool isOffice, bool isDayOff)
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(isOffice, date).IsDayOff = isDayOff;
        var other = store.Add(true, date.AddDays(1)) with { };
        store.SaveChanges();

        // Act
        store.ClearDay(date.AddHours(12));
        store.Reload();

        // Assert
        CollectionAssert.AreEqual(new[] { other }, store.GetAll());
    }

    [TestMethod]
    public void StoreClearDay_RemovesAllEntriesForSelectedDate()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(true, date);
        store.Add(false, date);
        store.SaveChanges();

        // Act
        store.ClearDay(date);
        store.Reload();

        // Assert
        Assert.HasCount(0, store.GetAll());
    }

    [TestMethod]
    public void StoreClearDay_MissingDateDoesNotSave()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();

        // Act
        store.ClearDay(new DateTime(2025, 1, 6));

        // Assert
        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
    public void StoreClearDay_FailedSaveRestoresRecordsAndDoesNotRetryDeletion()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(true, date).IsDayOff = true;
        store.Add(false, date.AddDays(1));
        store.SaveChanges();
        var expected = store.GetAll().Select(record => record with { }).ToArray();
        store.FailSave = true;

        // Act
        try { store.ClearDay(date); }
        catch (IOException) { }
        store.FailSave = false;
        store.SaveChanges();

        // Assert
        CollectionAssert.AreEqual(expected, store.GetAll());
    }

    [TestMethod]
    public void ClearDay_UpdatesMonthlyTotals()
    {
        // Arrange
        var date = new DateTime(2025, 1, 6);
        var records = new List<AttendanceRecord> { new() { Date = date, IsOffice = true, IsDayOff = true } };
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(storage => storage.GetMonth(It.IsAny<DateTime>())).Returns(records);
        store.Setup(storage => storage.ClearDay(date)).Callback(() => records.Clear());
        var service = CreateService(store.Object);

        // Act
        service.ClearDay(date);
        var report = service.GetMonthlyReport(date);

        // Assert
        Assert.AreEqual((0, 0, 23, 12, 11),
            (report.OfficeDays, report.DaysOff, report.BusinessDays, report.RequiredOfficeDays, report.UnrecordedBusinessDays));
    }

    private sealed class MemoryStore : AttendanceRecordFileStore
    {
        private List<AttendanceRecord> _saved = [];
        public bool FailSave { get; set; }
        public int SaveCount { get; private set; }

        public MemoryStore() : base(null, "unused.json") { }

        public override void Load() => _attendanceRecords = _saved.Select(record => record with { }).ToList();

        protected override void Save()
        {
            if (FailSave)
                throw new IOException("Cannot save");
            _saved = _attendanceRecords.Select(record => record with { }).ToList();
            SaveCount++;
        }
    }
}
