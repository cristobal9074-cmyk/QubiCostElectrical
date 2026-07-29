using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqlitePaymentCertificateTable
{
    internal const string Certificates = "payment_certificates";
    internal const string Lines = "payment_certificate_lines";
    internal const string Revisions = "payment_certificate_revisions";
    internal const string Audit = "payment_certificate_audit";
    internal const string Advances = "contract_advances";
    internal const string AdvanceAmortizations = "contract_advance_amortizations";
    internal const string Retentions = "contract_retentions";
    internal const string RetentionApplications = "contract_retention_applications";
    internal const string RetentionReleases = "contract_retention_releases";
    internal const string Observations = "contract_payment_observations";
    internal const string Snapshots = "contract_payment_snapshots";
}

public sealed class SqlitePaymentCertificateRepository : IPaymentCertificateRepository
{
    private readonly string _db;
    public SqlitePaymentCertificateRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(PaymentCertificate certificate, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO payment_certificates (
                id, contract_id, visible_number, revision_number, period_start, period_end,
                cut_off_date, presentation_date, currency, exchange_rate, status,
                presenter, revisor, aprobador, approved_at_utc, comment,
                subtotal_valorizado, reajuste, anticipo_amortizado, retenciones_aplicadas,
                descuentos, multas, impuestos, monto_bruto, monto_liquido,
                monto_anterior, monto_acumulado, saldo_contractual, saldo_por_valorizar,
                row_version, created_utc, created_by, modified_utc, modified_by,
                hash, compatibility_metadata, json_data)
            VALUES (
                $id, $cid, $vis, $rev, $ps, $pe,
                $cod, $pd, $cur, $er, $st,
                $pre, $revr, $apr, $aa, $com,
                $sv, $reaj, $aa_amt, $ra,
                $desc, $mult, $imp, $mb, $ml,
                $ma, $mac, $sc, $spv,
                $rv, $cu, $cb, $mu, $mb2,
                $hash, $cm, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, visible_number=excluded.visible_number,
                revision_number=excluded.revision_number, period_start=excluded.period_start,
                period_end=excluded.period_end, cut_off_date=excluded.cut_off_date,
                presentation_date=excluded.presentation_date, currency=excluded.currency,
                exchange_rate=excluded.exchange_rate, status=excluded.status,
                presenter=excluded.presenter, revisor=excluded.revisor,
                aprobador=excluded.aprobador, approved_at_utc=excluded.approved_at_utc,
                comment=excluded.comment,
                subtotal_valorizado=excluded.subtotal_valorizado, reajuste=excluded.reajuste,
                anticipo_amortizado=excluded.anticipo_amortizado, retenciones_aplicadas=excluded.retenciones_aplicadas,
                descuentos=excluded.descuentos, multas=excluded.multas,
                impuestos=excluded.impuestos, monto_bruto=excluded.monto_bruto,
                monto_liquido=excluded.monto_liquido,
                monto_anterior=excluded.monto_anterior, monto_acumulado=excluded.monto_acumulado,
                saldo_contractual=excluded.saldo_contractual, saldo_por_valorizar=excluded.saldo_por_valorizar,
                row_version=excluded.row_version, modified_utc=excluded.modified_utc,
                modified_by=excluded.modified_by,
                hash=excluded.hash, compatibility_metadata=excluded.compatibility_metadata,
                json_data=excluded.json_data;";
        BindCertificateParams(cmd, certificate);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PaymentCertificate?> GetByIdAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM payment_certificates WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<PaymentCertificate>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<PaymentCertificate?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM payment_certificates WHERE lower(visible_number)=lower($vis) LIMIT 1;";
        cmd.Parameters.AddWithValue("$vis", visibleNumber);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<PaymentCertificate>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<PaymentCertificate>> SearchAsync(string projectPath, string? text, PaymentCertificateStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT pc.json_data FROM payment_certificates pc
            INNER JOIN contracts c ON pc.contract_id = c.id
            WHERE c.project_path=$proj
              AND ($st IS NULL OR pc.status=$st)
              AND ($q='' OR lower(pc.visible_number) LIKE '%'||lower($q)||'%' OR lower(pc.json_data) LIKE '%'||lower($q)||'%')
            ORDER BY pc.created_utc DESC LIMIT $take OFFSET $skip;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        cmd.Parameters.AddWithValue("$st", status.HasValue ? (object)(int)status.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$q", text ?? string.Empty);
        cmd.Parameters.AddWithValue("$take", take <= 0 ? 50 : take);
        cmd.Parameters.AddWithValue("$skip", Math.Max(0, skip));
        var list = new List<PaymentCertificate>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var cert = JsonSerializer.Deserialize<PaymentCertificate>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (cert is not null) list.Add(cert);
        }
        return list;
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM payment_certificates pc
            INNER JOIN contracts c ON pc.contract_id = c.id
            WHERE c.project_path=$proj;";
        cmd.Parameters.AddWithValue("$proj", projectPath);
        var count = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(count ?? 0) + 1;
    }

    public async Task<List<PaymentCertificate>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM payment_certificates WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<PaymentCertificate>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var cert = JsonSerializer.Deserialize<PaymentCertificate>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (cert is not null) list.Add(cert);
        }
        return list;
    }

    private static void BindCertificateParams(SqliteCommand cmd, PaymentCertificate c)
    {
        cmd.Parameters.AddWithValue("$id", c.Id.Value);
        cmd.Parameters.AddWithValue("$cid", c.ContractId.Value);
        cmd.Parameters.AddWithValue("$vis", c.VisibleNumber);
        cmd.Parameters.AddWithValue("$rev", c.RevisionNumber);
        cmd.Parameters.AddWithValue("$ps", c.PeriodStart.ToString("O"));
        cmd.Parameters.AddWithValue("$pe", c.PeriodEnd.ToString("O"));
        cmd.Parameters.AddWithValue("$cod", c.CutOffDate.ToString("O"));
        cmd.Parameters.AddWithValue("$pd", c.PresentationDate.ToString("O"));
        cmd.Parameters.AddWithValue("$cur", c.Currency);
        cmd.Parameters.AddWithValue("$er", c.ExchangeRate);
        cmd.Parameters.AddWithValue("$st", (int)c.Status);
        cmd.Parameters.AddWithValue("$pre", c.Presenter);
        cmd.Parameters.AddWithValue("$revr", c.Revisor);
        cmd.Parameters.AddWithValue("$apr", c.Aprobador);
        cmd.Parameters.AddWithValue("$aa", c.ApprovedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$com", c.Comment);
        cmd.Parameters.AddWithValue("$sv", c.SubtotalValorizado);
        cmd.Parameters.AddWithValue("$reaj", c.Reajuste);
        cmd.Parameters.AddWithValue("$aa_amt", c.AnticipoAmortizado);
        cmd.Parameters.AddWithValue("$ra", c.RetencionesAplicadas);
        cmd.Parameters.AddWithValue("$desc", c.Descuentos);
        cmd.Parameters.AddWithValue("$mult", c.Multas);
        cmd.Parameters.AddWithValue("$imp", c.Impuestos);
        cmd.Parameters.AddWithValue("$mb", c.MontoBruto);
        cmd.Parameters.AddWithValue("$ml", c.MontoLiquido);
        cmd.Parameters.AddWithValue("$ma", c.MontoAnterior);
        cmd.Parameters.AddWithValue("$mac", c.MontoAcumulado);
        cmd.Parameters.AddWithValue("$sc", c.SaldoContractual);
        cmd.Parameters.AddWithValue("$spv", c.SaldoPorValorizar);
        cmd.Parameters.AddWithValue("$rv", c.RowVersion);
        cmd.Parameters.AddWithValue("$cu", c.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$cb", c.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", c.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb2", c.ModifiedBy);
        cmd.Parameters.AddWithValue("$hash", c.Hash);
        cmd.Parameters.AddWithValue("$cm", c.CompatibilityMetadata);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(c, ProfessionalDbHelpers.JsonOptions));
    }
}

public sealed class SqlitePaymentCertificateLineRepository : IPaymentCertificateLineRepository
{
    private readonly string _db;
    public SqlitePaymentCertificateLineRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(PaymentCertificateLine line, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO payment_certificate_lines (
                id, payment_certificate_id, partida_contractual, wbs_code, activity_id,
                apu_id, description, unit,
                cantidad_contractual, precio_unitario_contractual,
                cantidad_anterior, cantidad_periodo, cantidad_acumulada, saldo_cantidad,
                monto_anterior, monto_periodo, monto_acumulado, saldo_monetario,
                origen_avance, evidencia, observacion, status, row_version, json_data)
            VALUES (
                $id, $pcid, $pc, $wbs, $aid,
                $apid, $desc, $unit,
                $cc, $puc,
                $ca, $cp, $cac, $sc,
                $ma, $mp, $mac, $sm,
                $oa, $ev, $obs, $st, $rv, $json)
            ON CONFLICT(id) DO UPDATE SET
                payment_certificate_id=excluded.payment_certificate_id,
                partida_contractual=excluded.partida_contractual, wbs_code=excluded.wbs_code,
                activity_id=excluded.activity_id, apu_id=excluded.apu_id,
                description=excluded.description, unit=excluded.unit,
                cantidad_contractual=excluded.cantidad_contractual,
                precio_unitario_contractual=excluded.precio_unitario_contractual,
                cantidad_anterior=excluded.cantidad_anterior,
                cantidad_periodo=excluded.cantidad_periodo,
                cantidad_acumulada=excluded.cantidad_acumulada,
                saldo_cantidad=excluded.saldo_cantidad,
                monto_anterior=excluded.monto_anterior,
                monto_periodo=excluded.monto_periodo,
                monto_acumulado=excluded.monto_acumulado,
                saldo_monetario=excluded.saldo_monetario,
                origen_avance=excluded.origen_avance, evidencia=excluded.evidencia,
                observacion=excluded.observacion, status=excluded.status,
                row_version=excluded.row_version, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", line.Id.Value);
        cmd.Parameters.AddWithValue("$pcid", line.PaymentCertificateId.Value);
        cmd.Parameters.AddWithValue("$pc", line.PartidaContractual);
        cmd.Parameters.AddWithValue("$wbs", line.WbsCode);
        cmd.Parameters.AddWithValue("$aid", line.ActivityId);
        cmd.Parameters.AddWithValue("$apid", (object?)line.ApuId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$desc", line.Description);
        cmd.Parameters.AddWithValue("$unit", line.Unit);
        cmd.Parameters.AddWithValue("$cc", line.CantidadContractual);
        cmd.Parameters.AddWithValue("$puc", line.PrecioUnitarioContractual);
        cmd.Parameters.AddWithValue("$ca", line.CantidadAnterior);
        cmd.Parameters.AddWithValue("$cp", line.CantidadPeriodo);
        cmd.Parameters.AddWithValue("$cac", line.CantidadAcumulada);
        cmd.Parameters.AddWithValue("$sc", line.SaldoCantidad);
        cmd.Parameters.AddWithValue("$ma", line.MontoAnterior);
        cmd.Parameters.AddWithValue("$mp", line.MontoPeriodo);
        cmd.Parameters.AddWithValue("$mac", line.MontoAcumulado);
        cmd.Parameters.AddWithValue("$sm", line.SaldoMonetario);
        cmd.Parameters.AddWithValue("$oa", line.OrigenAvance);
        cmd.Parameters.AddWithValue("$ev", line.Evidencia);
        cmd.Parameters.AddWithValue("$obs", line.Observacion);
        cmd.Parameters.AddWithValue("$st", line.Status);
        cmd.Parameters.AddWithValue("$rv", line.RowVersion);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(line, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveRangeAsync(IEnumerable<PaymentCertificateLine> lines, CancellationToken ct = default)
    {
        foreach (var line in lines)
            await SaveAsync(line, ct);
    }

    public async Task<List<PaymentCertificateLine>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM payment_certificate_lines WHERE payment_certificate_id=$id ORDER BY partida_contractual;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var list = new List<PaymentCertificateLine>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var line = JsonSerializer.Deserialize<PaymentCertificateLine>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (line is not null) list.Add(line);
        }
        return list;
    }

    public async Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM payment_certificate_lines WHERE payment_certificate_id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteContractAdvanceRepository : IContractAdvanceRepository
{
    private readonly string _db;
    public SqliteContractAdvanceRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractAdvance advance, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_advances (
                id, contract_id, visible_number, amount, currency, date,
                amortization_percentage, initial_balance, current_balance, total_amortized,
                status, document_reference, comment, created_by, created_utc, advance_type, json_data)
            VALUES (
                $id, $cid, $vis, $amt, $cur, $dt,
                $ap, $ib, $cb, $ta,
                $st, $dr, $com, $crby, $crutc, $at, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, visible_number=excluded.visible_number,
                amount=excluded.amount, currency=excluded.currency, date=excluded.date,
                amortization_percentage=excluded.amortization_percentage,
                initial_balance=excluded.initial_balance, current_balance=excluded.current_balance,
                total_amortized=excluded.total_amortized,
                status=excluded.status, document_reference=excluded.document_reference,
                comment=excluded.comment, advance_type=excluded.advance_type,
                json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", advance.Id.Value);
        cmd.Parameters.AddWithValue("$cid", advance.ContractId.Value);
        cmd.Parameters.AddWithValue("$vis", advance.VisibleNumber);
        cmd.Parameters.AddWithValue("$amt", advance.Amount);
        cmd.Parameters.AddWithValue("$cur", advance.Currency);
        cmd.Parameters.AddWithValue("$dt", advance.Date.ToString("O"));
        cmd.Parameters.AddWithValue("$ap", advance.AmortizationPercentage);
        cmd.Parameters.AddWithValue("$ib", advance.InitialBalance);
        cmd.Parameters.AddWithValue("$cb", advance.CurrentBalance);
        cmd.Parameters.AddWithValue("$ta", advance.TotalAmortized);
        cmd.Parameters.AddWithValue("$st", advance.Status);
        cmd.Parameters.AddWithValue("$dr", advance.DocumentReference);
        cmd.Parameters.AddWithValue("$com", advance.Comment);
        cmd.Parameters.AddWithValue("$crby", advance.CreatedBy);
        cmd.Parameters.AddWithValue("$crutc", advance.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$at", (int)advance.AdvanceType);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(advance, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractAdvance?> GetByIdAsync(ContractAdvanceId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_advances WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractAdvance>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractAdvance>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_advances WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<ContractAdvance>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var adv = JsonSerializer.Deserialize<ContractAdvance>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (adv is not null) list.Add(adv);
        }
        return list;
    }
}

public sealed class SqliteAdvanceAmortizationRepository : IAdvanceAmortizationRepository
{
    private readonly string _db;
    public SqliteAdvanceAmortizationRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(AdvanceAmortization amortization, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_advance_amortizations (
                id, payment_certificate_id, contract_advance_id, amount, percentage,
                pre_amortization_balance, post_amortization_balance)
            VALUES ($id, $pcid, $caid, $amt, $pct, $pre, $post)
            ON CONFLICT(id) DO UPDATE SET
                payment_certificate_id=excluded.payment_certificate_id,
                contract_advance_id=excluded.contract_advance_id,
                amount=excluded.amount, percentage=excluded.percentage,
                pre_amortization_balance=excluded.pre_amortization_balance,
                post_amortization_balance=excluded.post_amortization_balance;";
        cmd.Parameters.AddWithValue("$id", amortization.Id);
        cmd.Parameters.AddWithValue("$pcid", amortization.PaymentCertificateId.Value);
        cmd.Parameters.AddWithValue("$caid", amortization.ContractAdvanceId.Value);
        cmd.Parameters.AddWithValue("$amt", amortization.Amount);
        cmd.Parameters.AddWithValue("$pct", amortization.Percentage);
        cmd.Parameters.AddWithValue("$pre", amortization.PreAmortizationBalance);
        cmd.Parameters.AddWithValue("$post", amortization.PostAmortizationBalance);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveRangeAsync(IEnumerable<AdvanceAmortization> amortizations, CancellationToken ct = default)
    {
        foreach (var a in amortizations)
            await SaveAsync(a, ct);
    }

    public async Task<List<AdvanceAmortization>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM contract_advance_amortizations WHERE payment_certificate_id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var list = new List<AdvanceAmortization>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new AdvanceAmortization
            {
                Id = r.GetString(0),
                PaymentCertificateId = PaymentCertificateId.From(r.GetString(1)),
                ContractAdvanceId = ContractAdvanceId.From(r.GetString(2)),
                Amount = (decimal)r.GetDouble(3),
                Percentage = (decimal)r.GetDouble(4),
                PreAmortizationBalance = (decimal)r.GetDouble(5),
                PostAmortizationBalance = (decimal)r.GetDouble(6)
            });
        }
        return list;
    }

    public async Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM contract_advance_amortizations WHERE payment_certificate_id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteContractRetentionRepository : IContractRetentionRepository
{
    private readonly string _db;
    public SqliteContractRetentionRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractRetention retention, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_retentions (
                id, contract_id, retention_type, base_type, percentage,
                fixed_amount, limit_amount, expiry_date, release_condition,
                status, observation, json_data)
            VALUES (
                $id, $cid, $rt, $bt, $pct,
                $fa, $la, $ed, $rc,
                $st, $obs, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, retention_type=excluded.retention_type,
                base_type=excluded.base_type, percentage=excluded.percentage,
                fixed_amount=excluded.fixed_amount, limit_amount=excluded.limit_amount,
                expiry_date=excluded.expiry_date, release_condition=excluded.release_condition,
                status=excluded.status, observation=excluded.observation,
                json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", retention.Id.Value);
        cmd.Parameters.AddWithValue("$cid", retention.ContractId.Value);
        cmd.Parameters.AddWithValue("$rt", (int)retention.RetentionType);
        cmd.Parameters.AddWithValue("$bt", retention.BaseType);
        cmd.Parameters.AddWithValue("$pct", retention.Percentage);
        cmd.Parameters.AddWithValue("$fa", retention.FixedAmount);
        cmd.Parameters.AddWithValue("$la", retention.LimitAmount);
        cmd.Parameters.AddWithValue("$ed", retention.ExpiryDate?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$rc", retention.ReleaseCondition);
        cmd.Parameters.AddWithValue("$st", retention.Status);
        cmd.Parameters.AddWithValue("$obs", retention.Observation);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(retention, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractRetention?> GetByIdAsync(ContractRetentionId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_retentions WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractRetention>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractRetention>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_retentions WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        var list = new List<ContractRetention>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var ret = JsonSerializer.Deserialize<ContractRetention>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (ret is not null) list.Add(ret);
        }
        return list;
    }
}

public sealed class SqliteRetentionApplicationRepository : IRetentionApplicationRepository
{
    private readonly string _db;
    public SqliteRetentionApplicationRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(RetentionApplication application, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_retention_applications (
                id, payment_certificate_id, contract_retention_id, base_amount, percentage,
                applied_amount, cumulative_applied, released_amount, pending_balance)
            VALUES ($id, $pcid, $crid, $ba, $pct, $aa, $ca, $ra, $pb)
            ON CONFLICT(id) DO UPDATE SET
                payment_certificate_id=excluded.payment_certificate_id,
                contract_retention_id=excluded.contract_retention_id,
                base_amount=excluded.base_amount, percentage=excluded.percentage,
                applied_amount=excluded.applied_amount,
                cumulative_applied=excluded.cumulative_applied,
                released_amount=excluded.released_amount,
                pending_balance=excluded.pending_balance;";
        cmd.Parameters.AddWithValue("$id", application.Id);
        cmd.Parameters.AddWithValue("$pcid", application.PaymentCertificateId.Value);
        cmd.Parameters.AddWithValue("$crid", application.ContractRetentionId.Value);
        cmd.Parameters.AddWithValue("$ba", application.BaseAmount);
        cmd.Parameters.AddWithValue("$pct", application.Percentage);
        cmd.Parameters.AddWithValue("$aa", application.AppliedAmount);
        cmd.Parameters.AddWithValue("$ca", application.CumulativeApplied);
        cmd.Parameters.AddWithValue("$ra", application.ReleasedAmount);
        cmd.Parameters.AddWithValue("$pb", application.PendingBalance);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveRangeAsync(IEnumerable<RetentionApplication> applications, CancellationToken ct = default)
    {
        foreach (var a in applications)
            await SaveAsync(a, ct);
    }

    public async Task<List<RetentionApplication>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM contract_retention_applications WHERE payment_certificate_id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var list = new List<RetentionApplication>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new RetentionApplication
            {
                Id = r.GetString(0),
                PaymentCertificateId = PaymentCertificateId.From(r.GetString(1)),
                ContractRetentionId = ContractRetentionId.From(r.GetString(2)),
                BaseAmount = (decimal)r.GetDouble(3),
                Percentage = (decimal)r.GetDouble(4),
                AppliedAmount = (decimal)r.GetDouble(5),
                CumulativeApplied = (decimal)r.GetDouble(6),
                ReleasedAmount = (decimal)r.GetDouble(7),
                PendingBalance = (decimal)r.GetDouble(8)
            });
        }
        return list;
    }

    public async Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM contract_retention_applications WHERE payment_certificate_id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SqliteRetentionReleaseRepository : IRetentionReleaseRepository
{
    private readonly string _db;
    public SqliteRetentionReleaseRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(RetentionRelease release, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_retention_releases (
                id, contract_retention_id, payment_certificate_id, released_amount, release_date, authorized_by, comment)
            VALUES ($id, $crid, $pcid, $amt, $dt, $by, $com)
            ON CONFLICT(id) DO UPDATE SET
                contract_retention_id=excluded.contract_retention_id,
                payment_certificate_id=excluded.payment_certificate_id,
                released_amount=excluded.released_amount, release_date=excluded.release_date,
                authorized_by=excluded.authorized_by, comment=excluded.comment;";
        cmd.Parameters.AddWithValue("$id", release.Id);
        cmd.Parameters.AddWithValue("$crid", release.ContractRetentionId.Value);
        cmd.Parameters.AddWithValue("$pcid", (object?)release.PaymentCertificateId?.Value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$amt", release.ReleasedAmount);
        cmd.Parameters.AddWithValue("$dt", release.ReleaseDate.ToString("O"));
        cmd.Parameters.AddWithValue("$by", release.AuthorizedBy);
        cmd.Parameters.AddWithValue("$com", release.Comment);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<RetentionRelease>> GetByRetentionAsync(ContractRetentionId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM contract_retention_releases WHERE contract_retention_id=$id ORDER BY release_date;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var list = new List<RetentionRelease>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new RetentionRelease
            {
                Id = r.GetString(0),
                ContractRetentionId = ContractRetentionId.From(r.GetString(1)),
                PaymentCertificateId = r.IsDBNull(2) ? null : PaymentCertificateId.From(r.GetString(2)),
                ReleasedAmount = (decimal)r.GetDouble(3),
                ReleaseDate = DateTime.Parse(r.GetString(4)),
                AuthorizedBy = r.GetString(5),
                Comment = r.GetString(6)
            });
        }
        return list;
    }

    public async Task<List<RetentionRelease>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM contract_retention_releases WHERE payment_certificate_id=$id ORDER BY release_date;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var list = new List<RetentionRelease>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new RetentionRelease
            {
                Id = r.GetString(0),
                ContractRetentionId = ContractRetentionId.From(r.GetString(1)),
                PaymentCertificateId = r.IsDBNull(2) ? null : PaymentCertificateId.From(r.GetString(2)),
                ReleasedAmount = (decimal)r.GetDouble(3),
                ReleaseDate = DateTime.Parse(r.GetString(4)),
                AuthorizedBy = r.GetString(5),
                Comment = r.GetString(6)
            });
        }
        return list;
    }
}

public sealed class SqlitePaymentObservationRepository : IPaymentObservationRepository
{
    private readonly string _db;
    public SqlitePaymentObservationRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(PaymentObservation observation, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO contract_payment_observations (
                id, payment_certificate_id, payment_certificate_line_id,
                author, created_utc, category, severity, status,
                comment, response, responder, resolved_utc, json_data)
            VALUES (
                $id, $pcid, $pclid,
                $auth, $cutc, $cat, $sev, $st,
                $com, $resp, $respdr, $rutc, $json)
            ON CONFLICT(id) DO UPDATE SET
                payment_certificate_id=excluded.payment_certificate_id,
                payment_certificate_line_id=excluded.payment_certificate_line_id,
                author=excluded.author, category=excluded.category,
                severity=excluded.severity, status=excluded.status,
                comment=excluded.comment, response=excluded.response,
                responder=excluded.responder, resolved_utc=excluded.resolved_utc,
                json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", observation.Id.Value);
        cmd.Parameters.AddWithValue("$pcid", observation.PaymentCertificateId.Value);
        cmd.Parameters.AddWithValue("$pclid", (object?)observation.PaymentCertificateLineId?.Value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$auth", observation.Author);
        cmd.Parameters.AddWithValue("$cutc", observation.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$cat", observation.Category);
        cmd.Parameters.AddWithValue("$sev", observation.Severity);
        cmd.Parameters.AddWithValue("$st", (int)observation.Status);
        cmd.Parameters.AddWithValue("$com", observation.Comment);
        cmd.Parameters.AddWithValue("$resp", observation.Response);
        cmd.Parameters.AddWithValue("$respdr", observation.Responder);
        cmd.Parameters.AddWithValue("$rutc", observation.ResolvedUtc?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(observation, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<PaymentObservation>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_payment_observations WHERE payment_certificate_id=$id ORDER BY created_utc;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var list = new List<PaymentObservation>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var obs = JsonSerializer.Deserialize<PaymentObservation>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (obs is not null) list.Add(obs);
        }
        return list;
    }

    public async Task<PaymentObservation?> GetByIdAsync(PaymentObservationId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_payment_observations WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<PaymentObservation>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }
}

public sealed class SqlitePaymentCertificateRevisionRepository : IPaymentCertificateRevisionRepository
{
    private readonly string _db;
    public SqlitePaymentCertificateRevisionRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(PaymentCertificateRevision revision, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO payment_certificate_revisions (
                id, payment_certificate_id, revision_number, created_utc, created_by,
                snapshot_json, hash, comment)
            VALUES ($id, $pcid, $rn, $cutc, $cb, $sj, $h, $com)
            ON CONFLICT(id) DO UPDATE SET
                payment_certificate_id=excluded.payment_certificate_id,
                revision_number=excluded.revision_number,
                snapshot_json=excluded.snapshot_json, hash=excluded.hash,
                comment=excluded.comment;";
        cmd.Parameters.AddWithValue("$id", revision.Id.Value);
        cmd.Parameters.AddWithValue("$pcid", revision.PaymentCertificateId.Value);
        cmd.Parameters.AddWithValue("$rn", revision.RevisionNumber);
        cmd.Parameters.AddWithValue("$cutc", revision.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$cb", revision.CreatedBy);
        cmd.Parameters.AddWithValue("$sj", revision.SnapshotJson);
        cmd.Parameters.AddWithValue("$h", revision.Hash);
        cmd.Parameters.AddWithValue("$com", revision.Comment);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<PaymentCertificateRevision>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM payment_certificate_revisions WHERE payment_certificate_id=$id ORDER BY revision_number;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var list = new List<PaymentCertificateRevision>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new PaymentCertificateRevision
            {
                Id = PaymentCertificateRevisionId.From(r.GetString(0)),
                PaymentCertificateId = PaymentCertificateId.From(r.GetString(1)),
                RevisionNumber = r.GetInt32(2),
                CreatedUtc = DateTime.Parse(r.GetString(3)),
                CreatedBy = r.GetString(4),
                SnapshotJson = r.GetString(5),
                Hash = r.GetString(6),
                Comment = r.GetString(7)
            });
        }
        return list;
    }

    public async Task<PaymentCertificateRevision?> GetByIdAsync(PaymentCertificateRevisionId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM payment_certificate_revisions WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        using var r = await cmd.ExecuteReaderAsync(ct);
        if (await r.ReadAsync(ct))
        {
            return new PaymentCertificateRevision
            {
                Id = PaymentCertificateRevisionId.From(r.GetString(0)),
                PaymentCertificateId = PaymentCertificateId.From(r.GetString(1)),
                RevisionNumber = r.GetInt32(2),
                CreatedUtc = DateTime.Parse(r.GetString(3)),
                CreatedBy = r.GetString(4),
                SnapshotJson = r.GetString(5),
                Hash = r.GetString(6),
                Comment = r.GetString(7)
            };
        }
        return null;
    }
}

public sealed class SqlitePaymentCertificateAuditRepository : IPaymentCertificateAuditRepository
{
    private readonly string _db;
    public SqlitePaymentCertificateAuditRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task RecordAsync(PaymentCertificateAuditEvent auditEvent, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO payment_certificate_audit (
                id, payment_certificate_id, action, actor, timestamp_utc, details_json)
            VALUES ($id, $pcid, $act, $actor, $ts, $details);";
        cmd.Parameters.AddWithValue("$id", auditEvent.Id);
        cmd.Parameters.AddWithValue("$pcid", auditEvent.PaymentCertificateId.Value);
        cmd.Parameters.AddWithValue("$act", auditEvent.Action);
        cmd.Parameters.AddWithValue("$actor", auditEvent.Actor);
        cmd.Parameters.AddWithValue("$ts", auditEvent.TimestampUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$details", auditEvent.DetailsJson);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<PaymentCertificateAuditEvent>> GetByCertificateAsync(PaymentCertificateId id, int limit, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM payment_certificate_audit WHERE payment_certificate_id=$id ORDER BY timestamp_utc DESC LIMIT $lim;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        cmd.Parameters.AddWithValue("$lim", limit <= 0 ? 100 : limit);
        var list = new List<PaymentCertificateAuditEvent>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new PaymentCertificateAuditEvent
            {
                Id = r.GetString(0),
                PaymentCertificateId = PaymentCertificateId.From(r.GetString(1)),
                Action = r.GetString(2),
                Actor = r.GetString(3),
                TimestampUtc = DateTime.Parse(r.GetString(4)),
                DetailsJson = r.GetString(5)
            });
        }
        return list;
    }
}
