using DesktopBoard.Core.Models;

namespace DesktopBoard.Core.Interfaces;

/// <summary>Basic CRUD shared by every board entity.</summary>
public interface IRepository<T> where T : Entity
{
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<T?> GetByIdAsync(long id);
    /// <summary>Inserts the entity and sets its Id.</summary>
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(long id);
    Task DeleteAllAsync();
}

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<IReadOnlyList<TaskItem>> GetBySectionAsync(Section section, TaskCategory category);
    /// <summary>Persists new SortOrder values for the given ids in one transaction.</summary>
    Task UpdateSortOrderAsync(IEnumerable<(long Id, int SortOrder)> order);
}

public interface IGoalRepository : IRepository<Goal>
{
    Task<IReadOnlyList<Goal>> GetBySectionAsync(Section section);
    Task UpdateSortOrderAsync(IEnumerable<(long Id, int SortOrder)> order);
}

public interface IProjectRepository : IRepository<Project>
{
    Task UpdateSortOrderAsync(IEnumerable<(long Id, int SortOrder)> order);
}

public interface IMeetingRepository : IRepository<Meeting>
{
    Task<IReadOnlyList<Meeting>> GetUpcomingAsync(DateOnly from, int take);
}

public interface INoteRepository : IRepository<Note>
{
    Task<Note?> GetBySectionAsync(Section section);
}

public interface IStickyNoteRepository : IRepository<StickyNote>
{
    Task<IReadOnlyList<StickyNote>> GetBySectionAsync(Section section);
}

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);
    Task<IReadOnlyList<AppSetting>> GetAllAsync();
    Task SetAsync(string key, string? value);
    Task DeleteAllAsync();
}
