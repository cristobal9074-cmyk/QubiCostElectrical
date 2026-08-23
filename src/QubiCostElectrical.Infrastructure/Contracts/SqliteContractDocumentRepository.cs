using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteContractDocumentRepository : IContractDocumentRepository
{
	private readonly string _db;

	public SqliteContractDocumentRepository(string databasePath)
	{
		_db = databasePath ?? throw new ArgumentNullException("databasePath");
	}

	public async Task SaveAsync(ContractDocument document, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "\r\n            INSERT INTO contract_documents (id, contract_id, doc_type, json_data)\r\n            VALUES ($id, $cid, $type, $json)\r\n            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, doc_type=excluded.doc_type, json_data=excluded.json_data;";
		cmd.Parameters.AddWithValue("$id", document.Id);
		cmd.Parameters.AddWithValue("$cid", document.ContractId.Value);
		cmd.Parameters.AddWithValue("$type", document.DocumentType);
		cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(document, ProfessionalDbHelpers.JsonOptions));
		await cmd.ExecuteNonQueryAsync(ct);
	}

	public async Task<List<ContractDocument>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "SELECT json_data FROM contract_documents WHERE contract_id=$cid;";
		cmd.Parameters.AddWithValue("$cid", contractId.Value);
		List<ContractDocument> list = new List<ContractDocument>();
		using SqliteDataReader r = await cmd.ExecuteReaderAsync(ct);
		while (await r.ReadAsync(ct))
		{
			ContractDocument? contractDocument = JsonSerializer.Deserialize<ContractDocument>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
			if (contractDocument != null)
			{
				list.Add(contractDocument);
			}
		}
		return list;
	}

	public async Task DeleteAsync(string documentId, CancellationToken ct = default(CancellationToken))
	{
		using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
		using SqliteCommand cmd = c.CreateCommand();
		cmd.CommandText = "DELETE FROM contract_documents WHERE id=$id;";
		cmd.Parameters.AddWithValue("$id", documentId);
		await cmd.ExecuteNonQueryAsync(ct);
	}
}
