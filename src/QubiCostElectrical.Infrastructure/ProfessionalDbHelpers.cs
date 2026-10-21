using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

internal static class ProfessionalDbHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    internal static SqliteConnection Open(string databasePath)
    {
        SqliteConnection sqliteConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        sqliteConnection.Open();
        using (SqliteCommand sqliteCommand = sqliteConnection.CreateCommand())
        {
            sqliteCommand.CommandText = "PRAGMA journal_mode=WAL;";
            sqliteCommand.ExecuteNonQuery();
        }
        using (SqliteCommand sqliteCommand2 = sqliteConnection.CreateCommand())
        {
            sqliteCommand2.CommandText = "PRAGMA synchronous=NORMAL;";
            sqliteCommand2.ExecuteNonQuery();
        }
        using (SqliteCommand sqliteCommand3 = sqliteConnection.CreateCommand())
        {
            sqliteCommand3.CommandText = "PRAGMA foreign_keys=ON;";
            sqliteCommand3.ExecuteNonQuery();
        }
        using SqliteCommand sqliteCommand4 = sqliteConnection.CreateCommand();
        sqliteCommand4.CommandText = "PRAGMA busy_timeout = 5000;";
        sqliteCommand4.ExecuteNonQuery();
        return sqliteConnection;
    }

    internal static SqliteTransaction BeginWriteTransaction(SqliteConnection connection)
    {
        return connection.BeginTransaction(IsolationLevel.Serializable);
    }

}
