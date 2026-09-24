using CommunityToolkit.Mvvm.ComponentModel;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;

namespace DesktopBoard.App.ViewModels;

/// <summary>Free-form text card. One note per section; created on first load if missing.</summary>
public sealed partial class NoteViewModel : ObservableObject, IDisposable
{
    private readonly INoteRepository _repo;
    private readonly Debouncer _debouncer = new(TimeSpan.FromMilliseconds(700));
    private Note? _entity;
    private bool _initializing = true;

    public NoteViewModel(INoteRepository repo, IBoardStateService state, Section section, string defaultTitle)
    {
        _repo = repo;
        Section = section;
        DefaultTitle = defaultTitle;
        _isEditMode = !state.IsLocked;
        state.LockStateChanged += (_, locked) => IsEditMode = !locked;
        _debouncer.Failed += (_, ex) => Logger.Error("Note save failed", ex);
    }

    public Section Section { get; }
    public string DefaultTitle { get; }

    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private bool _isEditMode;

    public async Task LoadAsync()
    {
        _entity = await _repo.GetBySectionAsync(Section);
        if (_entity is null)
        {
            _entity = new Note { Section = Section, Title = DefaultTitle, Content = string.Empty };
            await _repo.AddAsync(_entity);
        }
        _initializing = true;
        Content = _entity.Content;
        _initializing = false;
    }

    partial void OnContentChanged(string value)
    {
        if (_initializing || _entity is null) return;
        _entity.Content = value;
        _debouncer.Debounce(() => _repo.UpdateAsync(_entity));
    }

    public Task FlushAsync() => _debouncer.FlushAsync();
    public void Dispose() => _debouncer.Dispose();
}
