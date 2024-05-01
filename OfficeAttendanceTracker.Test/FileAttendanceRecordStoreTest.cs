using OfficeAttendanceTracker.Service;

namespace OfficeAttendanceTracker.Test
{
    [TestClass]
    public class FileAttendanceRecordStoreTest
    {
        private AttendanceRecordFileStore _attendanceService;


        [TestInitialize]
        public void Setup()
        {
            _attendanceService = new AttendanceRecordFileStore();
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
            _attendanceService.Add(true);

            // Assert
            Assert.AreEqual(initialCount + 1, _attendanceService.GetMonth(today).Count);
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
    }
}