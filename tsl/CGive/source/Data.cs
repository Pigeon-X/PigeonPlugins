using Microsoft.Data.Sqlite;
using TShockAPI;

namespace CGive;

public static class Data
{
    private static string _connectionString = string.Empty;

    public static void Init()
    {
        var storage = TShock.Config.GlobalSettings.StorageType ?? "sqlite";
        if (!storage.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("[CGive] 当前 UnifierTSL 适配版仅支持 SQLite。");

        var dbPath = Path.Combine(TShock.SavePath, "CGive.sqlite");
        _connectionString = "Data Source=" + dbPath;

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS CGive (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    executer TEXT,
    cmd TEXT,
    who TEXT
);
CREATE TABLE IF NOT EXISTS Given (
    name TEXT,
    id INTEGER
);";
        cmd.ExecuteNonQuery();
    }

    public static void Command(string sql, params object[] args)
    {
        using var conn = Open();
        using var cmd = CreateCommand(conn, sql, args);
        cmd.ExecuteNonQuery();
    }

    public static QueryResult QueryReader(string sql, params object[] args)
    {
        var conn = Open();
        try
        {
            var cmd = CreateCommand(conn, sql, args);
            var reader = cmd.ExecuteReader();
            return new QueryResult(conn, cmd, reader);
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    private static SqliteConnection Open()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("[CGive] 数据库尚未初始化。");
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static SqliteCommand CreateCommand(SqliteConnection conn, string sql, object[] args)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        for (var i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue("@" + i, args[i] ?? DBNull.Value);
        return cmd;
    }
}

public sealed class QueryResult : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteCommand _command;

    public SqliteDataReader Reader { get; }

    internal QueryResult(SqliteConnection connection, SqliteCommand command, SqliteDataReader reader)
    {
        _connection = connection;
        _command = command;
        Reader = reader;
    }

    public bool Read() => Reader.Read();

    public void Dispose()
    {
        Reader.Dispose();
        _command.Dispose();
        _connection.Dispose();
    }
}