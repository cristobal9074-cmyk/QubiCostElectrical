using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteCalendarRepository : ICalendarRepository
{
    private readonly string _databasePath;

    public SqliteCalendarRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<Calendar?> GetByIdAsync(CalendarId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_calendars WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<Calendar>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<Calendar>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = new List<Calendar>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_calendars ORDER BY name;";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<Calendar>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<List<Calendar>> GetByProjectAsync(string projectId, CancellationToken ct = default)
    {
        return await GetAllAsync(ct);
    }

    public async Task SaveAsync(Calendar calendar, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(calendar, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO planning_calendars (id, code, name, description, time_zone_id, is_active,
                    effective_date, author, modified_at, row_version, calendar_type, json)
                VALUES ($id, $code, $name, $desc, $tz, $active, $edate, $author, $mat, $rver, $ctype, $json)
                ON CONFLICT(id) DO UPDATE SET
                    code=excluded.code, name=excluded.name, description=excluded.description,
                    time_zone_id=excluded.time_zone_id, is_active=excluded.is_active,
                    effective_date=excluded.effective_date, author=excluded.author,
                    modified_at=excluded.modified_at, row_version=excluded.row_version,
                    calendar_type=excluded.calendar_type, json=excluded.json;
                """;
            cmd.Parameters.AddWithValue("$id", calendar.Id.Value);
            cmd.Parameters.AddWithValue("$code", calendar.Code);
            cmd.Parameters.AddWithValue("$name", calendar.Name);
            cmd.Parameters.AddWithValue("$desc", calendar.Description);
            cmd.Parameters.AddWithValue("$tz", calendar.TimeZoneId);
            cmd.Parameters.AddWithValue("$active", calendar.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("$edate", calendar.EffectiveDate?.ToString("O") ?? "");
            cmd.Parameters.AddWithValue("$author", calendar.Author);
            cmd.Parameters.AddWithValue("$mat", calendar.ModifiedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$rver", calendar.RowVersion);
            cmd.Parameters.AddWithValue("$ctype", calendar.CalendarType.ToString());
            cmd.Parameters.AddWithValue("$json", json);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(CalendarId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM planning_calendars WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> ExistsAsync(CalendarId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM planning_calendars WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }
}
