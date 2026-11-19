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
        settings.CancelRequested += (_, _) => SelectPage(settings: false);
        Pages.SelectionChanged += (_, args) =>
        {
            if (!ReferenceEquals(args.Source, Pages) || !ReportTab.IsSelected)
                return;

            if (args.RemovedItems.Contains(SettingsTab))
            {
                if (settings.HasUnsavedChanges || HasOpenDialog)
                {
                    SettingsTab.IsSelected = true;
                    if (ConfirmLeaveSettings(settings))
                        ReportTab.IsSelected = true;
                    return;
                }

                settings.ClearFeedback();
            }


            report.RefreshReport();
        };
        Activated += (_, _) =>
        {
            if (ReportTab.IsSelected)
                report.RefreshReport();
        };
        Closing += (_, args) => args.Cancel = !ConfirmLeaveSettings(settings);
    }

    internal void SelectPage(bool settings) => Pages.SelectedItem = settings ? SettingsTab : ReportTab;

    internal bool HasOpenDialog => OwnedWindows.Cast<Window>().Any(window => window.IsVisible);

    private bool ConfirmLeaveSettings(SettingsView settings)
    {
        if (HasOpenDialog)
            return false;

        if (!settings.HasUnsavedChanges)
            return true;

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        var dialog = new SettingsConfirmationDialog("Unsaved settings", "Discard your unsaved changes?",
            "_Discard changes", "_Keep editing") { Owner = this };

        if (dialog.ShowDialog() != true)
            return false;

        settings.DiscardChanges();
        return true;
    }
}
