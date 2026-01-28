using CsvHelper.Configuration;

namespace OfficeAttendanceTracker.Service
{
    public class AttendanceRecordMap : ClassMap<AttendanceRecord>
    {
        public AttendanceRecordMap()
        {
            Map(m => m.Date).TypeConverterOption.Format("yyyy-MM-dd");
            Map(m => m.IsOffice);
            Map(m => m.IsDayOff);
        }
    }
}
