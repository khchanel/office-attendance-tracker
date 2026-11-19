using System.Windows;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>Desktop attendance window opened by the tray.</summary>
public partial class MainWindow : Window
{
    internal MainWindow(ReportView report)
    {
        InitializeComponent();
        ReportHost.Content = report;
    }
}
