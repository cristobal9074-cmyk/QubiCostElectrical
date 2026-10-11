using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public static partial class SQLiteProjectStore
{
    public static void SaveSnapshot(string databasePath, ProjectFileData data)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Ruta SQLite vacía.", nameof(databasePath));

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory);

        using var connection = Open(databasePath);
        EnsureSchema(connection);

        using var transaction = BeginWriteTransaction(connection);
        try
        {
            string now = DateTime.UtcNow.ToString("O");

            SqliteStoreHelpers.UpsertMeta(connection, transaction, "schema_version", Math.Max(SQLiteSchemaMigrator.LatestSchemaVersion, SqliteMigratorHelpers.GetUserVersion(connection)).ToString());
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "product_version", ProductVersionInfo.ProductVersion);
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "project_format_version", ProductVersionInfo.ProjectFormatVersion.ToString());
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "calculation_engine_version", ProductVersionInfo.CalculationEngineVersion.ToString());
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "saved_at", now);
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "active_version_id", data.ActiveVersionId ?? "");

            var proyecto = SqliteProjectInfoStore.Prepare(data, now);
            SqliteProjectInfoStore.Save(connection, transaction, proyecto, now);
            SqliteProjectInfoStore.SaveCollections(connection, transaction, proyecto, now);

            var itemizado = data.Itemizado ?? new List<ItemizadoRow>();
            SqliteItemizadoStore.Save(connection, transaction, itemizado, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "itemizado", "id", itemizado.Select(x => x.Id));

            var materiales = data.Materiales ?? new List<MaterialRow>();
            SqliteMaterialesStore.Save(connection, transaction, materiales, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "materiales", "codigo", materiales.Select(x => x.Codigo));

            var cubicacion = data.Cubicacion ?? new List<CubicacionRow>();
            SqliteCubicacionStore.Save(connection, transaction, cubicacion, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion", "id", cubicacion.Select(x => x.Id));

            var plantillas = data.Plantillas ?? new List<TemplateRow>();
            SqliteCubicacionStore.SavePlantillas(connection, transaction, plantillas, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "plantillas", "nombre", plantillas.Select(x => x.Nombre));

            var conjuntos = data.ConjuntosElectricos ?? new List<ConjuntoElectricoRow>();
            SqliteCubicacionStore.SaveConjuntos(connection, transaction, conjuntos, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "conjuntos_electricos", "id", conjuntos.Select(x => x.Id));

            var recursos = data.CubicacionRecursos ?? new List<CubicacionRecursoRow>();
            SqliteCubicacionStore.SaveRecursos(connection, transaction, recursos, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_recursos", "id", recursos.Select(x => x.Id));

            var zonasCant = data.CubicacionZonaCantidades ?? new List<CubicacionZonaCantidadRow>();
            SqliteCubicacionStore.SaveZonaCantidades(connection, transaction, zonasCant, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_zona_cantidades", "id", zonasCant.Select(x => x.Id));

            var valorizaciones = data.CubicacionValorizaciones ?? new List<CubicacionValorizacionRow>();
            SqliteCubicacionStore.SaveValorizaciones(connection, transaction, valorizaciones, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_valorizaciones", "id", valorizaciones.Select(x => x.Id));

            var qtyHeaders = data.QtyHeadersByItem ?? new Dictionary<string, string[]>();
            SqliteCubicacionStore.SaveQtyHeaders(connection, transaction, qtyHeaders, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "qty_headers", "item_id", qtyHeaders.Keys);

            var zonas = data.Zonas ?? new List<ZonaProyecto>();
            SqliteCubicacionStore.SaveZonas(connection, transaction, zonas, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "zonas", "id", zonas.Select(x => x.Id));

            var mediciones = data.Mediciones ?? new List<MeasurementLineRow>();
            SqliteCubicacionStore.SaveMediciones(connection, transaction, mediciones, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mediciones", "id", mediciones.Select(x => x.Id));

            var hojaCubicacion = data.HojaCubicacion ?? new List<HojaCubicacionItem>();
            SqliteCubicacionStore.SaveHojaCubicacion(connection, transaction, hojaCubicacion, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "hoja_cubicacion", "id", hojaCubicacion.Select(x => x.Id));

            var solicitudes = data.Solicitudes ?? new List<SolicitudItemRow>();
            SqliteCubicacionStore.SaveSolicitudes(connection, transaction, solicitudes, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "solicitudes", "id", solicitudes.Select(x => x.Id));

            var cubicacionSc = data.CubicacionSC ?? new List<CuadroCargaSCRow>();
            SqliteCubicacionStore.SaveSC(connection, transaction, cubicacionSc, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_sc", "id", cubicacionSc.Select(x => x.Id));
            SqliteCubicacionStore.SaveScConfig(connection, transaction, data.CubicacionScConfig ?? CubicacionScConfig.Default(), now);

            var apus = data.Apus ?? new List<ApuRow>();
            SqliteApuStore.Save(connection, transaction, apus, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "apu", "id", apus.Select(x => x.Id));

            var vinculos = data.PartidaApuVinculos ?? new List<PartidaApuVinculoRow>();
            SqliteApuStore.SaveVinculos(connection, transaction, vinculos, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "partida_apu_vinculos", "item_id", vinculos.Select(x => x.ItemId));

            var versiones = data.Versiones ?? new List<BudgetVersionRow>();
            SqliteRevisionStore.SaveVersiones(connection, transaction, versiones, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "versiones", "id", versiones.Select(x => x.Id));

            var ordenes = data.OrdenesCambio ?? new List<ChangeOrderRow>();
            SqliteRevisionStore.SaveOrdenesCambio(connection, transaction, ordenes, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "ordenes_cambio", "id", ordenes.Select(x => x.Id));

            var moDirecta = data.MoDirecta ?? new List<MoDirectaRow>();
            SqliteMoDirectaStore.Save(connection, transaction, moDirecta, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mo_directa", "id", moDirecta.Select(x => x.Id));

            var moIndirecta = data.MoIndirecta ?? new List<MoIndirectaRow>();
            SqliteMoIndirectaStore.Save(connection, transaction, moIndirecta, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mo_indirecta", "id", moIndirecta.Select(x => x.Id));

            var gastosOp = data.GastosOperacionales ?? new List<GastoOperacionalRow>();
            SqliteGastoOperacionalStore.Save(connection, transaction, gastosOp, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "gastos_operacionales", "id", gastosOp.Select(x => x.Id));

            var moProjectionTasks = data.MoProjectionTasks ?? new List<MoProjectionTaskRow>();
            SqliteMoProjectionTaskStore.Save(connection, transaction, moProjectionTasks, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mo_projection_tasks", "id", moProjectionTasks.Select(x => x.Id));

            SqliteEppStore.Save(connection, transaction, data.Epp ?? EppBudgetData.Default(), now);

            var negotiatedDiscounts = data.NegotiatedDiscounts ?? new List<NegotiatedDiscountRow>();
            SqliteNegotiatedDiscountStore.Save(connection, transaction, negotiatedDiscounts, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "negotiated_discounts", "id", negotiatedDiscounts.Select(x => x.Id));

            var staffingPlan = data.StaffingPlan ?? new List<StaffingRoleRow>();
            SqliteStaffingPlanStore.Save(connection, transaction, staffingPlan, now);
            SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "staffing_plan", "id", staffingPlan.Select(x => x.Id));

            SqliteClosingPolicyStore.Save(connection, transaction, data.ClosingPolicy ?? ClosingPolicySelection.Default(), now);

            SqliteEstadoPagoStore.Save(
                connection,
                transaction,
                data.EstadoPagoMeses ?? new List<EstadoPagoMesRow>(),
                data.EstadoPagoConfig ?? EstadoPagoConfig.Default(),
                now);

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLog.Error("SQLite", $"Falló el guardado completo del proyecto en '{databasePath}'; se revierte la transacción", ex);
            transaction.Rollback();
            throw;
        }
    }

    public static void SaveIncremental(string databasePath, ProjectFileData data, IReadOnlyCollection<string> dirtyScopes)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Ruta SQLite vacía.", nameof(databasePath));

        if (dirtyScopes == null || dirtyScopes.Count == 0)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory);

        using var connection = Open(databasePath);
        EnsureSchema(connection);

        var scopes = new HashSet<string>(dirtyScopes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        string now = DateTime.UtcNow.ToString("O");

        using var transaction = BeginWriteTransaction(connection);
        try
        {
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "schema_version", Math.Max(SQLiteSchemaMigrator.LatestSchemaVersion, SqliteMigratorHelpers.GetUserVersion(connection)).ToString());
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "product_version", ProductVersionInfo.ProductVersion);
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "project_format_version", ProductVersionInfo.ProjectFormatVersion.ToString());
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "calculation_engine_version", ProductVersionInfo.CalculationEngineVersion.ToString());
            SqliteStoreHelpers.UpsertMeta(connection, transaction, "saved_at", now);

            if (scopes.Contains("meta") || scopes.Contains("versiones") || scopes.Contains("apu") || scopes.Contains("ordenes_cambio") || scopes.Contains("project_info"))
                SqliteStoreHelpers.UpsertMeta(connection, transaction, "active_version_id", data.ActiveVersionId ?? "");

            if (scopes.Contains("project_info") || scopes.Contains("meta"))
            {
                var proyecto = SqliteProjectInfoStore.Prepare(data, now);
                SqliteProjectInfoStore.Save(connection, transaction, proyecto, now);
                if (scopes.Contains("project_info"))
                    SqliteProjectInfoStore.SaveCollections(connection, transaction, proyecto, now);
            }

            if (scopes.Contains("itemizado"))
            {
                var rows = data.Itemizado ?? new List<ItemizadoRow>();
                SqliteItemizadoStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "itemizado", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("materiales"))
            {
                var rows = data.Materiales ?? new List<MaterialRow>();
                SqliteMaterialesStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "materiales", "codigo", rows.Select(x => x.Codigo));
            }

            if (scopes.Contains("cubicacion"))
            {
                var rows = data.Cubicacion ?? new List<CubicacionRow>();
                SqliteCubicacionStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion", "id", rows.Select(x => x.Id));

                var recursos = data.CubicacionRecursos ?? new List<CubicacionRecursoRow>();
                SqliteCubicacionStore.SaveRecursos(connection, transaction, recursos, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_recursos", "id", recursos.Select(x => x.Id));

                var zonasCant = data.CubicacionZonaCantidades ?? new List<CubicacionZonaCantidadRow>();
                SqliteCubicacionStore.SaveZonaCantidades(connection, transaction, zonasCant, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_zona_cantidades", "id", zonasCant.Select(x => x.Id));

                var valorizaciones = data.CubicacionValorizaciones ?? new List<CubicacionValorizacionRow>();
                SqliteCubicacionStore.SaveValorizaciones(connection, transaction, valorizaciones, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_valorizaciones", "id", valorizaciones.Select(x => x.Id));

                var qtyHeaders = data.QtyHeadersByItem ?? new Dictionary<string, string[]>();
                SqliteCubicacionStore.SaveQtyHeaders(connection, transaction, qtyHeaders, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "qty_headers", "item_id", qtyHeaders.Keys);

                var zonas = data.Zonas ?? new List<ZonaProyecto>();
                SqliteCubicacionStore.SaveZonas(connection, transaction, zonas, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "zonas", "id", zonas.Select(x => x.Id));

                var mediciones = data.Mediciones ?? new List<MeasurementLineRow>();
                SqliteCubicacionStore.SaveMediciones(connection, transaction, mediciones, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mediciones", "id", mediciones.Select(x => x.Id));
            }

            if (scopes.Contains("apu"))
            {
                var rows = data.Apus ?? new List<ApuRow>();
                SqliteApuStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "apu", "id", rows.Select(x => x.Id));

                var vinculos = data.PartidaApuVinculos ?? new List<PartidaApuVinculoRow>();
                SqliteApuStore.SaveVinculos(connection, transaction, vinculos, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "partida_apu_vinculos", "item_id", vinculos.Select(x => x.ItemId));
            }

            if (scopes.Contains("qty_headers"))
            {
                var headers = data.QtyHeadersByItem ?? new Dictionary<string, string[]>();
                SqliteCubicacionStore.SaveQtyHeaders(connection, transaction, headers, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "qty_headers", "item_id", headers.Keys);
            }

            if (scopes.Contains("plantillas"))
            {
                var rows = data.Plantillas ?? new List<TemplateRow>();
                SqliteCubicacionStore.SavePlantillas(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "plantillas", "nombre", rows.Select(x => x.Nombre));
            }

            if (scopes.Contains("conjuntos_electricos"))
            {
                var rows = data.ConjuntosElectricos ?? new List<ConjuntoElectricoRow>();
                SqliteCubicacionStore.SaveConjuntos(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "conjuntos_electricos", "id", rows.Select(x => x.Id));
            }



            if (scopes.Contains("versiones"))
            {
                var rows = data.Versiones ?? new List<BudgetVersionRow>();
                SqliteRevisionStore.SaveVersiones(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "versiones", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("ordenes_cambio"))
            {
                var rows = data.OrdenesCambio ?? new List<ChangeOrderRow>();
                SqliteRevisionStore.SaveOrdenesCambio(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "ordenes_cambio", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("zonas") || scopes.Contains("cubicacion"))
            {
                var rows = data.Zonas ?? new List<ZonaProyecto>();
                SqliteCubicacionStore.SaveZonas(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "zonas", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("mediciones") || scopes.Contains("cubicacion"))
            {
                var rows = data.Mediciones ?? new List<MeasurementLineRow>();
                SqliteCubicacionStore.SaveMediciones(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mediciones", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("hoja_cubicacion"))
            {
                var rows = data.HojaCubicacion ?? new List<HojaCubicacionItem>();
                SqliteCubicacionStore.SaveHojaCubicacion(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "hoja_cubicacion", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("solicitudes"))
            {
                var rows = data.Solicitudes ?? new List<SolicitudItemRow>();
                SqliteCubicacionStore.SaveSolicitudes(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "solicitudes", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("mo_directa"))
            {
                var rows = data.MoDirecta ?? new List<MoDirectaRow>();
                SqliteMoDirectaStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mo_directa", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("mo_indirecta"))
            {
                var rows = data.MoIndirecta ?? new List<MoIndirectaRow>();
                SqliteMoIndirectaStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mo_indirecta", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("gastos_operacionales"))
            {
                var rows = data.GastosOperacionales ?? new List<GastoOperacionalRow>();
                SqliteGastoOperacionalStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "gastos_operacionales", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("mo_projection_tasks"))
            {
                var rows = data.MoProjectionTasks ?? new List<MoProjectionTaskRow>();
                SqliteMoProjectionTaskStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "mo_projection_tasks", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("epp"))
            {
                SqliteEppStore.Save(connection, transaction, data.Epp ?? EppBudgetData.Default(), now);
            }

            if (scopes.Contains("negotiated_discounts"))
            {
                var rows = data.NegotiatedDiscounts ?? new List<NegotiatedDiscountRow>();
                SqliteNegotiatedDiscountStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "negotiated_discounts", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("staffing_plan"))
            {
                var rows = data.StaffingPlan ?? new List<StaffingRoleRow>();
                SqliteStaffingPlanStore.Save(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "staffing_plan", "id", rows.Select(x => x.Id));
            }

            if (scopes.Contains("closing_policy"))
            {
                SqliteClosingPolicyStore.Save(connection, transaction, data.ClosingPolicy ?? ClosingPolicySelection.Default(), now);
            }

            if (scopes.Contains("estados_pago"))
            {
                SqliteEstadoPagoStore.Save(
                    connection,
                    transaction,
                    data.EstadoPagoMeses ?? new List<EstadoPagoMesRow>(),
                    data.EstadoPagoConfig ?? EstadoPagoConfig.Default(),
                    now);
            }

            if (scopes.Contains("cubicacion_sc"))
            {
                var rows = data.CubicacionSC ?? new List<CuadroCargaSCRow>();
                SqliteCubicacionStore.SaveSC(connection, transaction, rows, now);
                SqliteStoreHelpers.DeleteMissingKeys(connection, transaction, "cubicacion_sc", "id", rows.Select(x => x.Id));
                SqliteCubicacionStore.SaveScConfig(connection, transaction, data.CubicacionScConfig ?? CubicacionScConfig.Default(), now);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLog.Error("SQLite", $"Falló el guardado incremental del proyecto en '{databasePath}'; se revierte la transacción", ex);
            transaction.Rollback();
            throw;
        }
    }

    public static ProjectFileData LoadSnapshot(string databasePath)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("No existe la base SQLite del proyecto.", databasePath);

        using var connection = Open(databasePath);
        EnsureSchema(connection);

        var proyecto = SqliteProjectInfoStore.Load(connection) ?? new ProjectInfoRow();
        SqliteProjectInfoStore.LoadCollections(connection, proyecto);

#pragma warning disable CS0618
        var data = new ProjectFileData
        {
            Proyecto = proyecto,
            Itemizado = SqliteStoreHelpers.ReadJsonRows<ItemizadoRow>(connection, "SELECT json FROM itemizado ORDER BY sort_order, id;"),
            Materiales = SqliteStoreHelpers.ReadJsonRows<MaterialRow>(connection, "SELECT json FROM materiales ORDER BY sort_order, codigo;"),
            Cubicacion = SqliteStoreHelpers.ReadJsonRows<CubicacionRow>(connection, "SELECT json FROM cubicacion ORDER BY sort_order, id;"),
            Plantillas = SqliteStoreHelpers.ReadJsonRows<TemplateRow>(connection, "SELECT json FROM plantillas ORDER BY sort_order, nombre;"),
            ConjuntosElectricos = SqliteStoreHelpers.ReadJsonRows<ConjuntoElectricoRow>(connection, "SELECT json FROM conjuntos_electricos ORDER BY sort_order, codigo;"),
            CubicacionRecursos = SqliteStoreHelpers.ReadJsonRows<CubicacionRecursoRow>(connection, "SELECT json FROM cubicacion_recursos ORDER BY sort_order, id;"),
            CubicacionZonaCantidades = SqliteStoreHelpers.ReadJsonRows<CubicacionZonaCantidadRow>(connection, "SELECT json FROM cubicacion_zona_cantidades ORDER BY sort_order, id;"),
            CubicacionValorizaciones = SqliteStoreHelpers.ReadJsonRows<CubicacionValorizacionRow>(connection, "SELECT json FROM cubicacion_valorizaciones ORDER BY recurso_id, id;"),
            PartidaApuVinculos = SqliteStoreHelpers.ReadJsonRows<PartidaApuVinculoRow>(connection, "SELECT json FROM partida_apu_vinculos ORDER BY item_id;"),
            Versiones = SqliteStoreHelpers.ReadJsonRows<BudgetVersionRow>(connection, "SELECT json FROM versiones ORDER BY sort_order, id;"),
            Apus = SqliteStoreHelpers.ReadJsonRows<ApuRow>(connection, "SELECT json FROM apu ORDER BY sort_order, id;"),
            OrdenesCambio = SqliteStoreHelpers.ReadJsonRows<ChangeOrderRow>(connection, "SELECT json FROM ordenes_cambio ORDER BY sort_order, numero;"),
            QtyHeadersByItem = SqliteCubicacionStore.LoadQtyHeaders(connection),
            Zonas = SqliteStoreHelpers.ReadJsonRows<ZonaProyecto>(connection, "SELECT json FROM zonas ORDER BY sort_order, id;"),
            Mediciones = SqliteStoreHelpers.ReadJsonRows<MeasurementLineRow>(connection, "SELECT json FROM mediciones ORDER BY sort_order, id;"),
            HojaCubicacion = SqliteStoreHelpers.ReadJsonRows<HojaCubicacionItem>(connection, "SELECT json FROM hoja_cubicacion ORDER BY sort_order, id;"),
            Solicitudes = SqliteStoreHelpers.ReadJsonRows<SolicitudItemRow>(connection, "SELECT json FROM solicitudes ORDER BY sort_order, id;"),
            CubicacionSC = SqliteStoreHelpers.ReadJsonRows<CuadroCargaSCRow>(connection, "SELECT json FROM cubicacion_sc ORDER BY sort_order, id;"),
            CubicacionScConfig = SqliteCubicacionStore.LoadScConfig(connection),
            MoDirecta = SqliteStoreHelpers.ReadJsonRows<MoDirectaRow>(connection, "SELECT json FROM mo_directa ORDER BY sort_order, id;"),
            MoIndirecta = SqliteStoreHelpers.ReadJsonRows<MoIndirectaRow>(connection, "SELECT json FROM mo_indirecta ORDER BY sort_order, id;"),
            GastosOperacionales = SqliteStoreHelpers.ReadJsonRows<GastoOperacionalRow>(connection, "SELECT json FROM gastos_operacionales ORDER BY sort_order, id;"),
            MoProjectionTasks = SqliteStoreHelpers.ReadJsonRows<MoProjectionTaskRow>(connection, "SELECT json FROM mo_projection_tasks ORDER BY sort_order, id;"),
            Epp = SqliteEppStore.Load(connection),
            NegotiatedDiscounts = SqliteStoreHelpers.ReadJsonRows<NegotiatedDiscountRow>(connection, "SELECT json FROM negotiated_discounts ORDER BY sort_order, id;"),
            StaffingPlan = SqliteStoreHelpers.ReadJsonRows<StaffingRoleRow>(connection, "SELECT json FROM staffing_plan ORDER BY sort_order, id;"),
            ClosingPolicy = SqliteClosingPolicyStore.Load(connection),
            EstadoPagoMeses = SqliteEstadoPagoStore.Load(connection),
            EstadoPagoConfig = SqliteEstadoPagoStore.LoadConfig(connection),
            ActiveVersionId = SqliteStoreHelpers.ReadMeta(connection, "active_version_id") ?? ""
        };
#pragma warning restore CS0618

        return data;
    }

    public static DateTime GetLastSavedAt(string databasePath)
    {
        if (!File.Exists(databasePath)) return DateTime.MinValue;

        using var connection = Open(databasePath);
        EnsureSchema(connection);
        string? value = SqliteStoreHelpers.ReadMeta(connection, "saved_at");
        return DateTime.TryParse(value, out DateTime result) ? result.ToLocalTime() : File.GetLastWriteTime(databasePath);
    }

    /// <summary>
    /// Lee solo el estado del presupuesto sin cargar itemizado, cubicación, materiales ni APUs.
    /// Pensado para listados (proyectos recientes) donde cargar el snapshot completo sería costoso.
    /// </summary>
    public static string? PeekEstadoPresupuesto(string databasePath)
    {
        if (!File.Exists(databasePath)) return null;

        using var connection = Open(databasePath);
        EnsureSchema(connection);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT estado FROM project_info WHERE id = 1;";
        return cmd.ExecuteScalar() as string;
    }

    private static SqliteTransaction BeginWriteTransaction(SqliteConnection connection)
    {
        // Hist. CRÍTICO/ALTO:
        // Todas las escrituras compuestas usan una transacción explícita y única.
        // Serializable evita que otro escritor intercale cambios mientras se eliminan/insertan
        // múltiples tablas del proyecto. Si ocurre una excepción antes del Commit(), el catch
        // ejecuta Rollback() y SQLite conserva el último estado consistente.
        return connection.BeginTransaction(IsolationLevel.Serializable);
    }

    private static SqliteConnection Open(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA busy_timeout = 5000;";
            cmd.ExecuteNonQuery();
        }

        bool isNetwork = ProductPaths.IsNetworkOrUncPath(databasePath);

        using (var cmd = connection.CreateCommand())
        {
            // SQLite WAL usa archivos -shm que fallan sobre SMB/red compartida; en red usamos TRUNCATE
            cmd.CommandText = isNetwork ? "PRAGMA journal_mode=TRUNCATE;" : "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA synchronous=NORMAL;";
            cmd.ExecuteNonQuery();
        }
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
        }

        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
        => SQLiteSchemaMigrator.MigrateToLatest(connection);
}
