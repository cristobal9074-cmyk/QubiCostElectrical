using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

internal static class SqliteProjectInfoStore
{
    internal static ProjectInfoRow Prepare(ProjectFileData data, string savedAtUtc)
    {
        data.Proyecto ??= new ProjectInfoRow();
        data.Proyecto.LastSQLiteSavedAt = DateTime.TryParse(savedAtUtc, out var savedAt)
            ? savedAt.ToLocalTime()
            : AppClock.Now;
        return data.Proyecto;
    }

    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, ProjectInfoRow row, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO project_info (id, nombre, cliente, moneda, estado, ubicacion, fecha_modificacion, carta_presentacion_titulo, carta_presentacion_incluida, herramientas_pct, mermas_pct, apu_costing_mode, riesgo_pct, descuento_pct, iva_pct, incluir_iva_en_total, json, updated_at)
            VALUES (1, $nombre, $cliente, $moneda, $estado, $ubicacion, $fecha_modificacion, $carta_presentacion_titulo, $carta_presentacion_incluida, $herramientas_pct, $mermas_pct, $apu_costing_mode, $riesgo_pct, $descuento_pct, $iva_pct, $incluir_iva_en_total, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                nombre = excluded.nombre,
                cliente = excluded.cliente,
                moneda = excluded.moneda,
                Estado = excluded.Estado,
                ubicacion = excluded.ubicacion,
                fecha_modificacion = excluded.fecha_modificacion,
                carta_presentacion_titulo = excluded.carta_presentacion_titulo,
                carta_presentacion_incluida = excluded.carta_presentacion_incluida,
                herramientas_pct = excluded.herramientas_pct,
                mermas_pct = excluded.mermas_pct,
                apu_costing_mode = excluded.apu_costing_mode,
                riesgo_pct = excluded.riesgo_pct,
                descuento_pct = excluded.descuento_pct,
                iva_pct = excluded.iva_pct,
                incluir_iva_en_total = excluded.incluir_iva_en_total,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        string fechaMod = row.FechaModificacion == DateTime.MinValue
            ? now
            : row.FechaModificacion.ToUniversalTime().ToString("O");

        cmd.Parameters.AddWithValue("$nombre", row.Nombre ?? "Proyecto sin nombre");
        cmd.Parameters.AddWithValue("$cliente", row.Cliente ?? "Cliente sin nombre");
        cmd.Parameters.AddWithValue("$moneda", row.MonedaPresupuesto ?? Moneda.CLP);
        cmd.Parameters.AddWithValue("$estado", row.EstadoPresupuesto ?? EstadoPartida.Borrador);
        cmd.Parameters.AddWithValue("$ubicacion", row.Ubicacion ?? "");
        cmd.Parameters.AddWithValue("$fecha_modificacion", fechaMod);
        cmd.Parameters.AddWithValue("$carta_presentacion_titulo", row.OfertaPdf?.PresentationLetterTitle ?? "");
        cmd.Parameters.AddWithValue("$carta_presentacion_incluida", (row.OfertaPdf?.IncludePresentationLetter ?? true) ? 1 : 0);
        cmd.Parameters.AddWithValue("$herramientas_pct", (double)row.HerramientasPct);
        cmd.Parameters.AddWithValue("$mermas_pct", (double)row.MermasPct);
        cmd.Parameters.AddWithValue("$apu_costing_mode", string.IsNullOrWhiteSpace(row.ApuCostingMode) ? "CubicacionMasApuComplementarios" : row.ApuCostingMode);
        cmd.Parameters.AddWithValue("$riesgo_pct", (double)row.RiesgoPct);
        cmd.Parameters.AddWithValue("$descuento_pct", (double)row.DescuentoPct);
        cmd.Parameters.AddWithValue("$iva_pct", (double)row.IvaPct);
        cmd.Parameters.AddWithValue("$incluir_iva_en_total", row.IncluirIvaEnTotal ? 1 : 0);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions));
        cmd.Parameters.AddWithValue("$updated_at", now);
        cmd.ExecuteNonQuery();
    }

    internal static void SaveCollections(SqliteConnection connection, SqliteTransaction transaction, ProjectInfoRow row, string now)
    {
        UpsertCollection(connection, transaction, "cubicacion_cell_comments", "id", row.CubicacionCellComments, x => x.Id, now);
        UpsertCollection(connection, transaction, "technical_validation_rules", "id", row.TechnicalValidationRules, x => x.Id, now);
        UpsertCollection(connection, transaction, "real_costs", "id", row.RealCosts, x => x.Id, now);
        UpsertCollection(connection, transaction, "partida_workflow", "item_id", row.PartidaWorkflow, x => x.ItemId, now);
        UpsertCollection(connection, transaction, "partida_timeline", "id", row.PartidaTimeline, x => x.Id, now);
        UpsertCollection(connection, transaction, "project_audit_log", "id", row.AuditLog, x => x.Id, now);
        UpsertCollection(connection, transaction, "proactive_alert_rules", "id", row.ProactiveAlertRules, x => x.Id, now);
        UpsertCollection(connection, transaction, "antecedentes_tecnicos", "id", row.AntecedentesTecnicos, x => x.Id, now);
    }

    private static void UpsertCollection<T>(
        SqliteConnection connection, SqliteTransaction transaction,
        string table, string pkColumn, List<T> rows, Func<T, string> getKey, string now)
    {
        if (rows.Count == 0)
        {
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, table, pkColumn, Enumerable.Empty<string>());
            return;
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            INSERT INTO {table} ({pkColumn}, json, updated_at)
            VALUES ($pk, $json, $updated_at)
            ON CONFLICT({pkColumn}) DO UPDATE SET
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$pk", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        var keys = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            string key = getKey(row) ?? "";
            if (string.IsNullOrWhiteSpace(key)) continue;
            keys.Add(key);
            cmd.Parameters["$pk"].Value = key;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
        SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, table, pkColumn, keys);
    }

    internal static ProjectInfoRow? Load(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM project_info WHERE id = 1;";
        object? value = cmd.ExecuteScalar();
        if (value == null || value == DBNull.Value) return null;
        return JsonSerializer.Deserialize<ProjectInfoRow>(Convert.ToString(value) ?? "{}", SqliteStoreHelpers.JsonOptions);
    }

    internal static void LoadCollections(SqliteConnection connection, ProjectInfoRow row)
    {
        row.CubicacionCellComments = SqliteStoreHelpers.ReadJsonRows<CubicacionCellCommentRow>(connection, "SELECT json FROM cubicacion_cell_comments;");
        row.TechnicalValidationRules = SqliteStoreHelpers.ReadJsonRows<TechnicalValidationRuleRow>(connection, "SELECT json FROM technical_validation_rules;");
        row.RealCosts = SqliteStoreHelpers.ReadJsonRows<RealCostRow>(connection, "SELECT json FROM real_costs;");
        row.PartidaWorkflow = SqliteStoreHelpers.ReadJsonRows<PartidaWorkflowRow>(connection, "SELECT json FROM partida_workflow;");
        row.PartidaTimeline = SqliteStoreHelpers.ReadJsonRows<PartidaChangeTimelineRow>(connection, "SELECT json FROM partida_timeline;");
        row.AuditLog = SqliteStoreHelpers.ReadJsonRows<ProjectAuditEntryRow>(connection, "SELECT json FROM project_audit_log;");
        if (row.AuditLog.Count > 1)
            row.AuditLog = row.AuditLog.OrderBy(x => x.OccurredAt).ToList();
        row.ProactiveAlertRules = SqliteStoreHelpers.ReadJsonRows<ProactiveAlertRuleRow>(connection, "SELECT json FROM proactive_alert_rules;");
        row.AntecedentesTecnicos = SqliteStoreHelpers.ReadJsonRows<AntecedenteTecnicoRow>(connection, "SELECT json FROM antecedentes_tecnicos;");
    }
}
