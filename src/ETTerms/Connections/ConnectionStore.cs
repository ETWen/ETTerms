using System.Text.Json;
using Microsoft.Data.Sqlite;
using ETTerms.Infrastructure;

namespace ETTerms.Connections;

/// <summary>
/// 連線 metadata 的 SQLite 持久化（單表 Connections）。
/// 路徑：%LocalAppData%\ETTerms\ettermsdb.sqlite，首次使用自動建表。
/// 密碼不存這裡——只存 CredentialKey，明碼走 CredentialVault。
/// </summary>
public sealed class ConnectionStore
{
    private readonly string _connString;

    private sealed record SettingsBlob(SshSettings? Ssh, SerialSettings? Serial);

    public ConnectionStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETTerms");
        Directory.CreateDirectory(dir);
        _connString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dir, "ettermsdb.sqlite")
        }.ToString();

        using var conn = Open();
        Exec(conn, """
            CREATE TABLE IF NOT EXISTS Connections (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Type INTEGER NOT NULL,
                SortOrder INTEGER NOT NULL,
                GroupName TEXT,
                LastUsedUtc TEXT NOT NULL,
                SettingsJson TEXT NOT NULL,
                CredentialKey TEXT
            );
            """);
        AppLogger.Info("ConnectionStore ready");
    }

    public List<Connection> GetAll()
    {
        var list = new List<Connection>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT Id,Name,Type,SortOrder,GroupName,LastUsedUtc,SettingsJson FROM Connections ORDER BY SortOrder";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var blob = JsonSerializer.Deserialize<SettingsBlob>(r.GetString(6));
            list.Add(new Connection
            {
                Id = Guid.Parse(r.GetString(0)),
                Name = r.GetString(1),
                Type = (ConnectionType)r.GetInt32(2),
                SortOrder = r.GetInt32(3),
                GroupName = r.IsDBNull(4) ? null : r.GetString(4),
                LastUsedUtc = DateTime.Parse(r.GetString(5)),
                Ssh = blob?.Ssh,
                Serial = blob?.Serial
            });
        }
        return list;
    }

    /// <summary>新增或更新一條連線（以 Id 為鍵）。</summary>
    public void Upsert(Connection c)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Connections (Id,Name,Type,SortOrder,GroupName,LastUsedUtc,SettingsJson,CredentialKey)
            VALUES ($id,$name,$type,$sort,$group,$used,$json,$cred)
            ON CONFLICT(Id) DO UPDATE SET
                Name=$name, Type=$type, SortOrder=$sort, GroupName=$group,
                LastUsedUtc=$used, SettingsJson=$json, CredentialKey=$cred;
            """;
        cmd.Parameters.AddWithValue("$id", c.Id.ToString());
        cmd.Parameters.AddWithValue("$name", c.Name);
        cmd.Parameters.AddWithValue("$type", (int)c.Type);
        cmd.Parameters.AddWithValue("$sort", c.SortOrder);
        cmd.Parameters.AddWithValue("$group", (object?)c.GroupName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$used", c.LastUsedUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(new SettingsBlob(c.Ssh, c.Serial)));
        cmd.Parameters.AddWithValue("$cred", c.CredentialKey);
        cmd.ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Connections WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connString);
        c.Open();
        return c;
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
