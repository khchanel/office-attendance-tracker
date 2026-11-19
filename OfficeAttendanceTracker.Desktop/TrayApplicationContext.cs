using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop
{
    internal sealed class TrayApplicationContext : IDisposable
    {
        internal const string AppName = "Office Attendance Tracker";

        private readonly NotifyIcon _trayIcon;
        private readonly IServiceProvider _services;
        private readonly IAttendanceServiceProvider _serviceProvider;
        private readonly SettingsManager _settingsManager;
        private readonly DispatcherTimer _updateTimer;
        private readonly DispatcherTimer _trayClickTimer;
        private Icon? _attendanceIcon;
        private int _lastCount = -1;
        private ComplianceStatus? _lastComplianceStatus;
        private MainWindow? _mainWindow;
        private ReportView? _report;

        /// <summary>
        /// Gets the current AttendanceService instance (may be recreated on settings change)
        /// </summary>
        private IAttendanceService AttendanceService => _serviceProvider.Current;

        internal TrayApplicationContext(IServiceProvider services, SettingsManager settingsManager)
        {
            _services = services;
            _serviceProvider = services.GetRequiredService<IAttendanceServiceProvider>();
            _settingsManager = settingsManager;

            _trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = false,
                Text = AppName
            };

            _trayIcon.ContextMenuStrip.Items.Add("Refresh", null, OnRefresh);
            _trayIcon.ContextMenuStrip.Items.Add("Reports...", null, (_, _) => ShowWindow(settings: false));
            _trayIcon.ContextMenuStrip.Items.Add("Settings...", null, (_, _) => ShowWindow(settings: true));
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Exit());
            _trayClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SystemInformation.DoubleClickTime) };
            _trayClickTimer.Tick += (sender, args) =>
            {
                _trayClickTimer.Stop();
                OnRefresh(sender, args);
            };
            _trayIcon.MouseClick += (_, args) =>
            {
                // Give Windows time to recognize a double-click before refreshing.
                if (args.Button == MouseButtons.Left)
                {
                    _trayClickTimer.Stop();
                    _trayClickTimer.Start();
                }
            };
            _trayIcon.MouseDoubleClick += (_, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    _trayClickTimer.Stop();
                    ShowWindow(settings: false);
                }
            };
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_settingsManager.CurrentSettings.PollIntervalMs)
            };
            _updateTimer.Tick += (_, _) => UpdateAttendanceCount();
            _serviceProvider.ServiceRecreated += OnServiceRecreated;
            _settingsManager.SettingsChanged += OnSettingsChanged;
        }

        internal void Start()
        {
            _trayIcon.Visible = true;
            _trayIcon.ShowBalloonTip(2000, AppName, "Application started and running in the system tray.", ToolTipIcon.Info);
            // Start timer if background worker is enabled
            if (_settingsManager.CurrentSettings.EnableBackgroundWorker)
            {
                _updateTimer.Start();
            }

            UpdateAttendanceCount();

            if (_settingsManager.IsFirstRun)
                ShowWindow(settings: true);
        }

        private void OnServiceRecreated(object? sender, IAttendanceService newService)
        {
            UpdateAttendanceCount(showBalloonTip: true, recordAttendance: false);
            _report?.RefreshReport();
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

        private static Icon CreateIconWithNumber(int attendance, ComplianceStatus status)
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

                var backgroundColor = ComplianceColors.For(status).Accent;

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
                    using var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    g.DrawString(text, font, textBrush, iconSize / 2f, iconSize / 2f, sf);
                }

                // Convert bitmap to icon
                IntPtr hicon = bitmap.GetHicon();
                try
                {
                    using var icon = Icon.FromHandle(hicon);
                    return (Icon)icon.Clone();
                }
                finally
                {
                    DestroyIcon(hicon);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);

        private void UpdateAttendanceCount(bool showBalloonTip = false, bool recordAttendance = true)
        {
            try
            {
                // Get current AttendanceService instance
                var service = AttendanceService;
                
                // Reload from disk to get latest persisted data
                service.Reload();

                // Immediately take attendance
                bool isInOffice;
                if (recordAttendance && _settingsManager.CurrentSettings.EnableBackgroundWorker)
                {
                    isInOffice = service.TakeAttendance();
                }
                else
                {
                    // If background worker is disabled, just check presence without recording
                    isInOffice = service.CheckAttendance();
                }

                // Tray refreshes and scheduled polls update the open report after the check.
                if (recordAttendance)
                    _report?.RefreshReport();

                var count = service.GetCurrentMonthAttendance();
                var businessDays = service.GetBusinessDaysInCurrentMonth();
                var requiredDays = service.CalculateRequiredOfficeDays(businessDays);
                var attendancePercentage = businessDays == 0 ? "N/A" : (1.0 * count / businessDays).ToString("P2");
                var currentMonth = DateTime.Today.ToString("MMMM yyyy");

                // Update icon with the count number
                var complianceStatus = service.GetComplianceStatus();
                if (count != _lastCount || complianceStatus != _lastComplianceStatus)
                {
                    var icon = CreateIconWithNumber(count, complianceStatus);
                    _trayIcon.Icon = icon;
                    _attendanceIcon?.Dispose();
                    _attendanceIcon = icon;

                    _lastCount = count;
                    _lastComplianceStatus = complianceStatus;
                }

                // Build status messages
                var complianceText = GetComplianceStatusText(complianceStatus);
                var officeStatusText = isInOffice ? "[YES] Currently in office" : "[NO] Not in office";
                var attendanceText = $"Office days ({currentMonth}): {count}/{requiredDays} ({attendancePercentage})";
                
                // Show warning if no networks configured
                string message;
                if (!service.IsReady)
                {
                    message = $"{attendanceText}\n[!] Configure networks in Settings";
                }
                else
                {
                    message = $"{attendanceText}\n{officeStatusText}\n{complianceText}";
                }
                
                _trayIcon.Text = message.Length <= 127 ? message : message[..127];
                
                if (showBalloonTip)
                {
                    _trayIcon.ShowBalloonTip(3000, AppName, message, ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                _trayIcon.Text = "Attendance check failed";
                Console.Error.WriteLine(ex);
                _lastCount = -1;
                _trayIcon.Icon = SystemIcons.Error;
            }
        }

        private void OnRefresh(object? sender, EventArgs e)
        {
            UpdateAttendanceCount(showBalloonTip: true);
        }

        private void ShowWindow(bool settings)
        {
            if (_mainWindow is null)
            {
                var report = new ReportView(_serviceProvider, _services.GetRequiredService<IDateTimeProvider>());
                var settingsView = new SettingsView(_settingsManager, _services.GetRequiredService<INetworkDetectionService>());
                var window = new MainWindow(report, settingsView);
                report.RecordsChanged += (_, _) => UpdateAttendanceCount(recordAttendance: false);
                window.Closed += (_, _) =>
                {
                    _mainWindow = null;
                    _report = null;
                };

                _mainWindow = window;
                _report = report;
                window.SelectPage(settings);
                window.Show();
            }
            else
            {
                _mainWindow.SelectPage(settings);
            }

            if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
                _mainWindow.WindowState = System.Windows.WindowState.Normal;
            _mainWindow.Activate();
        }

        private void OnSettingsChanged(object? sender, AppSettings settings)
        {
            // Update timer interval if changed
            if (_updateTimer.Interval.TotalMilliseconds != settings.PollIntervalMs)
            {
                bool wasRunning = _updateTimer.IsEnabled;
                _updateTimer.Stop();
                _updateTimer.Interval = TimeSpan.FromMilliseconds(settings.PollIntervalMs);
                if (wasRunning)
                {
                    _updateTimer.Start();
                }
            }
            
            // Enable/disable automatic tracking immediately
            if (settings.EnableBackgroundWorker && !_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
            else if (!settings.EnableBackgroundWorker && _updateTimer.IsEnabled)
            {
                _updateTimer.Stop();
            }
        }

        private void Exit()
        {
            if (_mainWindow?.HasOpenDialog == true)
                return;

            _mainWindow?.Close();
            if (_mainWindow is null)
                System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            _mainWindow?.Close();
            _trayClickTimer.Stop();
            _updateTimer.Stop();
            _serviceProvider.ServiceRecreated -= OnServiceRecreated;
            _settingsManager.SettingsChanged -= OnSettingsChanged;
            _trayIcon.Visible = false;
            _trayIcon.ContextMenuStrip?.Dispose();
            _trayIcon.Dispose();
            _attendanceIcon?.Dispose();
        }
    }
}
