using System.Globalization;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteAuthStore : IDisposable
{
    private readonly string _connectionString;

    public SqliteAuthStore()
    {
        var folder = ProductPaths.GetLocalAppDataRoot();
        Directory.CreateDirectory(folder);
        _connectionString = $"Data Source={ProductPaths.AuthDatabaseFile()}";
        EnsureSchema();
    }

    internal SqliteAuthStore(string connectionString)
    {
        _connectionString = connectionString;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = CreateConnection();
        SqliteStoreHelpers.Execute(conn, null, @"
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                email TEXT NOT NULL UNIQUE,
                pin_hash TEXT NOT NULL,
                display_name TEXT NOT NULL,
                role TEXT NOT NULL DEFAULT 'Estimator',
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                last_login_at TEXT,
                failed_attempts INTEGER NOT NULL DEFAULT 0,
                locked_until TEXT
            )");

        SqliteStoreHelpers.Execute(conn, null, @"
            CREATE TABLE IF NOT EXISTS auth_admin_audit (
                id TEXT PRIMARY KEY,
                occurred_at TEXT NOT NULL,
                actor_email TEXT NOT NULL,
                action TEXT NOT NULL,
                target_email TEXT NOT NULL,
                details TEXT NOT NULL
            )");
    }

    public UserAccount? FindByEmail(string email)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, pin_hash, display_name, role, is_active, created_at, last_login_at, failed_attempts, locked_until FROM users WHERE email = @email";
        cmd.Parameters.AddWithValue("@email", email);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return MapUser(reader);
    }

    public bool AnyUsersExist()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM users";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public void CreateUser(UserAccount user)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO users (id, email, pin_hash, display_name, role, is_active, created_at)
            VALUES (@id, @email, @pin_hash, @display_name, @role, @is_active, @created_at)";
        cmd.Parameters.AddWithValue("@id", user.Id.ToString());
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@pin_hash", user.PinHash);
        cmd.Parameters.AddWithValue("@display_name", user.DisplayName);
        cmd.Parameters.AddWithValue("@role", user.Role.ToString());
        cmd.Parameters.AddWithValue("@is_active", user.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@created_at", user.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<UserAccount> GetAllUsers()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, email, pin_hash, display_name, role, is_active, created_at, last_login_at, failed_attempts, locked_until
            FROM users
            ORDER BY created_at ASC";

        var users = new List<UserAccount>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            users.Add(MapUser(reader));
        return users;
    }

    public UserAccount? FindById(Guid userId)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, email, pin_hash, display_name, role, is_active, created_at, last_login_at, failed_attempts, locked_until
            FROM users
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;
        return MapUser(reader);
    }

    public bool EmailExists(string email)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM users WHERE email = @email";
        cmd.Parameters.AddWithValue("@email", email);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public bool UpdateUserRole(Guid userId, UserRole role)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE users SET role = @role WHERE id = @id";
        cmd.Parameters.AddWithValue("@role", role.ToString());
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateUserActive(Guid userId, bool isActive)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE users SET is_active = @is_active WHERE id = @id";
        cmd.Parameters.AddWithValue("@is_active", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateUserPin(Guid userId, string pinHash)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users
            SET pin_hash = @pin_hash, failed_attempts = 0, locked_until = NULL
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@pin_hash", pinHash);
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }

    public void RecordFailedAttempt(string email)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users
            SET failed_attempts = failed_attempts + 1,
                locked_until = CASE WHEN failed_attempts + 1 >= 5 THEN datetime('now', '+15 minutes') ELSE locked_until END
            WHERE email = @email";
        cmd.Parameters.AddWithValue("@email", email);
        cmd.ExecuteNonQuery();
    }

    public void ResetFailedAttempts(string email)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE users SET failed_attempts = 0, locked_until = NULL WHERE email = @email";
        cmd.Parameters.AddWithValue("@email", email);
        cmd.ExecuteNonQuery();
    }

    public void UpdateLastLogin(string email)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE users SET last_login_at = @now WHERE email = @email";
        cmd.Parameters.AddWithValue("@now", AppClock.Now.ToString("O"));
        cmd.Parameters.AddWithValue("@email", email);
        cmd.ExecuteNonQuery();
    }

    public bool UnlockUser(Guid userId)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users
            SET failed_attempts = 0, locked_until = NULL
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }

    public void AppendAdminAudit(string actorEmail, string action, string targetEmail, string details)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO auth_admin_audit (id, occurred_at, actor_email, action, target_email, details)
            VALUES (@id, @occurred_at, @actor_email, @action, @target_email, @details)";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@occurred_at", AppClock.Now.ToString("O"));
        cmd.Parameters.AddWithValue("@actor_email", actorEmail ?? "");
        cmd.Parameters.AddWithValue("@action", action);
        cmd.Parameters.AddWithValue("@target_email", targetEmail ?? "");
        cmd.Parameters.AddWithValue("@details", details ?? "");
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AuthAdminAuditEntry> GetRecentAdminAudit(int limit = 50)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, occurred_at, actor_email, action, target_email, details
            FROM auth_admin_audit
            ORDER BY occurred_at DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit));

        var entries = new List<AuthAdminAuditEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new AuthAdminAuditEntry(
                Guid.Parse(reader["id"].ToString()!),
                ParseStoredDate(reader["occurred_at"].ToString()!),
                reader["actor_email"].ToString() ?? "",
                reader["action"].ToString() ?? "",
                reader["target_email"].ToString() ?? "",
                reader["details"].ToString() ?? ""));
        }

        return entries;
    }

    // Las fechas se guardan con ToString("O") (round-trip UTC, ej. "...T10:30:00.0000000Z").
    // DateTime.Parse(string) sin DateTimeStyles.RoundtripKind convierte el "Z" a hora LOCAL en
    // vez de preservar Kind=Utc — CreatedAt/LastLoginAt/LockedUntil quedaban desplazados por el
    // huso horario del equipo (verificado con un test que compara antes/después del fix).
    // CultureInfo.InvariantCulture además evita cualquier ambigüedad de calendario no gregoriano
    // del sistema (ej. th-TH budista) al parsear el string ISO.
    private static DateTime ParseStoredDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static UserAccount MapUser(SqliteDataReader reader)
    {
        var lockedUntilStr = reader["locked_until"] as string;
        return new UserAccount
        {
            Id = Guid.Parse(reader["id"].ToString()!),
            Email = reader["email"].ToString()!,
            PinHash = reader["pin_hash"].ToString()!,
            DisplayName = reader["display_name"].ToString()!,
            Role = Enum.Parse<UserRole>(reader["role"].ToString()!),
            IsActive = Convert.ToInt32(reader["is_active"]) == 1,
            CreatedAt = ParseStoredDate(reader["created_at"].ToString()!),
            LastLoginAt = reader["last_login_at"] is string lastLogin ? ParseStoredDate(lastLogin) : null,
            FailedAttempts = Convert.ToInt32(reader["failed_attempts"]),
            LockedUntil = lockedUntilStr is { Length: > 0 } ? ParseStoredDate(lockedUntilStr) : null
        };
    }

    public void Dispose()
    {
    }
}
