using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Test;

[TestClass]
public class OfficeAttendanceEditTest
{
    private static AttendanceService CreateService(IAttendanceRecordStore store)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(provider => provider.Today).Returns(new DateTime(2025, 1, 15));
        return new AttendanceService(Mock.Of<ILogger<AttendanceService>>(), new ConfigurationBuilder().Build(),
            Mock.Of<INetworkInfoProvider>(), store, clock.Object);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SetOfficeAttendance_ReloadsBeforeSavingNormalizedDate(bool isOffice)
    {
        // Arrange
        var date = new DateTime(2025, 1, 15, 15, 30, 0);
        var sequence = new MockSequence();
        var store = new Mock<IAttendanceRecordStore>(MockBehavior.Strict);
        store.InSequence(sequence).Setup(records => records.Reload());
        store.InSequence(sequence).Setup(records => records.SetOfficeAttendance(isOffice, date.Date));
        var service = CreateService(store.Object);

        // Act
        service.SetOfficeAttendance(date, isOffice);

        // Assert
        store.VerifyAll();
    }

    [TestMethod]
    public void SetOfficeAttendance_RejectsFutureAttendance()
    {
        // Arrange
        var service = CreateService(Mock.Of<IAttendanceRecordStore>());

        // Act / Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.SetOfficeAttendance(new DateTime(2025, 1, 16), true));
    }

    [TestMethod]
    public void SetOfficeAttendance_AllowsRemovingExistingFutureAttendance()
    {
        // Arrange
        var store = new Mock<IAttendanceRecordStore>();
        var service = CreateService(store.Object);
        var date = new DateTime(2025, 1, 16);

        // Act
        service.SetOfficeAttendance(date, false);

        // Assert
        store.Verify(records => records.SetOfficeAttendance(false, date), Times.Once);
    }

    [TestMethod]
    public void SetOfficeAttendance_PropagatesSaveFailure()
    {
        // Arrange
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(records => records.SetOfficeAttendance(true, It.IsAny<DateTime>())).Throws(new IOException("Cannot save"));
        var service = CreateService(store.Object);

        // Act / Assert
        Assert.ThrowsExactly<IOException>(() => service.SetOfficeAttendance(new DateTime(2025, 1, 6), true));
    }

    [TestMethod]
    public void SetOfficeAttendance_DoesNotEditWhenReloadFails()
    {
        // Arrange
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(records => records.Reload()).Throws(new IOException("Cannot load"));
        var service = CreateService(store.Object);

        // Act
        try { service.SetOfficeAttendance(new DateTime(2025, 1, 6), true); }
        catch (IOException) { }

        // Assert
        store.Verify(records => records.SetOfficeAttendance(It.IsAny<bool>(), It.IsAny<DateTime>()), Times.Never);
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void StoreSetOfficeAttendance_PreservesDayOffFlag(bool isOffice, bool isDayOff)
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(!isOffice, date).IsDayOff = isDayOff;
        store.SaveChanges();

        // Act
        store.SetOfficeAttendance(isOffice, date);
        store.Reload();

        // Assert
        Assert.AreEqual(new AttendanceRecord { Date = date, IsOffice = isOffice, IsDayOff = isDayOff }, store.GetDate(date));
    }

    [TestMethod]
    public void StoreSetOfficeAttendance_CreatesNormalizedRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6, 12, 0, 0);

        // Act
        store.SetOfficeAttendance(true, date);
        store.Reload();

        // Assert
        Assert.AreEqual(new AttendanceRecord { Date = date.Date, IsOffice = true }, store.GetDate(date));
    }

    [TestMethod]
    public void StoreSetOfficeAttendance_UnmarkingMissingDateDoesNotCreateRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();

        // Act
        store.SetOfficeAttendance(false, new DateTime(2025, 1, 6));

        // Assert
        Assert.AreEqual(0, store.GetAll().Count);
    }

    [TestMethod]
    public void StoreSetOfficeAttendance_RepeatedMarkDoesNotDuplicateRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.SetOfficeAttendance(true, date);

        // Act
        store.SetOfficeAttendance(true, date);

        // Assert
        Assert.AreEqual(1, store.GetAll().Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void StoreSetOfficeAttendance_FailedSaveRestoresExistingAttendance(bool isOffice)
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(!isOffice, date);
        store.SaveChanges();
        store.FailSave = true;

        // Act
        try { store.SetOfficeAttendance(isOffice, date); }
        catch (IOException) { }

        // Assert
        Assert.AreEqual(!isOffice, store.GetDate(date)!.IsOffice);
    }

    [TestMethod]
    public void StoreSetOfficeAttendance_FailedSaveRemovesNewRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        store.FailSave = true;
        var date = new DateTime(2025, 1, 6);

        // Act
        try { store.SetOfficeAttendance(true, date); }
        catch (IOException) { }

        // Assert
        Assert.IsNull(store.GetDate(date));
    }

    [TestMethod]
    public void StoreSetOfficeAttendance_FailedEditIsNotRetriedBySaveChanges()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        store.FailSave = true;
        try { store.SetOfficeAttendance(true, new DateTime(2025, 1, 6)); }
        catch (IOException) { }
        store.FailSave = false;

        // Act
        store.SaveChanges();

        // Assert
        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
    [DataRow(true, false, 1)]
    [DataRow(false, false, 0)]
    [DataRow(true, true, 0)]
    [DataRow(false, true, 0)]
    public void SetOfficeAttendance_UpdatesReportWhileRespectingDaysOff(bool isOffice, bool isDayOff, int expected)
    {
        // Arrange
        var record = new AttendanceRecord { Date = new DateTime(2025, 1, 6), IsOffice = !isOffice, IsDayOff = isDayOff };
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(storage => storage.GetMonth(It.IsAny<DateTime>())).Returns([record]);
        store.Setup(storage => storage.SetOfficeAttendance(It.IsAny<bool>(), record.Date))
            .Callback<bool, DateTime>((value, _) => record.IsOffice = value);
        var service = CreateService(store.Object);

        // Act
        service.SetOfficeAttendance(record.Date, isOffice);

        // Assert
        Assert.AreEqual(expected, service.GetMonthlyReport(record.Date).OfficeDays);
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
