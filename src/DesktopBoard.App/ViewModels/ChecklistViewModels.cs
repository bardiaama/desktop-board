using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopBoard.App.Helpers;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;

namespace DesktopBoard.App.ViewModels;

/// <summary>One row in a checklist card (a task or a goal).</summary>
public abstract partial class ChecklistItemViewModel : ObservableObject, IDisposable
{
    private readonly Debouncer _textDebouncer = new(TimeSpan.FromMilliseconds(600));
    private bool _initializing = true;

    protected ChecklistItemViewModel(ChecklistViewModel owner)
    {
        Owner = owner;
        DeleteCommand = new RelayCommand(() => Persist.Run(Owner.RemoveAsync(this), "delete checklist item"));
        _textDebouncer.Failed += (_, ex) => Logger.Error("Checklist item save failed", ex);
    }

    public ChecklistViewModel Owner { get; }
    public long Id { get; protected set; }
    /// <summary>Backed by the entity so a reorder and a later full-row save never disagree.</summary>
    public abstract int SortOrder { get; set; }
    public IRelayCommand DeleteCommand { get; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _isCompleted;
    /// <summary>"HH:mm" or empty. Editable; parsed leniently.</summary>
    [ObservableProperty] private string _timeText = string.Empty;
    /// <summary>Weekday label for "This Week" rows.</summary>
    [ObservableProperty] private string _dayName = string.Empty;
    /// <summary>Hex accent color or empty.</summary>
    [ObservableProperty] private string _color = string.Empty;
    /// <summary>1-based row number, shown by the "numbered" style.</summary>
    [ObservableProperty] private int _number;

    /// <summary>Pushes the current row state into the entity.</summary>
    protected abstract void ApplyToEntity();
    protected abstract Task SaveEntityAsync();

    protected void EndInit() => _initializing = false;

    partial void OnTitleChanged(string value) => SaveDebounced();
    partial void OnTimeTextChanged(string value) => SaveDebounced();

    partial void OnIsCompletedChanged(bool value)
    {
        if (_initializing) return;
        ApplyToEntity();
        Persist.Run(SaveEntityAsync(), "toggle checklist item");
        Owner.RefreshCounters();
    }

    private void SaveDebounced()
    {
        if (_initializing) return;
        ApplyToEntity();
        _textDebouncer.Debounce(SaveEntityAsync);
    }

    public Task FlushAsync() => _textDebouncer.FlushAsync();

    public void Dispose() => _textDebouncer.Dispose();
}

/// <summary>A checklist card: ordered rows plus an "add" line. Tasks and goals share this.</summary>
public abstract partial class ChecklistViewModel : ObservableObject
{
    private readonly IBoardStateService _state;
    private UiDebounce? _orderDebounce;

    protected ChecklistViewModel(IBoardStateService state)
    {
        _state = state;
        _isEditMode = !state.IsLocked;
        state.LockStateChanged += (_, locked) => IsEditMode = !locked;
        Items.CollectionChanged += OnItemsChanged;
        AddCommand = new AsyncRelayCommand(AddFromInputAsync);
    }

    public ObservableCollection<ChecklistItemViewModel> Items { get; } = new();
    public IAsyncRelayCommand AddCommand { get; }

    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _newItemText = string.Empty;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _counterText = string.Empty;

    // Presentation switches used by the shared ChecklistControl.
    public bool ShowTime { get; init; }
    public bool ShowDay { get; init; }
    public bool ShowCheckbox { get; init; } = true;
    public bool ShowNumber { get; init; }
    public bool ShowDot { get; init; }
    public bool ShowCounter { get; init; }
    /// <summary>Right-align titles (used by the week list, where the colored dot sits at the right edge).</summary>
    public bool AlignTitleRight { get; init; }
    public string AddPlaceholder { get; init; } = string.Empty;

    public abstract Task LoadAsync();
    public abstract Task AddAsync(string title);
    public abstract Task RemoveAsync(ChecklistItemViewModel item);
    protected abstract Task PersistOrderAsync(IEnumerable<(long Id, int SortOrder)> changed);

    private async Task AddFromInputAsync()
    {
        var text = NewItemText.Trim();
        if (text.Length == 0) return;
        NewItemText = string.Empty;
        await AddAsync(text);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshCounters();
        if (e.Action is NotifyCollectionChangedAction.Reset) return;
        // Reorders arrive as Remove + Add; wait for the pair to settle before writing.
        _orderDebounce ??= new UiDebounce(TimeSpan.FromMilliseconds(120));
        _orderDebounce.Schedule(() =>
        {
            var changed = SortOrderHelper.Reindex(Items, i => i.Id, i => i.SortOrder, (i, n) => i.SortOrder = n);
            for (var n = 0; n < Items.Count; n++) Items[n].Number = n + 1;
            if (changed.Count > 0) Persist.Run(PersistOrderAsync(changed), "persist order");
        });
    }

    /// <summary>Flushes and disposes the current rows before a reload, so no debouncer leaks or writes late.</summary>
    protected async Task ClearItemsAsync()
    {
        foreach (var i in Items) await i.FlushAsync();
        foreach (var i in Items) i.Dispose();
        Items.Clear();
    }

    public void RefreshCounters()
    {
        TotalCount = Items.Count;
        CompletedCount = Items.Count(i => i.IsCompleted);
        CounterText = TotalCount == 0 ? string.Empty : $"{CompletedCount}/{TotalCount}";
    }

    public async Task FlushAsync()
    {
        _orderDebounce?.Flush();
        foreach (var i in Items) await i.FlushAsync();
    }
}

// ---------------------------------------------------------------------------------------
// Tasks

public sealed partial class TaskItemViewModel : ChecklistItemViewModel
{
    private readonly ITaskRepository _repo;

    public TaskItemViewModel(ChecklistViewModel owner, TaskItem entity, ITaskRepository repo) : base(owner)
    {
        _repo = repo;
        Entity = entity;
        Id = entity.Id;
        Title = entity.Title;
        IsCompleted = entity.IsCompleted;
        TimeText = TextUtil.FormatTime(entity.Time);
        Color = entity.Color ?? string.Empty;
        DayName = entity.DueDate is { } d ? PersianDayName(d.DayOfWeek) : string.Empty;
        EndInit();
    }

    public TaskItem Entity { get; }
    public override int SortOrder { get => Entity.SortOrder; set => Entity.SortOrder = value; }

    protected override void ApplyToEntity()
    {
        Entity.Title = Title;
        Entity.IsCompleted = IsCompleted;
        Entity.Time = TextUtil.ParseTime(TimeText);
        Entity.Color = string.IsNullOrEmpty(Color) ? null : Color;
    }

    protected override Task SaveEntityAsync() => _repo.UpdateAsync(Entity);

    public static string PersianDayName(DayOfWeek d) => d switch
    {
        DayOfWeek.Saturday => "شنبه",
        DayOfWeek.Sunday => "یکشنبه",
        DayOfWeek.Monday => "دوشنبه",
        DayOfWeek.Tuesday => "سه‌شنبه",
        DayOfWeek.Wednesday => "چهارشنبه",
        DayOfWeek.Thursday => "پنجشنبه",
        _ => "جمعه"
    };
}

public sealed partial class TaskListViewModel : ChecklistViewModel
{
    private static readonly string[] WeekColors = { "#3B82F6", "#EAB308", "#22C55E", "#A855F7", "#EC4899", "#60A5FA", "#9CA3AF" };
    private readonly ITaskRepository _repo;

    public TaskListViewModel(ITaskRepository repo, IBoardStateService state, Section section, TaskCategory category) : base(state)
    {
        _repo = repo;
        Section = section;
        Category = category;
    }

    public Section Section { get; }
    public TaskCategory Category { get; }

    public override async Task LoadAsync()
    {
        var rows = await _repo.GetBySectionAsync(Section, Category);
        await ClearItemsAsync();
        foreach (var t in rows) Items.Add(new TaskItemViewModel(this, t, _repo));
        for (var n = 0; n < Items.Count; n++) Items[n].Number = n + 1;
        RefreshCounters();
    }

    public override async Task AddAsync(string title)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var entity = new TaskItem
        {
            Title = title,
            Section = Section,
            Category = Category,
            SortOrder = Items.Count,
            DueDate = Category == TaskCategory.Future ? null : today,
            Color = Category == TaskCategory.ThisWeek ? WeekColors[Items.Count % WeekColors.Length] : null
        };
        await _repo.AddAsync(entity);
        Items.Add(new TaskItemViewModel(this, entity, _repo));
    }

    public override async Task RemoveAsync(ChecklistItemViewModel item)
    {
        await _repo.DeleteAsync(item.Id);
        Items.Remove(item);
        item.Dispose();
    }

    protected override Task PersistOrderAsync(IEnumerable<(long Id, int SortOrder)> changed) => _repo.UpdateSortOrderAsync(changed);
}

// ---------------------------------------------------------------------------------------
// Goals

public sealed partial class GoalItemViewModel : ChecklistItemViewModel
{
    private readonly IGoalRepository _repo;

    public GoalItemViewModel(ChecklistViewModel owner, Goal entity, IGoalRepository repo) : base(owner)
    {
        _repo = repo;
        Entity = entity;
        Id = entity.Id;
        Title = entity.Title;
        IsCompleted = entity.IsCompleted;
        EndInit();
    }

    public Goal Entity { get; }
    public override int SortOrder { get => Entity.SortOrder; set => Entity.SortOrder = value; }

    protected override void ApplyToEntity()
    {
        Entity.Title = Title;
        Entity.IsCompleted = IsCompleted;
    }

    protected override Task SaveEntityAsync() => _repo.UpdateAsync(Entity);
}

public sealed partial class GoalListViewModel : ChecklistViewModel
{
    private readonly IGoalRepository _repo;

    public GoalListViewModel(IGoalRepository repo, IBoardStateService state, Section section) : base(state)
    {
        _repo = repo;
        Section = section;
    }

    public Section Section { get; }

    public override async Task LoadAsync()
    {
        var rows = await _repo.GetBySectionAsync(Section);
        await ClearItemsAsync();
        foreach (var g in rows) Items.Add(new GoalItemViewModel(this, g, _repo));
        for (var n = 0; n < Items.Count; n++) Items[n].Number = n + 1;
        RefreshCounters();
    }

    public override async Task AddAsync(string title)
    {
        var entity = new Goal { Title = title, Section = Section, SortOrder = Items.Count };
        await _repo.AddAsync(entity);
        Items.Add(new GoalItemViewModel(this, entity, _repo));
    }

    public override async Task RemoveAsync(ChecklistItemViewModel item)
    {
        await _repo.DeleteAsync(item.Id);
        Items.Remove(item);
        item.Dispose();
    }

    protected override Task PersistOrderAsync(IEnumerable<(long Id, int SortOrder)> changed) => _repo.UpdateSortOrderAsync(changed);
}
