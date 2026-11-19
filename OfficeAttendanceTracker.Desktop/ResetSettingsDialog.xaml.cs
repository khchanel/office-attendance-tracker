using System.Windows;

namespace OfficeAttendanceTracker.Desktop;

internal partial class ResetSettingsDialog : Window
{
    internal ResetSettingsDialog() => InitializeComponent();

    private void ConfirmReset(object sender, RoutedEventArgs e) => DialogResult = true;
}
