using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeAttendanceTracker.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace OfficeAttendanceTracker.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var settings = new SettingsManager();
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Configuration.Sources.Add(new SettingsConfigurationSource(settings));
            builder.Services.AddSingleton(settings);
            builder.Services.AddTransient<INetworkInfoProvider, DefaultNetworkInfoProvider>();
            builder.Services.AddTransient<INetworkDetectionService, NetworkDetectionService>();
            builder.Services.AddSingleton<IDateTimeProvider, DefaultDateTimeProvider>();
            builder.Services.AddSingleton<IAttendanceServiceProvider, AttendanceServiceProvider>();

            using var host = builder.Build();
            host.StartAsync().GetAwaiter().GetResult();
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                using var tray = new TrayApplicationContext(host.Services, settings);
                app.Startup += (_, _) => tray.Start();
                return app.Run();
            }
            finally
            {
                host.StopAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            MessageBox.Show(ex.Message, "Office Attendance Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
