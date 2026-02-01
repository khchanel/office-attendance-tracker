using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeAttendanceTracker.Core;
using OfficeAttendanceTracker.Desktop;
using System.Windows.Forms;

try
{
    // Apply STAThread using module initializer approach
    System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.Unknown);
    System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.STA);

    // Global exception handlers
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
    Application.ThreadException += (sender, e) =>
    {
        var errorMessage = $"Application Error:\n\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
        Console.Error.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Application Thread Exception:");
        Console.Error.WriteLine(e.Exception.ToString());
        MessageBox.Show(errorMessage, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    };
    AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
    {
        var ex = e.ExceptionObject as Exception;
        var errorMessage = $"Unhandled Exception:\n\n{ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}";
        Console.Error.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Unhandled Domain Exception:");
        Console.Error.WriteLine(ex?.ToString() ?? "Unknown exception");
        MessageBox.Show(errorMessage, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    };

    var settingsManager = new SettingsManager();
    var builder = Host.CreateApplicationBuilder(args);

    // Clear default configuration sources and add our custom settings provider
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddInMemoryCollection(); // For logging configuration if needed
    var settingsSource = new SettingsConfigurationSource(settingsManager);
    builder.Configuration.Sources.Add(settingsSource);

    builder.Services.AddSingleton(settingsManager);

    builder.Services.AddTransient<INetworkInfoProvider, DefaultNetworkInfoProvider>();
    builder.Services.AddTransient<INetworkDetectionService, NetworkDetectionService>();
    builder.Services.AddSingleton<IDateTimeProvider, DefaultDateTimeProvider>();
    builder.Services.AddSingleton<IAttendanceService, AttendanceService>();

    var dataFileName = settingsManager.CurrentSettings.DataFileName;
    var extension = Path.GetExtension(dataFileName).ToLowerInvariant();

    builder.Services.AddSingleton<IAttendanceRecordStore>(provider =>
    {
        var config = provider.GetRequiredService<IConfiguration>();
        
        IAttendanceRecordStore store = extension switch
        {
            ".csv" => new AttendanceRecordCsvFileStore(config),
            ".json" => new AttendanceRecordJsonFileStore(config),
            _ => throw new NotSupportedException($"Unsupported file extension '{extension}'. Supported: .csv, .json")
        };

        store.Initialize();
        
        return store;
    });

    // Conditionally add Worker based on configuration
    if (settingsManager.CurrentSettings.EnableBackgroundWorker)
    {
        builder.Services.AddHostedService<Worker>();
    }

    var host = builder.Build();
    _ = Task.Run(() => host.RunAsync());

    // Initialize and run Windows Forms application with system tray
    var attendanceService = host.Services.GetRequiredService<IAttendanceService>();
    System.Windows.Forms.Application.EnableVisualStyles();
    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
    System.Windows.Forms.Application.Run(new TrayApplicationContext(host, attendanceService, settingsManager));
}
catch (Exception ex)
{
    var errorMessage = $"Startup Error:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
    Console.Error.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Startup Exception:");
    Console.Error.WriteLine(ex.ToString());
    MessageBox.Show(errorMessage, "Startup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
    Environment.Exit(1);
}
