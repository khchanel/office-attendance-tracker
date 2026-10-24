using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>Interactive monthly attendance report with day-off management.</summary>
public sealed class ReportsForm : Form
{
    private static readonly Color OfficeColor = Color.FromArgb(213, 239, 225);
    private static readonly Color AwayColor = Color.FromArgb(255, 231, 203);
    private static readonly Color OffColor = Color.FromArgb(221, 231, 252);
    private readonly IAttendanceServiceProvider _serviceProvider;
    private readonly IDateTimeProvider _clock;
    private readonly Font _calendarFont = new("Segoe UI", 9);
    private readonly ToolTip _toolTip = new();
    private readonly Button _dayOffButton = new() { Text = "Mark as day off", AutoSize = true, Height = 30, Enabled = false };
    private readonly DateTimePicker _month = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM yyyy", ShowUpDown = true, Width = 175 };
    private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 165 };
    private readonly TableLayoutPanel _calendar = new() { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 7, Padding = new Padding(6) };
    private readonly DataGridView _records = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Label _summary = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 10) };
    private readonly Label _target = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 6) };
    private readonly Label _detail = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
    private readonly TargetProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 18 };
    private MonthlyAttendanceReport? _report;
    private bool _loading;
    private bool _saving;

    /// <summary>Occurs after a day-off change has been saved.</summary>
    public event EventHandler? RecordsChanged;

    /// <summary>Creates a report using the active service and application clock.</summary>
    public ReportsForm(IAttendanceServiceProvider serviceProvider, IDateTimeProvider clock)
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
        Text = "Attendance report - Office Attendance Tracker";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 720);
        MinimumSize = new Size(950, 720);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(246, 248, 251);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(20) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 8; row++)
            layout.RowStyles.Add(new RowStyle(row == 6 ? SizeType.Percent : SizeType.AutoSize, row == 6 ? 100 : 0));
        layout.Controls.Add(new Label { Text = "Monthly attendance", AutoSize = true, Font = new Font(Font.FontFamily, 20, FontStyle.Bold) }, 0, 0);

        var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 4) };
        toolbar.Controls.Add(MakeButton("Previous", () => MoveMonth(-1)));
        _month.Value = new DateTime(clock.Today.Year, clock.Today.Month, 1);
        toolbar.Controls.Add(_month);
        toolbar.Controls.Add(MakeButton("Next", () => MoveMonth(1)));
        toolbar.Controls.Add(MakeButton("This month", () => _month.Value = new DateTime(_clock.Today.Year, _clock.Today.Month, 1)));
        toolbar.Controls.Add(MakeButton("Refresh", RefreshReport));
        toolbar.Controls.Add(_dayOffButton);
        _dayOffButton.Click += (_, _) => ToggleDayOff();
        layout.Controls.Add(toolbar, 0, 1);
        layout.Controls.Add(_summary, 0, 2);
        layout.Controls.Add(_target, 0, 3);
        layout.Controls.Add(_progress, 0, 4);
        layout.Controls.Add(new Label
        {
            Text = "Target uses your current settings. Weekday days off are excluded. Weekend office visits count, as in the tray total.",
            AutoSize = true, Padding = new Padding(0, 8, 0, 10), Dock = DockStyle.Fill
        }, 0, 5);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        var calendarGroup = new GroupBox { Text = "Calendar - select a day to review", Dock = DockStyle.Fill, Padding = new Padding(8) };
        calendarGroup.Controls.Add(_calendar);
        for (var column = 0; column < 7; column++)
            _calendar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
        _calendar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        for (var row = 1; row < 7; row++)
            _calendar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
        body.Controls.Add(calendarGroup, 0, 0);

        var recordsGroup = new GroupBox { Text = "Daily records", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var recordsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        recordsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        recordsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recordsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recordsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _filter.Items.AddRange(["All days", "Office", "Not in office", "Day off", "No record"]);
        _filter.SelectedIndex = 0;
        recordsLayout.Controls.Add(_filter, 0, 0);
        recordsLayout.Controls.Add(new Label { Text = "No record means no tracking data; it does not confirm absence.", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 6) }, 0, 1);
        _records.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 55 });
        _records.Columns.Add(new DataGridViewTextBoxColumn { Name = "Day", HeaderText = "Day", FillWeight = 45 });
        _records.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Record", FillWeight = 95 });
        foreach (DataGridViewColumn column in _records.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        recordsLayout.Controls.Add(_records, 0, 2);
        recordsGroup.Controls.Add(recordsLayout);
        body.Controls.Add(recordsGroup, 1, 0);
        layout.Controls.Add(body, 0, 6);
        layout.Controls.Add(_detail, 0, 7);
        Controls.Add(layout);

        _month.ValueChanged += (_, _) => RefreshReport();
        _filter.SelectedIndexChanged += (_, _) => PopulateRecords();
        _records.SelectionChanged += (_, _) => ShowSelectedDay();
        _records.CurrentCellChanged += (_, _) => ShowSelectedDay();
        Activated += (_, _) => RefreshReport();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _calendarFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30 };
        button.Click += (_, _) => action();
        return button;
    }

    private void MoveMonth(int offset)
    {
        var next = _month.Value.AddMonths(offset);
        if (next >= _month.MinDate && next <= _month.MaxDate)
            _month.Value = next;
    }

    /// <summary>Reloads stored data without taking attendance or writing records.</summary>
    public void RefreshReport()
    {
        if (_loading || _saving || IsDisposed)
            return;
        _loading = true;
        try
        {
            var service = _serviceProvider.Current;
            service.Reload();
            _report = service.GetMonthlyReport(_month.Value);
            var rate = _report.AttendanceRate is { } value ? value.ToString("P0") : "N/A";
            _summary.Text = $"{_report.OfficeDays} office days     |     {_report.BusinessDays} business days     |     {_report.DaysOff} days off     |     {rate} of month     |     {_report.UnrecordedBusinessDays} unrecorded weekdays to date";
            var needed = Math.Max(0, _report.RequiredOfficeDays - _report.OfficeDays);
            var missedTarget = _report.IsComplete && !_report.IsTargetMet;
            var status = missedTarget ? "Non-compliant" : _report.IsTargetMet ? "Target met" : "In progress";
            var outcome = missedTarget ? $"Month ended: {needed} {(needed == 1 ? "day" : "days")} short" :
                _report.IsTargetMet ? (_report.IsComplete ? "Month ended: compliant" : "Monthly target secured") : $"{needed} more needed";
            _target.BackColor = missedTarget ? Color.FromArgb(253, 228, 228) :
                _report.IsTargetMet ? OfficeColor : AwayColor;
            _target.ForeColor = missedTarget ? Color.FromArgb(160, 28, 38) :
                _report.IsTargetMet ? Color.FromArgb(25, 102, 63) : Color.FromArgb(130, 72, 12);
            _target.Text = $"{status} | Monthly target: {_report.OfficeDays} / {_report.RequiredOfficeDays} office days ({_report.Threshold:P0}) - {outcome}";
            _progress.ForeColor = _target.ForeColor;
            _progress.Value = _report.RequiredOfficeDays == 0 ? 1000 :
                (int)Math.Min(1000, 1000d * _report.OfficeDays / _report.RequiredOfficeDays);
            PopulateCalendar();
            PopulateRecords();
        }
        catch (Exception ex)
        {
            _report = null;
            ClearCalendar();
            _records.Rows.Clear();
            _summary.Text = "Report unavailable";
            _target.Text = "Use Refresh to try again.";
            _target.BackColor = SystemColors.Control;
            _target.ForeColor = SystemColors.ControlText;
            _progress.ForeColor = SystemColors.ControlDark;
            _progress.Value = 0;
            _detail.Text = $"Could not load attendance: {ex.Message}";
            _dayOffButton.Enabled = false;
        }
        finally
        {
            _loading = false;
        }
    }

    private string Status(DateTime date)
    {
        var record = _report?.Records.FirstOrDefault(record => record.Date.Date == date);
        if (record is not null)
            return record.IsDayOff ? "Day off" : record.IsOffice ? "Office" : "Not in office";
        return date > _clock.Today ? "Upcoming" : date.IsWeekday() ? "No record" : "Weekend";
    }

    private void ToggleDayOff()
    {
        if (_loading || _saving || _report == null || _records.CurrentRow?.Tag is not DateTime date)
            return;

        var isDayOff = _report.Records.FirstOrDefault(record => record.Date.Date == date)?.IsDayOff ?? false;
        _saving = true;
        _dayOffButton.Enabled = false;
        try
        {
            _serviceProvider.Current.SetDayOff(date, !isDayOff);
        }
        catch (Exception ex)
        {
            ShowSelectedDay();
            _detail.Text = $"Could not save the day-off change for {date:d}: {ex.Message}";
            return;
        }
        finally
        {
            _saving = false;
            _dayOffButton.Enabled = _report != null && _records.CurrentRow?.Tag is DateTime;
        }

        RefreshReport();
        if (_report != null)
            SelectDate(date);
        RecordsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Color StatusColor(string status) => status switch
    {
        "Office" => OfficeColor,
        "Not in office" => AwayColor,
        "Day off" => OffColor,
        "Weekend" or "Upcoming" => Color.FromArgb(237, 239, 243),
        _ => Color.White
    };

    private void ClearCalendar()
    {
        _toolTip.RemoveAll();
        while (_calendar.Controls.Count > 0)
        {
            var control = _calendar.Controls[0];
            _calendar.Controls.Remove(control);
            control.Dispose();
        }
    }

    private void PopulateCalendar()
    {
        ClearCalendar();
        if (_report is null)
            return;
        var names = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        for (var column = 0; column < 7; column++)
            _calendar.Controls.Add(new Label { Text = names[column], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, column, 0);
        var offset = ((int)_report.Month.DayOfWeek + 6) % 7;
        for (var day = 1; day <= DateTime.DaysInMonth(_report.Month.Year, _report.Month.Month); day++)
        {
            var date = _report.Month.AddDays(day - 1);
            var status = Status(date);
            var caption = status switch
            {
                "Not in office" => "Away",
                "Day off" => "Off",
                "Upcoming" => "Future",
                "Weekend" => "W/end",
                "No record" => "No data",
                _ => status
            };
            var button = new Button
            {
                Text = $"{day}{(date == _clock.Today ? " *" : "")}\n{caption}",
                Dock = DockStyle.Fill, Margin = new Padding(2), FlatStyle = FlatStyle.Flat,
                BackColor = StatusColor(status), UseVisualStyleBackColor = false, Tag = date,
                AccessibleName = $"{date:D}: {status}", Font = _calendarFont
            };
            _toolTip.SetToolTip(button, $"{date:D}: {status}");
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 216, 225);
            button.Click += (_, _) => SelectDate(date);
            var cell = offset + day - 1;
            _calendar.Controls.Add(button, cell % 7, cell / 7 + 1);
        }
    }

    private void PopulateRecords()
    {
        var selected = _records.CurrentRow?.Tag as DateTime?;
        _records.Rows.Clear();
        if (_report is null)
            return;
        for (var day = 1; day <= DateTime.DaysInMonth(_report.Month.Year, _report.Month.Month); day++)
        {
            var date = _report.Month.AddDays(day - 1);
            var status = Status(date);
            if (_filter.SelectedIndex > 0 && status != _filter.Text)
                continue;
            var index = _records.Rows.Add(date.ToString("dd MMM yyyy"), date.ToString("ddd"), status);
            _records.Rows[index].Tag = date;
            _records.Rows[index].DefaultCellStyle.BackColor = StatusColor(status);
        }
        if (selected.HasValue && _records.Rows.Cast<DataGridViewRow>().Any(row => Equals(row.Tag, selected.Value)))
            SelectDate(selected.Value);
        ShowSelectedDay();
    }

    private void SelectDate(DateTime date)
    {
        if (!_records.Rows.Cast<DataGridViewRow>().Any(row => Equals(row.Tag, date)))
            _filter.SelectedIndex = 0;
        foreach (DataGridViewRow row in _records.Rows)
        {
            if (!Equals(row.Tag, date))
                continue;
            _records.CurrentCell = row.Cells[0];
            _records.FirstDisplayedScrollingRowIndex = row.Index;
            break;
        }
        ShowSelectedDay();
    }

    private void ShowSelectedDay()
    {
        var date = _records.CurrentRow?.Tag as DateTime?;
        var isDayOff = date is { } selectedDate &&
            (_report?.Records.FirstOrDefault(record => record.Date.Date == selectedDate)?.IsDayOff ?? false);
        _dayOffButton.Enabled = _report != null && date.HasValue && !_saving;
        _dayOffButton.Text = isDayOff ? "Remove day off" : "Mark as day off";
        _toolTip.SetToolTip(_dayOffButton, date is { } actionDate
            ? $"{actionDate:D}. " + (actionDate.IsWeekday()
                ? "Weekday days off are excluded from the monthly target. Office attendance is preserved."
                : "Weekend days off do not reduce the monthly target. Office attendance is preserved.")
            : "Select a calendar day or daily record first.");
        _detail.Text = date is { } selected
            ? $"{selected:D}: {Status(selected)}.  * Today. Select a calendar day or filter the list to review records."
            : "No days match this filter. Select another filter to review records.";
        foreach (var button in _calendar.Controls.OfType<Button>())
        {
            var isSelected = Equals(button.Tag, date);
            button.FlatAppearance.BorderSize = isSelected ? 3 : 1;
            button.FlatAppearance.BorderColor = isSelected ? Color.FromArgb(38, 85, 160) : Color.FromArgb(210, 216, 225);
        }
    }

    // Native themed progress bars ignore ForeColor. Draw the fill so it matches the status banner.
    private sealed class TargetProgressBar : Control
    {
        private int _value;

        public TargetProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleRole = AccessibleRole.ProgressBar;
            AccessibleName = "Monthly target progress";
            BackColor = Color.FromArgb(228, 231, 236);
        }

        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Clamp(value, 0, 1000);
                AccessibleDescription = $"{_value / 10d:0.#}% of monthly target";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var brush = new SolidBrush(ForeColor);
            e.Graphics.FillRectangle(brush, 0, 0, ClientSize.Width * _value / 1000, ClientSize.Height);
            e.Graphics.DrawRectangle(SystemPens.ControlDark, 0, 0, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }
    }
}
