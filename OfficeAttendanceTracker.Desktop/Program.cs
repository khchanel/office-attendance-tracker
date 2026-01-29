using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeAttendanceTracker.Core;
using OfficeAttendanceTracker.Desktop;

var builder = Host.CreateApplicationBuilder(args);

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
