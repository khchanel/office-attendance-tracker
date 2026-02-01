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
        private const string AppName = "Office Attendance Tracker";

        private readonly NotifyIcon _trayIcon;
        private readonly IHost _host;
        private readonly IAttendanceService _attendanceService;
        private readonly SettingsManager _settingsManager;
        private readonly System.Windows.Forms.Timer _updateTimer;

        public TrayApplicationContext(IHost host, IAttendanceService attendanceService, SettingsManager settingsManager)
        {
            _host = host;
            _attendanceService = attendanceService;
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

            // Update timer using PollIntervalMs from settings
            var pollIntervalMs = _settingsManager.CurrentSettings.PollIntervalMs;
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = pollIntervalMs;
            _updateTimer.Tick += (s, e) => UpdateAttendanceCount();
            
            // Subscribe to settings changes
            _settingsManager.SettingsChanged += OnSettingsChanged;
            
            _updateTimer.Start();

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

        /// <summary>
        /// Maps compliance status to color (UI concern)
        /// </summary>
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

        /// <summary>
        /// Formats compliance status as user-friendly text
        /// </summary>
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

        /// <summary>
        /// render an icon with the attendnace as text
        /// </summary>
        /// <param name="attendance">current month attendance</param>
        /// <returns></returns>
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

                var status = _attendanceService.GetComplianceStatus();
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
                // Reload from disk to get latest persisted data
                _attendanceService.Reload();
                
                // Immediately take attendance (don't wait for Worker's next cycle)
                var isInOffice = _attendanceService.TakeAttendance();
                
                var count = _attendanceService.GetCurrentMonthAttendance();
                var currentMonth = DateTime.Today.ToString("MMMM yyyy");

                // Update icon with the count number
                var oldIcon = _trayIcon.Icon;
                _trayIcon.Icon = CreateIconWithNumber(count);

                // Dispose old icon to free resources (but not if it's the system icon)
                if (oldIcon != null && oldIcon != SystemIcons.Application)
                {
                    oldIcon.Dispose();
                }

                // Build status messages in order: count → in office status → compliance status
                var complianceStatus = _attendanceService.GetComplianceStatus();
                var complianceText = GetComplianceStatusText(complianceStatus);
                var officeStatusText = isInOffice ? "[YES] Currently in office" : "[NO] Not in office";
                
                var message = $"Office Days for {currentMonth}: {count} days\n{officeStatusText}\n{complianceText}";
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
            var networkDetectionService = _host.Services.GetRequiredService<INetworkDetectionService>();
            using var settingsForm = new SettingsForm(_settingsManager, networkDetectionService);
            settingsForm.ShowDialog();
        }

        private void OnSettingsChanged(object? sender, AppSettings settings)
        {
            // Update timer interval if changed
            if (_updateTimer.Interval != settings.PollIntervalMs)
            {
                _updateTimer.Stop();
                _updateTimer.Interval = settings.PollIntervalMs;
                _updateTimer.Start();
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _updateTimer.Stop();
            _updateTimer.Dispose();

            // Unsubscribe from settings changes
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
