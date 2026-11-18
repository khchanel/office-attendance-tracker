using CsvHelper.Configuration;

namespace OfficeAttendanceTracker.Core
{
    public class AttendanceRecordMap : ClassMap<AttendanceRecord>
    {
        public AttendanceRecordMap()
        {
            Map(m => m.Date).TypeConverterOption.Format("yyyy-MM-dd");
            Map(m => m.IsOffice);
            Map(m => m.IsDayOff);
            string[] timestampFormats = [DateTimeExtensions.OffsetTimestampFormat, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];
            Map(m => m.FirstOfficeDetectionAt).Optional().TypeConverterOption.Format(timestampFormats);
            Map(m => m.LastOfficeDetectionAt).Optional().TypeConverterOption.Format(timestampFormats);
            Map(m => m.TrackedOfficeSeconds).Optional();
        }
    }
}
