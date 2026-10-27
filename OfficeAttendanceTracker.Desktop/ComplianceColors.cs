using OfficeAttendanceTracker.Core;

namespace OfficeAttendanceTracker.Desktop;

/// <summary>Shared status colours for tray icons and report controls.</summary>
internal readonly record struct ComplianceColors(Color Accent, Color Background, Color Foreground)
{
    private static readonly ComplianceColors Secured = new(
        Color.FromArgb(34, 139, 34), Color.FromArgb(213, 239, 225), Color.FromArgb(25, 102, 63));
    private static readonly ComplianceColors Compliant = new(
        Color.FromArgb(0, 120, 215), Color.FromArgb(229, 237, 251), Color.FromArgb(0, 120, 215));
    private static readonly ComplianceColors Warning = new(
        Color.FromArgb(255, 140, 0), Color.FromArgb(255, 231, 203), Color.FromArgb(130, 72, 12));
    private static readonly ComplianceColors Critical = new(
        Color.FromArgb(220, 20, 60), Color.FromArgb(253, 228, 228), Color.FromArgb(160, 28, 38));

    internal static readonly ComplianceColors Neutral = new(
        Color.Gray, SystemColors.Control, SystemColors.ControlText);

    internal static ComplianceColors For(ComplianceStatus status) => status switch
    {
        ComplianceStatus.Secured => Secured,
        ComplianceStatus.Compliant => Compliant,
        ComplianceStatus.Warning => Warning,
        ComplianceStatus.Critical => Critical,
        _ => Neutral
    };
}
