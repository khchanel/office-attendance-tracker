using OfficeAttendanceTracker.Service;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            _attendanceService.Add("aaa", true, new DateTime(2024, 4, 15));
            _attendanceService.Add("bbb", false, new DateTime(2024, 4, 20));
            _attendanceService.Add("ccc", true, new DateTime(2024, 5, 1));
            _attendanceService.Add("ddd", true, new DateTime(2024, 3, 5));

            // Act
            var filteredRecords = _attendanceService.GetMonth(monthToFilter);

            // Assert
            Assert.AreEqual(2, filteredRecords.Count);
            Assert.IsTrue(filteredRecords.All(r => r.Date.Month == 4 && r.Date.Year == 2024));
        }

        [TestMethod]
        public void AddAttendanceRecord_AddsRecordWithTodayDate()
        {
            // Arrange
            DateTime today = DateTime.Today;
            int initialCount = _attendanceService.GetMonth(today).Count;

            // Act
            _attendanceService.Add("xxx", true);

            // Assert
            Assert.AreEqual(initialCount + 1, _attendanceService.GetMonth(today).Count);
        }


        [TestMethod]
        public void GetToday()
        {
            _attendanceService.Add("aaa", true, DateTime.Today);

            var records = _attendanceService.GetToday("aaa");

            Assert.AreEqual(1, records.Count);

        }
    }
}