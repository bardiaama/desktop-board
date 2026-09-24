using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopBoard.App.Helpers;
using Microsoft.UI.Dispatching;

namespace DesktopBoard.App.ViewModels;

public sealed partial class DayCellViewModel : ObservableObject
{
    [ObservableProperty] private string _letter = string.Empty;
    [ObservableProperty] private string _number = string.Empty;
    [ObservableProperty] private bool _isToday;
    [ObservableProperty] private bool _isWeekend;
}

/// <summary>Header clock: time, Gregorian date, Persian (Jalali) date, and the current week strip.</summary>
public sealed partial class ClockViewModel : ObservableObject
{
    private static readonly PersianCalendar Persian = new();
    private static readonly string[] PersianMonths =
        { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
    private static readonly string[] DayLetters = { "ش", "ی", "د", "س", "چ", "پ", "ج" }; // Saturday..Friday

    private readonly DispatcherQueueTimer _timer;
    private DateOnly _lastDate;

    public ClockViewModel()
    {
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => Update();
        Update();
        _timer.Start();
    }

    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private string _gregorianDate = string.Empty;
    [ObservableProperty] private string _persianDate = string.Empty;
    public ObservableCollection<DayCellViewModel> Week { get; } = new();

    public event EventHandler? DayChanged;

    private void Update()
    {
        var now = DateTime.Now;
        Time = now.ToString("HH:mm", CultureInfo.InvariantCulture);

        var today = DateOnly.FromDateTime(now);
        if (today == _lastDate) return;
        _lastDate = today;

        GregorianDate = now.ToString("dddd, d MMMM yyyy", CultureInfo.GetCultureInfo("en-US"));

        var py = Persian.GetYear(now);
        var pm = Persian.GetMonth(now);
        var pd = Persian.GetDayOfMonth(now);
        PersianDate = TextUtil.ToPersianDigits($"{TaskItemViewModel.PersianDayName(now.DayOfWeek)}، {pd} {PersianMonths[pm - 1]} {py}");

        var start = Core.Models.SeedData.StartOfPersianWeek(today);
        Week.Clear();
        for (var i = 0; i < 7; i++)
        {
            var d = start.AddDays(i);
            Week.Add(new DayCellViewModel
            {
                Letter = DayLetters[i],
                Number = d.Day.ToString(CultureInfo.InvariantCulture),
                IsToday = d == today,
                IsWeekend = d.DayOfWeek == DayOfWeek.Friday
            });
        }

        DayChanged?.Invoke(this, EventArgs.Empty);
    }
}
