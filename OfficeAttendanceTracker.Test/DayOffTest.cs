using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Test;

[TestClass]
public class DayOffTest
{
    private static AttendanceService CreateService(IAttendanceRecordStore store)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(provider => provider.Today).Returns(new DateTime(2025, 1, 15));
        return new AttendanceService(Mock.Of<ILogger<AttendanceService>>(), new ConfigurationBuilder().Build(),
            Mock.Of<INetworkInfoProvider>(), store, clock.Object);
    }

    [TestMethod]
    public void SetDayOff_ReloadsBeforeSavingNormalizedDate()
    {
        // Arrange
        var date = new DateTime(2025, 1, 6, 15, 30, 0);
        var sequence = new MockSequence();
        var store = new Mock<IAttendanceRecordStore>(MockBehavior.Strict);
        store.InSequence(sequence).Setup(records => records.Reload());
        store.InSequence(sequence).Setup(records => records.SetDayOff(true, date.Date));
        var service = CreateService(store.Object);

        // Act
        service.SetDayOff(date, true);

        // Assert
        store.VerifyAll();
    }

    [TestMethod]
    public void SetDayOff_PropagatesSaveFailure()
    {
        // Arrange
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(records => records.SetDayOff(true, It.IsAny<DateTime>())).Throws(new IOException("Cannot save"));
        var service = CreateService(store.Object);

        // Act / Assert
        Assert.ThrowsExactly<IOException>(() => service.SetDayOff(new DateTime(2025, 1, 6), true));
    }

    [TestMethod]
    public void SetDayOff_DoesNotEditWhenReloadFails()
    {
        // Arrange
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(records => records.Reload()).Throws(new IOException("Cannot load"));
        var service = CreateService(store.Object);

        // Act
        try { service.SetDayOff(new DateTime(2025, 1, 6), true); }
        catch (IOException) { }

        // Assert
        store.Verify(records => records.SetDayOff(It.IsAny<bool>(), It.IsAny<DateTime>()), Times.Never);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void StoreSetDayOff_PersistsFlag(bool isDayOff)
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(true, date).IsDayOff = !isDayOff;
        store.SaveChanges();

        // Act
        store.SetDayOff(isDayOff, date);
        store.Reload();

        // Assert
        Assert.AreEqual(isDayOff, store.GetDate(date)!.IsDayOff);
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void StoreSetDayOff_PreservesAttendance(bool isOffice, bool isDayOff)
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(isOffice, date).IsDayOff = !isDayOff;
        store.SaveChanges();

        // Act
        store.SetDayOff(isDayOff, date);
        store.Reload();

        // Assert
        Assert.AreEqual(isOffice, store.GetDate(date)!.IsOffice);
    }

    [TestMethod]
    public void StoreSetDayOff_CreatesFutureRecordWithNormalizedDate()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2030, 1, 6, 12, 0, 0);

        // Act
        store.SetDayOff(true, date);
        store.Reload();

        // Assert
        Assert.AreEqual(new AttendanceRecord { Date = date.Date, IsOffice = false, IsDayOff = true }, store.GetDate(date));
    }

    [TestMethod]
    public void StoreSetDayOff_RemovingUnrecordedDateDoesNotCreateRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();

        // Act
        store.SetDayOff(false, new DateTime(2025, 1, 6));

        // Assert
        Assert.AreEqual(0, store.GetAll().Count);
    }

    [TestMethod]
    public void StoreSetDayOff_RepeatedMarkDoesNotDuplicateRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.SetDayOff(true, date);

        // Act
        store.SetDayOff(true, date);

        // Assert
        Assert.AreEqual(1, store.GetAll().Count);
    }

    [TestMethod]
    public void StoreSetDayOff_FailedSaveRestoresExistingRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        var date = new DateTime(2025, 1, 6);
        store.Add(true, date);
        store.SaveChanges();
        store.FailSave = true;

        // Act
        try { store.SetDayOff(true, date); }
        catch (IOException) { }

        // Assert
        Assert.IsFalse(store.GetDate(date)!.IsDayOff);
    }

    [TestMethod]
    public void StoreSetDayOff_FailedSaveRemovesNewRecord()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        store.FailSave = true;
        var date = new DateTime(2025, 1, 6);

        // Act
        try { store.SetDayOff(true, date); }
        catch (IOException) { }

        // Assert
        Assert.IsNull(store.GetDate(date));
    }

    [TestMethod]
    public void StoreSetDayOff_FailedEditIsNotRetriedBySaveChanges()
    {
        // Arrange
        using var store = new MemoryStore();
        store.Initialize();
        store.FailSave = true;
        try { store.SetDayOff(true, new DateTime(2025, 1, 6)); }
        catch (IOException) { }
        store.FailSave = false;

        // Act
        store.SaveChanges();

        // Assert
        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
    public void SetDayOff_RefreshesMonthlyTarget()
    {
        // Arrange
        var records = new List<AttendanceRecord>
        {
            new() { Date = new DateTime(2025, 1, 6), IsOffice = false },
            new() { Date = new DateTime(2025, 1, 7), IsOffice = false }
        };
        var store = new Mock<IAttendanceRecordStore>();
        store.Setup(storage => storage.GetMonth(It.IsAny<DateTime>())).Returns(records);
        store.Setup(storage => storage.SetDayOff(It.IsAny<bool>(), It.IsAny<DateTime>()))
            .Callback<bool, DateTime>((isDayOff, date) => records.Single(record => record.Date == date).IsDayOff = isDayOff);
        var service = CreateService(store.Object);

        // Act
        service.SetDayOff(records[0].Date, true);
        service.SetDayOff(records[1].Date, true);

        // Assert
        Assert.AreEqual(11, service.GetMonthlyReport(new DateTime(2025, 1, 1)).RequiredOfficeDays);
    }

    // Exercise shared store behavior without accessing files or networks.
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
