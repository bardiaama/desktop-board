using DesktopBoard.Core.Services;
using Microsoft.UI.Dispatching;

namespace DesktopBoard.App.Helpers;

/// <summary>Fire-and-forget persistence with logging, so a failed write never crashes the UI.</summary>
public static class Persist
{
    public static void Run(Task task, string what)
    {
        task.ContinueWith(t => Logger.Error($"Persist failed: {what}", t.Exception?.GetBaseException()),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public static void Run(Func<Task> action, string what)
    {
        try { Run(action(), what); }
        catch (Exception ex) { Logger.Error($"Persist failed: {what}", ex); }
    }
}

/// <summary>A UI-thread debounce built on the DispatcherQueue timer (for collection-bound work).</summary>
public sealed class UiDebounce
{
    private readonly DispatcherQueueTimer _timer;
    private Action? _action;

    public UiDebounce(TimeSpan delay)
    {
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = delay;
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => { var a = _action; _action = null; a?.Invoke(); };
    }

    public void Schedule(Action action)
    {
        _action = action;
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        _timer.Stop();
        var a = _action;
        _action = null;
        a?.Invoke();
    }
}

/// <summary>Persian digit / time helpers used by editable fields.</summary>
public static class TextUtil
{
    private const string PersianDigits = "۰۱۲۳۴۵۶۷۸۹";
    private const string ArabicDigits = "٠١٢٣٤٥٦٧٨٩";

    public static string ToPersianDigits(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (chars[i] >= '0' && chars[i] <= '9') chars[i] = PersianDigits[chars[i] - '0'];
        return new string(chars);
    }

    public static string ToLatinDigits(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var p = PersianDigits.IndexOf(chars[i]);
            if (p >= 0) { chars[i] = (char)('0' + p); continue; }
            var a = ArabicDigits.IndexOf(chars[i]);
            if (a >= 0) chars[i] = (char)('0' + a);
        }
        return new string(chars);
    }

    /// <summary>Accepts "14:30", "14.30", "1430", "9", "۱۴:۳۰". Returns null when not a time.</summary>
    public static TimeOnly? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = ToLatinDigits(text.Trim()).Replace('.', ':').Replace('،', ':');
        if (t.Length is 3 or 4 && t.All(char.IsDigit))
            t = t.Length == 3 ? t[..1] + ":" + t[1..] : t[..2] + ":" + t[2..];
        if (t.All(char.IsDigit) && int.TryParse(t, out var hourOnly) && hourOnly is >= 0 and < 24)
            return new TimeOnly(hourOnly, 0);
        return TimeOnly.TryParseExact(t, new[] { "H:mm", "HH:mm", "H:m" }, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var result) ? result : null;
    }

    public static string FormatTime(TimeOnly? time) => time?.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}
