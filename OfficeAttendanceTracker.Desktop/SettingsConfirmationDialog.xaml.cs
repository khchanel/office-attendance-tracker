using System.Windows;

namespace OfficeAttendanceTracker.Desktop;

internal partial class SettingsConfirmationDialog : Window
{
    internal SettingsConfirmationDialog(string title, string question, string confirm, string cancel = "Cancel", string? details = null)
    {
        InitializeComponent();
        Title = title;
        Question.Text = question;
        ConfirmButton.Content = confirm;
        CancelButton.Content = cancel;
        Details.Text = details;
        Details.Visibility = details is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Confirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
