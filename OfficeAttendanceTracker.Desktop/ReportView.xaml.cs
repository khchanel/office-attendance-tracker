using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>Displays monthly attendance and edits the selected day.</summary>
public partial class ReportView : UserControl
{
    private const string AllDays = "All days";
    private const string BelowMinimum = "Below minimum";
    private static readonly string[] Filters = [AllDays, "Office", "Not in office", "Day off", "No record"];
    private static readonly DateTime FirstMonth = new(1753, 1, 1);
    private static readonly DateTime LastMonth = new(9998, 12, 1);
    private static readonly Brush OfficeColor = BrushFor(System.Drawing.Color.FromArgb(213, 239, 225));
    private static readonly Brush AwayColor = BrushFor(System.Drawing.Color.FromArgb(255, 231, 203));
    private static readonly Brush ShortColor = BrushFor(System.Drawing.Color.FromArgb(255, 236, 194));
    private static readonly Brush OffColor = BrushFor(System.Drawing.Color.FromArgb(221, 231, 252));
    private static readonly Brush InactiveColor = BrushFor(System.Drawing.Color.FromArgb(237, 239, 243));

    private readonly IAttendanceServiceProvider _serviceProvider;
    private readonly IDateTimeProvider _clock;
    private DateTime _month;
    private MonthlyAttendanceReport? _report;
    private CalendarDay[] _days = [];
    private bool _updating;
    private bool _saving;

    internal event EventHandler? RecordsChanged;

    internal ReportView(IAttendanceServiceProvider serviceProvider, IDateTimeProvider clock)
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
        InitializeComponent();
        MonthPicker.DisplayDateStart = FirstMonth;
        MonthPicker.DisplayDateEnd = LastMonth.AddMonths(1).AddDays(-1);
        SetMonth(clock.Today, clock.Today.Date);
    }

    private void PreviousMonth(object sender, RoutedEventArgs e) => SetMonth(_month.AddMonths(-1));

    private void NextMonth(object sender, RoutedEventArgs e) => SetMonth(_month.AddMonths(1));

    private void ThisMonth(object sender, RoutedEventArgs e) => SetMonth(_clock.Today, _clock.Today.Date);

    private void OpenMonthPicker(object sender, RoutedEventArgs e)
    {
        MonthPicker.DisplayDate = _month;
        MonthPicker.DisplayMode = CalendarMode.Year;
        MonthPopup.IsOpen = true;
    }

    private void MonthPickerOpened(object sender, EventArgs e) => MonthPicker.Focus();

    private void PickMonth(object sender, CalendarModeChangedEventArgs e)
    {
        if (MonthPopup.IsOpen && e.NewMode == CalendarMode.Month)
        {
            var date = MonthPicker.DisplayDate;
            CloseMonthPicker();
            SetMonth(date);
        }
    }

    private void MonthPickerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseMonthPicker();
            e.Handled = true;
        }
    }

    private void CloseMonthPicker()
    {
        MonthPopup.IsOpen = false;
        MonthButton.Focus();
    }


    private void SetMonth(DateTime date, DateTime? selected = null)
    {
        _month = new DateTime(date.Year, date.Month, 1);
        MonthHeading.Text = _month.ToString("MMMM yyyy");
        System.Windows.Automation.AutomationProperties.SetName(MonthButton, MonthHeading.Text);
        PreviousButton.IsEnabled = _month > FirstMonth;
        NextButton.IsEnabled = _month < LastMonth;

        RefreshReport();
        if (selected is { } day)
            SelectDate(day);
    }


    private void Refresh(object sender, RoutedEventArgs e) => RefreshReport();

    internal void RefreshReport()
    {
        if (_updating || _saving)
            return;

        _updating = true;
        try
        {
            var service = _serviceProvider.Current;
            service.Reload();
            _report = service.GetMonthlyReport(_month);
            var report = _report;
            var today = _clock.Today.Date;
            var showOfficeTime = report.TimeTrackingMode != OfficeTimeTrackingMode.Off;
            var missedTarget = report.IsComplete && !report.IsTargetMet;
            var needed = Math.Max(0, report.RequiredOfficeDays - report.OfficeDays);
            var status = report.IsTargetMet ? ComplianceStatus.Secured :
                missedTarget ? ComplianceStatus.Critical :
                report.Month.Year == today.Year && report.Month.Month == today.Month
                    ? service.GetComplianceStatus() : ComplianceStatus.Compliant;
            var dayLabel = needed == 1 ? "day" : "days";

            Outcome.Text = report.IsTargetMet ? "Monthly target met" :
                missedTarget ? $"Target missed: {needed} {dayLabel} short" : $"{needed} more office {dayLabel} needed";
            ApplyColors(ComplianceColors.For(status));
            TargetProgress.Value = report.RequiredOfficeDays == 0 ? 100 :
                Math.Min(100, 100d * report.OfficeDays / report.RequiredOfficeDays);
            OfficeDays.Text = report.OfficeDays.ToString();
            RequiredDays.Text = report.RequiredOfficeDays.ToString();
            BusinessDays.Text = report.BusinessDays.ToString();
            AttendanceRate.Text = report.AttendanceRate?.ToString("P2") ?? "N/A";
            ConfiguredTarget.Text = $"Required: {report.Threshold:P2}";
            ConfiguredTarget.ToolTip = showOfficeTime
                ? $"A tracked office day requires {OfficeTimeFormatter.Format(report.MinimumOfficeHours)}." : null;
            OfficeTime.Visibility = showOfficeTime ? Visibility.Visible : Visibility.Collapsed;

            var filters = showOfficeTime ? [.. Filters, BelowMinimum] : Filters;
            var filterChanged = Filter.Items.Count != filters.Length;
            if (filterChanged)
            {
                var filter = (string?)Filter.SelectedItem;
                Filter.ItemsSource = filters;
                Filter.SelectedItem = filters.Contains(filter) ? filter : AllDays;
            }

            var selected = (Records.SelectedItem as CalendarDay)?.Date;
            var days = Enumerable.Range(0, DateTime.DaysInMonth(_month.Year, _month.Month))
                .Select(offset => CreateDay(report, _month.AddDays(offset), today)).ToArray();
            if (!_days.Select(day => day.Description).SequenceEqual(days.Select(day => day.Description)) ||
                !_days.Select(day => day.Number).SequenceEqual(days.Select(day => day.Number)))
            {
                _days = days;
                var offset = ((int)_month.DayOfWeek + 6) % 7;
                Calendar.ItemsSource = Enumerable.Repeat(new CalendarDay(), offset).Concat(_days);
                PopulateRows(selected ?? today);
            }
            else if (filterChanged)
            {
                PopulateRows(selected ?? today);
            }

            UpdateSelection();
            ShowError("");
        }
        catch (Exception ex)
        {
            _report = null;
            _days = [];
            Calendar.ItemsSource = null;
            Records.ItemsSource = null;
            OfficeDays.Text = RequiredDays.Text = BusinessDays.Text = AttendanceRate.Text = "--";
            OfficeTime.Visibility = Visibility.Collapsed;
            ConfiguredTarget.Text = "";
            Outcome.Text = "Report unavailable. Refresh to retry.";
            TargetProgress.Value = 0;
            ApplyColors(ComplianceColors.Neutral);
            UpdateSelection();
            ShowError($"Could not load attendance: {ex.Message}");
        }
        finally
        {
            _updating = false;
        }
    }

    private void ApplyColors(ComplianceColors colors)
    {
        Outcome.Background = RateCard.Background = BrushFor(colors.Background);
        Outcome.Foreground = AttendanceRate.Foreground = BrushFor(colors.Foreground);
        TargetProgress.Foreground = BrushFor(colors.Accent);
    }

    private static CalendarDay CreateDay(MonthlyAttendanceReport report, DateTime date, DateTime today)
    {
        var record = report.Records.FirstOrDefault(record => record.Date.Date == date);
        var status = record is null ? date > today ? "Upcoming" : date.IsWeekday() ? "No record" : "Weekend" :
            record.IsDayOff ? "Day off" :
            report.CountsAsOfficeDay(record) ? "Office" : record.IsOffice ? BelowMinimum : "Not in office";
        var (caption, background) = status switch
        {
            "Office" => ("Office", OfficeColor),
            "Not in office" => ("WFH", AwayColor),
            BelowMinimum => ("Short", ShortColor),
            "Day off" => ("Day off", OffColor),
            "Weekend" => ("", InactiveColor),
            "Upcoming" => ("Future", InactiveColor),
            _ => ("No data", Brushes.White)
        };
        var hours = record is not null && report.GetOfficeHours(record) is { } value
            ? OfficeTimeFormatter.Format(value) : null;
        var isToday = date == today;
        return new CalendarDay
        {
            Date = date,
            Number = isToday ? $"{date.Day} *" : date.Day.ToString(),
            Caption = caption,
            Status = status,
            Hours = hours,
            Background = background,
            Description = $"{date:D}: {status}{(hours is null ? "" : $" ({hours})")}{(isToday ? " · Today" : "")}"
        };
    }

    private void FilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating)
            PopulateRows((Records.SelectedItem as CalendarDay)?.Date ?? _clock.Today.Date);
    }

    private void PopulateRows(DateTime? selected)
    {
        var filter = Filter.SelectedItem as string;
        var rows = filter is null or AllDays ? _days : _days.Where(day => day.Status == filter).ToArray();
        Records.ItemsSource = rows;
        var day = rows.FirstOrDefault(day => day.Date == selected) ??
                  rows.FirstOrDefault(day => day.Date == _clock.Today.Date);
        Records.SelectedItem = day;
        if (day is not null)
            RevealDay(day);
    }

    private void SelectCalendarDay(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CalendarDay { Date: { } date } })
            SelectDate(date);
    }

    private void SelectDate(DateTime date)
    {
        var day = _days.FirstOrDefault(day => day.Date == date);
        if (day is null)
            return;

        if (!Records.Items.Contains(day))
            Filter.SelectedItem = AllDays;
        Records.SelectedItem = day;
        RevealDay(day);
    }

    private void RevealDay(CalendarDay day)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (!IsVisible || !ReferenceEquals(Records.SelectedItem, day))
                return;

            Records.ScrollIntoView(day);
            if (Records.Template.FindName("DG_ScrollViewer", Records) is ScrollViewer scroll &&
                Records.Items.IndexOf(day) is var index && index >= 0)
            {
                scroll.ScrollToVerticalOffset(Math.Max(0, index - Math.Floor(scroll.ViewportHeight / 2)));
            }
        });
    }

    private void RecordChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating)
            UpdateSelection();
    }

    private void UpdateSelection()
    {
        var selected = Records.SelectedItem as CalendarDay;
        foreach (var day in _days)
            day.IsSelected = ReferenceEquals(day, selected);

        var date = selected?.Date;
        var record = _report?.Records.FirstOrDefault(record => record.Date.Date == date);
        DayOffButton.IsEnabled = _report is not null && date.HasValue && !_saving;
        OfficeButton.IsEnabled = DayOffButton.IsEnabled && (date <= _clock.Today.Date || record?.IsOffice == true);
        ClearButton.IsEnabled = DayOffButton.IsEnabled && record is not null;
        DayOffButton.Content = record?.IsDayOff == true ? "Remove day off" : "Set day off";
        OfficeButton.Content = record?.IsOffice == true ? "Remove office" : "Set in office";
        SelectedDetail.Text = date is { } dayDate
            ? $"{dayDate:ddd, dd MMM yyyy} | {selected!.Status}{(dayDate == _clock.Today.Date ? " | Today" : "")}"
            : _report is null ? "" : Records.Items.Count == 0 ? "No matching days" : "Select a day to edit its record.";
    }

    private void ToggleDayOff(object sender, RoutedEventArgs e) =>
        SaveDay((service, date, record) => service.SetDayOff(date, record?.IsDayOff != true));

    private void ToggleOffice(object sender, RoutedEventArgs e) =>
        SaveDay((service, date, record) => service.SetOfficeAttendance(date, record?.IsOffice != true));

    private void ClearDay(object sender, RoutedEventArgs e) =>
        SaveDay((service, date, _) => service.ClearDay(date));

    private void SaveDay(Action<IAttendanceService, DateTime, AttendanceRecord?> save)
    {
        if (_updating || _saving || _report is null || Records.SelectedItem is not CalendarDay { Date: { } date })
            return;

        _saving = true;
        UpdateSelection();
        try
        {
            var record = _report.Records.FirstOrDefault(record => record.Date.Date == date);
            save(_serviceProvider.Current, date, record);
        }
        catch (Exception ex)
        {
            ShowError($"Could not save the change for {date:d}: {ex.Message}");
            return;
        }
        finally
        {
            _saving = false;
            UpdateSelection();
        }

        RefreshReport();
        SelectDate(date);
        RecordsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowError(string message)
    {
        Error.Text = message;
        Error.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static Brush BrushFor(System.Drawing.Color color)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private sealed class CalendarDay : INotifyPropertyChanged
    {
        private bool _isSelected;

        public DateTime? Date { get; init; }
        public string? Number { get; init; }
        public string? Caption { get; init; }
        public string? Status { get; init; }
        public string? Hours { get; init; }
        public string? Description { get; init; }
        public Brush? Background { get; init; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
