using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Services;
using DesktopBoard.Data.Repositories;
using DesktopBoard.Data.SQLite;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopBoard.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the SQLite database, all repositories and the data-backed services.</summary>
    public static IServiceCollection AddDesktopBoardData(this IServiceCollection services, string databasePath)
    {
        // Factory registration: the container owns the instance and disposes it with the provider.
        services.AddSingleton(_ => new SqliteDatabase(databasePath));
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<IGoalRepository, GoalRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IMeetingRepository, MeetingRepository>();
        services.AddSingleton<INoteRepository, NoteRepository>();
        services.AddSingleton<IStickyNoteRepository, StickyNoteRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IBoardStateService, BoardStateService>();
        return services;
    }
}
