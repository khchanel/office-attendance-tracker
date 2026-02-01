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
builder.Services.AddSingleton<IDateTimeProvider, DefaultDateTimeProvider>();
builder.Services.AddSingleton<IAttendanceService, AttendanceService>();

// Get DataFileName from configuration
var dataFileName = builder.Configuration["DataFileName"] ?? "attendance.csv";
var extension = Path.GetExtension(dataFileName).ToLowerInvariant();

// Register store with explicit initialization
builder.Services.AddSingleton<IAttendanceRecordStore>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    
    IAttendanceRecordStore store = extension switch
    {
        ".csv" => new AttendanceRecordCsvFileStore(config),
        ".json" => new AttendanceRecordJsonFileStore(config),
        _ => throw new NotSupportedException($"Unsupported file extension '{extension}'. Supported: .csv, .json")
    };
    
    // Explicit initialization - load data and start auto-save timer
    store.Initialize();
    
    return store;
});

var host = builder.Build();
host.Run();



