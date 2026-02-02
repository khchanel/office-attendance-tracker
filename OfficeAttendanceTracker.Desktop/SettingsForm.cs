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
        private readonly bool _originalWorkerEnabled;
        private AppSettings _workingSettings;

        private TextBox _networksTextBox;
        private Button _detectNetworkButton;
        private NumericUpDown _pollIntervalNumeric;
        private CheckBox _enableBackgroundWorkerCheckBox;
        private NumericUpDown _complianceThresholdNumeric;
        private TextBox _dataFilePathTextBox;
        private TextBox _dataFileNameTextBox;
        private Button _browseButton;
        private Button _saveButton;
        private Button _cancelButton;
        private Button _resetButton;
        private Label _restartLabel;

        public SettingsForm(SettingsManager settingsManager, INetworkDetectionService networkDetectionService)
        {
            _settingsManager = settingsManager;
            _networkDetectionService = networkDetectionService;
            _workingSettings = _settingsManager.CurrentSettings;
            _originalWorkerEnabled = _workingSettings.EnableBackgroundWorker;
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Text = "Office Attendance Tracker - Settings";
            this.Size = new System.Drawing.Size(600, 500);
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
                RowCount = 8,
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

            // Enable Background Worker
            var enableWorkerLabel = new Label
            {
                Text = "Enable Background Worker: *",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _enableBackgroundWorkerCheckBox = new CheckBox
            {
                Dock = DockStyle.Left,
                Width = 30
            };
            _enableBackgroundWorkerCheckBox.CheckedChanged += (s, e) => UpdateRestartWarning();
            toolTip.SetToolTip(_enableBackgroundWorkerCheckBox, "Automatically track attendance in the background\n\n* Restart required to enable/disable");
            mainPanel.Controls.Add(enableWorkerLabel, 0, 2);
            mainPanel.Controls.Add(_enableBackgroundWorkerCheckBox, 1, 2);

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
            mainPanel.Controls.Add(complianceLabel, 0, 3);
            mainPanel.Controls.Add(_complianceThresholdNumeric, 1, 3);

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
            mainPanel.Controls.Add(dataFilePathLabel, 0, 4);
            mainPanel.Controls.Add(pathPanel, 1, 4);

            // Data File Name
            var dataFileNameLabel = new Label
            {
                Text = "Data File Name:",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _dataFileNameTextBox = new TextBox
            {
                Dock = DockStyle.Left,
                Width = 200
            };
            toolTip.SetToolTip(_dataFileNameTextBox, "Name of the attendance file.\nSupported formats: .csv or .json\n\nApplied immediately after save.");
            mainPanel.Controls.Add(dataFileNameLabel, 0, 5);
            mainPanel.Controls.Add(_dataFileNameTextBox, 1, 5);

            // Restart warning label - shown only when Worker enable/disable changes
            _restartLabel = new Label
            {
                Text = "⚠ Application restart required to enable/disable Background Worker",
                ForeColor = System.Drawing.Color.DarkOrange,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 10),
                Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, 8.5f, System.Drawing.FontStyle.Bold),
                Visible = false
            };
            mainPanel.SetColumnSpan(_restartLabel, 2);
            mainPanel.Controls.Add(_restartLabel, 0, 6);

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
            mainPanel.Controls.Add(buttonPanel, 0, 7);

            this.Controls.Add(mainPanel);
            this.AcceptButton = _saveButton;
            this.CancelButton = _cancelButton;
        }

        private void LoadSettings()
        {
            _networksTextBox.Text = string.Join(Environment.NewLine, _workingSettings.Networks);
            _pollIntervalNumeric.Value = _workingSettings.PollIntervalMs / 1000;
            _enableBackgroundWorkerCheckBox.Checked = _workingSettings.EnableBackgroundWorker;
            _complianceThresholdNumeric.Value = (decimal)(_workingSettings.ComplianceThreshold * 100);
            _dataFilePathTextBox.Text = _workingSettings.DataFilePath ?? string.Empty;
            _dataFileNameTextBox.Text = _workingSettings.DataFileName;
            
            UpdateRestartWarning();
        }

        private void UpdateRestartWarning()
        {
            bool workerChanged = _enableBackgroundWorkerCheckBox.Checked != _originalWorkerEnabled;
            _restartLabel.Visible = workerChanged;
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select data file folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(_dataFilePathTextBox.Text) && Directory.Exists(_dataFilePathTextBox.Text))
            {
                folderDialog.SelectedPath = _dataFilePathTextBox.Text;
            }

            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                _dataFilePathTextBox.Text = folderDialog.SelectedPath;
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

                // Check if restart is required
                bool requiresRestart = _enableBackgroundWorkerCheckBox.Checked != _originalWorkerEnabled;

                // Update settings
                _workingSettings.Networks = networks;
                _workingSettings.PollIntervalMs = (int)_pollIntervalNumeric.Value * 1000;
                _workingSettings.EnableBackgroundWorker = _enableBackgroundWorkerCheckBox.Checked;
                _workingSettings.ComplianceThreshold = (double)_complianceThresholdNumeric.Value / 100;
                _workingSettings.DataFilePath = string.IsNullOrWhiteSpace(_dataFilePathTextBox.Text)
                    ? null
                    : _dataFilePathTextBox.Text;
                _workingSettings.DataFileName = _dataFileNameTextBox.Text.Trim();

                // Save settings
                _settingsManager.SaveSettings(_workingSettings);

                if (requiresRestart)
                {
                    MessageBox.Show(
                        "Settings saved successfully!\n\n" +
                        "⚠ Application restart required because:\n" +
                        "- Background Worker was enabled/disabled\n\n" +
                        "All other settings have been applied immediately.",
                        "Settings Saved - Restart Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(
                        "Settings saved and applied immediately!\n\n" +
                        "All changes are now active without requiring a restart.",
                        "Settings Saved",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

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
                $"- Compliance Threshold: {defaults.ComplianceThreshold * 100}%\n" +
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
                    "Settings Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        "No Networks Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        "Network Detection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Append new networks
                var allNetworks = existingNetworks.Concat(newNetworks).ToList();
                _networksTextBox.Text = string.Join(Environment.NewLine, allNetworks);

                MessageBox.Show($"Added {newNetworks.Count} network(s):\n\n{string.Join("\n", newNetworks)}\n\nRemember to click 'Save' to apply the changes.",
                    "Networks Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to detect networks: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
