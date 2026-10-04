using System.Data;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace CGive;

public class Data
{
    private static IDbConnection? _db;

    public static void Init()
    {
        // CGive 使用独立 SQLite 存储，不跟随 TShock 的 MySQL 连接。
        _db = new SqliteConnection("Data Source=" + Path.Combine(TShock.SavePath, "CGive.sqlite"));

        var cgiveTable = new SqlTable("CGive",
            new SqlColumn("id", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
            new SqlColumn("executer", MySqlDbType.Text),
            new SqlColumn("cmd", MySqlDbType.Text),
            new SqlColumn("who", MySqlDbType.Text)
        );

        var givenTable = new SqlTable("Given",
            new SqlColumn("name", MySqlDbType.Text),
            new SqlColumn("id", MySqlDbType.Int32)
        );

        var creator = new SqlTableCreator(_db, _db.GetSqlQueryBuilder());
        creator.EnsureTableStructure(cgiveTable);
        creator.EnsureTableStructure(givenTable);
    }

    public static void Command(string cmd, params object[] args)
    {
        _db!.Query(cmd, args);
    }

    public static QueryResult QueryReader(string query, params object[] args)
    {
        return _db!.QueryReader(query, args);
    }
}