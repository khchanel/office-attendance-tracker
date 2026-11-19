using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Settings configuration form
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly SettingsManager _settingsManager;
        private readonly INetworkDetectionService _networkDetectionService;
        private AppSettings _workingSettings;

        private TextBox _networksTextBox = null!;
        private Button _detectNetworkButton = null!;
        private NumericUpDown _pollIntervalNumeric = null!;
        private CheckBox _enableBackgroundWorkerCheckBox = null!;
        private CheckBox _startWithWindowsCheckBox = null!;
        private NumericUpDown _complianceThresholdNumeric = null!;
        private ComboBox _officeTimeModeComboBox = null!;
        private NumericUpDown _minimumOfficeHoursNumeric = null!;
        private TextBox _dataFilePathTextBox = null!;
        private TextBox _dataFileNameTextBox = null!;
        private Button _browseButton = null!;
        private Button _saveButton = null!;
        private Button _cancelButton = null!;
        private Button _resetButton = null!;

        public SettingsForm(SettingsManager settingsManager, INetworkDetectionService networkDetectionService)
        {
            _settingsManager = settingsManager;
            _networkDetectionService = networkDetectionService;
            _workingSettings = _settingsManager.CurrentSettings;
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Text = "Office Attendance Tracker - Settings";
            this.Size = new System.Drawing.Size(600, 550);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create tooltip for help text
            var toolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 500,
                ShowAlways = true
            };

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15),
                ColumnCount = 2,
                RowCount = 10,
                AutoSize = true
            };

            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Networks
            var networksLabel = new Label
            {
                Text = "Office Networks (CIDR):",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoSize = false,
                Height = 60
            };
            
            var networksPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 60
            };
            
            _networksTextBox = new TextBox
            {
                Multiline = true,
                Height = 60,
                Width = 250,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "Enter one CIDR per line, e.g.:\n10.8.1.0/24\n10.1.0.0/16",
                Location = new System.Drawing.Point(0, 0),
                AcceptsReturn = true
            };
            toolTip.SetToolTip(_networksTextBox, "Enter your office network ranges in CIDR notation.\nOne network per line. Example: 10.8.1.0/24\n\nApplied immediately after save.");
            
            _detectNetworkButton = new Button
            {
                Text = "Detect Current",
                Width = 110,
                Height = 25,
                Location = new System.Drawing.Point(255, 2)
            };
            _detectNetworkButton.Click += DetectNetworkButton_Click;
            toolTip.SetToolTip(_detectNetworkButton, "Automatically detect and add current network configurations");
            
            networksPanel.Controls.Add(_networksTextBox);
            networksPanel.Controls.Add(_detectNetworkButton);
            
            mainPanel.Controls.Add(networksLabel, 0, 0);
            mainPanel.Controls.Add(networksPanel, 1, 0);

            // Poll Interval
            var pollIntervalLabel = new Label
            {
                Text = "Poll Interval (seconds):",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _pollIntervalNumeric = new NumericUpDown
            {
                Minimum = 60,
                Maximum = 86400,
                Increment = 60,
                Dock = DockStyle.Left,
                Width = 100
            };
            toolTip.SetToolTip(_pollIntervalNumeric, "How often to check if you're on the office network.\nDefault: 1800 seconds (30 minutes)\n\nApplied immediately after save.");
            mainPanel.Controls.Add(pollIntervalLabel, 0, 1);
            mainPanel.Controls.Add(_pollIntervalNumeric, 1, 1);

            // Enable Automatic Tracking
            var enableWorkerLabel = new Label
            {
                Text = "Enable Automatic Tracking:",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _enableBackgroundWorkerCheckBox = new CheckBox
            {
                Dock = DockStyle.Left,
                Width = 30
            };
            toolTip.SetToolTip(_enableBackgroundWorkerCheckBox, "Automatically track attendance in the background\n\nApplied immediately after save.");
            mainPanel.Controls.Add(enableWorkerLabel, 0, 2);
            mainPanel.Controls.Add(_enableBackgroundWorkerCheckBox, 1, 2);

            // Start with Windows
            var startWithWindowsLabel = new Label
            {
                Text = "Start with Windows:",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _startWithWindowsCheckBox = new CheckBox
            {
                Dock = DockStyle.Left,
                Width = 30
            };
            toolTip.SetToolTip(_startWithWindowsCheckBox, "Launch Office Attendance Tracker automatically when you log in to Windows\n\nApplied immediately after save.");
            mainPanel.Controls.Add(startWithWindowsLabel, 0, 3);
            mainPanel.Controls.Add(_startWithWindowsCheckBox, 1, 3);

            // Compliance Threshold
            var complianceLabel = new Label
            {
                Text = "Compliance Threshold (%):",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _complianceThresholdNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 100,
                Increment = 5,
                DecimalPlaces = 0,
                Dock = DockStyle.Left,
                Width = 100
            };
            toolTip.SetToolTip(_complianceThresholdNumeric, "Attendance percentage threshold for compliance status.\nDefault: 50%\n\nApplied immediately after save.");
            mainPanel.Controls.Add(complianceLabel, 0, 4);
            mainPanel.Controls.Add(_complianceThresholdNumeric, 1, 4);

            var officeTimeModeLabel = new Label
            {
                Text = "Office time tracking:",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _officeTimeModeComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Left,
                Width = 250
            };
            _officeTimeModeComboBox.Items.AddRange(["Off", "First to last detection", "Accumulated office time"]);
            toolTip.SetToolTip(_officeTimeModeComboBox,
                "First to last includes gaps between visits. Accumulated time credits consecutive office polls.");
            mainPanel.Controls.Add(officeTimeModeLabel, 0, 5);
            mainPanel.Controls.Add(_officeTimeModeComboBox, 1, 5);

            var minimumOfficeHoursLabel = new Label
            {
                Text = "Minimum office hours:",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _minimumOfficeHoursNumeric = new NumericUpDown
            {
                Minimum = 0.01m,
                Maximum = 24,
                Increment = 0.25m,
                DecimalPlaces = 2,
                Dock = DockStyle.Left,
                Width = 100
            };
            _officeTimeModeComboBox.SelectedIndexChanged += (_, _) =>
                minimumOfficeHoursLabel.Visible = _minimumOfficeHoursNumeric.Visible =
                    _officeTimeModeComboBox.SelectedIndex > 0;
            toolTip.SetToolTip(_minimumOfficeHoursNumeric,
                "Minimum hours for an office day. The current rule also applies to past tracked days.");
            mainPanel.Controls.Add(minimumOfficeHoursLabel, 0, 6);
            mainPanel.Controls.Add(_minimumOfficeHoursNumeric, 1, 6);

            // Data File Path
            var dataFilePathLabel = new Label
            {
                Text = "Data File Path:",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            var pathPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0)
            };
            _dataFilePathTextBox = new TextBox
            {
                Width = 250,
                PlaceholderText = "Leave empty for default"
            };
            toolTip.SetToolTip(_dataFilePathTextBox, "Custom path for attendance data file.\nLeave empty to use user profile directory (%USERPROFILE%)\n\nApplied immediately after save.");
            _browseButton = new Button
            {
                Text = "Browse...",
                Width = 80,
                Margin = new Padding(5, 0, 0, 0)
            };
            _browseButton.Click += BrowseButton_Click;
            pathPanel.Controls.Add(_dataFilePathTextBox);
            pathPanel.Controls.Add(_browseButton);
            mainPanel.Controls.Add(dataFilePathLabel, 0, 7);
            mainPanel.Controls.Add(pathPanel, 1, 7);

            // Data File Name
            var dataFileNameLabel = new Label
            {
                Text = "Data File Name:",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _dataFileNameTextBox = new TextBox
            {
                Width = 200
            };
            var fileNamePanel = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = Padding.Empty
            };
            var openFolderButton = new Button
            {
                Text = "Open Folder",
                AutoSize = true,
                Margin = new Padding(5, 0, 0, 0)
            };
            openFolderButton.Click += OpenFolderButton_Click;
            toolTip.SetToolTip(openFolderButton, "Open the attendance file's folder in File Explorer.");
            fileNamePanel.Controls.Add(_dataFileNameTextBox);
            fileNamePanel.Controls.Add(openFolderButton);
            toolTip.SetToolTip(_dataFileNameTextBox, "Name of the attendance file.\nSupported formats: .csv or .json\n\nApplied immediately after save.");
            mainPanel.Controls.Add(dataFileNameLabel, 0, 8);
            mainPanel.Controls.Add(fileNamePanel, 1, 8);

            // Buttons
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0),
                Height = 40
            };

            // Reset button on the left
            _resetButton = new Button
            {
                Text = "Reset to Default",
                Width = 130,
                Height = 30,
                Location = new System.Drawing.Point(0, 10)
            };
            _resetButton.Click += ResetButton_Click;
            buttonPanel.Controls.Add(_resetButton);

            // Save/Cancel buttons on the right
            var rightButtonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Padding = new Padding(0)
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(0, 0, 0, 0)
            };
            _cancelButton.Click += CancelButton_Click;

            _saveButton = new Button
            {
                Text = "Save",
                Width = 100,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0)
            };
            _saveButton.Click += SaveButton_Click;

            rightButtonPanel.Controls.Add(_cancelButton);
            rightButtonPanel.Controls.Add(_saveButton);
            buttonPanel.Controls.Add(rightButtonPanel);

            mainPanel.SetColumnSpan(buttonPanel, 2);
            mainPanel.Controls.Add(buttonPanel, 0, 9);

            this.Controls.Add(mainPanel);
            this.AcceptButton = _saveButton;
            this.CancelButton = _cancelButton;
        }

        private void LoadSettings()
        {
            _networksTextBox.Text = string.Join(Environment.NewLine, _workingSettings.Networks);
            _pollIntervalNumeric.Value = _workingSettings.PollIntervalMs / 1000;
            _enableBackgroundWorkerCheckBox.Checked = _workingSettings.EnableBackgroundWorker;
            _startWithWindowsCheckBox.Checked = _workingSettings.StartWithWindows;
            _complianceThresholdNumeric.Value = (decimal)(_workingSettings.ComplianceThreshold * 100);
            _officeTimeModeComboBox.SelectedIndex = Enum.IsDefined(_workingSettings.OfficeTimeTrackingMode)
                ? (int)_workingSettings.OfficeTimeTrackingMode : 0;
            _minimumOfficeHoursNumeric.Value = Math.Clamp(_workingSettings.MinimumOfficeHours, 0.01m, 24m);
            _dataFilePathTextBox.Text = _workingSettings.DataFilePath ?? string.Empty;
            _dataFileNameTextBox.Text = _workingSettings.DataFileName;
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select data file folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            var folder = SettingsManager.ResolveDataFolder(_dataFilePathTextBox.Text);
            if (Directory.Exists(folder))
            {
                folderDialog.SelectedPath = folder;
            }

            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                _dataFilePathTextBox.Text = folderDialog.SelectedPath;
            }
        }

        private void OpenFolderButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var folder = SettingsManager.ResolveDataFolder(_dataFilePathTextBox.Text);
                var fileName = _dataFileNameTextBox.Text.Trim();
                var fileFolder = fileName.Length == 0
                    ? Path.GetFullPath(folder)
                    : Path.GetDirectoryName(Path.GetFullPath(Path.Combine(folder, fileName)));
                if (fileFolder == null || !Directory.Exists(fileFolder))
                {
                    MessageBox.Show(this, $"The folder could not be found:\n{fileFolder}",
                        "Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var process = Process.Start(new ProcessStartInfo(fileFolder) { UseShellExecute = true, Verb = "open" });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open the folder: {ex.Message}",
                    "Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Parse networks
                var networks = _networksTextBox.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                if (networks.Count == 0)
                {
                    MessageBox.Show("Please enter at least one network in CIDR format.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validate CIDR format for each network
                var invalidNetworks = new List<string>();
                foreach (var network in networks)
                {
                    if (!_networkDetectionService.IsValidCidr(network))
                    {
                        invalidNetworks.Add(network);
                    }
                }

                if (invalidNetworks.Count > 0)
                {
                    var message = "The following network(s) are not in valid CIDR format:\n\n" +
                                  string.Join("\n", invalidNetworks) +
                                  "\n\nExpected format: X.X.X.X/Y (e.g., 192.168.1.0/24)";
                    MessageBox.Show(message, "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validate file name
                if (string.IsNullOrWhiteSpace(_dataFileNameTextBox.Text))
                {
                    MessageBox.Show("Please enter a data file name.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Update settings
                _workingSettings.Networks = networks;
                _workingSettings.PollIntervalMs = (int)_pollIntervalNumeric.Value * 1000;
                _workingSettings.EnableBackgroundWorker = _enableBackgroundWorkerCheckBox.Checked;
                _workingSettings.StartWithWindows = _startWithWindowsCheckBox.Checked;
                _workingSettings.ComplianceThreshold = (double)_complianceThresholdNumeric.Value / 100;
                _workingSettings.OfficeTimeTrackingMode = (OfficeTimeTrackingMode)_officeTimeModeComboBox.SelectedIndex;
                _workingSettings.MinimumOfficeHours = _minimumOfficeHoursNumeric.Value;
                _workingSettings.DataFilePath = string.IsNullOrWhiteSpace(_dataFilePathTextBox.Text)
                    ? null
                    : _dataFilePathTextBox.Text;
                _workingSettings.DataFileName = _dataFileNameTextBox.Text.Trim();

                // Apply Windows startup setting
                _settingsManager.SetStartupEnabled(_workingSettings.StartWithWindows);

                // Save settings
                _settingsManager.SaveSettings(_workingSettings);

                MessageBox.Show(
                    "Settings saved and applied immediately!\n\n",
                    "Settings Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.None);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            var defaults = AppSettings.CreateDesktopDefaults();
            
            var message = "Are you sure you want to reset all settings to their default values?\n\nThis will restore:\n" +
                $"- Office Networks: {string.Join(", ", defaults.Networks)}\n" +
                $"- Poll Interval: {defaults.PollIntervalMs / 1000} seconds ({defaults.PollIntervalMs / 60000} minutes)\n" +
                $"- Background Worker: {(defaults.EnableBackgroundWorker ? "Enabled" : "Disabled")}\n" +
                $"- Start with Windows: {(defaults.StartWithWindows ? "Enabled" : "Disabled")}\n" +
                $"- Compliance Threshold: {defaults.ComplianceThreshold * 100}%\n" +
                $"- Office Time Tracking: {defaults.OfficeTimeTrackingMode}\n" +
                $"- Data File Name: {defaults.DataFileName}\n" +
                $"- Data File Path: {(string.IsNullOrEmpty(defaults.DataFilePath) ? "(default)" : defaults.DataFilePath)}";

            var result = MessageBox.Show(
                message,
                "Reset to Default",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _workingSettings = defaults;
                LoadSettings();
                MessageBox.Show("Settings have been reset to default values.\n\nClick 'Save' to apply these changes.",
                    "Settings Reset", MessageBoxButtons.OK, MessageBoxIcon.None);
            }
        }

        private void DetectNetworkButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var detectedNetworks = _networkDetectionService.DetectCurrentNetworks();

                if (detectedNetworks.Count == 0)
                {
                    MessageBox.Show("No active network connections found.\n\nPlease ensure you are connected to a network.",
                        "No Networks Detected", MessageBoxButtons.OK, MessageBoxIcon.None);
                    return;
                }

                // Get existing networks
                var existingNetworks = _networksTextBox.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Add detected networks that don't already exist
                var newNetworks = detectedNetworks.Where(n => !existingNetworks.Contains(n)).ToList();

                if (newNetworks.Count == 0)
                {
                    MessageBox.Show("All detected networks are already in the list.",
                        "Network Detection", MessageBoxButtons.OK, MessageBoxIcon.None);
                    return;
                }

                // Append new networks
                var allNetworks = existingNetworks.Concat(newNetworks).ToList();
                _networksTextBox.Text = string.Join(Environment.NewLine, allNetworks);

                MessageBox.Show($"Added {newNetworks.Count} network(s):\n\n{string.Join("\n", newNetworks)}\n\nRemember to click 'Save' to apply the changes.",
                    "Networks Detected", MessageBoxButtons.OK, MessageBoxIcon.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to detect networks: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
