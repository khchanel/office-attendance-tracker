using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>Interactive monthly attendance report with attendance and day-off management.</summary>
public sealed class ReportsForm : Form
{
    private static readonly Color OfficeColor = Color.FromArgb(213, 239, 225);
    private static readonly Color AwayColor = Color.FromArgb(255, 231, 203);
    private static readonly Color OffColor = Color.FromArgb(221, 231, 252);
    private readonly IAttendanceServiceProvider _serviceProvider;
    private readonly IDateTimeProvider _clock;
    private readonly Font _calendarFont = new("Segoe UI", 9);
    private readonly Font _metricFont = new("Segoe UI", 24, FontStyle.Bold);
    private readonly ToolTip _toolTip = new();
    private readonly Button _dayOffButton = new() { Text = "Set day off", AutoSize = true, Height = 30, Enabled = false };
    private readonly Button _officeButton = new() { Text = "Set in office", AutoSize = true, Height = 30, Enabled = false };
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
    private readonly Label _officeDays = new();
    private readonly Label _requiredDays = new();
    private readonly Label _businessDays = new();
    private readonly Label _attendanceRate = new();
    private readonly TableLayoutPanel _attendanceCard;
    private readonly Label _configuredTarget = new() { AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Color.FromArgb(92, 104, 122) };
    private readonly Label _target = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 6) };
    private readonly Label _detail = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
    private readonly TargetProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 6, Margin = new Padding(3, 3, 3, 12) };
    private MonthlyAttendanceReport? _report;
    private bool _loading;
    private bool _saving;

    /// <summary>Occurs after an attendance or day-off change has been saved.</summary>
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

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(20) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 7; row++)
            layout.RowStyles.Add(new RowStyle(row == 5 ? SizeType.Percent : SizeType.AutoSize, row == 5 ? 100 : 0));
        var heading = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(new Label { Text = "Monthly attendance", AutoSize = true, Font = new Font(Font.FontFamily, 20, FontStyle.Bold) }, 0, 0);
        heading.Controls.Add(_configuredTarget, 1, 0);
        layout.Controls.Add(heading, 0, 0);

        var toolbar = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(0, 8, 0, 4)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var readActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        var editActions = new FlowLayoutPanel
        {
            AutoSize = true, Anchor = AnchorStyles.Right, WrapContents = false,
            Margin = new Padding(20, 0, 0, 0)
        };
        readActions.Controls.Add(MakeButton("Previous", () => MoveMonth(-1)));
        _month.Value = new DateTime(clock.Today.Year, clock.Today.Month, 1);
        readActions.Controls.Add(_month);
        readActions.Controls.Add(MakeButton("Next", () => MoveMonth(1)));
        readActions.Controls.Add(MakeButton("This month", () => _month.Value = new DateTime(_clock.Today.Year, _clock.Today.Month, 1)));
        readActions.Controls.Add(MakeButton("Refresh", RefreshReport));
        editActions.Controls.Add(_dayOffButton);
        _dayOffButton.Click += (_, _) => ToggleDayOff();
        editActions.Controls.Add(_officeButton);
        _officeButton.Click += (_, _) => ToggleOfficeAttendance();
        toolbar.Controls.Add(readActions, 0, 0);
        toolbar.Controls.Add(editActions, 1, 0);
        layout.Controls.Add(toolbar, 0, 1);
        var insights = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
            Margin = new Padding(0, 12, 0, 10)
        };
        for (var column = 0; column < 4; column++)
            insights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        insights.Controls.Add(CreateInsightCard("In office", _officeDays, Color.White), 0, 0);
        insights.Controls.Add(CreateInsightCard("Required days", _requiredDays, Color.White), 1, 0);
        insights.Controls.Add(CreateInsightCard("Business days", _businessDays, Color.White), 2, 0);
        _attendanceCard = CreateInsightCard("Attendance", _attendanceRate, Color.White);
        insights.Controls.Add(_attendanceCard, 3, 0);
        layout.Controls.Add(insights, 0, 2);
        layout.Controls.Add(_target, 0, 3);
        layout.Controls.Add(_progress, 0, 4);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        var calendarGroup = new GroupBox { Text = "Calendar", Dock = DockStyle.Fill, Padding = new Padding(8) };
        calendarGroup.Controls.Add(_calendar);
        for (var column = 0; column < 7; column++)
            _calendar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
        _calendar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        for (var row = 1; row < 7; row++)
            _calendar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
        body.Controls.Add(calendarGroup, 0, 0);

        var recordsGroup = new GroupBox { Text = "Daily records", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var recordsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        recordsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        recordsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recordsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _filter.Items.AddRange(["All days", "Office", "Not in office", "Day off", "No record"]);
        _filter.SelectedIndex = 0;
        recordsLayout.Controls.Add(_filter, 0, 0);
        _records.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 55 });
        _records.Columns.Add(new DataGridViewTextBoxColumn { Name = "Day", HeaderText = "Day", FillWeight = 45 });
        _records.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Record", FillWeight = 95 });
        foreach (DataGridViewColumn column in _records.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        recordsLayout.Controls.Add(_records, 0, 1);
        recordsGroup.Controls.Add(recordsLayout);
        body.Controls.Add(recordsGroup, 1, 0);
        layout.Controls.Add(body, 0, 5);
        layout.Controls.Add(_detail, 0, 6);
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
            _metricFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30 };
        button.Click += (_, _) => action();
        return button;
    }

    private TableLayoutPanel CreateInsightCard(string title, Label value, Color color)
    {
        var card = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = color, Padding = new Padding(14, 10, 14, 10), Margin = new Padding(4)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.Controls.Add(new Label
        {
            Text = title, AutoSize = true, Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(72, 84, 102)
        }, 0, 0);
        value.Text = "--";
        value.AutoSize = true;
        value.Dock = DockStyle.Fill;
        value.Font = _metricFont;
        value.ForeColor = Color.FromArgb(30, 47, 72);
        value.AccessibleName = title;
        card.Controls.Add(value, 0, 1);
        return card;
    }

    private void MoveMonth(int offset)
    {
        var next = _month.Value.AddMonths(offset);
        if (next >= _month.MinDate && next <= _month.MaxDate)
            _month.Value = next;
    }

    private void ApplyComplianceColors(ComplianceColors colors)
    {
        _target.BackColor = colors.Background;
        _target.ForeColor = colors.Foreground;
        _progress.ForeColor = colors.Accent;
        _attendanceCard.BackColor = colors.Background;
        _attendanceRate.ForeColor = colors.Foreground;
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
            var rate = _report.AttendanceRate is { } value ? value.ToString("P2") : "N/A";
            _officeDays.Text = _report.OfficeDays.ToString();
            _requiredDays.Text = _report.RequiredOfficeDays.ToString();
            _businessDays.Text = _report.BusinessDays.ToString();
            _attendanceRate.Text = rate;
            _configuredTarget.Text = $"Required: {_report.Threshold:P2}";
            var needed = Math.Max(0, _report.RequiredOfficeDays - _report.OfficeDays);
            var missedTarget = _report.IsComplete && !_report.IsTargetMet;
            var isCurrentMonth = _report.Month.Year == _clock.Today.Year && _report.Month.Month == _clock.Today.Month;
            var compliance = _report.IsTargetMet ? ComplianceStatus.Secured :
                missedTarget ? ComplianceStatus.Critical :
                isCurrentMonth ? service.GetComplianceStatus() : ComplianceStatus.Compliant;
            var outcome = missedTarget ? $"Target missed: {needed} {(needed == 1 ? "day" : "days")} short" :
                _report.IsTargetMet ? "Monthly target met" : $"{needed} more office {(needed == 1 ? "day" : "days")} needed";
            ApplyComplianceColors(ComplianceColors.For(compliance));
            _target.Text = outcome;
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
            foreach (var value in new[] { _officeDays, _requiredDays, _businessDays, _attendanceRate })
                value.Text = "--";
            _configuredTarget.Text = string.Empty;
            _target.Text = "Report unavailable. Refresh to retry.";
            ApplyComplianceColors(ComplianceColors.Neutral);
            _progress.Value = 0;
            _detail.Text = $"Could not load attendance: {ex.Message}";
            _dayOffButton.Enabled = false;
            _officeButton.Enabled = false;
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
        => SaveSelectedDate((service, date, record) => service.SetDayOff(date, !(record?.IsDayOff ?? false)), "day-off change");

    private void ToggleOfficeAttendance()
        => SaveSelectedDate((service, date, record) => service.SetOfficeAttendance(date, !(record?.IsOffice ?? false)), "office attendance");

    private void SaveSelectedDate(Action<IAttendanceService, DateTime, AttendanceRecord?> save, string description)
    {
        if (_loading || _saving || _report == null || _records.CurrentRow?.Tag is not DateTime date)
            return;

        var record = _report.Records.FirstOrDefault(record => record.Date.Date == date);
        _saving = true;
        _dayOffButton.Enabled = false;
        _officeButton.Enabled = false;
        try
        {
            save(_serviceProvider.Current, date, record);
        }
        catch (Exception ex)
        {
            _detail.Text = $"Could not save {description} for {date:d}: {ex.Message}";
            return;
        }
        finally
        {
            _saving = false;
            _dayOffButton.Enabled = _report != null && _records.CurrentRow?.Tag is DateTime;
            _officeButton.Enabled = _dayOffButton.Enabled && (date <= _clock.Today || record?.IsOffice == true);
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
        var record = date is { } selectedDate
            ? _report?.Records.FirstOrDefault(record => record.Date.Date == selectedDate) : null;
        var isDayOff = record?.IsDayOff ?? false;
        _dayOffButton.Enabled = _report != null && date.HasValue && !_saving;
        _dayOffButton.Text = isDayOff ? "Remove day off" : "Set day off";
        _officeButton.Enabled = _dayOffButton.Enabled && (date <= _clock.Today || record?.IsOffice == true);
        _officeButton.Text = record?.IsOffice == true ? "Remove office" : "Set in office";
        _toolTip.SetToolTip(_officeButton, date is { } officeDate
            ? $"{officeDate:D}. " + (officeDate > _clock.Today
                ? "Future visits cannot be marked as completed attendance. Existing future attendance can be removed."
                : isDayOff
                    ? "The day-off flag is preserved. Remove it to include this office attendance in totals."
                    : "Changes save immediately. Automatic tracking may mark today in office again if office presence is detected.")
            : "Select a calendar day or daily record first.");
        _toolTip.SetToolTip(_dayOffButton, date is { } actionDate
            ? $"{actionDate:D}. " + (actionDate.IsWeekday()
                ? "Weekday days off are excluded from the monthly target. Office attendance is preserved."
                : "Weekend days off do not reduce the monthly target. Office attendance is preserved.")
            : "Select a calendar day or daily record first.");
        _detail.Text = date is { } selected
            ? $"{selected:ddd, dd MMM yyyy} | {Status(selected)}{(selected == _clock.Today ? " | Today" : "")}"
            : "No matching days";
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
