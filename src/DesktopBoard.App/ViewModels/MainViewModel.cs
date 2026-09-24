using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopBoard.App.Services;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace DesktopBoard.App.ViewModels;

/// <summary>Root of the board. Owns every card view-model and the wallpaper/scale state.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IBoardStateService _state;
    private readonly ISettingsService _settings;
    private readonly ISystemWallpaperService _systemWallpaper;
    private readonly WallpaperImageService _wallpaperImages;
    private readonly DispatcherQueue _dispatcher;
    private int _wallpaperVersion;

    public MainViewModel(
        IBoardStateService state,
        ISettingsService settings,
        ISystemWallpaperService systemWallpaper,
        WallpaperImageService wallpaperImages,
        UiStrings strings,
        ITaskRepository tasks,
        IGoalRepository goals,
        IProjectRepository projects,
        IMeetingRepository meetings,
        INoteRepository notes,
        IStickyNoteRepository stickies)
    {
        _state = state;
        _settings = settings;
        _systemWallpaper = systemWallpaper;
        _wallpaperImages = wallpaperImages;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Strings = strings;
        Clock = new ClockViewModel();

        WorkGoals = new GoalListViewModel(goals, state, Section.Work) { AddPlaceholder = strings.AddGoal, ShowCounter = true };
        PersonalGoals = new GoalListViewModel(goals, state, Section.Personal) { AddPlaceholder = strings.AddGoal, ShowCounter = true };
        WorkToday = new TaskListViewModel(tasks, state, Section.Work, TaskCategory.Today) { ShowTime = true, ShowCounter = true, AddPlaceholder = strings.AddTask };
        PersonalToday = new TaskListViewModel(tasks, state, Section.Personal, TaskCategory.Today) { ShowNumber = true, ShowCounter = true, AddPlaceholder = strings.AddTask };
        Week = new TaskListViewModel(tasks, state, Section.Personal, TaskCategory.ThisWeek) { ShowDay = true, ShowDot = true, ShowCheckbox = false, AlignTitleRight = true, AddPlaceholder = strings.AddTask };
        Future = new TaskListViewModel(tasks, state, Section.Personal, TaskCategory.Future) { AddPlaceholder = strings.AddTask };
        Projects = new ProjectListViewModel(projects, state, strings);
        Meetings = new MeetingListViewModel(meetings, state, strings);
        WorkNote = new NoteViewModel(notes, state, Section.Work, "یادداشت سریع");
        PersonalNote = new NoteViewModel(notes, state, Section.Personal, "یادداشت‌ها");
        WorkIdeas = new StickyBoardViewModel(stickies, state, strings, Section.Work);
        PersonalIdeas = new StickyBoardViewModel(stickies, state, strings, Section.Personal);

        _isLocked = state.IsLocked;
        state.LockStateChanged += (_, locked) =>
        {
            Logger.Info(locked ? "Board locked" : "Board unlocked");
            IsLocked = locked;
            OnPropertyChanged(nameof(IsEditMode));
        };

        ToggleLockCommand = new RelayCommand(() => _state.Toggle());
        LockCommand = new RelayCommand(() => _state.Lock());
        UnlockCommand = new RelayCommand(() => _state.Unlock());

        ApplySettings();
        settings.SettingChanged += (_, key) => _dispatcher.TryEnqueue(() => OnSettingChanged(key));
        Clock.DayChanged += (_, _) => _dispatcher.TryEnqueue(() => _ = ReloadDailyAsync());
    }

    public UiStrings Strings { get; }
    public ClockViewModel Clock { get; }

    public GoalListViewModel WorkGoals { get; }
    public GoalListViewModel PersonalGoals { get; }
    public TaskListViewModel WorkToday { get; }
    public TaskListViewModel PersonalToday { get; }
    public TaskListViewModel Week { get; }
    public TaskListViewModel Future { get; }
    public ProjectListViewModel Projects { get; }
    public MeetingListViewModel Meetings { get; }
    public NoteViewModel WorkNote { get; }
    public NoteViewModel PersonalNote { get; }
    public StickyBoardViewModel WorkIdeas { get; }
    public StickyBoardViewModel PersonalIdeas { get; }

    public IRelayCommand ToggleLockCommand { get; }
    public IRelayCommand LockCommand { get; }
    public IRelayCommand UnlockCommand { get; }

    [ObservableProperty] private bool _isLocked = true;
    public bool IsEditMode => !IsLocked;

    [ObservableProperty] private bool _holdToUnlock = true;
    [ObservableProperty] private ImageSource? _backgroundImage;
    [ObservableProperty] private double _darkness = 0.45;
    /// <summary>User-chosen UI scale; 0 means automatic (from screen height).</summary>
    [ObservableProperty] private double _uiScale;
    /// <summary>The scale actually applied by the window.</summary>
    [ObservableProperty] private double _effectiveUiScale = 1.0;
    [ObservableProperty] private bool _glassEnabled = true;
    /// <summary>Logical pixels kept free on the left so desktop icons do not cover the board.</summary>
    [ObservableProperty] private double _insetLeft;
    [ObservableProperty] private bool _isLoaded;

    partial void OnIsLockedChanged(bool value) => OnPropertyChanged(nameof(IsEditMode));

    public async Task LoadAsync()
    {
        if (_settings.GetBool(SettingKeys.DefaultLocked, true)) _state.Lock(); else _state.Unlock();

        await Task.WhenAll(
            WorkGoals.LoadAsync(), PersonalGoals.LoadAsync(),
            WorkToday.LoadAsync(), PersonalToday.LoadAsync(), Week.LoadAsync(), Future.LoadAsync(),
            Projects.LoadAsync(), Meetings.LoadAsync(),
            WorkNote.LoadAsync(), PersonalNote.LoadAsync(),
            WorkIdeas.LoadAsync(), PersonalIdeas.LoadAsync());

        IsLoaded = true;
        _ = ReloadWallpaperAsync();
    }

    private Task ReloadDailyAsync() => Meetings.LoadAsync();

    private void ApplySettings()
    {
        HoldToUnlock = _settings.GetBool(SettingKeys.HoldToUnlock, true);
        Darkness = Math.Clamp(_settings.GetDouble(SettingKeys.BackgroundDarkness, 0.45), 0, 0.95);
        var scale = _settings.GetDouble(SettingKeys.UiScale, 0);
        UiScale = scale > 0 ? Math.Clamp(scale, 0.7, 1.6) : 0;
        GlassEnabled = _settings.GetBool(SettingKeys.GlassEnabled, true);
        InsetLeft = Math.Clamp(_settings.GetDouble(SettingKeys.BoardInsetLeft, 0), 0, 400);
    }

    private void OnSettingChanged(string key)
    {
        ApplySettings();
        if (key is SettingKeys.BackgroundImagePath or SettingKeys.BackgroundBlur)
            _ = ReloadWallpaperAsync();
    }

    public async Task ReloadWallpaperAsync()
    {
        var version = ++_wallpaperVersion;
        var custom = _settings.GetString(SettingKeys.BackgroundImagePath);
        var path = !string.IsNullOrWhiteSpace(custom) && File.Exists(custom) ? custom : _systemWallpaper.GetCurrentWallpaperPath();
        if (path is null)
        {
            BackgroundImage = null;
            return;
        }

        var blur = Math.Clamp(_settings.GetDouble(SettingKeys.BackgroundBlur, 0.35), 0, 1);
        var decoded = await _wallpaperImages.LoadAsync(path, blur);
        if (decoded is null || version != _wallpaperVersion) return;
        BackgroundImage = WallpaperImageService.ToBitmap(decoded);
        Logger.Info($"Wallpaper loaded: {path} ({decoded.Width}x{decoded.Height}, blur {blur:0.00})");
    }

    /// <summary>Writes every pending debounced edit. Called on exit.</summary>
    public async Task FlushAsync()
    {
        await Task.WhenAll(
            WorkGoals.FlushAsync(), PersonalGoals.FlushAsync(),
            WorkToday.FlushAsync(), PersonalToday.FlushAsync(), Week.FlushAsync(), Future.FlushAsync(),
            Projects.FlushAsync(), Meetings.FlushAsync(),
            WorkNote.FlushAsync(), PersonalNote.FlushAsync(),
            WorkIdeas.FlushAsync(), PersonalIdeas.FlushAsync());
    }

    public async Task ResetLayoutAsync()
    {
        await WorkIdeas.ResetLayoutAsync();
        await PersonalIdeas.ResetLayoutAsync();
        await _settings.SetAsync(SettingKeys.UiScale, (string?)null);
        await _settings.SetAsync(SettingKeys.BackgroundDarkness, 0.45);
        await _settings.SetAsync(SettingKeys.BackgroundBlur, 0.35);
    }

    /// <summary>Re-reads everything from the database (after a backup import).</summary>
    public async Task ReloadAllAsync()
    {
        await _settings.LoadAsync();
        await LoadAsync();
    }
}
