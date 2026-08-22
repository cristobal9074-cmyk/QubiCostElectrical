using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Cartera histórica de proyectos cerrados para benchmark de UF/m², compartida entre proyectos
/// (vive en la misma base del banco de precios, igual que el catálogo APU profesional).
/// </summary>
public sealed class SqliteProjectBenchmarkRepository : IProjectBenchmarkRepository
{
    private readonly string databasePath;

    public SqliteProjectBenchmarkRepository(string databasePath)
    {
        this.databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task SaveAsync(ProjectBenchmarkRow row)
    {
        if (row == null) throw new ArgumentNullException(nameof(row));
        if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");

        using SqliteConnection connection = ProfessionalDbHelpers.Open(databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteTransaction transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO professional_project_benchmarks
                    (id, project_name, tipologia, uf_m2_con_equipos, uf_m2_sin_equipos, fecha_cierre, json)
                VALUES ($id, $name, $tipo, $ufcon, $ufsin, $fecha, $json)
                ON CONFLICT(id) DO UPDATE SET
                    project_name = excluded.project_name,
                    tipologia = excluded.tipologia,
                    uf_m2_con_equipos = excluded.uf_m2_con_equipos,
                    uf_m2_sin_equipos = excluded.uf_m2_sin_equipos,
                    fecha_cierre = excluded.fecha_cierre,
                    json = excluded.json;
                """;
            cmd.Parameters.AddWithValue("$id", row.Id);
            cmd.Parameters.AddWithValue("$name", row.ProjectName ?? "");
            cmd.Parameters.AddWithValue("$tipo", row.Tipologia ?? "");
            cmd.Parameters.AddWithValue("$ufcon", row.UfM2ConEquipos);
            cmd.Parameters.AddWithValue("$ufsin", row.UfM2SinEquipos);
            cmd.Parameters.AddWithValue("$fecha", row.FechaCierre.ToString("O"));
            cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(row, ProfessionalDbHelpers.JsonOptions));
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectBenchmarkRow>> GetByTipologiaAsync(string tipologia)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_project_benchmarks WHERE tipologia = $tipo ORDER BY fecha_cierre DESC;";
        cmd.Parameters.AddWithValue("$tipo", tipologia ?? "");
        return await ReadRowsAsync(cmd);
    }

    public async Task<IReadOnlyList<ProjectBenchmarkRow>> GetAllAsync()
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_project_benchmarks ORDER BY fecha_cierre DESC;";
        return await ReadRowsAsync(cmd);
    }

    public async Task<BenchmarkStats> GetStatsAsync(string tipologia)
    {
        var rows = await GetByTipologiaAsync(tipologia);
        if (rows.Count == 0)
            return new BenchmarkStats { Tipologia = tipologia ?? "" };

        decimal sum = 0m, min = decimal.MaxValue, max = decimal.MinValue;
        foreach (var r in rows)
        {
            sum += r.UfM2ConEquipos;
            if (r.UfM2ConEquipos < min) min = r.UfM2ConEquipos;
            if (r.UfM2ConEquipos > max) max = r.UfM2ConEquipos;
        }

        return new BenchmarkStats
        {
            Tipologia = tipologia ?? "",
            Muestras = rows.Count,
            PromedioUfM2 = sum / rows.Count,
            MinUfM2 = min,
            MaxUfM2 = max
        };
    }

    private static async Task<IReadOnlyList<ProjectBenchmarkRow>> ReadRowsAsync(SqliteCommand cmd)
    {
        var list = new List<ProjectBenchmarkRow>();
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = JsonSerializer.Deserialize<ProjectBenchmarkRow>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) list.Add(row);
        }
        return list;
    }
}
