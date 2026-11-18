using CsvHelper;
using OfficeAttendanceTracker.Core;
using System.Globalization;
using System.Text.Json;

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

            [TestMethod]
            public void JsonTimestamp_WritesWholeSecondsAfterReadingFractionalSeconds()
            {
                const string legacy = """
                    {"Date":"2026-11-18","IsOffice":true,"FirstOfficeDetectionAt":"2026-11-18T09:00:00.1234567+11:00"}
                    """;
                var record = JsonSerializer.Deserialize<AttendanceRecord>(legacy);

                using var rewritten = JsonDocument.Parse(JsonSerializer.Serialize(record));

                Assert.AreEqual("2026-11-18T09:00:00+11:00",
                    rewritten.RootElement.GetProperty("FirstOfficeDetectionAt").GetString());
            }
        }

        [TestClass]
        public class CsvAttendanceRecordStoreTest : AttendanceRecordStoreTestBase
        {
            public override IAttendanceRecordStore CreateStore() => new AttendanceRecordCsvFileStore();

            [TestMethod]
            public void CsvTimestamp_WritesWholeSecondsAfterReadingFractionalSeconds()
            {
                using var source = new StringReader("Date,IsOffice,IsDayOff,FirstOfficeDetectionAt\r\n2026-11-18,True,False,2026-11-18T09:00:00.1234567+11:00\r\n");
                using var reader = new CsvReader(source, CultureInfo.InvariantCulture);
                reader.Context.RegisterClassMap<AttendanceRecordMap>();
                using var output = new StringWriter(CultureInfo.InvariantCulture);
                using var writer = new CsvWriter(output, CultureInfo.InvariantCulture);
                writer.Context.RegisterClassMap<AttendanceRecordMap>();

                writer.WriteRecords(reader.GetRecords<AttendanceRecord>());

                StringAssert.Contains(output.ToString(), "2026-11-18T09:00:00+11:00");
            }

            [TestMethod]
            public void LegacyCsvRow_LoadsWithoutTrackingData()
            {
                using var reader = new StringReader("Date,IsOffice,IsDayOff\r\n2025-01-08,True,False\r\n");
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                csv.Context.RegisterClassMap<AttendanceRecordMap>();

                var row = csv.GetRecords<AttendanceRecord>().Single();

                Assert.AreEqual(new AttendanceRecord { Date = new DateTime(2025, 1, 8), IsOffice = true }, row);
            }
        }


        [TestInitialize]
        public void Setup()
        {
            _attendanceService = CreateStore();
            _attendanceService.Initialize(); // Initialize the store before use
            _attendanceService.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _attendanceService?.Dispose(); // Properly dispose of the store
        }


        [TestMethod]
        public void RecordOfficeDetection_RoundTripsIntervalAndOffset()
        {
            var first = new DateTimeOffset(2025, 1, 8, 9, 0, 0, TimeSpan.FromHours(11));
            var second = first.AddMinutes(5);
            var afterAway = second.AddHours(2);
            _attendanceService.RecordOfficeDetection(first.AddTicks(1234), null);
            _attendanceService.Reload();
            _attendanceService.RecordOfficeDetection(second.AddTicks(2345), first.AddTicks(1234));
            _attendanceService.RecordOfficeDetection(afterAway.AddTicks(3456), null);

            _attendanceService.Reload();
            var record = _attendanceService.GetDate(first.Date);

            Assert.AreEqual((true, (DateTimeOffset?)first, (DateTimeOffset?)afterAway, (decimal?)300, (TimeSpan?)first.Offset),
                (record?.IsOffice == true, record?.FirstOfficeDetectionAt, record?.LastOfficeDetectionAt,
                    record?.TrackedOfficeSeconds, record?.FirstOfficeDetectionAt?.Offset));
        }

        [TestMethod]
        public void RecordOfficeDetection_StartsNewRecordAtLocalMidnight()
        {
            var beforeMidnight = new DateTimeOffset(2026, 11, 18, 23, 58, 0, TimeSpan.FromHours(11));
            var afterMidnight = beforeMidnight.AddMinutes(5);
            var nextPoll = afterMidnight.AddMinutes(5);

            _attendanceService.RecordOfficeDetection(beforeMidnight, null);
            _attendanceService.RecordOfficeDetection(afterMidnight, beforeMidnight);
            _attendanceService.RecordOfficeDetection(nextPoll, afterMidnight);
            _attendanceService.Reload();
            var previousDay = _attendanceService.GetDate(beforeMidnight.Date);
            var newDay = _attendanceService.GetDate(afterMidnight.Date);

            Assert.AreEqual((beforeMidnight, afterMidnight, nextPoll, 300m),
                (previousDay!.LastOfficeDetectionAt!.Value, newDay!.FirstOfficeDetectionAt!.Value,
                    newDay.LastOfficeDetectionAt!.Value, newDay.TrackedOfficeSeconds!.Value));
        }

        [TestMethod]
        public void RecordOfficeDetection_DoesNotReplaceLastDetectionWhenClockGoesBack()
        {
            var morning = new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.FromHours(11));
            _attendanceService.RecordOfficeDetection(morning, null);

            _attendanceService.RecordOfficeDetection(morning.AddHours(-9).AddMinutes(10), morning);

            Assert.AreEqual(morning, _attendanceService.GetDate(morning.Date)?.LastOfficeDetectionAt);
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
