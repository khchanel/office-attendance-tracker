using OfficeAttendanceTracker.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Office Attendance Tracker";
});
builder.Services.AddTransient<IAttendanceService, AttendanceService>();

// Get DataFileName from configuration
var dataFileName = builder.Configuration["DataFileName"] ?? "attendance.csv";
var extension = Path.GetExtension(dataFileName).ToLowerInvariant();

switch (extension)
{
    case ".csv":
        builder.Services.AddTransient<IAttendanceRecordStore, AttendanceRecordCsvFileStore>();
        break;
    case ".json":
        builder.Services.AddTransient<IAttendanceRecordStore, AttendanceRecordJsonFileStore>();
        break;
    default:
        Console.Error.WriteLine($"Unsupported file extension '{extension}' in DataFileName. Supported: .csv, .json");
        Environment.Exit(1);
        break;
}

var host = builder.Build();
host.Run();
