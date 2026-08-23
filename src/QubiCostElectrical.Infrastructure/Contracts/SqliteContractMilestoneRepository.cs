using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteContractMilestoneRepository : IContractMilestoneRepository
{
	private readonly string _db;

	public SqliteContractMilestoneRepository(string databasePath)
	{
		_db = databasePath ?? throw new ArgumentNullException("databasePath");
	}

	public async Task SaveAsync(ContractMilestone milestone, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "\r\n            INSERT INTO contract_milestones (id, contract_id, code, json_data)\r\n            VALUES ($id, $cid, $code, $json)\r\n            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, code=excluded.code, json_data=excluded.json_data;";
		cmd.Parameters.AddWithValue("$id", milestone.Id);
		cmd.Parameters.AddWithValue("$cid", milestone.ContractId.Value);
		cmd.Parameters.AddWithValue("$code", milestone.Code);
		cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(milestone, ProfessionalDbHelpers.JsonOptions));
		await cmd.ExecuteNonQueryAsync(ct);
	}

	public async Task<List<ContractMilestone>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "SELECT json_data FROM contract_milestones WHERE contract_id=$cid;";
		cmd.Parameters.AddWithValue("$cid", contractId.Value);
		List<ContractMilestone> list = new List<ContractMilestone>();
		using SqliteDataReader r = await cmd.ExecuteReaderAsync(ct);
		while (await r.ReadAsync(ct))
		{
			ContractMilestone? contractMilestone = JsonSerializer.Deserialize<ContractMilestone>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
			if (contractMilestone != null)
			{
				list.Add(contractMilestone);
			}
		}
		return list;
	}

	public async Task DeleteAsync(string milestoneId, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "DELETE FROM contract_milestones WHERE id=$id;";
		cmd.Parameters.AddWithValue("$id", milestoneId);
		await cmd.ExecuteNonQueryAsync(ct);
	}
}
