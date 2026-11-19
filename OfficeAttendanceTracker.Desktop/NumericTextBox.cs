using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>An editable decimal field with stepper buttons and arrow-key adjustment.</summary>
[TemplatePart(Name = IncreasePart, Type = typeof(RepeatButton))]
[TemplatePart(Name = DecreasePart, Type = typeof(RepeatButton))]
public sealed class NumericTextBox : TextBox
{
    private const string IncreasePart = "PART_IncreaseButton";
    private const string DecreasePart = "PART_DecreaseButton";
    private RepeatButton? _increaseButton;
    private RepeatButton? _decreaseButton;

    /// <summary>Gets or sets the lowest value allowed by the stepper.</summary>
    public decimal Minimum { get; set; }

    /// <summary>Gets or sets the highest value allowed by the stepper.</summary>
    public decimal Maximum { get; set; }

    /// <summary>Gets or sets the amount added or subtracted per step.</summary>
    public decimal Increment { get; set; }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        if (_increaseButton is not null)
            _increaseButton.Click -= Increase;
        if (_decreaseButton is not null)
            _decreaseButton.Click -= Decrease;

        base.OnApplyTemplate();
        _increaseButton = GetTemplateChild(IncreasePart) as RepeatButton;
        _decreaseButton = GetTemplateChild(DecreasePart) as RepeatButton;

        if (_increaseButton is not null)
            _increaseButton.Click += Increase;
        if (_decreaseButton is not null)
            _decreaseButton.Click += Decrease;
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Up or Key.Down)
        {
            Step(e.Key == Key.Up ? Increment : -Increment);
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }

    private void Increase(object sender, RoutedEventArgs e) => Step(Increment);

    private void Decrease(object sender, RoutedEventArgs e) => Step(-Increment);

    private void Step(decimal amount)
    {
        if (IsReadOnly || !decimal.TryParse(Text, out var value))
            return;

        var next = Math.Clamp(Math.Clamp(value, Minimum, Maximum) + amount, Minimum, Maximum);
        if (next == value)
            return;

        SetCurrentValue(TextProperty, next.ToString());
        CaretIndex = Text.Length;
    }
}
