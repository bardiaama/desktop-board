using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DesktopBoard.Data.SQLite;

/// <summary>
/// Owns the single SQLite connection used by the app. All access goes through
/// <see cref="RunAsync{T}"/>, which serializes work on a background thread so the UI
/// never blocks and SQLite never sees concurrent use of one connection.
/// </summary>
public sealed class SqliteDatabase : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    public SqliteDatabase(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    /// <summary>Opens the connection lazily. Safe to call many times.</summary>
    public SqliteConnection Connection
    {
        get
        {
            if (_connection is not null) return _connection;

            var dir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default,
                Pooling = false
            }.ToString();

            var c = new SqliteConnection(cs);
            c.Open();
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
                cmd.ExecuteNonQuery();
            }
            _connection = c;
            return c;
        }
    }

    public async Task<T> RunAsync<T>(Func<SqliteConnection, T> work)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => work(Connection)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task RunAsync(Action<SqliteConnection> work) => RunAsync<object?>(c => { work(c); return null; });

    /// <summary>Runs work inside a single transaction.</summary>
    public Task<T> RunInTransactionAsync<T>(Func<SqliteConnection, SqliteTransaction, T> work) => RunAsync(c =>
    {
        using var tx = c.BeginTransaction();
        var result = work(c, tx);
        tx.Commit();
        return result;
    });

    public Task RunInTransactionAsync(Action<SqliteConnection, SqliteTransaction> work) =>
        RunInTransactionAsync<object?>((c, t) => { work(c, t); return null; });

    /// <summary>Flushes the WAL into the main file so a file copy is a complete backup.</summary>
    public Task CheckpointAsync() => RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
    });

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        _gate.Dispose();
    }

    // ---- value helpers shared by repositories ----

    public static string ToDb(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    public static string? ToDb(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string? ToDb(TimeOnly? value) => value?.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static DateTime ReadDateTime(SqliteDataReader r, int i) =>
        DateTime.Parse(r.GetString(i), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateOnly? ReadDateOnly(SqliteDataReader r, int i) =>
        r.IsDBNull(i) ? null : DateOnly.ParseExact(r.GetString(i), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static TimeOnly? ReadTimeOnly(SqliteDataReader r, int i) =>
        r.IsDBNull(i) ? null : TimeOnly.ParseExact(r.GetString(i), "HH:mm", CultureInfo.InvariantCulture);

    public static string? ReadString(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    public static int? ReadInt(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);
    public static object DbValue(object? value) => value ?? DBNull.Value;
}
