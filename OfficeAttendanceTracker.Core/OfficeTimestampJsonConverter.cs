using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OfficeAttendanceTracker.Core;

internal sealed class OfficeTimestampJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is { } timestamp)
            writer.WriteStringValue(timestamp.ToString(DateTimeExtensions.OffsetTimestampFormat, CultureInfo.InvariantCulture));
        else
            writer.WriteNullValue();
    }
}
