using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteContractPartyRepository : IContractPartyRepository
{
	private readonly string _db;

	public SqliteContractPartyRepository(string databasePath)
	{
		_db = databasePath ?? throw new ArgumentNullException("databasePath");
	}

	public async Task SaveAsync(ContractParty party, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "\r\n            INSERT INTO contract_parties (id, contract_id, role, json_data)\r\n            VALUES ($id, $cid, $role, $json)\r\n            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, role=excluded.role, json_data=excluded.json_data;";
		cmd.Parameters.AddWithValue("$id", party.Id);
		cmd.Parameters.AddWithValue("$cid", party.ContractId.Value);
		cmd.Parameters.AddWithValue("$role", (int)party.Role);
		cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(party, ProfessionalDbHelpers.JsonOptions));
		await cmd.ExecuteNonQueryAsync(ct);
	}

	public async Task<List<ContractParty>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "SELECT json_data FROM contract_parties WHERE contract_id=$cid;";
		cmd.Parameters.AddWithValue("$cid", contractId.Value);
		List<ContractParty> list = new List<ContractParty>();
		using SqliteDataReader r = await cmd.ExecuteReaderAsync(ct);
		while (await r.ReadAsync(ct))
		{
			ContractParty? contractParty = JsonSerializer.Deserialize<ContractParty>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
			if (contractParty != null)
			{
				list.Add(contractParty);
			}
		}
		return list;
	}

	public async Task DeleteAsync(string partyId, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "DELETE FROM contract_parties WHERE id=$id;";
		cmd.Parameters.AddWithValue("$id", partyId);
		await cmd.ExecuteNonQueryAsync(ct);
	}
}
