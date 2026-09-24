using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;

namespace DesktopBoard.Data;

/// <summary>
/// JSON package backup. The same <see cref="BoardSnapshot"/> shape is the intended
/// wire format for a future cloud sync, so nothing here is tied to the local file.
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

    private readonly ITaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly INoteRepository _notes;
    private readonly IStickyNoteRepository _stickies;
    private readonly IMeetingRepository _meetings;
    private readonly IGoalRepository _goals;
    private readonly ISettingsRepository _settings;

    public BackupService(ITaskRepository tasks, IProjectRepository projects, INoteRepository notes,
        IStickyNoteRepository stickies, IMeetingRepository meetings, IGoalRepository goals, ISettingsRepository settings)
    {
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
        var snapshot = JsonSerializer.Deserialize<BoardSnapshot>(json, JsonOptions)
                       ?? throw new InvalidDataException("The backup file is empty or not a Desktop Board backup.");
        if (snapshot.FormatVersion > 1)
            throw new InvalidDataException($"Backup format {snapshot.FormatVersion} is newer than this version of Desktop Board supports.");
        await RestoreAsync(snapshot).ConfigureAwait(false);
    }

    public async Task RestoreAsync(BoardSnapshot snapshot)
    {
        await _tasks.DeleteAllAsync().ConfigureAwait(false);
        await _projects.DeleteAllAsync().ConfigureAwait(false);
        await _notes.DeleteAllAsync().ConfigureAwait(false);
        await _stickies.DeleteAllAsync().ConfigureAwait(false);
        await _meetings.DeleteAllAsync().ConfigureAwait(false);
        await _goals.DeleteAllAsync().ConfigureAwait(false);

        foreach (var x in snapshot.Tasks) { x.Id = 0; await _tasks.AddAsync(x).ConfigureAwait(false); }
        foreach (var x in snapshot.Projects) { x.Id = 0; await _projects.AddAsync(x).ConfigureAwait(false); }
        foreach (var x in snapshot.Notes) { x.Id = 0; await _notes.AddAsync(x).ConfigureAwait(false); }
        foreach (var x in snapshot.StickyNotes) { x.Id = 0; await _stickies.AddAsync(x).ConfigureAwait(false); }
        foreach (var x in snapshot.Meetings) { x.Id = 0; await _meetings.AddAsync(x).ConfigureAwait(false); }
        foreach (var x in snapshot.Goals) { x.Id = 0; await _goals.AddAsync(x).ConfigureAwait(false); }
        foreach (var x in snapshot.Settings)
        {
            if (x.Key == SettingKeys.SchemaSeeded) continue;
            await _settings.SetAsync(x.Key, x.Value).ConfigureAwait(false);
        }
    }
}
