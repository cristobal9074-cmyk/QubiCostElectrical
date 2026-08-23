using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteContractRepository : IContractRepository
{
	private readonly string _db;

	public SqliteContractRepository(string databasePath)
	{
		_db = databasePath ?? throw new ArgumentNullException("databasePath");
	}

	public async Task SaveAsync(Contract contract, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "\r\n            INSERT INTO contracts (id, project_path, visible_number, status, updated_utc, json_data)\r\n            VALUES ($id, $proj, $vis, $st, $utc, $json)\r\n            ON CONFLICT(id) DO UPDATE SET project_path=excluded.project_path, visible_number=excluded.visible_number, status=excluded.status, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
		cmd.Parameters.AddWithValue("$id", contract.Id.Value);
		cmd.Parameters.AddWithValue("$proj", contract.ProjectPath);
		cmd.Parameters.AddWithValue("$vis", contract.VisibleNumber);
		cmd.Parameters.AddWithValue("$st", (int)contract.Status);
		cmd.Parameters.AddWithValue("$utc", contract.ModifiedUtc.ToString("O"));
		cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(contract, ProfessionalDbHelpers.JsonOptions));
		await cmd.ExecuteNonQueryAsync(ct);
	}

	public async Task<Contract?> GetByIdAsync(ContractId id, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "SELECT json_data FROM contracts WHERE id=$id;";
		cmd.Parameters.AddWithValue("$id", id.Value);
		return (await cmd.ExecuteScalarAsync(ct) is string json) ? JsonSerializer.Deserialize<Contract>(json, ProfessionalDbHelpers.JsonOptions) : null;
	}

	public async Task<Contract?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "SELECT json_data FROM contracts WHERE lower(visible_number)=lower($vis) LIMIT 1;";
		cmd.Parameters.AddWithValue("$vis", visibleNumber);
		return (await cmd.ExecuteScalarAsync(ct) is string json) ? JsonSerializer.Deserialize<Contract>(json, ProfessionalDbHelpers.JsonOptions) : null;
	}

	public async Task<List<Contract>> SearchAsync(string projectPath, string? text, ContractStatus? status, int skip, int take, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "SELECT json_data FROM contracts\r\n            WHERE project_path=$proj\r\n              AND ($st IS NULL OR status=$st)\r\n              AND ($q='' OR lower(visible_number) LIKE '%'||lower($q)||'%' OR lower(json_data) LIKE '%'||lower($q)||'%')\r\n            ORDER BY visible_number LIMIT $take OFFSET $skip;";
		cmd.Parameters.AddWithValue("$proj", projectPath);
		cmd.Parameters.AddWithValue("$st", status.HasValue ? ((object)(int)status.Value) : DBNull.Value);
		cmd.Parameters.AddWithValue("$q", text ?? string.Empty);
		cmd.Parameters.AddWithValue("$take", (take <= 0) ? 50 : take);
		cmd.Parameters.AddWithValue("$skip", Math.Max(0, skip));
		List<Contract> list = new List<Contract>();
		using SqliteDataReader r = await cmd.ExecuteReaderAsync(ct);
		while (await r.ReadAsync(ct))
		{
			Contract? contract = JsonSerializer.Deserialize<Contract>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
			if (contract != null)
			{
				list.Add(contract);
			}
		}
		return list;
	}

	public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*) FROM contracts WHERE project_path=$proj;";
		cmd.Parameters.AddWithValue("$proj", projectPath);
		return Convert.ToInt32((await cmd.ExecuteScalarAsync(ct)) ?? ((object)0)) + 1;
	}
}
