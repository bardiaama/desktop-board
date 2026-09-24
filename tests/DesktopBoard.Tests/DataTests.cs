using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;
using DesktopBoard.Data;
using DesktopBoard.Data.Migrations;
using DesktopBoard.Data.SQLite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DesktopBoard.Tests;

/// <summary>Spins up a real SQLite database in a temp folder for each test.</summary>
public sealed class DataFixture : IDisposable
{
    public DataFixture()
    {
        Directory.CreateDirectory(Folder);
        var services = new ServiceCollection();
        services.AddDesktopBoardData(DbPath);
        Provider = services.BuildServiceProvider();
    }

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "DesktopBoardTests", Guid.NewGuid().ToString("N"));
    public string DbPath => Path.Combine(Folder, "board.db");
    public ServiceProvider Provider { get; }
    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    public void Dispose()
    {
        Provider.Dispose();
        try { Directory.Delete(Folder, recursive: true); } catch { /* best effort */ }
    }
}

public class MigrationTests : IDisposable
{
    private readonly DataFixture _f = new();
    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task Initialize_creates_schema_and_seeds_once()
    {
        var init = _f.Get<IDatabaseInitializer>();
        await init.InitializeAsync();

        var tasks = await _f.Get<ITaskRepository>().GetAllAsync();
        var projects = await _f.Get<IProjectRepository>().GetAllAsync();
        Assert.NotEmpty(tasks);
        Assert.Equal(4, projects.Count);

        // Second run must not duplicate the sample data.
        await init.InitializeAsync();
        Assert.Equal(projects.Count, (await _f.Get<IProjectRepository>().GetAllAsync()).Count);

        var version = await _f.Get<SqliteDatabase>().RunAsync(MigrationRunner.CurrentVersion);
        Assert.Equal(MigrationRunner.All.Max(m => m.Version), version);
    }
}

public class RepositoryTests : IDisposable
{
    private readonly DataFixture _f = new();
    public void Dispose() => _f.Dispose();

    private async Task<T> Repo<T>() where T : notnull
    {
        await _f.Get<SqliteDatabase>().RunAsync(c => MigrationRunner.Run(c));
        return _f.Get<T>();
    }

    [Fact]
    public async Task Task_roundtrips_unicode_time_and_date()
    {
        var repo = await Repo<ITaskRepository>();
        var t = new TaskItem
        {
            Title = "تکمیل طراحی GSM Module",
            Section = Section.Work,
            Category = TaskCategory.Today,
            Time = new TimeOnly(14, 30),
            DueDate = new DateOnly(2026, 9, 24),
            Priority = Priority.High,
            Color = "#3B82F6"
        };
        await repo.AddAsync(t);
        Assert.True(t.Id > 0);

        var back = await repo.GetByIdAsync(t.Id);
        Assert.NotNull(back);
        Assert.Equal(t.Title, back!.Title);
        Assert.Equal(new TimeOnly(14, 30), back.Time);
        Assert.Equal(new DateOnly(2026, 9, 24), back.DueDate);
        Assert.Equal(Priority.High, back.Priority);
        Assert.Equal("#3B82F6", back.Color);

        back.IsCompleted = true;
        back.Title = "ویرایش شد";
        await repo.UpdateAsync(back);
        var again = await repo.GetByIdAsync(t.Id);
        Assert.True(again!.IsCompleted);
        Assert.Equal("ویرایش شد", again.Title);

        await repo.DeleteAsync(t.Id);
        Assert.Null(await repo.GetByIdAsync(t.Id));
    }

    [Fact]
    public async Task Tasks_are_filtered_by_section_and_category_and_sorted()
    {
        var repo = await Repo<ITaskRepository>();
        await repo.AddAsync(new TaskItem { Title = "b", Section = Section.Work, Category = TaskCategory.Today, SortOrder = 1 });
        await repo.AddAsync(new TaskItem { Title = "a", Section = Section.Work, Category = TaskCategory.Today, SortOrder = 0 });
        await repo.AddAsync(new TaskItem { Title = "x", Section = Section.Personal, Category = TaskCategory.Today });
        await repo.AddAsync(new TaskItem { Title = "y", Section = Section.Work, Category = TaskCategory.Future });

        var work = await repo.GetBySectionAsync(Section.Work, TaskCategory.Today);
        Assert.Equal(new[] { "a", "b" }, work.Select(t => t.Title));

        await repo.UpdateSortOrderAsync(new[] { (work[0].Id, 5), (work[1].Id, 0) });
        var reordered = await repo.GetBySectionAsync(Section.Work, TaskCategory.Today);
        Assert.Equal(new[] { "b", "a" }, reordered.Select(t => t.Title));
    }

    [Fact]
    public async Task Sticky_note_position_roundtrips()
    {
        var repo = await Repo<IStickyNoteRepository>();
        var s = new StickyNote { Content = "ایده", Section = Section.Personal, Color = "pink", PositionX = 12.5, PositionY = 40, Rotation = -2.5 };
        await repo.AddAsync(s);
        var back = (await repo.GetBySectionAsync(Section.Personal)).Single();
        Assert.Equal(12.5, back.PositionX);
        Assert.Equal(40, back.PositionY);
        Assert.Equal(-2.5, back.Rotation);
        Assert.Equal("pink", back.Color);
    }

    [Fact]
    public async Task Settings_upsert()
    {
        var repo = await Repo<ISettingsRepository>();
        await repo.SetAsync("k", "1");
        await repo.SetAsync("k", "2");
        Assert.Equal("2", await repo.GetAsync("k"));
        Assert.Null(await repo.GetAsync("missing"));
    }

    [Fact]
    public async Task Meetings_upcoming_orders_by_date_then_time()
    {
        var repo = await Repo<IMeetingRepository>();
        var today = new DateOnly(2026, 9, 24);
        await repo.AddAsync(new Meeting { Title = "late", Date = today, Time = new TimeOnly(16, 30) });
        await repo.AddAsync(new Meeting { Title = "early", Date = today, Time = new TimeOnly(9, 0) });
        await repo.AddAsync(new Meeting { Title = "yesterday", Date = today.AddDays(-1), Time = new TimeOnly(9, 0) });
        var list = await repo.GetUpcomingAsync(today, 10);
        Assert.Equal(new[] { "early", "late" }, list.Select(m => m.Title));
    }
}

public class BackupTests : IDisposable
{
    private readonly DataFixture _f = new();
    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task Export_then_import_restores_everything()
    {
        await _f.Get<IDatabaseInitializer>().InitializeAsync();
        var backup = _f.Get<IBackupService>();
        var tasks = _f.Get<ITaskRepository>();

        await tasks.AddAsync(new TaskItem { Title = "unique-task-✓", Section = Section.Personal, Category = TaskCategory.Future });
        var before = await backup.CreateSnapshotAsync();

        var file = Path.Combine(_f.Folder, "backup.json");
        await backup.ExportAsync(file);
        Assert.True(File.Exists(file));

        await tasks.DeleteAllAsync();
        Assert.Empty(await tasks.GetAllAsync());

        await backup.ImportAsync(file);
        var after = await backup.CreateSnapshotAsync();
        Assert.Equal(before.Tasks.Count, after.Tasks.Count);
        Assert.Contains(after.Tasks, t => t.Title == "unique-task-✓");
        Assert.Equal(before.Projects.Count, after.Projects.Count);
        Assert.Equal(before.StickyNotes.Count, after.StickyNotes.Count);
        Assert.Equal(before.Notes.Select(n => n.Content), after.Notes.Select(n => n.Content));
    }
}

public class BackupSafetyTests : IDisposable
{
    private readonly DataFixture _f = new();
    public void Dispose() => _f.Dispose();

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"name\":\"not a backup\"}")]
    [InlineData("not json at all")]
    public async Task Import_rejects_files_that_are_not_backups_and_keeps_data(string content)
    {
        await _f.Get<IDatabaseInitializer>().InitializeAsync();
        var tasks = _f.Get<ITaskRepository>();
        var before = (await tasks.GetAllAsync()).Count;
        Assert.True(before > 0);

        var file = Path.Combine(_f.Folder, "bad.json");
        await File.WriteAllTextAsync(file, content);
        await Assert.ThrowsAsync<InvalidDataException>(() => _f.Get<IBackupService>().ImportAsync(file));

        Assert.Equal(before, (await tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Restore_is_atomic_when_a_row_fails()
    {
        await _f.Get<IDatabaseInitializer>().InitializeAsync();
        var tasks = _f.Get<ITaskRepository>();
        var goals = _f.Get<IGoalRepository>();
        var before = await tasks.GetAllAsync();
        var goalsBefore = await goals.GetAllAsync();

        // Tasks are restored before goals; a null goal title violates NOT NULL and must roll everything back.
        var snapshot = new BoardSnapshot();
        snapshot.Tasks.Add(new TaskItem { Title = "replacement", Section = Section.Work });
        snapshot.Goals.Add(new Goal { Title = null!, Section = Section.Work });

        await Assert.ThrowsAnyAsync<Exception>(() => _f.Get<IBackupService>().RestoreAsync(snapshot));

        Assert.Equal(before.Select(t => t.Title), (await tasks.GetAllAsync()).Select(t => t.Title));
        Assert.Equal(goalsBefore.Count, (await goals.GetAllAsync()).Count);
    }

    [Fact]
    public void Parse_accepts_a_real_backup_and_fills_null_strings()
    {
        var json = "{\"FormatVersion\":1,\"Tasks\":[{\"Id\":5,\"Section\":\"Work\",\"Category\":\"Today\"}],\"Projects\":[{\"Name\":null}]}";
        var snapshot = BackupService.Parse(json);
        Assert.Single(snapshot.Tasks);
        Assert.Equal(string.Empty, snapshot.Tasks[0].Title);
        Assert.Equal("#3B82F6", snapshot.Projects[0].Color);
    }
}

public class CoreServiceTests
{
    [Fact]
    public async Task Debouncer_flush_waits_for_an_action_that_is_already_running()
    {
        using var d = new Debouncer(TimeSpan.FromMilliseconds(20));
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var finished = false;
        d.Debounce(async () => { started.SetResult(); await release.Task; finished = true; });
        await started.Task;                 // the action is now in flight and _pending is empty

        var flush = d.FlushAsync();
        await Task.Delay(50);
        Assert.False(flush.IsCompleted);    // flush must wait for the running write
        release.SetResult();
        await flush;
        Assert.True(finished);
    }

    [Fact]
    public async Task Debouncer_runs_only_last_action_after_delay()
    {
        using var d = new Debouncer(TimeSpan.FromMilliseconds(80));
        var calls = new List<int>();
        for (var i = 0; i < 5; i++) { var n = i; d.Debounce(() => { lock (calls) calls.Add(n); return Task.CompletedTask; }); }
        await Task.Delay(300);
        Assert.Equal(new[] { 4 }, calls);
    }

    [Fact]
    public async Task Debouncer_flush_runs_pending_immediately()
    {
        using var d = new Debouncer(TimeSpan.FromSeconds(10));
        var ran = false;
        d.Debounce(() => { ran = true; return Task.CompletedTask; });
        Assert.True(d.HasPending);
        await d.FlushAsync();
        Assert.True(ran);
        Assert.False(d.HasPending);
    }

    [Fact]
    public void BoardState_defaults_locked_and_raises_events()
    {
        var s = new BoardStateService();
        Assert.True(s.IsLocked);
        var events = new List<bool>();
        s.LockStateChanged += (_, locked) => events.Add(locked);
        s.Unlock();
        s.Unlock(); // no duplicate event
        s.Lock();
        Assert.Equal(new[] { false, true }, events);
    }

    [Fact]
    public void SortOrderHelper_returns_only_changed_rows()
    {
        var items = new List<Goal>
        {
            new() { Id = 10, SortOrder = 0 },
            new() { Id = 11, SortOrder = 2 },
            new() { Id = 12, SortOrder = 1 },
        };
        var changed = SortOrderHelper.Reindex(items, g => g.Id, g => g.SortOrder, (g, i) => g.SortOrder = i);
        Assert.Equal(new[] { (11L, 1), (12L, 2) }, changed);
        Assert.Equal(new[] { 0, 1, 2 }, items.Select(g => g.SortOrder));
    }

    [Fact]
    public void Persian_week_starts_on_saturday()
    {
        // 2026-09-24 is a Thursday.
        var thursday = new DateOnly(2026, 9, 24);
        Assert.Equal(DayOfWeek.Thursday, thursday.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 9, 19), SeedData.StartOfPersianWeek(thursday));
        Assert.Equal(new DateOnly(2026, 9, 19), SeedData.StartOfPersianWeek(new DateOnly(2026, 9, 19)));
    }
}
