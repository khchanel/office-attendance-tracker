using OfficeAttendanceTracker.Desktop;

namespace OfficeAttendanceTracker.Test;

[TestClass]
public class OfficeTimeFormatterTest
{
    [TestMethod]
    [DataRow(0, "0h 0m")]
    [DataRow(25200, "7h 0m")]
    [DataRow(27000, "7h 30m")]
    [DataRow(29, "0h 0m")]
    [DataRow(30, "0h 1m")]
    [DataRow(26969, "7h 29m")]
    [DataRow(26970, "7h 30m")]
    [DataRow(3569, "0h 59m")]
    [DataRow(3570, "1h 0m")]
    [DataRow(86400, "24h 0m")]
    public void Format_RoundsToNearestMinute(int seconds, string expected)
    {
        // Arrange
        var hours = seconds / 3600m;

        // Act
        var result = OfficeTimeFormatter.Format(hours);

        // Assert
        Assert.AreEqual(expected, result);
    }
}
