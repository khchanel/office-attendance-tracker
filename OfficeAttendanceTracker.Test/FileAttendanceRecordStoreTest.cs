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

        #region Duplicate Date Tests

        [TestMethod]
        public void Add_UpdatesExistingRecord_WhenDateAlreadyExists()
        {
            // Arrange
            DateTime testDate = new DateTime(2026, 1, 3);
            _attendanceService.Add(true, testDate);

            // Act - Add with same date but different value
            var updatedRecord = _attendanceService.Add(false, testDate);

            // Assert
            Assert.IsFalse(updatedRecord.IsOffice, "Record should be updated to false");
            
            var allRecords = _attendanceService.GetAll();
            var recordsForDate = allRecords.Where(r => r.Date == testDate).ToList();
            Assert.AreEqual(1, recordsForDate.Count, "Should only have one record for this date");
            Assert.IsFalse(recordsForDate[0].IsOffice, "The record should have the updated value");
        }

        [TestMethod]
        public void Add_ReturnsSameReference_WhenUpdatingExistingDate()
        {
            // Arrange
            DateTime testDate = new DateTime(2026, 1, 3);
            var firstRecord = _attendanceService.Add(true, testDate);

            // Act
            var secondRecord = _attendanceService.Add(false, testDate);

            // Assert
            Assert.AreSame(firstRecord, secondRecord, "Should return the same record instance");
            Assert.IsFalse(firstRecord.IsOffice, "Original reference should reflect the update");
        }

        [TestMethod]
        public void Load_DeduplicatesByDate_KeepsLastOccurrence()
        {
            // Arrange - Add multiple records with same date
            DateTime testDate = new DateTime(2026, 1, 3);
            _attendanceService.Add(true, testDate);
            _attendanceService.Add(false, testDate);
            _attendanceService.Add(true, testDate);

            // Act - Reload from storage
            _attendanceService.Load();

            // Assert
            var allRecords = _attendanceService.GetAll();
            var recordsForDate = allRecords.Where(r => r.Date == testDate).ToList();
            Assert.AreEqual(1, recordsForDate.Count, "Should only have one record for this date after reload");
            Assert.IsTrue(recordsForDate[0].IsOffice, "Should keep the last value (true)");
        }

        [TestMethod]
        public void GetCurrentMonthAttendance_CountsEachDateOnce_WithDuplicates()
        {
            // Arrange - Simulate the bug scenario from user's input
            DateTime jan3 = new DateTime(2026, 1, 3);
            DateTime jan6 = new DateTime(2026, 1, 6);
            DateTime jan21 = new DateTime(2026, 1, 21);
            DateTime jan22 = new DateTime(2026, 1, 22);
            DateTime jan23 = new DateTime(2026, 1, 23);
            DateTime jan26 = new DateTime(2026, 1, 26);
            DateTime jan27 = new DateTime(2026, 1, 27);

            // Add records (mimicking the CSV input with duplicates)
            _attendanceService.Add(true, jan3);   // Office day
            _attendanceService.Add(false, jan6);  // Not office
            _attendanceService.Add(true, jan3);   // Duplicate - should update, not add
            _attendanceService.Add(true, jan3);   // Another duplicate
            _attendanceService.Add(true, jan26);  // Office day
            _attendanceService.Add(true, jan23);  // Office day
            _attendanceService.Add(true, jan22);  // Office day
            _attendanceService.Add(true, jan21);  // Office day
            _attendanceService.Add(true, jan27);  // Office day

            // Act
            var monthRecords = _attendanceService.GetMonth(new DateTime(2026, 1, 1));
            var officeCount = monthRecords.Count(r => r.IsOffice);

            // Assert
            Assert.AreEqual(7, monthRecords.Count, "Should have 7 unique dates");
            Assert.AreEqual(6, officeCount, "Should count 6 office days (jan3, jan21, jan22, jan23, jan26, jan27)");
        }

        [TestMethod]
        public void GetDate_ReturnsNull_WhenNoRecordExists()
        {
            // Arrange
            DateTime testDate = new DateTime(2026, 1, 15);

            // Act
            var record = _attendanceService.GetDate(testDate);

            // Assert
            Assert.IsNull(record);
        }

        [TestMethod]
        public void GetDate_ReturnsSingleRecord_AfterMultipleAdds()
        {
            // Arrange
            DateTime testDate = new DateTime(2026, 1, 3);
            _attendanceService.Add(true, testDate);
            _attendanceService.Add(false, testDate);

            // Act
            var record = _attendanceService.GetDate(testDate);

            // Assert
            Assert.IsNotNull(record);
            Assert.IsFalse(record.IsOffice, "Should return the updated (last) value");
        }

        [TestMethod]
        public void MultipleAdds_MaintainsCorrectCount()
        {
            // Arrange
            DateTime date1 = new DateTime(2026, 1, 1);
            DateTime date2 = new DateTime(2026, 1, 2);
            DateTime date3 = new DateTime(2026, 1, 3);

            // Act - Add multiple times
            _attendanceService.Add(true, date1);
            _attendanceService.Add(true, date1);  // Duplicate
            _attendanceService.Add(true, date2);
            _attendanceService.Add(false, date2); // Duplicate (update)
            _attendanceService.Add(true, date3);

            // Assert
            var allRecords = _attendanceService.GetAll();
            Assert.AreEqual(3, allRecords.Count, "Should only have 3 unique dates");
            
            Assert.IsTrue(_attendanceService.GetDate(date1)!.IsOffice);
            Assert.IsFalse(_attendanceService.GetDate(date2)!.IsOffice);
            Assert.IsTrue(_attendanceService.GetDate(date3)!.IsOffice);
        }

        [TestMethod]
        public void GetMonth_DeduplicatesResults()
        {
            // Arrange
            DateTime jan1 = new DateTime(2026, 1, 1);
            DateTime jan2 = new DateTime(2026, 1, 2);
            DateTime feb1 = new DateTime(2026, 2, 1);

            // Add records with duplicates
            _attendanceService.Add(true, jan1);
            _attendanceService.Add(false, jan2);
            _attendanceService.Add(true, feb1);

            // Act
            var janRecords = _attendanceService.GetMonth(new DateTime(2026, 1, 1));

            // Assert
            Assert.AreEqual(2, janRecords.Count, "January should have 2 unique dates");
            Assert.AreEqual(2, janRecords.Select(r => r.Date).Distinct().Count(), "All dates should be unique");
        }

        [TestMethod]
        public void GetAll_WithDateRange_DeduplicatesResults()
        {
            // Arrange
            DateTime date1 = new DateTime(2026, 1, 1);
            DateTime date2 = new DateTime(2026, 1, 5);
            DateTime date3 = new DateTime(2026, 1, 10);

            _attendanceService.Add(true, date1);
            _attendanceService.Add(true, date2);
            _attendanceService.Add(true, date3);

            // Act
            var records = _attendanceService.GetAll(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

            // Assert
            Assert.AreEqual(3, records.Count, "Should have 3 unique dates");
            Assert.AreEqual(3, records.Select(r => r.Date).Distinct().Count(), "All dates should be unique");
        }

        [TestMethod]
        public void GetAll_ReturnsNoDuplicates()
        {
            // Arrange
            DateTime date1 = new DateTime(2026, 1, 1);
            DateTime date2 = new DateTime(2026, 1, 2);
            DateTime date3 = new DateTime(2026, 1, 3);

            _attendanceService.Add(true, date1);
            _attendanceService.Add(false, date2);
            _attendanceService.Add(true, date3);

            // Act
            var allRecords = _attendanceService.GetAll();

            // Assert
            Assert.AreEqual(3, allRecords.Count);
            Assert.AreEqual(3, allRecords.Select(r => r.Date).Distinct().Count(), "All returned dates should be unique");
            
            // Verify results are sorted by date
            Assert.IsTrue(allRecords[0].Date < allRecords[1].Date);
            Assert.IsTrue(allRecords[1].Date < allRecords[2].Date);
        }

        #endregion
    }
}