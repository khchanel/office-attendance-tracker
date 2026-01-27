using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OfficeAttendanceTracker.Service;

namespace OfficeAttendanceTracker.Desktop
{
    public class TrayApplicationContext : ApplicationContext
    {
        private const string AppName = "Office Attendance Tracker";

        private readonly NotifyIcon _trayIcon;
        private readonly IHost _host;
        private readonly IAttendanceService _attendanceService;
        private readonly System.Windows.Forms.Timer _updateTimer;

        public TrayApplicationContext(IHost host, IAttendanceService attendanceService, IConfiguration configuration)
        {
            _host = host;
            _attendanceService = attendanceService;

            _trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = AppName
            };

            _trayIcon.ContextMenuStrip.Items.Add("Refresh", null, OnRefresh);
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

            // Update timer using PollIntervalMs from configuration
            var pollIntervalMs = configuration.GetValue("PollIntervalMs", 60000); // Default 60 seconds
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = pollIntervalMs;
            _updateTimer.Tick += (s, e) => UpdateAttendanceCount();
            _updateTimer.Start();

            UpdateAttendanceCount();
        }

        private Icon CreateIconWithNumber(int number)
        {
            // Create a bitmap to draw the number
            int iconSize = 32;
            using (Bitmap bitmap = new Bitmap(iconSize, iconSize))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Enable anti-aliasing for smoother text
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                // Draw background (circle)
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(0, 120, 215))) // Blue background
                {
                    g.FillEllipse(bgBrush, 0, 0, iconSize - 1, iconSize - 1);
                }

                // Draw border
                using (Pen borderPen = new Pen(Color.White, 2))
                {
                    g.DrawEllipse(borderPen, 1, 1, iconSize - 3, iconSize - 3);
                }

                // Draw the number
                string text = number.ToString();

                // Adjust font size based on number of digits
                int fontSize = text.Length == 1 ? 18 : (text.Length == 2 ? 14 : 10);
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

        private void UpdateAttendanceCount()
        {
            try
            {
                _attendanceService.Reload();
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

                _trayIcon.Text = $"Office Days for {currentMonth} is: {count} days";
                _trayIcon.BalloonTipText = $"Current month office attendance: {count} days";
            }
            catch (Exception ex)
            {
                _trayIcon.Text = $"Error: {ex.Message}";
                _trayIcon.Icon = SystemIcons.Error;
            }
        }

        private void OnRefresh(object? sender, EventArgs e)
        {
            UpdateAttendanceCount();
            _trayIcon.ShowBalloonTip(2000, AppName, $"Refreshed: {_trayIcon.Text}", ToolTipIcon.Info);
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _updateTimer.Stop();
            _updateTimer.Dispose();

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
