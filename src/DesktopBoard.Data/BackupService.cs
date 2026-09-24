using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Data.Repositories;
using DesktopBoard.Data.SQLite;
using Microsoft.Data.Sqlite;

namespace DesktopBoard.Data;

/// <summary>
/// JSON package backup. The same <see cref="BoardSnapshot"/> shape is the intended
/// wire format for a future cloud sync, so nothing here is tied to the local file.
/// Restore is atomic: everything is deleted and re-inserted inside one transaction, so a
/// bad file leaves the existing board untouched.
/// </summary>
public sealed class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SqliteDatabase _db;
    private readonly ITaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly INoteRepository _notes;
    private readonly IStickyNoteRepository _stickies;
    private readonly IMeetingRepository _meetings;
    private readonly IGoalRepository _goals;
    private readonly ISettingsRepository _settings;

    public BackupService(SqliteDatabase db, ITaskRepository tasks, IProjectRepository projects, INoteRepository notes,
        IStickyNoteRepository stickies, IMeetingRepository meetings, IGoalRepository goals, ISettingsRepository settings)
    {
        _db = db;
        _tasks = tasks;
        _projects = projects;
        _notes = notes;
        _stickies = stickies;
        _meetings = meetings;
        _goals = goals;
        _settings = settings;
    }

    public async Task<BoardSnapshot> CreateSnapshotAsync()
    {
        var s = new BoardSnapshot { ExportedAt = DateTime.UtcNow };
        s.Tasks.AddRange(await _tasks.GetAllAsync().ConfigureAwait(false));
        s.Projects.AddRange(await _projects.GetAllAsync().ConfigureAwait(false));
        s.Notes.AddRange(await _notes.GetAllAsync().ConfigureAwait(false));
        s.StickyNotes.AddRange(await _stickies.GetAllAsync().ConfigureAwait(false));
        s.Meetings.AddRange(await _meetings.GetAllAsync().ConfigureAwait(false));
        s.Goals.AddRange(await _goals.GetAllAsync().ConfigureAwait(false));
        s.Settings.AddRange((await _settings.GetAllAsync().ConfigureAwait(false)).Where(x => x.Key != SettingKeys.SchemaSeeded));
        return s;
    }

    public async Task ExportAsync(string filePath)
    {
        var snapshot = await CreateSnapshotAsync().ConfigureAwait(false);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var tmp = filePath + ".tmp";
        await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
        File.Move(tmp, filePath, overwrite: true);
    }

    public async Task ImportAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var snapshot = Parse(json);
        await RestoreAsync(snapshot).ConfigureAwait(false);
    }

    /// <summary>Parses and validates a backup document. Throws <see cref="InvalidDataException"/> for anything else.</summary>
    public static BoardSnapshot Parse(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new InvalidDataException("The file is not valid JSON.", ex); }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(nameof(BoardSnapshot.FormatVersion), out var version) ||
                version.ValueKind != JsonValueKind.Number)
                throw new InvalidDataException("The file is not a Desktop Board backup (missing FormatVersion).");

            if (version.GetInt32() > 1)
                throw new InvalidDataException($"Backup format {version.GetInt32()} is newer than this version of Desktop Board supports.");
        }

        var snapshot = JsonSerializer.Deserialize<BoardSnapshot>(json, JsonOptions)
                       ?? throw new InvalidDataException("The backup file is empty.");

        foreach (var t in snapshot.Tasks) t.Title ??= string.Empty;
        foreach (var g in snapshot.Goals) g.Title ??= string.Empty;
        foreach (var p in snapshot.Projects) { p.Name ??= string.Empty; p.Color ??= "#3B82F6"; }
        foreach (var m in snapshot.Meetings) { m.Title ??= string.Empty; m.Color ??= "#3B82F6"; }
        foreach (var n in snapshot.Notes) { n.Title ??= string.Empty; n.Content ??= string.Empty; }
        foreach (var s in snapshot.StickyNotes) { s.Content ??= string.Empty; s.Color ??= "yellow"; }
        return snapshot;
    }

    public Task RestoreAsync(BoardSnapshot snapshot) => _db.RunInTransactionAsync((c, tx) =>
    {
        Replace(c, tx, _tasks, snapshot.Tasks);
        Replace(c, tx, _projects, snapshot.Projects);
        Replace(c, tx, _notes, snapshot.Notes);
        Replace(c, tx, _stickies, snapshot.StickyNotes);
        Replace(c, tx, _meetings, snapshot.Meetings);
        Replace(c, tx, _goals, snapshot.Goals);

        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ($k, $v) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
        var pk = cmd.Parameters.Add("$k", SqliteType.Text);
        var pv = cmd.Parameters.Add("$v", SqliteType.Text);
        foreach (var x in snapshot.Settings)
        {
            if (x.Key == SettingKeys.SchemaSeeded || string.IsNullOrEmpty(x.Key)) continue;
            pk.Value = x.Key;
            pv.Value = SqliteDatabase.DbValue(x.Value);
            cmd.ExecuteNonQuery();
        }
    });

    private static void Replace<T>(SqliteConnection c, SqliteTransaction tx, object repository, List<T> rows) where T : Entity
    {
        if (repository is not IBulkWritable<T> bulk)
            throw new InvalidOperationException($"Repository for {typeof(T).Name} does not support bulk restore.");
        bulk.DeleteAll(c, tx);
        foreach (var row in rows)
        {
            row.Id = 0;
            bulk.Insert(c, tx, row);
        }
    }
}
