// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteApuSnapshotRepository : IApuSnapshotRepository
{
	private readonly string _databasePath;

	public SqliteApuSnapshotRepository(string databasePath)
	{
		_databasePath = databasePath ?? throw new ArgumentNullException("databasePath");
	}

	public async Task<ApuVersionSnapshot?> GetByIdAsync(string snapshotId)
	{
		using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
		ProfessionalDbHelpers.EnsureTables(connection);
		using SqliteCommand cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT json FROM professional_apu_snapshots WHERE snapshot_id = $id;";
		cmd.Parameters.AddWithValue("$id", snapshotId);
		return (await cmd.ExecuteScalarAsync() is string json) ? JsonSerializer.Deserialize<ApuVersionSnapshot>(json, ProfessionalDbHelpers.JsonOptions) : null;
	}

	public async Task<IReadOnlyList<ApuVersionSnapshot>> GetByVersionIdAsync(string versionId)
	{
		List<ApuVersionSnapshot> rows = new List<ApuVersionSnapshot>();
		using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
		ProfessionalDbHelpers.EnsureTables(connection);
		using SqliteCommand cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT json FROM professional_apu_snapshots WHERE source_version_id = $vid ORDER BY captured_at DESC;";
		cmd.Parameters.AddWithValue("$vid", versionId);
		using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			ApuVersionSnapshot apuVersionSnapshot = JsonSerializer.Deserialize<ApuVersionSnapshot>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
			if (apuVersionSnapshot != null)
			{
				rows.Add(apuVersionSnapshot);
			}
		}
		return rows.AsReadOnly();
	}

	public async Task SaveAsync(ApuVersionSnapshot snapshot)
	{
		using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
		ProfessionalDbHelpers.EnsureTables(connection);
		using SqliteTransaction transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
		try
		{
			string value = JsonSerializer.Serialize(snapshot, ProfessionalDbHelpers.JsonOptions);
			using SqliteCommand cmd = connection.CreateCommand();
			cmd.Transaction = transaction;
			cmd.CommandText = "INSERT INTO professional_apu_snapshots (snapshot_id, source_version_id, captured_at,\n    version_number, author, captured_by, change_reason, reason, total_direct_cost,\n    total_final_price, currency, component_count, resource_ref_count,\n    economic_concept_count, scenario_count, integrity_hash, json)\nVALUES ($sid, $svid, $cap, $vnum, $author, $cby, $creason, $reason, $tdc, $tfp,\n    $cur, $cc, $rrc, $ecc, $sc, $ihash, $json)\nON CONFLICT(snapshot_id) DO NOTHING;";
			cmd.Parameters.AddWithValue("$sid", snapshot.SnapshotId);
			cmd.Parameters.AddWithValue("$svid", snapshot.SourceVersionId);
			cmd.Parameters.AddWithValue("$cap", snapshot.CapturedAt.ToString("O"));
			cmd.Parameters.AddWithValue("$vnum", snapshot.VersionNumber);
			cmd.Parameters.AddWithValue("$author", snapshot.Author);
			cmd.Parameters.AddWithValue("$cby", snapshot.CapturedBy);
			cmd.Parameters.AddWithValue("$creason", snapshot.ChangeReason ?? "");
			cmd.Parameters.AddWithValue("$reason", snapshot.Reason ?? "");
			cmd.Parameters.AddWithValue("$tdc", snapshot.TotalDirectCost);
			cmd.Parameters.AddWithValue("$tfp", snapshot.TotalFinalPrice);
			cmd.Parameters.AddWithValue("$cur", snapshot.Currency);
			cmd.Parameters.AddWithValue("$cc", snapshot.ComponentCount);
			cmd.Parameters.AddWithValue("$rrc", snapshot.ResourceRefCount);
			cmd.Parameters.AddWithValue("$ecc", snapshot.EconomicConceptCount);
			cmd.Parameters.AddWithValue("$sc", snapshot.ScenarioCount);
			cmd.Parameters.AddWithValue("$ihash", snapshot.IntegrityHash ?? "");
			cmd.Parameters.AddWithValue("$json", value);
			await cmd.ExecuteNonQueryAsync();
			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	public async Task<bool> ExistsAsync(string snapshotId)
	{
		using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
		ProfessionalDbHelpers.EnsureTables(connection);
		using SqliteCommand cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*) FROM professional_apu_snapshots WHERE snapshot_id = $id;";
		cmd.Parameters.AddWithValue("$id", snapshotId);
		return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
	}

	public async Task<ApuVersionSnapshot?> GetLatestByApuIdAsync(string apuId)
	{
		using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
		ProfessionalDbHelpers.EnsureTables(connection);
		using SqliteCommand cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT s.json FROM professional_apu_snapshots s\nINNER JOIN professional_apu_versions v ON s.source_version_id = v.version_id\nWHERE v.apu_id = $aid\nORDER BY s.captured_at DESC LIMIT 1;";
		cmd.Parameters.AddWithValue("$aid", apuId);
		return (await cmd.ExecuteScalarAsync() is string json) ? JsonSerializer.Deserialize<ApuVersionSnapshot>(json, ProfessionalDbHelpers.JsonOptions) : null;
	}
}
