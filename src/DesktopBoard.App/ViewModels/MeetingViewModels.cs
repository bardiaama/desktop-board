using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopBoard.App.Helpers;
using DesktopBoard.App.Services;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;

namespace DesktopBoard.App.ViewModels;

public sealed partial class MeetingItemViewModel : ObservableObject, IDisposable
{
    private readonly IMeetingRepository _repo;
    private readonly Debouncer _debouncer = new(TimeSpan.FromMilliseconds(600));
    private bool _initializing = true;

    public MeetingItemViewModel(MeetingListViewModel owner, Meeting entity, IMeetingRepository repo)
    {
        Owner = owner;
        Entity = entity;
        _repo = repo;
        Title = entity.Title;
        TimeText = TextUtil.FormatTime(entity.Time);
        Color = entity.Color;
        DateText = FormatDate(entity.Date);
        _initializing = false;

        DeleteCommand = new RelayCommand(() => Persist.Run(Owner.RemoveAsync(this), "delete meeting"));
        CycleColorCommand = new RelayCommand(() =>
        {
            var i = Array.IndexOf(ProjectItemViewModel.Palette, Color);
            Color = ProjectItemViewModel.Palette[(i + 1 + ProjectItemViewModel.Palette.Length) % ProjectItemViewModel.Palette.Length];
        });
        _debouncer.Failed += (_, ex) => Logger.Error("Meeting save failed", ex);
    }

    public MeetingListViewModel Owner { get; }
    public Meeting Entity { get; }
    public long Id => Entity.Id;
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand CycleColorCommand { get; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _timeText = string.Empty;
    [ObservableProperty] private string _color = "#3B82F6";
    [ObservableProperty] private string _dateText = string.Empty;

    /// <summary>Shown when the meeting is not today, e.g. "فردا" or "۲۶ سپتامبر".</summary>
    public bool ShowDate => Entity.Date != DateOnly.FromDateTime(DateTime.Now);

    partial void OnTitleChanged(string value) => SaveDebounced();
    partial void OnTimeTextChanged(string value) => SaveDebounced();
    partial void OnColorChanged(string value)
    {
        if (_initializing) return;
        Entity.Color = value;
        Persist.Run(_repo.UpdateAsync(Entity), "meeting color");
    }

    private void SaveDebounced()
    {
        if (_initializing) return;
        Entity.Title = Title;
        Entity.Time = TextUtil.ParseTime(TimeText);
        _debouncer.Debounce(() => _repo.UpdateAsync(Entity));
    }

    private static string FormatDate(DateOnly d)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (d == today) return string.Empty;
        if (d == today.AddDays(1)) return "فردا";
        return TextUtil.ToPersianDigits(d.ToString("dd/MM"));
    }

    public Task FlushAsync() => _debouncer.FlushAsync();
    public void Dispose() => _debouncer.Dispose();
}

public sealed partial class MeetingListViewModel : ObservableObject
{
    private readonly IMeetingRepository _repo;
    private readonly UiStrings _strings;

    public MeetingListViewModel(IMeetingRepository repo, IBoardStateService state, UiStrings strings)
    {
        _repo = repo;
        _strings = strings;
        _isEditMode = !state.IsLocked;
        state.LockStateChanged += (_, locked) => IsEditMode = !locked;
        AddCommand = new AsyncRelayCommand(AddAsync);
    }

    public ObservableCollection<MeetingItemViewModel> Items { get; } = new();
    public IAsyncRelayCommand AddCommand { get; }

    [ObservableProperty] private bool _isEditMode;

    public async Task LoadAsync()
    {
        var rows = await _repo.GetUpcomingAsync(DateOnly.FromDateTime(DateTime.Now), 8);
        foreach (var i in Items) await i.FlushAsync();
        foreach (var i in Items) i.Dispose();
        Items.Clear();
        foreach (var m in rows) Items.Add(new MeetingItemViewModel(this, m, _repo));
    }

    public async Task AddAsync()
    {
        var now = DateTime.Now;
        var nextHour = new TimeOnly(Math.Min(23, now.Hour + 1), 0);
        var entity = new Meeting
        {
            Title = _strings.NewMeeting,
            Date = DateOnly.FromDateTime(now),
            Time = nextHour,
            Color = ProjectItemViewModel.Palette[Items.Count % ProjectItemViewModel.Palette.Length],
            SortOrder = Items.Count
        };
        await _repo.AddAsync(entity);
        Items.Add(new MeetingItemViewModel(this, entity, _repo));
    }

    public async Task RemoveAsync(MeetingItemViewModel item)
    {
        await _repo.DeleteAsync(item.Id);
        Items.Remove(item);
        item.Dispose();
    }

    public async Task FlushAsync()
    {
        foreach (var i in Items) await i.FlushAsync();
    }
}
