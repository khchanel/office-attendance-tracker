using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    public class TrayApplicationContext : ApplicationContext
    {
        internal const string AppName = "Office Attendance Tracker";

        private readonly NotifyIcon _trayIcon;
        private readonly IHost _host;
        private readonly IAttendanceServiceProvider _serviceProvider;
        private readonly SettingsManager _settingsManager;
        private readonly System.Windows.Forms.Timer _updateTimer;
        private int _lastCount = 0;
        private SettingsForm? _settingsForm;

        /// <summary>
        /// Gets the current AttendanceService instance (may be recreated on settings change)
        /// </summary>
        private IAttendanceService AttendanceService => _serviceProvider.Current;


        public TrayApplicationContext(IHost host, IAttendanceServiceProvider serviceProvider, SettingsManager settingsManager)
        {
            _host = host;
            _serviceProvider = serviceProvider;
            _settingsManager = settingsManager;

            _trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = AppName
            };

            _trayIcon.ContextMenuStrip.Items.Add("Refresh", null, OnRefresh);
            _trayIcon.ContextMenuStrip.Items.Add("Settings...", null, OnSettings);
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, OnExit);
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    OnRefresh(s, e);
                }
            };
            _trayIcon.ShowBalloonTip(2000, AppName, "Application started and running in the system tray.", ToolTipIcon.Info);

            // Timer for periodic attendance check and UI update
            var pollIntervalMs = _settingsManager.CurrentSettings.PollIntervalMs;
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = pollIntervalMs;
            _updateTimer.Tick += (s, e) => UpdateAttendanceCount();
            
            // Subscribe to service recreation and settings changes
            _serviceProvider.ServiceRecreated += OnServiceRecreated;
            _settingsManager.SettingsChanged += OnSettingsChanged;
            
            // Start timer if background worker is enabled
            if (_settingsManager.CurrentSettings.EnableBackgroundWorker)
            {
                _updateTimer.Start();
            }

            UpdateAttendanceCount();

            // Show settings on first run
            if (_settingsManager.IsFirstRun)
            {
                MessageBox.Show(
                    "Welcome to Office Attendance Tracker!\n\n" +
                    "Please configure your office network settings to begin tracking attendance.\n\n" +
                    "You can access settings anytime by right-clicking the tray icon.",
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                
                OnSettings(null, EventArgs.Empty);
            }
        }

        private void OnServiceRecreated(object? sender, IAttendanceService newService)
        {
            UpdateAttendanceCount(showBalloonTip: true);
        }

        private Color GetComplianceColor(ComplianceStatus status)
        {
            return status switch
            {
                ComplianceStatus.Secured => Color.FromArgb(34, 139, 34),   // Green
                ComplianceStatus.Compliant => Color.FromArgb(0, 120, 215), // Blue
                ComplianceStatus.Warning => Color.FromArgb(255, 140, 0),   // Orange
                ComplianceStatus.Critical => Color.FromArgb(220, 20, 60),  // Red
                _ => Color.Gray
            };
        }

        private static string GetComplianceStatusText(ComplianceStatus status)
        {
            return status switch
            {
                ComplianceStatus.Secured => "[Secured] - Target met",
                ComplianceStatus.Compliant => "[Compliant] - On track",
                ComplianceStatus.Warning => "[Warning] - Below target",
                ComplianceStatus.Critical => "[Critical] - Cannot meet target",
                _ => "Unknown"
            };
        }

        private Icon CreateIconWithNumber(int attendance)
        {
            // bitmap size and font scale
            // 32x32 scale 1.0x | 1 digit font 24 | 2 digit font 20
            // 48x48 scale 1.5x | 1 digit font 36 | 2 digit font 30
            // 64x64 scale 2.0x | 1 digit font 48 | 2 digit font 40
            int iconSize = 48;
            using (Bitmap bitmap = new Bitmap(iconSize, iconSize))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Enable anti-aliasing for smoother text
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                var status = AttendanceService.GetComplianceStatus();
                var backgroundColor = GetComplianceColor(status);

                using (SolidBrush bgBrush = new SolidBrush(backgroundColor))
                {
                    g.FillEllipse(bgBrush, 0, 0, iconSize - 1, iconSize - 1);
                }

                // Draw border
                using (Pen borderPen = new Pen(Color.White, 2))
                {
                    g.DrawEllipse(borderPen, 1, 1, iconSize - 3, iconSize - 3);  // 32x32
                    //g.DrawEllipse(borderPen, 2, 2, iconSize - 4, iconSize - 4); // 48x48 or 64x64
                }

                // Draw the number
                // Only need 1-digit (0-9) or 2-digit (10-31) - max is 31 days per month
                string text = attendance.ToString();
                int fontSize = text.Length == 1 ? 36 : 30;
                using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    // Center the text
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    g.DrawString(text, font, textBrush, iconSize / 2f, iconSize / 2f, sf);
                }

                // Convert bitmap to icon
                IntPtr hicon = bitmap.GetHicon();
                Icon icon = Icon.FromHandle(hicon);
                return icon;
            }
        }

        private void UpdateAttendanceCount(bool showBalloonTip = false)
        {
            try
            {
                // Get current AttendanceService instance
                var service = AttendanceService;
                
                // Reload from disk to get latest persisted data
                service.Reload();

                // Immediately take attendance
                bool isInOffice;
                if (_settingsManager.CurrentSettings.EnableBackgroundWorker)
                {
                    isInOffice = service.TakeAttendance();
                }
                else
                {
                    // If background worker is disabled, just check presence without recording
                    isInOffice = service.CheckAttendance();
                }

                var count = service.GetCurrentMonthAttendance();
                var currentMonth = DateTime.Today.ToString("MMMM yyyy");

                // Update icon with the count number
                if (count != _lastCount)
                {
                    var oldIcon = _trayIcon.Icon;
                    _trayIcon.Icon = CreateIconWithNumber(count);

                    // Dispose old icon to free resources (but not if it's the system icon)
                    if (oldIcon != null && oldIcon != SystemIcons.Application)
                    {
                        oldIcon.Dispose();
                    }

                    _lastCount = count;
                }

                // Build status messages
                var complianceStatus = service.GetComplianceStatus();
                var complianceText = GetComplianceStatusText(complianceStatus);
                var officeStatusText = isInOffice ? "[YES] Currently in office" : "[NO] Not in office";
                
                // Show warning if no networks configured
                string message;
                if (!service.IsReady)
                {
                    message = $"Office Days for {currentMonth}: {count} days\n[!] Not configured - Right-click and open Settings to configure networks then restart";
                }
                else
                {
                    message = $"Office Days for {currentMonth}: {count} days\n{officeStatusText}\n{complianceText}";
                }
                
                _trayIcon.Text = message;
                
                if (showBalloonTip)
                {
                    _trayIcon.ShowBalloonTip(3000, AppName, message, ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                _trayIcon.Text = $"Error: {ex.Message}";
                _trayIcon.Icon = SystemIcons.Error;
            }
        }

        private void OnRefresh(object? sender, EventArgs e)
        {
            UpdateAttendanceCount(showBalloonTip: true);
        }

        private void OnSettings(object? sender, EventArgs e)
        {
            // If settings form is already open, bring it to front
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.Activate();
                _settingsForm.BringToFront();
                return;
            }

            // Create new settings form
            var networkDetectionService = _host.Services.GetRequiredService<INetworkDetectionService>();
            _settingsForm = new SettingsForm(_settingsManager, networkDetectionService);
            
            // Clear reference when form is closed
            _settingsForm.FormClosed += (s, args) => _settingsForm = null;
            
            _settingsForm.ShowDialog();
        }

        private void OnSettingsChanged(object? sender, AppSettings settings)
        {
            // Update timer interval if changed
            if (_updateTimer.Interval != settings.PollIntervalMs)
            {
                bool wasRunning = _updateTimer.Enabled;
                _updateTimer.Stop();
                _updateTimer.Interval = settings.PollIntervalMs;
                if (wasRunning)
                {
                    _updateTimer.Start();
                }
            }
            
            // Enable/disable automatic tracking immediately
            if (settings.EnableBackgroundWorker && !_updateTimer.Enabled)
            {
                _updateTimer.Start();
            }
            else if (!settings.EnableBackgroundWorker && _updateTimer.Enabled)
            {
                _updateTimer.Stop();
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _updateTimer.Stop();
            _updateTimer.Dispose();

            // Unsubscribe from events
            _serviceProvider.ServiceRecreated -= OnServiceRecreated;
            _settingsManager.SettingsChanged -= OnSettingsChanged;

            // Dispose icon before disposing the NotifyIcon
            if (_trayIcon.Icon != null && _trayIcon.Icon != SystemIcons.Application)
            {
                _trayIcon.Icon.Dispose();
            }

            _trayIcon.Dispose();

            // Stop the host
            _host.StopAsync().Wait();
            Application.Exit();
        }
    }
}
