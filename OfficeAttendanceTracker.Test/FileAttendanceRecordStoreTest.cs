using OfficeAttendanceTracker.Service;

namespace OfficeAttendanceTracker.Test
{
    [TestClass]
    public abstract class AttendanceRecordStoreTestBase
    {
        protected IAttendanceRecordStore _attendanceService = null!;

        public abstract IAttendanceRecordStore CreateStore();


        [TestClass]
        public class FileAttendanceRecordStoreTest : AttendanceRecordStoreTestBase
        {
            public override IAttendanceRecordStore CreateStore() => new AttendanceRecordJsonFileStore();
        }

        [TestClass]
        public class CsvAttendanceRecordStoreTest : AttendanceRecordStoreTestBase
        {
            public override IAttendanceRecordStore CreateStore() => new AttendanceRecordCsvFileStore();
        }


        [TestInitialize]
        public void Setup()
        {
            _attendanceService = CreateStore();
            _attendanceService.Clear();
        }


        [TestMethod]
        public void GetAttendanceRecordsByMonth_ReturnsCorrectRecords()
        {
            // Arrange
            DateTime monthToFilter = new DateTime(2024, 4, 1);
            _attendanceService.Add(true, new DateTime(2024, 4, 15));
            _attendanceService.Add(false, new DateTime(2024, 4, 20));
            _attendanceService.Add(true, new DateTime(2024, 5, 1));
            _attendanceService.Add(true, new DateTime(2024, 3, 5));

            // Act
            var filteredRecords = _attendanceService.GetMonth(monthToFilter);

            // Assert
            Assert.AreEqual(2, filteredRecords.Count);
            Assert.IsTrue(filteredRecords.TrueForAll(r => r.Date.Month == 4 && r.Date.Year == 2024));
        }

        [TestMethod]
        public void AddAttendanceRecord_AddsRecordWithTodayDate()
        {
            // Arrange
            DateTime today = DateTime.Today;
            int initialCount = _attendanceService.GetMonth(today).Count;

            // Act
            _attendanceService.Add(true, DateTime.Today);

            // Assert
            Assert.AreEqual(initialCount + 1, _attendanceService.GetMonth(today).Count);
        }

        [TestMethod]
        public void AddAttendanceRecord_AddsRecordReturnReferenceCopy()
        {
            // Arrange
            DateTime today = DateTime.Today;
            int initialCount = _attendanceService.GetMonth(today).Count;

            // Act
            var record = _attendanceService.Add(true, DateTime.Today);


            // Assert
            var todayRecord = _attendanceService.GetToday();
            Assert.IsNotNull(todayRecord);
            Assert.AreSame<AttendanceRecord>(record, todayRecord);
            Assert.IsTrue(todayRecord.IsOffice);
            record.IsOffice = false;

            var updatedRecord = _attendanceService.GetToday();
            Assert.IsNotNull(updatedRecord);
            Assert.IsFalse(updatedRecord.IsOffice);
        }



        [TestMethod]
        public void GetToday()
        {
            _attendanceService.Add(true, DateTime.Today);

            var record = _attendanceService.GetToday();

            Assert.IsNotNull(record);
            Assert.AreEqual(true, record.IsOffice);
            Assert.AreEqual(DateTime.Today, record.Date);

        }

        [TestMethod]
        public void GetToday_Null()
        {
            var record = _attendanceService.GetToday();

            Assert.IsNull(record);

        }

        [TestMethod]
        public void GetMonth()
        {
            _attendanceService.Add(true, new DateTime(2024, 5, 1));
            _attendanceService.Add(false, new DateTime(2024, 5, 2));
            _attendanceService.Add(true, new DateTime(2024, 4, 1));



            var records = _attendanceService.GetMonth(new DateTime(2024,5,1));
            Assert.IsNotNull(records);
            Assert.AreEqual(2, records.Count);
            foreach (var r in records)
            {
                Assert.AreEqual(5, r.Date.Month);
            }

        }


        [TestMethod]
        public void Update_Existing()
        {
            _attendanceService.Add(false, DateTime.Today.AddDays(-2));
            _attendanceService.Add(false, DateTime.Today);
            Assert.AreEqual(2, _attendanceService.GetAll().Count);

            _attendanceService.Update(true, DateTime.Today);

            var x = _attendanceService.GetAll();
            Assert.AreEqual(2, x.Count);
            Assert.IsFalse(x[0].IsOffice);
            Assert.IsTrue(x[1].IsOffice);
        }
    }
}