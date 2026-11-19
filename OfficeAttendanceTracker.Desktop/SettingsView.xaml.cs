using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using OfficeAttendanceTracker.Core;
using UserControl = System.Windows.Controls.UserControl;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>Edits and applies desktop settings.</summary>
public partial class SettingsView : UserControl
{
    private readonly SettingsManager _settingsManager;
    private readonly INetworkDetectionService _networkDetectionService;
    private Draft _savedDraft;

    internal event EventHandler? CancelRequested;
    internal bool HasUnsavedChanges => ReadDraft() != _savedDraft;

    internal SettingsView(SettingsManager settingsManager, INetworkDetectionService networkDetectionService)
    {
        _settingsManager = settingsManager;
        _networkDetectionService = networkDetectionService;
        InitializeComponent();
        var settings = settingsManager.CurrentSettings;
        Welcome.Visibility = settingsManager.IsFirstRun && settings.Networks.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        LoadSettings(settings);
        _savedDraft = ReadDraft();
    }

    private void LoadSettings(AppSettings settings)
    {
        Networks.Text = string.Join(Environment.NewLine, settings.Networks);
        PollInterval.Text = Math.Clamp(settings.PollIntervalMs / 1000, 60, 86400).ToString();
        AutomaticTracking.IsChecked = settings.EnableBackgroundWorker;
        StartWithWindows.IsChecked = settings.StartWithWindows;
        Threshold.Text = FormatThreshold(settings.ComplianceThreshold);
        TimeMode.SelectedIndex = Enum.IsDefined(settings.OfficeTimeTrackingMode) ? (int)settings.OfficeTimeTrackingMode : 0;
        MinimumHours.Text = Math.Clamp(settings.MinimumOfficeHours, 0.01m, 24m).ToString("G29");
        DataFolder.Text = settings.DataFilePath ?? string.Empty;
        FileName.Text = settings.DataFileName;
        ClearFeedback();
    }

    private static string FormatThreshold(double threshold)
    {
        var culture = CultureInfo.CurrentCulture;
        // Scale the shortest round-trip representation to avoid binary rounding in the displayed percentage.
        if (threshold is >= 0 and <= 1 &&
            decimal.TryParse(threshold.ToString(culture), NumberStyles.Float, culture, out var fraction) &&
            (fraction != 0 || threshold == 0))
        {
            return (fraction * 100).ToString("0.############################", culture);
        }

        return (threshold * 100).ToString(culture);
    }

    private void TimeModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MinimumHoursField is not null)
            MinimumHoursField.Visibility = TimeMode.SelectedIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string[] ReadNetworks() =>
        Networks.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        var networks = ReadNetworks();
        if (networks.Length == 0)
        {
            Invalid(Networks, "Enter at least one network in CIDR format.");
            return;
        }

        var invalid = networks.Where(network => !_networkDetectionService.IsValidCidr(network)).ToArray();
        if (invalid.Length > 0)
        {
            Invalid(Networks, $"Invalid CIDR: {string.Join(", ", invalid)}. Example: 192.168.1.0/24.");
            return;
        }
        if (!int.TryParse(PollInterval.Text, out var seconds) || seconds is < 60 or > 86400)
        {
            Invalid(PollInterval, "Enter a poll interval from 60 to 86400 seconds.");
            return;
        }
        if (!decimal.TryParse(Threshold.Text, NumberStyles.Number | NumberStyles.AllowExponent,
                CultureInfo.CurrentCulture, out var threshold) || threshold is < 0 or > 100)
        {
            Invalid(Threshold, "Enter a compliance threshold from 0 to 100 percent.");
            return;
        }

        var settings = _settingsManager.CurrentSettings;
        if (!decimal.TryParse(MinimumHours.Text, out var hours) || hours is < 0.01m or > 24m)
        {
            if (TimeMode.SelectedIndex > 0)
            {
                Invalid(MinimumHours, "Enter minimum office hours from 0.01 to 24.");
                return;
            }
            hours = settings.MinimumOfficeHours;
        }

        var fileName = FileName.Text.Trim();
        if (Path.GetExtension(fileName).ToLowerInvariant() is not (".csv" or ".json"))
        {
            Invalid(FileName, "Enter an attendance file name ending in .csv or .json.");
            return;
        }

        try
        {
            var folder = string.IsNullOrWhiteSpace(DataFolder.Text) ? null : DataFolder.Text;
            _ = ResolveFilePath(folder, fileName);
            settings.Networks = [.. networks];
            settings.PollIntervalMs = seconds * 1000;
            settings.EnableBackgroundWorker = AutomaticTracking.IsChecked == true;
            settings.StartWithWindows = StartWithWindows.IsChecked == true;
            if (Threshold.Text != _savedDraft.Threshold)
                settings.ComplianceThreshold = (double)(threshold / 100);
            settings.OfficeTimeTrackingMode = (OfficeTimeTrackingMode)TimeMode.SelectedIndex;
            settings.MinimumOfficeHours = hours;
            settings.DataFilePath = folder;
            settings.DataFileName = fileName;

            _settingsManager.ApplySettings(settings);
        }
        catch (Exception ex)
        {
            SetFeedback($"Could not save settings: {ex.Message}", true);
            return;
        }

        _savedDraft = ReadDraft();
        Welcome.Visibility = Visibility.Collapsed;
        SetNetworkFeedback();
        SetFeedback("Settings saved and applied.");
    }

    private void CancelSettings(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

    internal void ClearFeedback()
    {
        SetNetworkFeedback();
        SetFeedback("");
    }

    internal void DiscardChanges()
    {
        LoadSettings(_settingsManager.CurrentSettings);
        _savedDraft = ReadDraft();
    }

    private void ResetSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsConfirmationDialog("Reset to default", "Reset all settings to their defaults?", "_Reset",
            details: "Click Save on the Settings tab to apply the defaults.")
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
        {
            LoadSettings(AppSettings.CreateDesktopDefaults());
            SetFeedback("Defaults loaded. Click Save to apply them.");
        }
    }

    private void NetworksChanged(object sender, TextChangedEventArgs e)
    {
        if (NetworkFeedback is not null)
            SetNetworkFeedback();
    }

    private void SetNetworkFeedback(string? message = null, bool error = false)
    {
        NetworkFeedback.Text = message ?? "One CIDR per line, for example 192.168.1.0/24.";
        NetworkFeedback.Foreground = error ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.DarkSlateGray;
        if (message is not null)
            UIElementAutomationPeer.CreatePeerForElement(NetworkFeedback)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void DetectNetworks(object sender, RoutedEventArgs e)
    {
        try
        {
            var detected = _networkDetectionService.DetectCurrentNetworks();
            var existing = ReadNetworks();
            var added = detected.Except(existing, StringComparer.OrdinalIgnoreCase).ToArray();
            if (added.Length == 0)
            {
                SetNetworkFeedback(detected.Count == 0 ? "No active network connections found." : "All detected networks are already listed.");
                return;
            }

            Networks.Text = string.Join(Environment.NewLine, existing.Concat(added));
            var count = added.Length == 1 ? "1 network" : $"{added.Length} networks";
            SetNetworkFeedback($"Added {count}. Click Save to apply.");
        }
        catch (Exception ex)
        {
            SetNetworkFeedback($"Could not detect networks: {ex.Message}", true);
        }
    }

    private void BrowseFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = SettingsManager.ResolveDataFolder(DataFolder.Text);
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select data file folder" };
            if (Directory.Exists(folder))
                dialog.InitialDirectory = Path.GetFullPath(folder);
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                DataFolder.Text = dialog.FolderName;
        }
        catch (Exception ex)
        {
            SetFeedback($"Could not browse for a folder: {ex.Message}", true);
        }
    }

    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var fileName = FileName.Text.Trim();
            var folder = fileName.Length == 0
                ? Path.GetFullPath(SettingsManager.ResolveDataFolder(DataFolder.Text))
                : Path.GetDirectoryName(ResolveFilePath(DataFolder.Text, fileName));
            if (!Directory.Exists(folder))
            {
                SetFeedback($"Folder could not be found: {folder}", true);
                return;
            }

            using var process = Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true, Verb = "open" });
        }
        catch (Exception ex)
        {
            SetFeedback($"Could not open the folder: {ex.Message}", true);
        }
    }

    private static string ResolveFilePath(string? folder, string fileName) =>
        Path.GetFullPath(Path.Combine(SettingsManager.ResolveDataFolder(folder), fileName));

    private void Invalid(System.Windows.Controls.Control field, string message)
    {
        SetFeedback(message, true);
        field.Focus();
        field.BringIntoView();
    }

    private void SetFeedback(string text, bool error = false)
    {
        Feedback.Text = text;
        Feedback.Foreground = error ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.DarkSlateGray;
        Feedback.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private Draft ReadDraft() => new(Networks.Text, PollInterval.Text, AutomaticTracking.IsChecked, StartWithWindows.IsChecked,
        Threshold.Text, TimeMode.SelectedIndex, MinimumHours.Text, DataFolder.Text, FileName.Text);

    private readonly record struct Draft(string Networks, string PollInterval, bool? AutomaticTracking, bool? StartWithWindows,
        string Threshold, int TimeMode, string MinimumHours, string DataFolder, string FileName);
}
