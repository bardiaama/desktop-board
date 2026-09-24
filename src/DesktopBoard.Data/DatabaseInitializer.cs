using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Data.Migrations;
using DesktopBoard.Data.SQLite;

namespace DesktopBoard.Data;

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly SqliteDatabase _db;
    private readonly IBackupService _backup;
    private readonly ISettingsRepository _settings;

    public DatabaseInitializer(SqliteDatabase db, IBackupService backup, ISettingsRepository settings)
    {
        _db = db;
        _backup = backup;
        _settings = settings;
    }

    public async Task InitializeAsync()
    {
        await _db.RunAsync(c => MigrationRunner.Run(c)).ConfigureAwait(false);

        var seeded = await _settings.GetAsync(SettingKeys.SchemaSeeded).ConfigureAwait(false);
        if (seeded is null)
        {
            var snapshot = SeedData.Create(DateOnly.FromDateTime(DateTime.Now));
            await _backup.RestoreAsync(snapshot).ConfigureAwait(false);
            await _settings.SetAsync(SettingKeys.SchemaSeeded, "1").ConfigureAwait(false);
        }
    }
}
