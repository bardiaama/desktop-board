using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopBoard.App.Helpers;
using DesktopBoard.App.Services;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;

namespace DesktopBoard.App.ViewModels;

public sealed partial class ProjectItemViewModel : ObservableObject, IDisposable
{
    public static readonly string[] Palette = { "#3B82F6", "#22C55E", "#EAB308", "#A855F7", "#EC4899", "#F97316", "#06B6D4" };

    private readonly IProjectRepository _repo;
    private readonly Debouncer _debouncer = new(TimeSpan.FromMilliseconds(600));
    private readonly UiStrings _strings;
    private bool _initializing = true;

    public ProjectItemViewModel(ProjectListViewModel owner, Project entity, IProjectRepository repo, UiStrings strings)
    {
        Owner = owner;
        Entity = entity;
        _repo = repo;
        _strings = strings;
        Name = entity.Name;
        Description = entity.Description ?? string.Empty;
        Color = entity.Color;
        Status = entity.Status;
        Progress = entity.Progress ?? 0;
        HasProgress = entity.Progress.HasValue;
        _initializing = false;

        DeleteCommand = new RelayCommand(() => Persist.Run(Owner.RemoveAsync(this), "delete project"));
        CycleColorCommand = new RelayCommand(CycleColor);
        CycleStatusCommand = new RelayCommand(CycleStatus);
        _debouncer.Failed += (_, ex) => Logger.Error("Project save failed", ex);
        strings.PropertyChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
    }

    public ProjectListViewModel Owner { get; }
    public Project Entity { get; }
    public long Id => Entity.Id;
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand CycleColorCommand { get; }
    public IRelayCommand CycleStatusCommand { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _color = "#3B82F6";
    [ObservableProperty] private ProjectStatus _status;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _hasProgress;

    public string StatusText => _strings.StatusLabel(Status);
    public string ProgressText => HasProgress ? $"{(int)Progress}%" : string.Empty;

    /// <summary>ComboBox-friendly view of <see cref="Status"/>.</summary>
    public int StatusIndex
    {
        get => (int)Status;
        set { if (value >= 0 && value <= 2 && (int)Status != value) Status = (ProjectStatus)value; }
    }

    partial void OnNameChanged(string value) => SaveDebounced();
    partial void OnDescriptionChanged(string value) => SaveDebounced();
    partial void OnColorChanged(string value) => SaveNow();
    partial void OnStatusChanged(ProjectStatus value) { OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusIndex)); SaveNow(); }
    partial void OnProgressChanged(double value) { OnPropertyChanged(nameof(ProgressText)); SaveDebounced(); }
    partial void OnHasProgressChanged(bool value) { OnPropertyChanged(nameof(ProgressText)); SaveNow(); }

    private void Apply()
    {
        Entity.Name = Name;
        Entity.Description = string.IsNullOrWhiteSpace(Description) ? null : Description;
        Entity.Color = Color;
        Entity.Status = Status;
        Entity.Progress = HasProgress ? (int)Math.Round(Progress) : null;
    }

    private void SaveDebounced()
    {
        if (_initializing) return;
        Apply();
        _debouncer.Debounce(() => _repo.UpdateAsync(Entity));
    }

    private void SaveNow()
    {
        if (_initializing) return;
        Apply();
        Persist.Run(_repo.UpdateAsync(Entity), "save project");
    }

    private void CycleColor()
    {
        var i = Array.IndexOf(Palette, Color);
        Color = Palette[(i + 1 + Palette.Length) % Palette.Length];
    }

    private void CycleStatus() => Status = (ProjectStatus)(((int)Status + 1) % 3);

    public Task FlushAsync() => _debouncer.FlushAsync();
    public void Dispose() => _debouncer.Dispose();
}

public sealed partial class ProjectListViewModel : ObservableObject
{
    private readonly IProjectRepository _repo;
    private readonly UiStrings _strings;
    private UiDebounce? _orderDebounce;

    public ProjectListViewModel(IProjectRepository repo, IBoardStateService state, UiStrings strings)
    {
        _repo = repo;
        _strings = strings;
        _isEditMode = !state.IsLocked;
        state.LockStateChanged += (_, locked) => IsEditMode = !locked;
        Items.CollectionChanged += OnItemsChanged;
        AddCommand = new AsyncRelayCommand(AddAsync);
    }

    public ObservableCollection<ProjectItemViewModel> Items { get; } = new();
    public IAsyncRelayCommand AddCommand { get; }

    [ObservableProperty] private bool _isEditMode;

    public async Task LoadAsync()
    {
        var rows = await _repo.GetAllAsync();
        Items.Clear();
        foreach (var p in rows) Items.Add(new ProjectItemViewModel(this, p, _repo, _strings));
    }

    public async Task AddAsync()
    {
        var entity = new Project
        {
            Name = _strings.NewProject,
            Color = ProjectItemViewModel.Palette[Items.Count % ProjectItemViewModel.Palette.Length],
            SortOrder = Items.Count
        };
        await _repo.AddAsync(entity);
        Items.Add(new ProjectItemViewModel(this, entity, _repo, _strings));
    }

    public async Task RemoveAsync(ProjectItemViewModel item)
    {
        await _repo.DeleteAsync(item.Id);
        Items.Remove(item);
        item.Dispose();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset) return;
        _orderDebounce ??= new UiDebounce(TimeSpan.FromMilliseconds(120));
        _orderDebounce.Schedule(() =>
        {
            var changed = SortOrderHelper.Reindex(Items, i => i.Id, i => i.Entity.SortOrder, (i, n) => i.Entity.SortOrder = n);
            if (changed.Count > 0) Persist.Run(_repo.UpdateSortOrderAsync(changed), "project order");
        });
    }

    public async Task FlushAsync()
    {
        _orderDebounce?.Flush();
        foreach (var i in Items) await i.FlushAsync();
    }
}
