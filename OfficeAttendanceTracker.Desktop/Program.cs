using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeAttendanceTracker.Core;
using OfficeAttendanceTracker.Desktop;

var builder = Host.CreateApplicationBuilder(args);

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

// Conditionally add Worker based on configuration
var enableBackgroundWorker = builder.Configuration.GetValue("EnableBackgroundWorker", true);
if (enableBackgroundWorker)
{
    builder.Services.AddHostedService<Worker>();
}

var host = builder.Build();

// Start the host in a background task
_ = Task.Run(() => host.RunAsync());

// Get the attendance service from DI
var attendanceService = host.Services.GetRequiredService<IAttendanceService>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

// Initialize and run Windows Forms application with system tray
System.Windows.Forms.Application.EnableVisualStyles();
System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
System.Windows.Forms.Application.Run(new TrayApplicationContext(host, attendanceService, configuration));
