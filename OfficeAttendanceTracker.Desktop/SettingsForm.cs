using System.Windows.Forms;

namespace OfficeAttendanceTracker.Desktop
{
    /// <summary>
    /// Settings configuration form
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly SettingsManager _settingsManager;
        private AppSettings _workingSettings;

        private TextBox _networksTextBox;
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

        public SettingsForm(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _workingSettings = _settingsManager.CurrentSettings;
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
                Text = "Office Networks (CIDR): *",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoSize = false,
                Height = 60
            };
            _networksTextBox = new TextBox
            {
                Multiline = true,
                Height = 60,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "Enter one CIDR per line, e.g.:\n10.8.1.0/24\n10.1.0.0/16"
            };
            toolTip.SetToolTip(_networksTextBox, "Enter your office network ranges in CIDR notation.\nOne network per line. Example: 10.8.1.0/24\n\n* Requires application restart");
            mainPanel.Controls.Add(networksLabel, 0, 0);
            mainPanel.Controls.Add(_networksTextBox, 1, 0);

            // Poll Interval
            var pollIntervalLabel = new Label
            {
                Text = "Poll Interval (seconds): *",
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
            toolTip.SetToolTip(_pollIntervalNumeric, "How often to check if you're on the office network.\nDefault: 1800 seconds (30 minutes)\n\n* Requires application restart");
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
            toolTip.SetToolTip(_enableBackgroundWorkerCheckBox, "Automatically track attendance in the background\n\n* Requires application restart");
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
            toolTip.SetToolTip(_complianceThresholdNumeric, "Attendance percentage threshold for compliance status.\nDefault: 50%");
            mainPanel.Controls.Add(complianceLabel, 0, 3);
            mainPanel.Controls.Add(_complianceThresholdNumeric, 1, 3);

            // Data File Path
            var dataFilePathLabel = new Label
            {
                Text = "Data File Path: *",
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
            toolTip.SetToolTip(_dataFilePathTextBox, "Custom path for attendance data file.\nLeave empty to use application directory\n\n* Requires application restart");
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
                Text = "Data File Name: *",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            _dataFileNameTextBox = new TextBox
            {
                Dock = DockStyle.Left,
                Width = 200
            };
            toolTip.SetToolTip(_dataFileNameTextBox, "Name of the attendance file.\nSupported formats: .csv or .json\n\n* Requires application restart");
            mainPanel.Controls.Add(dataFileNameLabel, 0, 5);
            mainPanel.Controls.Add(_dataFileNameTextBox, 1, 5);

            // Restart warning label - positioned right after settings that require restart
            _restartLabel = new Label
            {
                Text = "* Application restart required for these settings to take effect",
                ForeColor = System.Drawing.Color.DarkOrange,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 10),
                Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, 8.5f, System.Drawing.FontStyle.Italic)
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
            _pollIntervalNumeric.Value = _workingSettings.PollIntervalMs / 1000; // Convert to seconds
            _enableBackgroundWorkerCheckBox.Checked = _workingSettings.EnableBackgroundWorker;
            _complianceThresholdNumeric.Value = (decimal)(_workingSettings.ComplianceThreshold * 100); // Convert to percentage
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

                // Validate file name
                if (string.IsNullOrWhiteSpace(_dataFileNameTextBox.Text))
                {
                    MessageBox.Show("Please enter a data file name.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Update settings
                _workingSettings.Networks = networks;
                _workingSettings.PollIntervalMs = (int)_pollIntervalNumeric.Value * 1000; // Convert to ms
                _workingSettings.EnableBackgroundWorker = _enableBackgroundWorkerCheckBox.Checked;
                _workingSettings.ComplianceThreshold = (double)_complianceThresholdNumeric.Value / 100; // Convert to decimal
                _workingSettings.DataFilePath = string.IsNullOrWhiteSpace(_dataFilePathTextBox.Text)
                    ? null
                    : _dataFilePathTextBox.Text;
                _workingSettings.DataFileName = _dataFileNameTextBox.Text.Trim();

                // Save settings
                _settingsManager.SaveSettings(_workingSettings);

                MessageBox.Show("Settings saved successfully!\n\nPlease restart the application for all changes to take effect.",
                    "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
            var defaults = new AppSettings();
            
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
    }
}
