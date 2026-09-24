using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Data.SQLite;
using Microsoft.Data.Sqlite;

namespace DesktopBoard.Data.Repositories;

/// <summary>
/// Shared plumbing for the per-entity repositories. Subclasses provide the table name,
/// the column list, a reader and a parameter binder; everything else is generic.
/// </summary>
public abstract class RepositoryBase<T> : IRepository<T> where T : Entity
{
    protected RepositoryBase(SqliteDatabase db) => Db = db;

    protected SqliteDatabase Db { get; }

    protected abstract string Table { get; }
    /// <summary>Columns other than Id, CreatedAt, UpdatedAt.</summary>
    protected abstract string[] Columns { get; }
    protected abstract string DefaultOrderBy { get; }
    protected abstract T Read(SqliteDataReader r);
    /// <summary>Binds one parameter per entry in <see cref="Columns"/>, named $c0, $c1, ...</summary>
    protected abstract void Bind(SqliteCommand cmd, T entity);

    protected string SelectList => "Id, CreatedAt, UpdatedAt, " + string.Join(", ", Columns);

    public virtual Task<IReadOnlyList<T>> GetAllAsync() => QueryAsync($"SELECT {SelectList} FROM {Table} ORDER BY {DefaultOrderBy}");

    public Task<T?> GetByIdAsync(long id) => Db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {SelectList} FROM {Table} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    });

    public Task<T> AddAsync(T entity) => Db.RunAsync(c =>
    {
        entity.CreatedAt = entity.CreatedAt == default ? DateTime.UtcNow : entity.CreatedAt;
        entity.UpdatedAt = DateTime.UtcNow;
        using var cmd = c.CreateCommand();
        var cols = string.Join(", ", Columns);
        var pars = string.Join(", ", Columns.Select((_, i) => "$c" + i));
        cmd.CommandText = $"INSERT INTO {Table} (CreatedAt, UpdatedAt, {cols}) VALUES ($created, $updated, {pars}); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$created", SqliteDatabase.ToDb(entity.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", SqliteDatabase.ToDb(entity.UpdatedAt));
        Bind(cmd, entity);
        entity.Id = (long)cmd.ExecuteScalar()!;
        return entity;
    });

    public Task UpdateAsync(T entity) => Db.RunAsync(c =>
    {
        entity.UpdatedAt = DateTime.UtcNow;
        using var cmd = c.CreateCommand();
        var sets = string.Join(", ", Columns.Select((col, i) => $"{col} = $c{i}"));
        cmd.CommandText = $"UPDATE {Table} SET UpdatedAt = $updated, {sets} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$updated", SqliteDatabase.ToDb(entity.UpdatedAt));
        cmd.Parameters.AddWithValue("$id", entity.Id);
        Bind(cmd, entity);
        cmd.ExecuteNonQuery();
    });

    public Task DeleteAsync(long id) => Db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"DELETE FROM {Table} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    });

    public Task DeleteAllAsync() => Db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"DELETE FROM {Table}";
        cmd.ExecuteNonQuery();
    });

    protected Task<IReadOnlyList<T>> QueryAsync(string sql, params (string Name, object? Value)[] parameters) => Db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, SqliteDatabase.DbValue(value));
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(Read(r));
        return (IReadOnlyList<T>)list;
    });

    /// <summary>Writes SortOrder for many rows in one transaction.</summary>
    protected Task UpdateSortOrderCoreAsync(IEnumerable<(long Id, int SortOrder)> order) => Db.RunInTransactionAsync((c, tx) =>
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"UPDATE {Table} SET SortOrder = $s WHERE Id = $id";
        var pS = cmd.Parameters.Add("$s", SqliteType.Integer);
        var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
        foreach (var (id, sort) in order)
        {
            pS.Value = sort;
            pId.Value = id;
            cmd.ExecuteNonQuery();
        }
    });

    protected static void ReadEntityHeader(SqliteDataReader r, Entity e)
    {
        e.Id = r.GetInt64(0);
        e.CreatedAt = SqliteDatabase.ReadDateTime(r, 1);
        e.UpdatedAt = SqliteDatabase.ReadDateTime(r, 2);
    }
}
