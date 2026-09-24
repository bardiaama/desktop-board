using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopBoard.App.Helpers;
using DesktopBoard.App.Services;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;

namespace DesktopBoard.App.ViewModels;

public sealed partial class StickyNoteViewModel : ObservableObject, IDisposable
{
    public static readonly string[] Palette = { "yellow", "pink", "cyan", "green", "purple" };

    private readonly IStickyNoteRepository _repo;
    private readonly Debouncer _debouncer = new(TimeSpan.FromMilliseconds(600));
    private bool _initializing = true;

    public StickyNoteViewModel(StickyBoardViewModel owner, StickyNote entity, IStickyNoteRepository repo)
    {
        Owner = owner;
        Entity = entity;
        _repo = repo;
        Content = entity.Content;
        Color = entity.Color;
        X = entity.PositionX;
        Y = entity.PositionY;
        Width = entity.Width;
        Height = entity.Height;
        Rotation = entity.Rotation;
        _initializing = false;

        DeleteCommand = new RelayCommand(() => Persist.Run(Owner.RemoveAsync(this), "delete sticky"));
        CycleColorCommand = new RelayCommand(() =>
        {
            var i = Array.IndexOf(Palette, Color);
            Color = Palette[(i + 1 + Palette.Length) % Palette.Length];
        });
        _debouncer.Failed += (_, ex) => Logger.Error("Sticky save failed", ex);
    }

    public StickyBoardViewModel Owner { get; }
    public StickyNote Entity { get; }
    public long Id => Entity.Id;
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand CycleColorCommand { get; }

    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private string _color = "yellow";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width = 150;
    [ObservableProperty] private double _height = 90;
    [ObservableProperty] private double _rotation;

    partial void OnContentChanged(string value)
    {
        if (_initializing) return;
        Entity.Content = value;
        _debouncer.Debounce(() => _repo.UpdateAsync(Entity));
    }

    partial void OnColorChanged(string value)
    {
        if (_initializing) return;
        Entity.Color = value;
        Persist.Run(_repo.UpdateAsync(Entity), "sticky color");
    }

    /// <summary>Called by the view while dragging. Position is only written when the drag ends.</summary>
    public void MoveBy(double dx, double dy, double maxX, double maxY)
    {
        X = Math.Clamp(X + dx, 0, Math.Max(0, maxX - Width));
        Y = Math.Clamp(Y + dy, 0, Math.Max(0, maxY - Height));
    }

    public void CommitPosition()
    {
        Entity.PositionX = X;
        Entity.PositionY = Y;
        Persist.Run(_repo.UpdateAsync(Entity), "sticky position");
    }

    public Task FlushAsync() => _debouncer.FlushAsync();
    public void Dispose() => _debouncer.Dispose();
}

/// <summary>A free-positioned canvas of sticky notes for one section.</summary>
public sealed partial class StickyBoardViewModel : ObservableObject
{
    private readonly IStickyNoteRepository _repo;
    private readonly UiStrings _strings;

    public StickyBoardViewModel(IStickyNoteRepository repo, IBoardStateService state, UiStrings strings, Section section)
    {
        _repo = repo;
        _strings = strings;
        Section = section;
        _isEditMode = !state.IsLocked;
        state.LockStateChanged += (_, locked) => IsEditMode = !locked;
        AddCommand = new AsyncRelayCommand(AddAsync);
    }

    public Section Section { get; }
    public ObservableCollection<StickyNoteViewModel> Items { get; } = new();
    public IAsyncRelayCommand AddCommand { get; }

    [ObservableProperty] private bool _isEditMode;
    /// <summary>Canvas size, reported by the view so new notes and drags stay inside it.</summary>
    [ObservableProperty] private double _canvasWidth = 600;
    [ObservableProperty] private double _canvasHeight = 110;

    public async Task LoadAsync()
    {
        var rows = await _repo.GetBySectionAsync(Section);
        Items.Clear();
        foreach (var s in rows) Items.Add(new StickyNoteViewModel(this, s, _repo));
    }

    public async Task AddAsync()
    {
        var n = Items.Count;
        var entity = new StickyNote
        {
            Content = _strings.NewIdea,
            Section = Section,
            Color = StickyNoteViewModel.Palette[n % StickyNoteViewModel.Palette.Length],
            PositionX = Math.Max(0, Math.Min(CanvasWidth - 150, 8 + (n % 4) * 164)),
            PositionY = Math.Max(0, Math.Min(CanvasHeight - 90, 6 + (n / 4) * 12)),
            Rotation = (n % 2 == 0 ? -1 : 1) * (1 + n % 3),
            Width = 150,
            Height = 90
        };
        await _repo.AddAsync(entity);
        Items.Add(new StickyNoteViewModel(this, entity, _repo));
    }

    public async Task RemoveAsync(StickyNoteViewModel item)
    {
        await _repo.DeleteAsync(item.Id);
        Items.Remove(item);
        item.Dispose();
    }

    /// <summary>Lays the notes out again in a neat row. Used by "Reset layout".</summary>
    public async Task ResetLayoutAsync()
    {
        for (var n = 0; n < Items.Count; n++)
        {
            var it = Items[n];
            it.X = Math.Max(0, Math.Min(CanvasWidth - it.Width, 8 + (n % 4) * 164));
            it.Y = Math.Max(0, Math.Min(CanvasHeight - it.Height, 6 + (n / 4) * 12));
            it.Entity.PositionX = it.X;
            it.Entity.PositionY = it.Y;
            await _repo.UpdateAsync(it.Entity);
        }
    }

    public async Task FlushAsync()
    {
        foreach (var i in Items) await i.FlushAsync();
    }
}
