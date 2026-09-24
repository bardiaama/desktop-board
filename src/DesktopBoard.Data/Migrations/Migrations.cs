using Microsoft.Data.Sqlite;

namespace DesktopBoard.Data.Migrations;

/// <summary>A forward-only schema step. Version numbers must be unique and increasing.</summary>
public interface IMigration
{
    int Version { get; }
    string Description { get; }
    void Apply(SqliteConnection connection, SqliteTransaction transaction);
}

/// <summary>Version 1: the initial schema.</summary>
public sealed class Migration001Initial : IMigration
{
    public int Version => 1;
    public string Description => "Initial schema";

    public void Apply(SqliteConnection c, SqliteTransaction tx)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Tasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT NULL,
                Category INTEGER NOT NULL DEFAULT 0,
                Section INTEGER NOT NULL DEFAULT 0,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 0,
                DueDate TEXT NULL,
                Time TEXT NULL,
                Color TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Tasks_Section_Category ON Tasks(Section, Category, SortOrder);

            CREATE TABLE IF NOT EXISTS Projects (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                Progress INTEGER NULL,
                Color TEXT NOT NULL DEFAULT '#3B82F6',
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Notes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL DEFAULT '',
                Content TEXT NOT NULL DEFAULT '',
                Section INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS StickyNotes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Content TEXT NOT NULL DEFAULT '',
                Section INTEGER NOT NULL DEFAULT 0,
                Color TEXT NOT NULL DEFAULT 'yellow',
                PositionX REAL NOT NULL DEFAULT 0,
                PositionY REAL NOT NULL DEFAULT 0,
                Width REAL NOT NULL DEFAULT 150,
                Height REAL NOT NULL DEFAULT 90,
                Rotation REAL NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Meetings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Date TEXT NOT NULL,
                Time TEXT NULL,
                Color TEXT NOT NULL DEFAULT '#3B82F6',
                Description TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Meetings_Date ON Meetings(Date, Time);

            CREATE TABLE IF NOT EXISTS Goals (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Section INTEGER NOT NULL DEFAULT 0,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }
}

/// <summary>Applies pending migrations in order and records them in SchemaVersion.</summary>
public sealed class MigrationRunner
{
    public static IReadOnlyList<IMigration> All { get; } = new IMigration[]
    {
        new Migration001Initial(),
    };

    public static int CurrentVersion(SqliteConnection c)
    {
        using var create = c.CreateCommand();
        create.CommandText = "CREATE TABLE IF NOT EXISTS SchemaVersion (Version INTEGER PRIMARY KEY, AppliedAt TEXT NOT NULL, Description TEXT NULL);";
        create.ExecuteNonQuery();

        using var q = c.CreateCommand();
        q.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        return Convert.ToInt32(q.ExecuteScalar());
    }

    /// <summary>Returns the number of migrations applied.</summary>
    public static int Run(SqliteConnection c)
    {
        var current = CurrentVersion(c);
        var applied = 0;
        foreach (var m in All.OrderBy(m => m.Version))
        {
            if (m.Version <= current) continue;
            using var tx = c.BeginTransaction();
            m.Apply(c, tx);
            using (var rec = c.CreateCommand())
            {
                rec.Transaction = tx;
                rec.CommandText = "INSERT INTO SchemaVersion (Version, AppliedAt, Description) VALUES ($v, $at, $d);";
                rec.Parameters.AddWithValue("$v", m.Version);
                rec.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
                rec.Parameters.AddWithValue("$d", m.Description);
                rec.ExecuteNonQuery();
            }
            tx.Commit();
            applied++;
        }
        return applied;
    }
}
