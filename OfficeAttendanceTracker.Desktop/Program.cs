using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeAttendanceTracker.Core;
using OfficeAttendanceTracker.Desktop;

// Apply STAThread using module initializer approach
System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.Unknown);
System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.STA);

// Initialize SettingsManager first
var settingsManager = new SettingsManager();

var builder = Host.CreateApplicationBuilder(args);

// Clear default configuration sources and add our custom settings provider
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection(); // For logging configuration if needed
var settingsSource = new SettingsConfigurationSource(settingsManager);
builder.Configuration.Sources.Add(settingsSource);

// Register SettingsManager as singleton
builder.Services.AddSingleton(settingsManager);

builder.Services.AddTransient<INetworkInfoProvider, DefaultNetworkInfoProvider>();
builder.Services.AddTransient<INetworkDetectionService, NetworkDetectionService>();
builder.Services.AddSingleton<IDateTimeProvider, DefaultDateTimeProvider>();
builder.Services.AddSingleton<IAttendanceService, AttendanceService>();

// Get DataFileName from settings
var dataFileName = settingsManager.CurrentSettings.DataFileName;
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
if (settingsManager.CurrentSettings.EnableBackgroundWorker)
{
    builder.Services.AddHostedService<Worker>();
}

var host = builder.Build();

// Start the host in a background task
_ = Task.Run(() => host.RunAsync());

// Get the attendance service from DI
var attendanceService = host.Services.GetRequiredService<IAttendanceService>();

// Initialize and run Windows Forms application with system tray
System.Windows.Forms.Application.EnableVisualStyles();
System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
System.Windows.Forms.Application.Run(new TrayApplicationContext(host, attendanceService, settingsManager));
