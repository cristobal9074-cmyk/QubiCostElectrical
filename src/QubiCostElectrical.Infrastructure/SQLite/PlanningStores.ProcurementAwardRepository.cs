using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteSupplierOfferRepository : ISupplierOfferRepository
{
    private readonly string _db;
    public SqliteSupplierOfferRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(SupplierOffer offer, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_offers (id, rfq_id, supplier_id, status, version, updated_utc, json_data)
            VALUES ($id, $rfq, $sid, $st, $ver, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status, version=excluded.version, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", offer.Id.Value);
        cmd.Parameters.AddWithValue("$rfq", offer.RfqId.Value);
        cmd.Parameters.AddWithValue("$sid", offer.SupplierId.Value);
        cmd.Parameters.AddWithValue("$st", (int)offer.Status);
        cmd.Parameters.AddWithValue("$ver", offer.Version);
        cmd.Parameters.AddWithValue("$utc", offer.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(offer, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<SupplierOffer?> GetByIdAsync(SupplierOfferId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_offers WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<SupplierOffer>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<SupplierOffer>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_offers WHERE rfq_id=$rfq ORDER BY version DESC;";
        cmd.Parameters.AddWithValue("$rfq", rfqId.Value);
        var list = new List<SupplierOffer>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<SupplierOffer>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteTechnicalEvaluationRepository : ITechnicalEvaluationRepository
{
    private readonly string _db;
    public SqliteTechnicalEvaluationRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(TechnicalEvaluation evaluation, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_tech_evaluations (id, offer_id, compliance, json_data)
            VALUES ($id, $oid, $comp, $json)
            ON CONFLICT(id) DO UPDATE SET compliance=excluded.compliance, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", evaluation.EvaluationId);
        cmd.Parameters.AddWithValue("$oid", evaluation.OfferId.Value);
        cmd.Parameters.AddWithValue("$comp", (int)evaluation.Compliance);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evaluation, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<TechnicalEvaluation>> GetByOfferAsync(SupplierOfferId offerId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_tech_evaluations WHERE offer_id=$oid;";
        cmd.Parameters.AddWithValue("$oid", offerId.Value);
        return await ReadTech(cmd, ct);
    }

    public async Task<List<TechnicalEvaluation>> GetByRfqOffersAsync(IEnumerable<SupplierOfferId> offerIds, CancellationToken ct = default)
    {
        var ids = offerIds.Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0) return new List<TechnicalEvaluation>();
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data, offer_id FROM procurement_tech_evaluations;";
        var list = new List<TechnicalEvaluation>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            if (!ids.Contains(r.GetString(1))) continue;
            var item = JsonSerializer.Deserialize<TechnicalEvaluation>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }

    private static async Task<List<TechnicalEvaluation>> ReadTech(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<TechnicalEvaluation>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<TechnicalEvaluation>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteCommercialEvaluationRepository : ICommercialEvaluationRepository
{
    private readonly string _db;
    public SqliteCommercialEvaluationRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(CommercialEvaluation evaluation, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_commercial_evaluations (id, offer_id, risk, json_data)
            VALUES ($id, $oid, $risk, $json)
            ON CONFLICT(id) DO UPDATE SET risk=excluded.risk, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", evaluation.EvaluationId);
        cmd.Parameters.AddWithValue("$oid", evaluation.OfferId.Value);
        cmd.Parameters.AddWithValue("$risk", (int)evaluation.Risk);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(evaluation, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<CommercialEvaluation?> GetByOfferAsync(SupplierOfferId offerId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_commercial_evaluations WHERE offer_id=$oid ORDER BY id DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$oid", offerId.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<CommercialEvaluation>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<CommercialEvaluation>> GetByRfqOffersAsync(IEnumerable<SupplierOfferId> offerIds, CancellationToken ct = default)
    {
        var result = new List<CommercialEvaluation>();
        foreach (var id in offerIds)
        {
            var e = await GetByOfferAsync(id, ct);
            if (e is not null) result.Add(e);
        }
        return result;
    }
}

public sealed class SqliteBidComparisonRepository : IBidComparisonRepository
{
    private readonly string _db;
    public SqliteBidComparisonRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(BidComparison comparison, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_bid_comparisons (id, rfq_id, status, base_currency, updated_utc, json_data)
            VALUES ($id, $rfq, $st, $cur, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status, json_data=excluded.json_data, updated_utc=excluded.updated_utc;";
        cmd.Parameters.AddWithValue("$id", comparison.Id.Value);
        cmd.Parameters.AddWithValue("$rfq", comparison.RfqId.Value);
        cmd.Parameters.AddWithValue("$st", (int)comparison.Status);
        cmd.Parameters.AddWithValue("$cur", comparison.BaseCurrency);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(comparison, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<BidComparison?> GetByIdAsync(BidComparisonId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_bid_comparisons WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<BidComparison>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<BidComparison>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_bid_comparisons WHERE rfq_id=$rfq ORDER BY updated_utc DESC;";
        cmd.Parameters.AddWithValue("$rfq", rfqId.Value);
        var list = new List<BidComparison>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<BidComparison>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteAwardRepository : IAwardRepository
{
    private readonly string _db;
    public SqliteAwardRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(AwardDecision award, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_awards (id, rfq_id, status, kind, updated_utc, json_data)
            VALUES ($id, $rfq, $st, $kind, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status, kind=excluded.kind, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", award.Id.Value);
        cmd.Parameters.AddWithValue("$rfq", award.RfqId.Value);
        cmd.Parameters.AddWithValue("$st", (int)award.Status);
        cmd.Parameters.AddWithValue("$kind", (int)award.Kind);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(award, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<AwardDecision?> GetByIdAsync(AwardId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_awards WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<AwardDecision>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<AwardDecision>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_awards WHERE rfq_id=$rfq ORDER BY updated_utc DESC;";
        cmd.Parameters.AddWithValue("$rfq", rfqId.Value);
        var list = new List<AwardDecision>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<AwardDecision>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}
