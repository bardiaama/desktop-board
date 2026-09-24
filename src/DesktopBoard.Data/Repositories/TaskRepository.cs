using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Data.SQLite;
using Microsoft.Data.Sqlite;

namespace DesktopBoard.Data.Repositories;

public sealed class TaskRepository : RepositoryBase<TaskItem>, ITaskRepository
{
    public TaskRepository(SqliteDatabase db) : base(db) { }

    protected override string Table => "Tasks";
    protected override string[] Columns { get; } =
        { "Title", "Description", "Category", "Section", "IsCompleted", "Priority", "DueDate", "Time", "Color", "SortOrder" };
    protected override string DefaultOrderBy => "Section, Category, SortOrder, Id";

    protected override TaskItem Read(SqliteDataReader r)
    {
        var t = new TaskItem();
        ReadEntityHeader(r, t);
        t.Title = r.GetString(3);
        t.Description = SqliteDatabase.ReadString(r, 4);
        t.Category = (TaskCategory)r.GetInt32(5);
        t.Section = (Section)r.GetInt32(6);
        t.IsCompleted = r.GetInt32(7) != 0;
        t.Priority = (Priority)r.GetInt32(8);
        t.DueDate = SqliteDatabase.ReadDateOnly(r, 9);
        t.Time = SqliteDatabase.ReadTimeOnly(r, 10);
        t.Color = SqliteDatabase.ReadString(r, 11);
        t.SortOrder = r.GetInt32(12);
        return t;
    }

    protected override void Bind(SqliteCommand cmd, TaskItem t)
    {
        cmd.Parameters.AddWithValue("$c0", t.Title);
        cmd.Parameters.AddWithValue("$c1", SqliteDatabase.DbValue(t.Description));
        cmd.Parameters.AddWithValue("$c2", (int)t.Category);
        cmd.Parameters.AddWithValue("$c3", (int)t.Section);
        cmd.Parameters.AddWithValue("$c4", t.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$c5", (int)t.Priority);
        cmd.Parameters.AddWithValue("$c6", SqliteDatabase.DbValue(SqliteDatabase.ToDb(t.DueDate)));
        cmd.Parameters.AddWithValue("$c7", SqliteDatabase.DbValue(SqliteDatabase.ToDb(t.Time)));
        cmd.Parameters.AddWithValue("$c8", SqliteDatabase.DbValue(t.Color));
        cmd.Parameters.AddWithValue("$c9", t.SortOrder);
    }

    public Task<IReadOnlyList<TaskItem>> GetBySectionAsync(Section section, TaskCategory category) =>
        QueryAsync($"SELECT {SelectList} FROM {Table} WHERE Section = $s AND Category = $c ORDER BY SortOrder, Id",
            ("$s", (int)section), ("$c", (int)category));

    public Task UpdateSortOrderAsync(IEnumerable<(long Id, int SortOrder)> order) => UpdateSortOrderCoreAsync(order);
}

public sealed class GoalRepository : RepositoryBase<Goal>, IGoalRepository
{
    public GoalRepository(SqliteDatabase db) : base(db) { }

    protected override string Table => "Goals";
    protected override string[] Columns { get; } = { "Title", "Section", "IsCompleted", "SortOrder" };
    protected override string DefaultOrderBy => "Section, SortOrder, Id";

    protected override Goal Read(SqliteDataReader r)
    {
        var g = new Goal();
        ReadEntityHeader(r, g);
        g.Title = r.GetString(3);
        g.Section = (Section)r.GetInt32(4);
        g.IsCompleted = r.GetInt32(5) != 0;
        g.SortOrder = r.GetInt32(6);
        return g;
    }

    protected override void Bind(SqliteCommand cmd, Goal g)
    {
        cmd.Parameters.AddWithValue("$c0", g.Title);
        cmd.Parameters.AddWithValue("$c1", (int)g.Section);
        cmd.Parameters.AddWithValue("$c2", g.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$c3", g.SortOrder);
    }

    public Task<IReadOnlyList<Goal>> GetBySectionAsync(Section section) =>
        QueryAsync($"SELECT {SelectList} FROM {Table} WHERE Section = $s ORDER BY SortOrder, Id", ("$s", (int)section));

    public Task UpdateSortOrderAsync(IEnumerable<(long Id, int SortOrder)> order) => UpdateSortOrderCoreAsync(order);
}

public sealed class ProjectRepository : RepositoryBase<Project>, IProjectRepository
{
    public ProjectRepository(SqliteDatabase db) : base(db) { }

    protected override string Table => "Projects";
    protected override string[] Columns { get; } = { "Name", "Description", "Status", "Progress", "Color", "SortOrder" };
    protected override string DefaultOrderBy => "SortOrder, Id";

    protected override Project Read(SqliteDataReader r)
    {
        var p = new Project();
        ReadEntityHeader(r, p);
        p.Name = r.GetString(3);
        p.Description = SqliteDatabase.ReadString(r, 4);
        p.Status = (ProjectStatus)r.GetInt32(5);
        p.Progress = SqliteDatabase.ReadInt(r, 6);
        p.Color = r.GetString(7);
        p.SortOrder = r.GetInt32(8);
        return p;
    }

    protected override void Bind(SqliteCommand cmd, Project p)
    {
        cmd.Parameters.AddWithValue("$c0", p.Name);
        cmd.Parameters.AddWithValue("$c1", SqliteDatabase.DbValue(p.Description));
        cmd.Parameters.AddWithValue("$c2", (int)p.Status);
        cmd.Parameters.AddWithValue("$c3", SqliteDatabase.DbValue(p.Progress));
        cmd.Parameters.AddWithValue("$c4", p.Color);
        cmd.Parameters.AddWithValue("$c5", p.SortOrder);
    }

    public Task UpdateSortOrderAsync(IEnumerable<(long Id, int SortOrder)> order) => UpdateSortOrderCoreAsync(order);
}

public sealed class MeetingRepository : RepositoryBase<Meeting>, IMeetingRepository
{
    public MeetingRepository(SqliteDatabase db) : base(db) { }

    protected override string Table => "Meetings";
    protected override string[] Columns { get; } = { "Title", "Date", "Time", "Color", "Description", "SortOrder" };
    protected override string DefaultOrderBy => "Date, Time, SortOrder, Id";

    protected override Meeting Read(SqliteDataReader r)
    {
        var m = new Meeting();
        ReadEntityHeader(r, m);
        m.Title = r.GetString(3);
        m.Date = SqliteDatabase.ReadDateOnly(r, 4) ?? DateOnly.MinValue;
        m.Time = SqliteDatabase.ReadTimeOnly(r, 5);
        m.Color = r.GetString(6);
        m.Description = SqliteDatabase.ReadString(r, 7);
        m.SortOrder = r.GetInt32(8);
        return m;
    }

    protected override void Bind(SqliteCommand cmd, Meeting m)
    {
        cmd.Parameters.AddWithValue("$c0", m.Title);
        cmd.Parameters.AddWithValue("$c1", SqliteDatabase.ToDb(m.Date)!);
        cmd.Parameters.AddWithValue("$c2", SqliteDatabase.DbValue(SqliteDatabase.ToDb(m.Time)));
        cmd.Parameters.AddWithValue("$c3", m.Color);
        cmd.Parameters.AddWithValue("$c4", SqliteDatabase.DbValue(m.Description));
        cmd.Parameters.AddWithValue("$c5", m.SortOrder);
    }

    public Task<IReadOnlyList<Meeting>> GetUpcomingAsync(DateOnly from, int take) =>
        QueryAsync($"SELECT {SelectList} FROM {Table} WHERE Date >= $d ORDER BY Date, Time, SortOrder, Id LIMIT $n",
            ("$d", SqliteDatabase.ToDb(from)), ("$n", take));
}

public sealed class NoteRepository : RepositoryBase<Note>, INoteRepository
{
    public NoteRepository(SqliteDatabase db) : base(db) { }

    protected override string Table => "Notes";
    protected override string[] Columns { get; } = { "Title", "Content", "Section" };
    protected override string DefaultOrderBy => "Section, Id";

    protected override Note Read(SqliteDataReader r)
    {
        var n = new Note();
        ReadEntityHeader(r, n);
        n.Title = r.GetString(3);
        n.Content = r.GetString(4);
        n.Section = (Section)r.GetInt32(5);
        return n;
    }

    protected override void Bind(SqliteCommand cmd, Note n)
    {
        cmd.Parameters.AddWithValue("$c0", n.Title);
        cmd.Parameters.AddWithValue("$c1", n.Content);
        cmd.Parameters.AddWithValue("$c2", (int)n.Section);
    }

    public async Task<Note?> GetBySectionAsync(Section section)
    {
        var list = await QueryAsync($"SELECT {SelectList} FROM {Table} WHERE Section = $s ORDER BY Id LIMIT 1", ("$s", (int)section)).ConfigureAwait(false);
        return list.Count == 0 ? null : list[0];
    }
}

public sealed class StickyNoteRepository : RepositoryBase<StickyNote>, IStickyNoteRepository
{
    public StickyNoteRepository(SqliteDatabase db) : base(db) { }

    protected override string Table => "StickyNotes";
    protected override string[] Columns { get; } = { "Content", "Section", "Color", "PositionX", "PositionY", "Width", "Height", "Rotation" };
    protected override string DefaultOrderBy => "Section, Id";

    protected override StickyNote Read(SqliteDataReader r)
    {
        var s = new StickyNote();
        ReadEntityHeader(r, s);
        s.Content = r.GetString(3);
        s.Section = (Section)r.GetInt32(4);
        s.Color = r.GetString(5);
        s.PositionX = r.GetDouble(6);
        s.PositionY = r.GetDouble(7);
        s.Width = r.GetDouble(8);
        s.Height = r.GetDouble(9);
        s.Rotation = r.GetDouble(10);
        return s;
    }

    protected override void Bind(SqliteCommand cmd, StickyNote s)
    {
        cmd.Parameters.AddWithValue("$c0", s.Content);
        cmd.Parameters.AddWithValue("$c1", (int)s.Section);
        cmd.Parameters.AddWithValue("$c2", s.Color);
        cmd.Parameters.AddWithValue("$c3", s.PositionX);
        cmd.Parameters.AddWithValue("$c4", s.PositionY);
        cmd.Parameters.AddWithValue("$c5", s.Width);
        cmd.Parameters.AddWithValue("$c6", s.Height);
        cmd.Parameters.AddWithValue("$c7", s.Rotation);
    }

    public Task<IReadOnlyList<StickyNote>> GetBySectionAsync(Section section) =>
        QueryAsync($"SELECT {SelectList} FROM {Table} WHERE Section = $s ORDER BY UpdatedAt, Id", ("$s", (int)section)); // most recently touched note renders on top
}

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly SqliteDatabase _db;
    public SettingsRepository(SqliteDatabase db) => _db = db;

    public Task<string?> GetAsync(string key) => _db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? null : (string)v;
    });

    public Task<IReadOnlyList<AppSetting>> GetAllAsync() => _db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM AppSettings";
        using var r = cmd.ExecuteReader();
        var list = new List<AppSetting>();
        while (r.Read()) list.Add(new AppSetting { Key = r.GetString(0), Value = SqliteDatabase.ReadString(r, 1) });
        return (IReadOnlyList<AppSetting>)list;
    });

    public Task SetAsync(string key, string? value) => _db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ($k, $v) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", SqliteDatabase.DbValue(value));
        cmd.ExecuteNonQuery();
    });

    public Task DeleteAllAsync() => _db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM AppSettings";
        cmd.ExecuteNonQuery();
    });
}
