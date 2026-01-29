using OfficeAttendanceTracker.Core;

var builder = Host.CreateApplicationBuilder(args);

// Conditionally add Worker based on configuration
var enableBackgroundWorker = builder.Configuration.GetValue("EnableBackgroundWorker", true);
if (enableBackgroundWorker)
{
    builder.Services.AddHostedService<Worker>();
}

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Office Attendance Tracker";
});
builder.Services.AddTransient<INetworkInfoProvider, DefaultNetworkInfoProvider>();
builder.Services.AddSingleton<IAttendanceService, AttendanceService>();

// Get DataFileName from configuration
var dataFileName = builder.Configuration["DataFileName"] ?? "attendance.csv";
var extension = Path.GetExtension(dataFileName).ToLowerInvariant();

switch (extension)
{
    case ".csv":
        builder.Services.AddSingleton<IAttendanceRecordStore, AttendanceRecordCsvFileStore>();
        break;
    case ".json":
        builder.Services.AddSingleton<IAttendanceRecordStore, AttendanceRecordJsonFileStore>();
        break;
    default:
        Console.Error.WriteLine($"Unsupported file extension '{extension}' in DataFileName. Supported: .csv, .json");
        Environment.Exit(1);
        break;
}

var host = builder.Build();
host.Run();



