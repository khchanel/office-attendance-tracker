using System.Windows;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>Hosts the monthly report and settings in one desktop window.</summary>
public partial class MainWindow : Window
{
    internal MainWindow(ReportView report, SettingsView settings)
    {
        InitializeComponent();
        ReportTab.Content = report;
        SettingsTab.Content = settings;
        settings.Cancelled += (_, _) => SelectPage(settings: false);
        Pages.SelectionChanged += (sender, args) =>
        {
            if (ReferenceEquals(args.Source, Pages) && ReportTab.IsSelected)
                report.RefreshReport();
        };
        Activated += (_, _) =>
        {
            if (ReportTab.IsSelected)
                report.RefreshReport();
        };
    }

    internal void SelectPage(bool settings) => Pages.SelectedItem = settings ? SettingsTab : ReportTab;
}
