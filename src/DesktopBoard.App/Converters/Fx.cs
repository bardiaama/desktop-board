using DesktopBoard.App.Helpers;
using Microsoft.UI;
using Microsoft.UI.Input;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DesktopBoard.App.Converters;

/// <summary>
/// Static functions for x:Bind function bindings. Cheaper than IValueConverter (compiled,
/// no boxing) and easier to read in XAML: {x:Bind fx:Fx.Vis(IsEditMode), Mode=OneWay}.
/// </summary>
public static class Fx
{
    private static readonly Dictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    public static Visibility Vis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisNot(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisIfText(string? value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisIfNull(object? value) => value is null ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisAnd(bool a, bool b) => a && b ? Visibility.Visible : Visibility.Collapsed;
    public static bool Not(bool value) => !value;
    /// <summary>
    /// TextAlignment is logical in WinUI (Left = start). Our title elements run RightToLeft,
    /// so "physical right" is TextAlignment.Left and vice versa.
    /// </summary>
    public static TextAlignment Align(bool physicalRight) => physicalRight ? TextAlignment.Left : TextAlignment.Right;
    public static Thickness BoardPadding(double insetLeft) => new(28 + Math.Max(0, insetLeft), 16, 28, 18);
    public static bool And(bool a, bool b) => a && b;

    public static double CompletedOpacity(bool completed) => completed ? 0.5 : 1.0;
    public static TextDecorations Strike(bool completed) => completed ? TextDecorations.Strikethrough : TextDecorations.None;
    public static string Persian(string? value) => value is null ? string.Empty : TextUtil.ToPersianDigits(value);
    public static string PersianInt(int value) => TextUtil.ToPersianDigits(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static string Percent(double value) => $"{(int)Math.Round(value * 100)}%";
    public static string Scale(double value) => $"{(int)Math.Round(value * 100)}%";
    public static string Px(double value) => $"{(int)Math.Round(value)} px";

    public static ManipulationModes DragMode(bool editMode) =>
        editMode ? ManipulationModes.TranslateX | ManipulationModes.TranslateY : ManipulationModes.None;

    public static InputSystemCursorShape Cursor(bool editMode) => editMode ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Arrow;

    /// <summary>Parses #RRGGBB / #AARRGGBB. Unknown input yields a neutral grey.</summary>
    public static Color ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.FromArgb(255, 148, 163, 184);
        var s = hex.TrimStart('#');
        try
        {
            if (s.Length == 6)
                return Color.FromArgb(255, Convert.ToByte(s[..2], 16), Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16));
            if (s.Length == 8)
                return Color.FromArgb(Convert.ToByte(s[..2], 16), Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16), Convert.ToByte(s[6..8], 16));
        }
        catch { /* fall through */ }
        return Color.FromArgb(255, 148, 163, 184);
    }

    public static SolidColorBrush Brush(string? hex)
    {
        var key = string.IsNullOrWhiteSpace(hex) ? "#94A3B8" : hex;
        if (!BrushCache.TryGetValue(key, out var b))
        {
            b = new SolidColorBrush(ParseColor(key));
            BrushCache[key] = b;
        }
        return b;
    }

    /// <summary>Soft translucent version of a hex color (used for progress tracks and glows).</summary>
    public static SolidColorBrush BrushAlpha(string? hex, double alpha)
    {
        var c = ParseColor(hex);
        c.A = (byte)Math.Clamp(alpha * 255, 0, 255);
        var key = $"{hex}@{alpha:0.00}";
        if (!BrushCache.TryGetValue(key, out var b))
        {
            b = new SolidColorBrush(c);
            BrushCache[key] = b;
        }
        return b;
    }

    public static readonly IReadOnlyDictionary<string, (string Top, string Bottom)> StickyColors = new Dictionary<string, (string, string)>
    {
        ["yellow"] = ("#FDE68A", "#FCD34D"),
        ["pink"]   = ("#FBCFE8", "#F9A8D4"),
        ["cyan"]   = ("#A5F3FC", "#67E8F9"),
        ["green"]  = ("#BBF7D0", "#86EFAC"),
        ["purple"] = ("#DDD6FE", "#C4B5FD"),
    };

    private static readonly Dictionary<string, LinearGradientBrush> StickyCache = new();

    public static Brush StickyBrush(string? name)
    {
        var key = name is not null && StickyColors.ContainsKey(name) ? name : "yellow";
        if (!StickyCache.TryGetValue(key, out var brush))
        {
            var (top, bottom) = StickyColors[key];
            brush = new LinearGradientBrush
            {
                StartPoint = new global::Windows.Foundation.Point(0, 0),
                EndPoint = new global::Windows.Foundation.Point(0.3, 1),
                GradientStops =
                {
                    new GradientStop { Color = ParseColor(top), Offset = 0 },
                    new GradientStop { Color = ParseColor(bottom), Offset = 1 }
                }
            };
            StickyCache[key] = brush;
        }
        return brush;
    }

    public static SolidColorBrush StickyText(string? name) => Brush("#1F2937");

    public static double ProgressWidth(double progress, double trackWidth) => Math.Clamp(progress, 0, 100) / 100.0 * trackWidth;

    /// <summary>Star-sized column widths for a 0-100 progress bar built from two grid columns.</summary>
    public static GridLength StarOf(double progress) => new(Math.Max(0.0001, Math.Clamp(progress, 0, 100)), GridUnitType.Star);
    public static GridLength StarRest(double progress) => new(Math.Max(0.0001, 100 - Math.Clamp(progress, 0, 100)), GridUnitType.Star);
}
