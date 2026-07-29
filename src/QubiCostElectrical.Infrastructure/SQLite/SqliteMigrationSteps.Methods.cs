using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static partial class SqliteMigrationSteps
{
    internal static void Migration001_BaseInicial(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS app_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS project_info (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                nombre TEXT NOT NULL,
                cliente TEXT NOT NULL,
                moneda TEXT NOT NULL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS itemizado (
                id TEXT PRIMARY KEY,
                sort_order INTEGER NOT NULL,
                item TEXT,
                descripcion TEXT,
                unidad TEXT,
                cantidad REAL,
                es_titulo INTEGER,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS materiales (
                codigo TEXT PRIMARY KEY,
                sort_order INTEGER NOT NULL,
                descripcion TEXT,
                unidad TEXT,
                categoria TEXT,
                subcategoria TEXT,
                subcategoria2 TEXT,
                tipo_instalacion TEXT,
                especificacion_tecnica TEXT,
                mat_uf REAL,
                mo_uf REAL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cubicacion (
                id TEXT PRIMARY KEY,
                sort_order INTEGER NOT NULL,
                item_id TEXT,
                codigo TEXT,
                descripcion TEXT,
                unidad TEXT,
                cantidad REAL,
                mat_uf REAL,
                mo_uf REAL,
                area TEXT,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS plantillas (
                nombre TEXT PRIMARY KEY,
                sort_order INTEGER NOT NULL,
                descripcion TEXT,
                unidad_base TEXT,
                area TEXT,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS qty_headers (
                item_id TEXT PRIMARY KEY,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS calculadora (
                row_idx INTEGER NOT NULL,
                col_idx INTEGER NOT NULL,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (row_idx, col_idx)
            );

            CREATE INDEX IF NOT EXISTS idx_cubicacion_item ON cubicacion(item_id);
            CREATE INDEX IF NOT EXISTS idx_cubicacion_codigo ON cubicacion(codigo);
            CREATE INDEX IF NOT EXISTS idx_materiales_search ON materiales(descripcion, categoria);
            CREATE INDEX IF NOT EXISTS idx_materiales_categoria_pro ON materiales(categoria, subcategoria, subcategoria2);
            """);
    }

    internal static void Migration002_Versiones(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS versiones (
                id TEXT PRIMARY KEY,
                sort_order INTEGER NOT NULL,
                codigo TEXT,
                nombre TEXT,
                estado TEXT,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """);
    }

    internal static void Migration003_Apu(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS apu (
                id TEXT PRIMARY KEY,
                sort_order INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                descripcion TEXT,
                unidad TEXT,
                precio_unit_uf REAL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_apu_item ON apu(item_id);
            """);
    }

    internal static void Migration004_OrdenesCambio(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS ordenes_cambio (
                id TEXT PRIMARY KEY,
                sort_order INTEGER NOT NULL,
                numero TEXT,
                titulo TEXT,
                estado TEXT,
                delta_costo_uf REAL,
                delta_venta_uf REAL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_oc_numero ON ordenes_cambio(numero);
            """);
    }

    internal static void Migration005_CompatibilidadVersionada(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Columnas ejecutivas livianas para poder listar proyectos recientes y estados sin leer todo el JSON.
        // Se agregan con DEFAULT para no romper bases existentes ni inserts antiguos.
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "estado", "TEXT NOT NULL DEFAULT 'Borrador'");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "ubicacion", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "fecha_modificacion", "TEXT NOT NULL DEFAULT ''");

        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE INDEX IF NOT EXISTS idx_project_info_estado ON project_info(estado);
            CREATE INDEX IF NOT EXISTS idx_project_info_cliente ON project_info(cliente);
            CREATE INDEX IF NOT EXISTS idx_cubicacion_item ON cubicacion(item_id);
            CREATE INDEX IF NOT EXISTS idx_cubicacion_codigo ON cubicacion(codigo);
            CREATE INDEX IF NOT EXISTS idx_materiales_search ON materiales(descripcion, categoria);
            CREATE INDEX IF NOT EXISTS idx_versiones_estado ON versiones(estado);
            CREATE INDEX IF NOT EXISTS idx_apu_item ON apu(item_id);
            CREATE INDEX IF NOT EXISTS idx_oc_numero ON ordenes_cambio(numero);
            CREATE INDEX IF NOT EXISTS idx_oc_estado ON ordenes_cambio(estado);
            """);
    }

    internal static void Migration006_CartaPresentacion(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Columnas livianas para preview/listado. El texto completo se conserva en project_info.json
        // para no duplicar contenido largo ni romper compatibilidad con proyectos antiguos.
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "carta_presentacion_titulo", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "carta_presentacion_incluida", "INTEGER NOT NULL DEFAULT 1");
    }

    internal static void Migration007_MotorCostos(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Columnas livianas para políticas del BudgetEngine. El JSON sigue siendo la fuente completa,
        // pero estas columnas permiten preview/listado sin leer todo el proyecto.
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "herramientas_pct", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "mermas_pct", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "riesgo_pct", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "descuento_pct", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "iva_pct", "REAL NOT NULL DEFAULT 19");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "incluir_iva_en_total", "INTEGER NOT NULL DEFAULT 0");
    }

    internal static void Migration008_ApuCostingMode(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "project_info", "apu_costing_mode", "TEXT NOT NULL DEFAULT 'CubicacionMasApuComplementarios'");
    }

    internal static void Migration009_ApuProfesional(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "apu", "estado", "TEXT NOT NULL DEFAULT 'OK'");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "apu", "fuente_precio", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "apu", "proveedor", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "apu", "fecha_precio", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.Execute(connection, transaction, "CREATE INDEX IF NOT EXISTS idx_apu_estado ON apu(estado);");
    }

    internal static void Migration010_TrazabilidadPrecios(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "moneda_precio", "TEXT NOT NULL DEFAULT 'CLP'");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "uf_usada_clp", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "proveedor", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "origen_precio", "TEXT NOT NULL DEFAULT 'Lista de precios'");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "fecha_precio", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "usuario_precio", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "estado_precio", "TEXT NOT NULL DEFAULT 'OK'");
        SqliteMigratorHelpers.Execute(connection, transaction, "CREATE INDEX IF NOT EXISTS idx_materiales_estado_precio ON materiales(estado_precio);");
    }

    internal static void Migration011_CatalogoMaterialesPro(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "subcategoria", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "subcategoria2", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "tipo_instalacion", "TEXT NOT NULL DEFAULT ''");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "especificacion_tecnica", "TEXT NOT NULL DEFAULT ''");

        SqliteMigratorHelpers.Execute(connection, transaction, "CREATE INDEX IF NOT EXISTS idx_materiales_categoria_pro ON materiales(categoria, subcategoria, subcategoria2);");
        SqliteMigratorHelpers.Execute(connection, transaction, "CREATE INDEX IF NOT EXISTS idx_materiales_tipo_instalacion ON materiales(tipo_instalacion);");
    }

    internal static void Migration012_PerformanceIndexes(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE INDEX IF NOT EXISTS idx_itemizado_item ON itemizado(item);
            CREATE INDEX IF NOT EXISTS idx_itemizado_descripcion ON itemizado(descripcion);
            CREATE INDEX IF NOT EXISTS idx_materiales_codigo ON materiales(codigo);
            CREATE INDEX IF NOT EXISTS idx_materiales_descripcion ON materiales(descripcion);
            CREATE INDEX IF NOT EXISTS idx_materiales_codigo_desc ON materiales(codigo, descripcion);
            CREATE INDEX IF NOT EXISTS idx_materiales_categoria_desc ON materiales(categoria, subcategoria, descripcion);
            CREATE INDEX IF NOT EXISTS idx_cubicacion_item_sort ON cubicacion(item_id, sort_order);
            CREATE INDEX IF NOT EXISTS idx_cubicacion_item_codigo ON cubicacion(item_id, codigo);
            CREATE INDEX IF NOT EXISTS idx_cubicacion_descripcion ON cubicacion(descripcion);
            CREATE INDEX IF NOT EXISTS idx_apu_item_estado ON apu(item_id, estado);
            CREATE INDEX IF NOT EXISTS idx_qty_headers_item ON qty_headers(item_id);
            """);
    }

    internal static void Migration013_ProfessionalSearchIndexes(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE INDEX IF NOT EXISTS idx_cubicacion_itemid ON cubicacion(item_id);
            CREATE INDEX IF NOT EXISTS idx_material_codigo ON materiales(codigo);
            CREATE INDEX IF NOT EXISTS idx_material_search ON materiales(descripcion, categoria);
            CREATE INDEX IF NOT EXISTS idx_material_codigo_categoria ON materiales(codigo, categoria, subcategoria);
            CREATE INDEX IF NOT EXISTS idx_material_fecha_precio ON materiales(fecha_precio);
            CREATE INDEX IF NOT EXISTS idx_partida_proyecto ON itemizado(id, item);
            """);
    }

    internal static void Migration014_SeparateCollections(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS cubicacion_cell_comments (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS partida_schedules (
                item_id    TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS technical_validation_rules (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS real_costs (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS workspace_layouts (
                nombre     TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS partida_workflow (
                item_id    TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS partida_timeline (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS proactive_alert_rules (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS cubicacion_attachments (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS offer_digital_signatures (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS estados_pago (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """);
    }

    internal static void Migration015_DropUnusedCollections(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            DROP TABLE IF EXISTS workspace_layouts;
            DROP TABLE IF EXISTS partida_schedules;
            DROP TABLE IF EXISTS cubicacion_attachments;
            DROP TABLE IF EXISTS offer_digital_signatures;
            """);
    }

    internal static void Migration016_ZonasYMediciones(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS zonas (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS mediciones (
                id              TEXT NOT NULL PRIMARY KEY,
                item_id         TEXT NOT NULL DEFAULT '',
                cubicacion_row_id TEXT NOT NULL DEFAULT '',
                zona_offset     INTEGER NOT NULL DEFAULT -1,
                sort_order      INTEGER NOT NULL DEFAULT 0,
                json            TEXT NOT NULL,
                updated_at      TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_mediciones_item_id ON mediciones(item_id);
            CREATE INDEX IF NOT EXISTS idx_mediciones_cubicacion_row_id ON mediciones(cubicacion_row_id);
            """);
    }

    internal static void Migration017_CubicacionSC(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS cubicacion_sc (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                nombre      TEXT NOT NULL DEFAULT '',
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cubicacion_sc_config (
                id          INTEGER PRIMARY KEY CHECK (id = 1),
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_cubicacion_sc_nombre ON cubicacion_sc(nombre);
            """);
    }

    internal static void Migration018_AntecedentesTecnicos(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS antecedentes_tecnicos (
                id         TEXT NOT NULL PRIMARY KEY,
                json       TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """);
    }

    internal static void Migration019_ConjuntosElectricos(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS conjuntos_electricos (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                codigo      TEXT NOT NULL DEFAULT '',
                nombre      TEXT NOT NULL DEFAULT '',
                tipo        TEXT NOT NULL DEFAULT '',
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_conjuntos_codigo ON conjuntos_electricos(codigo);
            CREATE INDEX IF NOT EXISTS idx_conjuntos_tipo ON conjuntos_electricos(tipo);
            """);
    }

    internal static void Migration020_CubicacionComposicion(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS cubicacion_recursos (
                id          TEXT NOT NULL PRIMARY KEY,
                item_id     TEXT NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cubicacion_recursos_item ON cubicacion_recursos(item_id);

            CREATE TABLE IF NOT EXISTS cubicacion_zona_cantidades (
                id          TEXT NOT NULL PRIMARY KEY,
                recurso_id  TEXT NOT NULL DEFAULT '',
                item_id     TEXT NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cubicacion_zona_recurso ON cubicacion_zona_cantidades(recurso_id);
            CREATE INDEX IF NOT EXISTS idx_cubicacion_zona_item ON cubicacion_zona_cantidades(item_id);

            CREATE TABLE IF NOT EXISTS cubicacion_valorizaciones (
                id          TEXT NOT NULL PRIMARY KEY,
                recurso_id  TEXT NOT NULL DEFAULT '',
                item_id     TEXT NOT NULL DEFAULT '',
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cubicacion_valorizacion_recurso ON cubicacion_valorizaciones(recurso_id);

            CREATE TABLE IF NOT EXISTS partida_apu_vinculos (
                item_id     TEXT NOT NULL PRIMARY KEY,
                apu_id      TEXT NOT NULL DEFAULT '',
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_partida_apu_apu ON partida_apu_vinculos(apu_id);
            """);
    }

    internal static void Migration021_ProjectAuditLog(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS project_audit_log (
                id          TEXT NOT NULL PRIMARY KEY,
                occurred_at TEXT NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_project_audit_occurred ON project_audit_log(occurred_at);
            """);
    }

    // Los comentarios de celda de cubicación se indexaban por (ItemId, VisualRow), una posición
    // dentro de la partida. Al mover/duplicar/borrar filas esa posición deja de apuntar a la fila
    // correcta. Esta migración agrega CubicacionRowId (referencia estable a CubicacionRow.Id) y
    // hace un backfill best-effort para los comentarios existentes, usando el orden actual de
    // session.Cubicacion (sort_order) como snapshot de a qué fila apuntaba cada VisualRow.
    internal static void Migration022_CubicacionCellCommentsRowId(SqliteConnection connection, SqliteTransaction transaction)
    {
        if (!SqliteStoreHelpers.TableExists(connection, "cubicacion_cell_comments") ||
            !SqliteStoreHelpers.TableExists(connection, "cubicacion"))
            return;

        var rowIdsByItem = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(
            System.StringComparer.OrdinalIgnoreCase);

        using (var readCubicacion = connection.CreateCommand())
        {
            readCubicacion.Transaction = transaction;
            readCubicacion.CommandText = "SELECT id, item_id FROM cubicacion ORDER BY sort_order, id;";
            using var reader = readCubicacion.ExecuteReader();
            while (reader.Read())
            {
                string id = reader.GetString(0);
                string itemId = reader.IsDBNull(1) ? "" : reader.GetString(1);
                if (!rowIdsByItem.TryGetValue(itemId, out var list))
                {
                    list = new System.Collections.Generic.List<string>();
                    rowIdsByItem[itemId] = list;
                }
                list.Add(id);
            }
        }

        var updates = new System.Collections.Generic.List<(string Id, string Json)>();

        using (var readComments = connection.CreateCommand())
        {
            readComments.Transaction = transaction;
            readComments.CommandText = "SELECT id, json FROM cubicacion_cell_comments;";
            using var reader = readComments.ExecuteReader();
            while (reader.Read())
            {
                string id = reader.GetString(0);
                string json = reader.GetString(1);
                var comment = JsonSerializer.Deserialize<CubicacionCellCommentRow>(json, SqliteStoreHelpers.JsonOptions);
                if (comment == null || !string.IsNullOrWhiteSpace(comment.CubicacionRowId))
                    continue;

                if (rowIdsByItem.TryGetValue(comment.ItemId ?? "", out var rowIds) &&
                    comment.VisualRow >= 0 && comment.VisualRow < rowIds.Count)
                {
                    comment.CubicacionRowId = rowIds[comment.VisualRow];
                    updates.Add((id, JsonSerializer.Serialize(comment, SqliteStoreHelpers.JsonOptions)));
                }
            }
        }

        if (updates.Count == 0)
            return;

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE cubicacion_cell_comments SET json = $json WHERE id = $id;";
        update.Parameters.Add("$json", SqliteType.Text);
        update.Parameters.Add("$id", SqliteType.Text);
        update.Prepare();

        foreach (var (id, json) in updates)
        {
            update.Parameters["$json"].Value = json;
            update.Parameters["$id"].Value = id;
            update.ExecuteNonQuery();
        }
    }

    // Hoja de Cubicación: herramienta de apoyo manual (ítems con filas de datos y fórmulas estilo
    // Excel) para sumar mediciones antes de trasladarlas a la cubicación real. Antes vivía solo en
    // memoria durante la sesión; esta tabla la persiste con el proyecto para trazabilidad futura.
    internal static void Migration023_HojaCubicacion(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS hoja_cubicacion (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    internal static void Migration024_Solicitudes(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS solicitudes (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    // Fuente del precio en pesos: columnas indexadas mat_clp/mo_clp (el JSON ya podía traer
    // MatClp/MoClp; sin columnas el listado/diagnóstico y algunos caminos solo veían mat_uf/mo_uf).
    // Backfill desde JSON; si MatClp falta, se deja 0 y ProjectSnapshotIntegrity lo deriva al cargar.
    internal static void Migration025_MatClpMoClpColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "mat_clp", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "materiales", "mo_clp", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "cubicacion", "mat_clp", "REAL NOT NULL DEFAULT 0");
        SqliteMigratorHelpers.AddColumnIfMissing(connection, transaction, "cubicacion", "mo_clp", "REAL NOT NULL DEFAULT 0");

        SqliteMigratorHelpers.Execute(connection, transaction, """
            UPDATE materiales
            SET mat_clp = COALESCE(CAST(json_extract(json, '$.MatClp') AS REAL), 0),
                mo_clp  = COALESCE(CAST(json_extract(json, '$.MoClp') AS REAL), 0)
            WHERE mat_clp = 0 AND mo_clp = 0;
            """);

        SqliteMigratorHelpers.Execute(connection, transaction, """
            UPDATE cubicacion
            SET mat_clp = COALESCE(CAST(json_extract(json, '$.MatClp') AS REAL), 0),
                mo_clp  = COALESCE(CAST(json_extract(json, '$.MoClp') AS REAL), 0)
            WHERE mat_clp = 0 AND mo_clp = 0;
            """);
    }

    internal static void Migration026_MoDotacionGastosOperacionales(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS mo_directa (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS mo_indirecta (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS gastos_operacionales (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    internal static void Migration027_EppBudget(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS epp_config (
                id          INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    internal static void Migration028_ProfessionalResources(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, SqliteResourceTable.CreateSql
            + SqlitePriceHistoryTable.CreateSql
            + SqliteExchangeRateTable.CreateSql);
    }

    internal static void Migration029_ApuVersioning(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, SqliteApuVersionTable.CreateSql
            + SqliteApuSnapshotTable.CreateSql
            + SqliteApuCatalogTable.CreateSql);
    }

    internal static void Migration030_EconomicConfig(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS professional_economic_configs (
                id              TEXT NOT NULL PRIMARY KEY,
                concept_code    TEXT NOT NULL,
                concept_name    TEXT NOT NULL DEFAULT '',
                category        TEXT NOT NULL DEFAULT 'Indirecto',
                concept_type    TEXT NOT NULL DEFAULT 'Porcentaje',
                base_type       TEXT NOT NULL DEFAULT 'SubtotalAnterior',
                rate_or_amount  REAL NOT NULL DEFAULT 0,
                sort_order      INTEGER NOT NULL DEFAULT 0,
                is_informative  INTEGER NOT NULL DEFAULT 0,
                included_in_total INTEGER NOT NULL DEFAULT 1,
                is_active       INTEGER NOT NULL DEFAULT 1,
                created_at_utc  TEXT NOT NULL,
                updated_at_utc  TEXT NOT NULL,
                row_version     INTEGER NOT NULL DEFAULT 1,
                json_data       TEXT NOT NULL DEFAULT '{}'
            );

            CREATE TABLE IF NOT EXISTS professional_scenarios (
                id              TEXT NOT NULL PRIMARY KEY,
                name            TEXT NOT NULL,
                scenario_type   TEXT NOT NULL DEFAULT 'Custom',
                base_apu_id     TEXT,
                description     TEXT NOT NULL DEFAULT '',
                overrides_json  TEXT NOT NULL DEFAULT '{}',
                is_active       INTEGER NOT NULL DEFAULT 1,
                created_at_utc  TEXT NOT NULL,
                updated_at_utc  TEXT NOT NULL,
                row_version     INTEGER NOT NULL DEFAULT 1,
                json_data       TEXT NOT NULL DEFAULT '{}'
            );

            CREATE INDEX IF NOT EXISTS idx_professional_economic_configs_category
                ON professional_economic_configs(category);
            CREATE INDEX IF NOT EXISTS idx_professional_economic_configs_order
                ON professional_economic_configs(sort_order);
            CREATE INDEX IF NOT EXISTS idx_professional_economic_configs_active
                ON professional_economic_configs(is_active);

            CREATE INDEX IF NOT EXISTS idx_professional_scenarios_name
                ON professional_scenarios(name);
            CREATE INDEX IF NOT EXISTS idx_professional_scenarios_type
                ON professional_scenarios(scenario_type);
            CREATE INDEX IF NOT EXISTS idx_professional_scenarios_active
                ON professional_scenarios(is_active);
            """);
    }

    internal static void Migration031_PlanningCore(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS project_schedules (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                time_zone_id TEXT NOT NULL DEFAULT '',
                default_calendar_id TEXT NOT NULL DEFAULT '',
                project_start_date TEXT,
                target_end_date TEXT,
                status TEXT NOT NULL DEFAULT 'Draft',
                version INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_at TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                remarks TEXT NOT NULL DEFAULT '',
                compatibility_metadata TEXT NOT NULL DEFAULT '',
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_project_schedules_status ON project_schedules(status);
            CREATE INDEX IF NOT EXISTS idx_project_schedules_created ON project_schedules(created_by);

            CREATE TABLE IF NOT EXISTS planning_wbs_nodes (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                wbs_code TEXT NOT NULL DEFAULT '',
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                node_type TEXT NOT NULL DEFAULT 'Summary',
                parent_id TEXT,
                level INTEGER NOT NULL DEFAULT 1,
                sort_order INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'Draft',
                responsible TEXT,
                budget_item_id TEXT,
                apu_id TEXT,
                created_at TEXT NOT NULL,
                modified_at TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                tags TEXT NOT NULL DEFAULT '',
                remarks TEXT NOT NULL DEFAULT '',
                is_deleted INTEGER NOT NULL DEFAULT 0,
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_wbs_nodes_schedule ON planning_wbs_nodes(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_wbs_nodes_parent ON planning_wbs_nodes(parent_id);
            CREATE INDEX IF NOT EXISTS idx_wbs_nodes_code ON planning_wbs_nodes(wbs_code);

            CREATE TABLE IF NOT EXISTS planning_activities (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                wbs_node_id TEXT NOT NULL,
                code TEXT NOT NULL DEFAULT '',
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                type TEXT NOT NULL DEFAULT 'Task',
                planned_duration_json TEXT NOT NULL DEFAULT '{}',
                calendar_id TEXT,
                planned_start TEXT,
                planned_finish TEXT,
                min_date TEXT,
                max_date TEXT,
                priority INTEGER NOT NULL DEFAULT 500,
                status TEXT NOT NULL DEFAULT 'NotStarted',
                responsible_id TEXT,
                planned_progress_pct REAL NOT NULL DEFAULT 0,
                planned_quantity REAL,
                quantity_unit TEXT NOT NULL DEFAULT '',
                tags TEXT NOT NULL DEFAULT '',
                remarks TEXT NOT NULL DEFAULT '',
                author TEXT NOT NULL DEFAULT '',
                last_editor TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL,
                modified_at TEXT NOT NULL,
                row_version TEXT NOT NULL DEFAULT '',
                is_deleted INTEGER NOT NULL DEFAULT 0,
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_activities_schedule ON planning_activities(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_activities_wbs_node ON planning_activities(wbs_node_id);
            CREATE INDEX IF NOT EXISTS idx_activities_status ON planning_activities(status);
            CREATE INDEX IF NOT EXISTS idx_activities_code ON planning_activities(code);
            """);
    }

    internal static void Migration032_PlanningCalendars(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_calendars (
                id TEXT PRIMARY KEY,
                code TEXT NOT NULL DEFAULT '',
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                time_zone_id TEXT NOT NULL DEFAULT '',
                is_active INTEGER NOT NULL DEFAULT 1,
                effective_date TEXT,
                author TEXT NOT NULL DEFAULT '',
                modified_at TEXT NOT NULL,
                row_version TEXT NOT NULL DEFAULT '',
                calendar_type TEXT NOT NULL DEFAULT 'Corporate',
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_calendars_code ON planning_calendars(code);
            CREATE INDEX IF NOT EXISTS idx_planning_calendars_active ON planning_calendars(is_active);

            CREATE TABLE IF NOT EXISTS planning_calendar_week (
                id TEXT PRIMARY KEY,
                calendar_id TEXT NOT NULL,
                day_of_week INTEGER NOT NULL,
                is_working_day INTEGER NOT NULL DEFAULT 0,
                shifts_json TEXT NOT NULL DEFAULT '[]',
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_calendar_week_cal ON planning_calendar_week(calendar_id);

            CREATE TABLE IF NOT EXISTS planning_calendar_exceptions (
                id TEXT PRIMARY KEY,
                calendar_id TEXT NOT NULL,
                date TEXT NOT NULL,
                exception_type TEXT NOT NULL DEFAULT 'NonWorkingDay',
                special_shifts_json TEXT NOT NULL DEFAULT 'null',
                reason TEXT NOT NULL DEFAULT '',
                source TEXT NOT NULL DEFAULT '',
                user TEXT NOT NULL DEFAULT '',
                registered_at TEXT NOT NULL,
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_calendar_exceptions_cal ON planning_calendar_exceptions(calendar_id);
            CREATE INDEX IF NOT EXISTS idx_calendar_exceptions_date ON planning_calendar_exceptions(date);
            """);
    }

    internal static void Migration033_PlanningLinksAndAudit(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_constraints (
                id TEXT PRIMARY KEY,
                activity_id TEXT NOT NULL,
                constraint_type TEXT NOT NULL DEFAULT 'None',
                constraint_date TEXT,
                reason TEXT NOT NULL DEFAULT '',
                author TEXT NOT NULL DEFAULT '',
                is_active INTEGER NOT NULL DEFAULT 1,
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_constraints_activity ON planning_constraints(activity_id);

            CREATE TABLE IF NOT EXISTS planning_responsibles (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                responsible_type TEXT NOT NULL DEFAULT 'InternalUser',
                display_name TEXT NOT NULL DEFAULT '',
                internal_user_id TEXT,
                role TEXT NOT NULL DEFAULT '',
                assigned_at TEXT NOT NULL,
                remarks TEXT NOT NULL DEFAULT '',
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_responsibles_schedule ON planning_responsibles(schedule_id);

            CREATE TABLE IF NOT EXISTS planning_budget_links (
                id TEXT PRIMARY KEY,
                activity_id TEXT NOT NULL,
                budget_item_id TEXT NOT NULL DEFAULT '',
                apu_id TEXT,
                allocation_method TEXT NOT NULL DEFAULT 'FullItem',
                allocation_value REAL NOT NULL DEFAULT 0,
                is_primary INTEGER NOT NULL DEFAULT 0,
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_budget_links_activity ON planning_budget_links(activity_id);
            CREATE INDEX IF NOT EXISTS idx_planning_budget_links_item ON planning_budget_links(budget_item_id);

            CREATE TABLE IF NOT EXISTS planning_audit (
                id TEXT PRIMARY KEY,
                actor TEXT NOT NULL DEFAULT '',
                utc_timestamp TEXT NOT NULL,
                entity_type TEXT NOT NULL DEFAULT '',
                entity_id TEXT NOT NULL DEFAULT '',
                action TEXT NOT NULL DEFAULT '',
                previous_state TEXT,
                new_state TEXT,
                relevant_values TEXT,
                comment TEXT NOT NULL DEFAULT '',
                result TEXT NOT NULL DEFAULT '',
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_audit_entity ON planning_audit(entity_type, entity_id);
            CREATE INDEX IF NOT EXISTS idx_planning_audit_timestamp ON planning_audit(utc_timestamp);
            """);
    }

    internal static void Migration034_PlanningDependenciesAndCpm(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_dependencies (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                predecessor_id TEXT NOT NULL,
                successor_id TEXT NOT NULL,
                type TEXT NOT NULL,
                lag_json TEXT NOT NULL,
                is_lead INTEGER NOT NULL DEFAULT 0,
                json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_dependencies_schedule ON planning_dependencies(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_planning_dependencies_predecessor ON planning_dependencies(predecessor_id);
            CREATE INDEX IF NOT EXISTS idx_planning_dependencies_successor ON planning_dependencies(successor_id);
            """);
    }

    internal static void Migration035_PlanningBaselines(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_baselines (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                version INTEGER NOT NULL,
                is_official INTEGER NOT NULL DEFAULT 0,
                json_data TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS planning_baseline_snapshots (
                baseline_id TEXT PRIMARY KEY,
                snapshot_json TEXT NOT NULL,
                FOREIGN KEY(baseline_id) REFERENCES planning_baselines(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_planning_baselines_schedule ON planning_baselines(schedule_id);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_planning_baselines_sched_ver ON planning_baselines(schedule_id, version);
            """);
    }

    internal static void Migration036_PlanningProgressCore(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_status_dates (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL UNIQUE,
                status_date TEXT NOT NULL,
                time_zone_id TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                updated_by TEXT NOT NULL,
                comment TEXT NOT NULL,
                row_version TEXT NOT NULL,
                json_data TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS planning_activity_progress (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                activity_id TEXT NOT NULL,
                status_date TEXT NOT NULL,
                row_version TEXT NOT NULL,
                json_data TEXT NOT NULL,
                UNIQUE(schedule_id, activity_id)
            );

            CREATE TABLE IF NOT EXISTS planning_progress_intervals (
                id TEXT PRIMARY KEY,
                progress_update_id TEXT NOT NULL,
                schedule_id TEXT NOT NULL,
                activity_id TEXT NOT NULL,
                json_data TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_progress_schedule ON planning_activity_progress(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_planning_progress_activity ON planning_activity_progress(activity_id);
            CREATE INDEX IF NOT EXISTS idx_planning_intervals_activity ON planning_progress_intervals(activity_id);
            CREATE INDEX IF NOT EXISTS idx_planning_intervals_update ON planning_progress_intervals(progress_update_id);
            """);
    }

    internal static void Migration037_PlanningProgressSnapshots(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_progress_snapshots (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                version INTEGER NOT NULL,
                status_date TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL,
                sha256 TEXT NOT NULL,
                json_data TEXT NOT NULL,
                UNIQUE(schedule_id, version)
            );

            CREATE TABLE IF NOT EXISTS planning_progress_snapshot_items (
                snapshot_id TEXT NOT NULL,
                activity_id TEXT NOT NULL,
                json_data TEXT NOT NULL,
                PRIMARY KEY(snapshot_id, activity_id),
                FOREIGN KEY(snapshot_id) REFERENCES planning_progress_snapshots(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_planning_progress_snap_sched ON planning_progress_snapshots(schedule_id);
            """);
    }

    internal static void Migration038_PlanningProgressDelayEvidence(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_delay_reasons (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                activity_id TEXT NOT NULL,
                progress_update_id TEXT NOT NULL,
                code TEXT NOT NULL,
                json_data TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS planning_progress_evidences (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                activity_id TEXT,
                progress_update_id TEXT NOT NULL,
                type TEXT NOT NULL,
                reference TEXT NOT NULL,
                json_data TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_planning_delay_sched ON planning_delay_reasons(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_planning_delay_act ON planning_delay_reasons(activity_id);
            CREATE INDEX IF NOT EXISTS idx_planning_evidence_update ON planning_progress_evidences(progress_update_id);
            """);
    }

    internal static void Migration039_PlanningEarnedValue(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_earned_value (
                schedule_id TEXT PRIMARY KEY,
                status_date TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS planning_earned_value_snapshots (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                version INTEGER NOT NULL,
                status_date TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL,
                sha256 TEXT NOT NULL,
                json_data TEXT NOT NULL,
                UNIQUE(schedule_id, version)
            );

            CREATE TABLE IF NOT EXISTS planning_earned_value_history (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                status_date TEXT NOT NULL,
                recorded_utc TEXT NOT NULL,
                recorded_by TEXT NOT NULL,
                json_data TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_evm_snap_sched ON planning_earned_value_snapshots(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_evm_hist_sched ON planning_earned_value_history(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_evm_hist_utc ON planning_earned_value_history(recorded_utc);
            """);
    }

    internal static void Migration040_PlanningDashboard(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS planning_dashboard_snapshots (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                version INTEGER NOT NULL,
                status_date TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL,
                sha256 TEXT NOT NULL,
                json_data TEXT NOT NULL,
                UNIQUE(schedule_id, version)
            );

            CREATE TABLE IF NOT EXISTS planning_dashboard_alerts (
                id TEXT PRIMARY KEY,
                schedule_id TEXT NOT NULL,
                code TEXT NOT NULL,
                severity INTEGER NOT NULL,
                lifecycle INTEGER NOT NULL,
                raised_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS planning_dashboard_preferences (
                user_key TEXT NOT NULL,
                schedule_id TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL,
                PRIMARY KEY (user_key, schedule_id)
            );

            CREATE INDEX IF NOT EXISTS idx_dash_snap_sched ON planning_dashboard_snapshots(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_dash_alert_sched ON planning_dashboard_alerts(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_dash_alert_life ON planning_dashboard_alerts(lifecycle);
            """);
    }

    internal static void Migration041_ProcurementSuppliers(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_suppliers (
                id TEXT PRIMARY KEY,
                code TEXT NOT NULL UNIQUE,
                status INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_supplier_contacts (
                id TEXT PRIMARY KEY,
                supplier_id TEXT NOT NULL,
                is_active INTEGER NOT NULL,
                json_data TEXT NOT NULL,
                FOREIGN KEY(supplier_id) REFERENCES procurement_suppliers(id)
            );
            CREATE INDEX IF NOT EXISTS idx_proc_sup_code ON procurement_suppliers(code);
            CREATE INDEX IF NOT EXISTS idx_proc_sup_status ON procurement_suppliers(status);
            CREATE INDEX IF NOT EXISTS idx_proc_contact_sup ON procurement_supplier_contacts(supplier_id);
            """);
    }

    internal static void Migration042_ProcurementRequisitions(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_requisitions (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL,
                number TEXT NOT NULL,
                status INTEGER NOT NULL,
                required_date TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_requisition_items (
                id TEXT PRIMARY KEY,
                requisition_id TEXT NOT NULL,
                resource_code TEXT,
                required_date TEXT,
                json_data TEXT NOT NULL,
                FOREIGN KEY(requisition_id) REFERENCES procurement_requisitions(id)
            );
            CREATE TABLE IF NOT EXISTS procurement_requisition_origins (
                id TEXT PRIMARY KEY,
                requisition_item_id TEXT NOT NULL,
                origin_kind INTEGER NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_pr_proj ON procurement_requisitions(project_path);
            CREATE INDEX IF NOT EXISTS idx_proc_pr_status ON procurement_requisitions(status);
            CREATE INDEX IF NOT EXISTS idx_proc_pr_num ON procurement_requisitions(number);
            CREATE INDEX IF NOT EXISTS idx_proc_pr_reqdate ON procurement_requisitions(required_date);
            CREATE INDEX IF NOT EXISTS idx_proc_pri_req ON procurement_requisition_items(requisition_id);
            """);
    }

    internal static void Migration043_ProcurementRfq(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_rfqs (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL,
                number TEXT NOT NULL,
                status INTEGER NOT NULL,
                deadline_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_rfq_items (
                id TEXT PRIMARY KEY,
                rfq_id TEXT NOT NULL,
                json_data TEXT NOT NULL,
                FOREIGN KEY(rfq_id) REFERENCES procurement_rfqs(id)
            );
            CREATE TABLE IF NOT EXISTS procurement_rfq_suppliers (
                id TEXT PRIMARY KEY,
                rfq_id TEXT NOT NULL,
                supplier_id TEXT NOT NULL,
                status INTEGER NOT NULL,
                json_data TEXT NOT NULL,
                FOREIGN KEY(rfq_id) REFERENCES procurement_rfqs(id)
            );
            CREATE TABLE IF NOT EXISTS procurement_quotation_responses (
                id TEXT PRIMARY KEY,
                rfq_id TEXT NOT NULL,
                supplier_id TEXT NOT NULL,
                received_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_rfq_proj ON procurement_rfqs(project_path);
            CREATE INDEX IF NOT EXISTS idx_proc_rfq_status ON procurement_rfqs(status);
            CREATE INDEX IF NOT EXISTS idx_proc_rfq_num ON procurement_rfqs(number);
            CREATE INDEX IF NOT EXISTS idx_proc_rfq_deadline ON procurement_rfqs(deadline_utc);
            CREATE INDEX IF NOT EXISTS idx_proc_rfq_inv_rfq ON procurement_rfq_suppliers(rfq_id);
            CREATE INDEX IF NOT EXISTS idx_proc_rfq_inv_sup ON procurement_rfq_suppliers(supplier_id);
            CREATE INDEX IF NOT EXISTS idx_proc_qr_rfq ON procurement_quotation_responses(rfq_id);
            """);
    }

    internal static void Migration044_ProcurementAuditDocuments(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_documents (
                id TEXT PRIMARY KEY,
                entity_type TEXT NOT NULL,
                entity_id TEXT NOT NULL,
                kind INTEGER NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_audit (
                id TEXT PRIMARY KEY,
                entity_type TEXT NOT NULL,
                entity_id TEXT NOT NULL,
                action TEXT NOT NULL,
                actor TEXT NOT NULL,
                utc_ts TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_doc_ent ON procurement_documents(entity_type, entity_id);
            CREATE INDEX IF NOT EXISTS idx_proc_audit_ent ON procurement_audit(entity_type, entity_id);
            CREATE INDEX IF NOT EXISTS idx_proc_audit_utc ON procurement_audit(utc_ts);
            """);
    }

    internal static void Migration045_ProcurementOffers(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_offers (
                id TEXT PRIMARY KEY,
                rfq_id TEXT NOT NULL,
                supplier_id TEXT NOT NULL,
                status INTEGER NOT NULL,
                version INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_offer_items (
                id TEXT PRIMARY KEY,
                offer_id TEXT NOT NULL,
                rfq_item_id TEXT,
                json_data TEXT NOT NULL,
                FOREIGN KEY(offer_id) REFERENCES procurement_offers(id)
            );
            CREATE TABLE IF NOT EXISTS procurement_offer_conditions (
                id TEXT PRIMARY KEY,
                offer_id TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_offer_rfq ON procurement_offers(rfq_id);
            CREATE INDEX IF NOT EXISTS idx_proc_offer_sup ON procurement_offers(supplier_id);
            CREATE INDEX IF NOT EXISTS idx_proc_offer_st ON procurement_offers(status);
            """);
    }

    internal static void Migration046_ProcurementComparisons(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_tech_evaluations (
                id TEXT PRIMARY KEY,
                offer_id TEXT NOT NULL,
                compliance INTEGER NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_commercial_evaluations (
                id TEXT PRIMARY KEY,
                offer_id TEXT NOT NULL,
                risk INTEGER NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_bid_comparisons (
                id TEXT PRIMARY KEY,
                rfq_id TEXT NOT NULL,
                status INTEGER NOT NULL,
                base_currency TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_tech_offer ON procurement_tech_evaluations(offer_id);
            CREATE INDEX IF NOT EXISTS idx_proc_com_offer ON procurement_commercial_evaluations(offer_id);
            CREATE INDEX IF NOT EXISTS idx_proc_cmp_rfq ON procurement_bid_comparisons(rfq_id);
            """);
    }

    internal static void Migration047_ProcurementAwards(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_awards (
                id TEXT PRIMARY KEY,
                rfq_id TEXT NOT NULL,
                status INTEGER NOT NULL,
                kind INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_award_items (
                id TEXT PRIMARY KEY,
                award_id TEXT NOT NULL,
                supplier_id TEXT NOT NULL,
                offer_id TEXT NOT NULL,
                json_data TEXT NOT NULL,
                FOREIGN KEY(award_id) REFERENCES procurement_awards(id)
            );
            CREATE INDEX IF NOT EXISTS idx_proc_award_rfq ON procurement_awards(rfq_id);
            CREATE INDEX IF NOT EXISTS idx_proc_award_st ON procurement_awards(status);
            CREATE INDEX IF NOT EXISTS idx_proc_award_item_aw ON procurement_award_items(award_id);
            """);
    }

    internal static void Migration048_PurchaseOrders(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_purchase_orders (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL,
                award_id TEXT,
                supplier_id TEXT NOT NULL,
                status INTEGER NOT NULL,
                version INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_po_proj ON procurement_purchase_orders(project_path);
            CREATE INDEX IF NOT EXISTS idx_proc_po_award ON procurement_purchase_orders(award_id);
            CREATE INDEX IF NOT EXISTS idx_proc_po_st ON procurement_purchase_orders(status);
            """);
    }

    internal static void Migration049_GoodsReceiptsReturns(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_goods_receipts (
                id TEXT PRIMARY KEY,
                po_id TEXT NOT NULL,
                project_path TEXT NOT NULL,
                status INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_goods_returns (
                id TEXT PRIMARY KEY,
                po_id TEXT NOT NULL,
                project_path TEXT NOT NULL,
                status INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_gr_po ON procurement_goods_receipts(po_id);
            CREATE INDEX IF NOT EXISTS idx_proc_gr_proj ON procurement_goods_receipts(project_path);
            CREATE INDEX IF NOT EXISTS idx_proc_ret_proj ON procurement_goods_returns(project_path);
            """);
    }

    internal static void Migration050_ProjectInventory(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_inventory (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL,
                code TEXT NOT NULL,
                lot TEXT NOT NULL,
                location TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procurement_inventory_movements (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL,
                code TEXT NOT NULL,
                kind INTEGER NOT NULL,
                utc_ts TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_inv_proj ON procurement_inventory(project_path);
            CREATE INDEX IF NOT EXISTS idx_proc_inv_code ON procurement_inventory(project_path, code);
            CREATE INDEX IF NOT EXISTS idx_proc_mov_proj ON procurement_inventory_movements(project_path);
            """);
    }

    internal static void Migration051_ProcurementCommitments(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS procurement_commitment_snapshots (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL,
                asof_utc TEXT NOT NULL,
                hash TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proc_commit_proj ON procurement_commitment_snapshots(project_path);
            """);
    }

    internal static void Migration048_Contracts(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Reservado / no registrado: scaffolding contractual fuera de Fase 9.
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS contracts (
                id TEXT PRIMARY KEY,
                project_path TEXT NOT NULL,
                visible_number TEXT NOT NULL,
                status INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            """);
    }

    internal static void Migration052_PaymentCertificates(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS payment_certificates (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                visible_number TEXT NOT NULL,
                revision_number INTEGER NOT NULL DEFAULT 1,
                period_start TEXT NOT NULL,
                period_end TEXT NOT NULL,
                cut_off_date TEXT NOT NULL,
                presentation_date TEXT NOT NULL,
                currency TEXT NOT NULL DEFAULT 'CLP',
                exchange_rate REAL NOT NULL DEFAULT 1.0,
                status INTEGER NOT NULL DEFAULT 0,
                presenter TEXT NOT NULL DEFAULT '',
                revisor TEXT NOT NULL DEFAULT '',
                aprobador TEXT NOT NULL DEFAULT '',
                approved_at_utc TEXT,
                comment TEXT NOT NULL DEFAULT '',
                subtotal_valorizado REAL NOT NULL DEFAULT 0,
                reajuste REAL NOT NULL DEFAULT 0,
                anticipo_amortizado REAL NOT NULL DEFAULT 0,
                retenciones_aplicadas REAL NOT NULL DEFAULT 0,
                descuentos REAL NOT NULL DEFAULT 0,
                multas REAL NOT NULL DEFAULT 0,
                impuestos REAL NOT NULL DEFAULT 0,
                monto_bruto REAL NOT NULL DEFAULT 0,
                monto_liquido REAL NOT NULL DEFAULT 0,
                monto_anterior REAL NOT NULL DEFAULT 0,
                monto_acumulado REAL NOT NULL DEFAULT 0,
                saldo_contractual REAL NOT NULL DEFAULT 0,
                saldo_por_valorizar REAL NOT NULL DEFAULT 0,
                row_version TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL,
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                hash TEXT NOT NULL DEFAULT '',
                compatibility_metadata TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_pc_contract ON payment_certificates(contract_id);
            CREATE INDEX IF NOT EXISTS idx_pc_visible ON payment_certificates(visible_number);
            CREATE INDEX IF NOT EXISTS idx_pc_status ON payment_certificates(status);
            CREATE INDEX IF NOT EXISTS idx_pc_created ON payment_certificates(created_utc);
            CREATE TABLE IF NOT EXISTS payment_certificate_lines (
                id TEXT PRIMARY KEY,
                payment_certificate_id TEXT NOT NULL,
                partida_contractual TEXT NOT NULL DEFAULT '',
                wbs_code TEXT NOT NULL DEFAULT '',
                activity_id TEXT NOT NULL DEFAULT '',
                apu_id TEXT,
                description TEXT NOT NULL,
                unit TEXT NOT NULL,
                cantidad_contractual REAL NOT NULL DEFAULT 0,
                precio_unitario_contractual REAL NOT NULL DEFAULT 0,
                cantidad_anterior REAL NOT NULL DEFAULT 0,
                cantidad_periodo REAL NOT NULL DEFAULT 0,
                cantidad_acumulada REAL NOT NULL DEFAULT 0,
                saldo_cantidad REAL NOT NULL DEFAULT 0,
                monto_anterior REAL NOT NULL DEFAULT 0,
                monto_periodo REAL NOT NULL DEFAULT 0,
                monto_acumulado REAL NOT NULL DEFAULT 0,
                saldo_monetario REAL NOT NULL DEFAULT 0,
                origen_avance TEXT NOT NULL DEFAULT 'Manual',
                evidencia TEXT NOT NULL DEFAULT '',
                observacion TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'Pending',
                row_version TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_pcl_certificate ON payment_certificate_lines(payment_certificate_id);
            CREATE TABLE IF NOT EXISTS payment_certificate_revisions (
                id TEXT PRIMARY KEY,
                payment_certificate_id TEXT NOT NULL,
                revision_number INTEGER NOT NULL,
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL,
                snapshot_json TEXT NOT NULL,
                hash TEXT NOT NULL,
                comment TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_pcr_certificate ON payment_certificate_revisions(payment_certificate_id);
            CREATE INDEX IF NOT EXISTS idx_pcr_revision ON payment_certificate_revisions(payment_certificate_id, revision_number);
            CREATE TABLE IF NOT EXISTS payment_certificate_audit (
                id TEXT PRIMARY KEY,
                payment_certificate_id TEXT NOT NULL,
                action TEXT NOT NULL,
                actor TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                details_json TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_pca_certificate ON payment_certificate_audit(payment_certificate_id);
            CREATE INDEX IF NOT EXISTS idx_pca_actor ON payment_certificate_audit(actor);
            CREATE INDEX IF NOT EXISTS idx_pca_timestamp ON payment_certificate_audit(timestamp_utc);
            """);
    }

    internal static void Migration053_ContractAdvancesRetentions(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS contract_advances (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                visible_number TEXT NOT NULL DEFAULT '',
                amount REAL NOT NULL,
                currency TEXT NOT NULL DEFAULT 'CLP',
                date TEXT NOT NULL,
                amortization_percentage REAL NOT NULL DEFAULT 0,
                initial_balance REAL NOT NULL DEFAULT 0,
                current_balance REAL NOT NULL DEFAULT 0,
                total_amortized REAL NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'Active',
                document_reference TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                advance_type INTEGER NOT NULL DEFAULT 0,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ca_contract ON contract_advances(contract_id);
            CREATE INDEX IF NOT EXISTS idx_ca_status ON contract_advances(status);
            CREATE TABLE IF NOT EXISTS contract_advance_amortizations (
                id TEXT PRIMARY KEY,
                payment_certificate_id TEXT NOT NULL,
                contract_advance_id TEXT NOT NULL,
                amount REAL NOT NULL DEFAULT 0,
                percentage REAL NOT NULL DEFAULT 0,
                pre_amortization_balance REAL NOT NULL DEFAULT 0,
                post_amortization_balance REAL NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_caa_certificate ON contract_advance_amortizations(payment_certificate_id);
            CREATE INDEX IF NOT EXISTS idx_caa_advance ON contract_advance_amortizations(contract_advance_id);
            CREATE TABLE IF NOT EXISTS contract_retentions (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                retention_type INTEGER NOT NULL DEFAULT 0,
                base_type TEXT NOT NULL DEFAULT 'Subtotal',
                percentage REAL NOT NULL DEFAULT 0,
                fixed_amount REAL NOT NULL DEFAULT 0,
                limit_amount REAL NOT NULL DEFAULT 0,
                expiry_date TEXT,
                release_condition TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'Active',
                observation TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cr_contract ON contract_retentions(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cr_status ON contract_retentions(status);
            CREATE TABLE IF NOT EXISTS contract_retention_applications (
                id TEXT PRIMARY KEY,
                payment_certificate_id TEXT NOT NULL,
                contract_retention_id TEXT NOT NULL,
                base_amount REAL NOT NULL DEFAULT 0,
                percentage REAL NOT NULL DEFAULT 0,
                applied_amount REAL NOT NULL DEFAULT 0,
                cumulative_applied REAL NOT NULL DEFAULT 0,
                released_amount REAL NOT NULL DEFAULT 0,
                pending_balance REAL NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_crea_certificate ON contract_retention_applications(payment_certificate_id);
            CREATE INDEX IF NOT EXISTS idx_crea_retention ON contract_retention_applications(contract_retention_id);
            CREATE TABLE IF NOT EXISTS contract_retention_releases (
                id TEXT PRIMARY KEY,
                contract_retention_id TEXT NOT NULL,
                payment_certificate_id TEXT,
                released_amount REAL NOT NULL DEFAULT 0,
                release_date TEXT NOT NULL,
                authorized_by TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_crelr_retention ON contract_retention_releases(contract_retention_id);
            CREATE INDEX IF NOT EXISTS idx_crelr_certificate ON contract_retention_releases(payment_certificate_id);
            """);
    }

    internal static void Migration055_ContractChanges(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS contract_change_requests (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                visible_number TEXT NOT NULL DEFAULT '',
                source INTEGER NOT NULL DEFAULT 0,
                title TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                cause TEXT NOT NULL DEFAULT '',
                requester TEXT NOT NULL DEFAULT '',
                request_date TEXT NOT NULL,
                urgency INTEGER NOT NULL DEFAULT 1,
                wbs_code TEXT NOT NULL DEFAULT '',
                affected_activities TEXT NOT NULL DEFAULT '',
                affected_items TEXT NOT NULL DEFAULT '',
                supporting_documents TEXT NOT NULL DEFAULT '',
                preliminary_cost_impact REAL NOT NULL DEFAULT 0,
                preliminary_days_impact INTEGER NOT NULL DEFAULT 0,
                currency_code TEXT NOT NULL DEFAULT 'CLP',
                status INTEGER NOT NULL DEFAULT 0,
                responsible TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ccr_contract ON contract_change_requests(contract_id);
            CREATE INDEX IF NOT EXISTS idx_ccr_status ON contract_change_requests(status);
            CREATE INDEX IF NOT EXISTS idx_ccr_date ON contract_change_requests(request_date);

            CREATE TABLE IF NOT EXISTS contract_change_orders (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                change_request_id TEXT DEFAULT '',
                visible_number TEXT NOT NULL DEFAULT '',
                version INTEGER NOT NULL DEFAULT 1,
                effective_date TEXT NOT NULL,
                currency_code TEXT NOT NULL DEFAULT 'CLP',
                change_amount REAL NOT NULL DEFAULT 0,
                added_days INTEGER NOT NULL DEFAULT 0,
                added_items TEXT NOT NULL DEFAULT '',
                modified_items TEXT NOT NULL DEFAULT '',
                removed_items TEXT NOT NULL DEFAULT '',
                affected_activities TEXT NOT NULL DEFAULT '',
                affected_milestones TEXT NOT NULL DEFAULT '',
                cost_impact REAL NOT NULL DEFAULT 0,
                schedule_impact_days INTEGER NOT NULL DEFAULT 0,
                forecast_impact REAL NOT NULL DEFAULT 0,
                evm_impact REAL NOT NULL DEFAULT 0,
                status INTEGER NOT NULL DEFAULT 0,
                approvers TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                hash_sha256 TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cco_contract ON contract_change_orders(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cco_cr ON contract_change_orders(change_request_id);
            CREATE INDEX IF NOT EXISTS idx_cco_status ON contract_change_orders(status);

            CREATE TABLE IF NOT EXISTS contract_change_order_lines (
                id TEXT PRIMARY KEY,
                change_order_id TEXT NOT NULL,
                item_code TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                unit TEXT NOT NULL DEFAULT '',
                quantity REAL NOT NULL DEFAULT 0,
                unit_price REAL NOT NULL DEFAULT 0,
                total_price REAL NOT NULL DEFAULT 0,
                change_type TEXT NOT NULL DEFAULT 'Add',
                comment TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_ccol_order ON contract_change_order_lines(change_order_id);

            CREATE TABLE IF NOT EXISTS contract_change_order_versions (
                id TEXT PRIMARY KEY,
                change_order_id TEXT NOT NULL,
                version_number INTEGER NOT NULL,
                snapshot_json TEXT NOT NULL,
                hash_sha256 TEXT NOT NULL DEFAULT '',
                changed_by TEXT NOT NULL DEFAULT '',
                change_reason TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ccov_order ON contract_change_order_versions(change_order_id);

            CREATE TABLE IF NOT EXISTS contract_change_order_snapshots (
                id TEXT PRIMARY KEY,
                change_order_id TEXT NOT NULL,
                contract_id TEXT NOT NULL,
                project_path TEXT NOT NULL DEFAULT '',
                asof_utc TEXT NOT NULL,
                snapshot_json TEXT NOT NULL,
                hash TEXT NOT NULL DEFAULT '',
                version_label TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_ccos_order ON contract_change_order_snapshots(change_order_id);
            CREATE INDEX IF NOT EXISTS idx_ccos_contract ON contract_change_order_snapshots(contract_id);

            CREATE TABLE IF NOT EXISTS contract_change_order_audit (
                id TEXT PRIMARY KEY,
                change_order_id TEXT NOT NULL,
                action TEXT NOT NULL,
                actor TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                details_json TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_ccoa_order ON contract_change_order_audit(change_order_id);

            CREATE TABLE IF NOT EXISTS contract_extra_works (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                change_request_id TEXT DEFAULT '',
                change_order_id TEXT DEFAULT '',
                visible_number TEXT NOT NULL DEFAULT '',
                instruction TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                origin INTEGER NOT NULL DEFAULT 0,
                instruction_date TEXT NOT NULL,
                wbs_code TEXT NOT NULL DEFAULT '',
                activity_code TEXT NOT NULL DEFAULT '',
                resources TEXT NOT NULL DEFAULT '',
                materials TEXT NOT NULL DEFAULT '',
                labor TEXT NOT NULL DEFAULT '',
                equipment TEXT NOT NULL DEFAULT '',
                quantities REAL NOT NULL DEFAULT 0,
                hours REAL NOT NULL DEFAULT 0,
                estimated_cost REAL NOT NULL DEFAULT 0,
                final_cost REAL NOT NULL DEFAULT 0,
                currency_code TEXT NOT NULL DEFAULT 'CLP',
                evidence TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                approved_by TEXT NOT NULL DEFAULT '',
                approval_date TEXT,
                valuation_amount REAL NOT NULL DEFAULT 0,
                status INTEGER NOT NULL DEFAULT 0,
                row_version TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cew_contract ON contract_extra_works(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cew_cr ON contract_extra_works(change_request_id);
            CREATE INDEX IF NOT EXISTS idx_cew_co ON contract_extra_works(change_order_id);
            CREATE INDEX IF NOT EXISTS idx_cew_status ON contract_extra_works(status);

            CREATE TABLE IF NOT EXISTS contract_claims (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                visible_number TEXT NOT NULL DEFAULT '',
                type INTEGER NOT NULL DEFAULT 0,
                event_description TEXT NOT NULL DEFAULT '',
                event_date TEXT NOT NULL,
                notification_date TEXT NOT NULL,
                cause TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                amount_claimed REAL NOT NULL DEFAULT 0,
                days_claimed INTEGER NOT NULL DEFAULT 0,
                amount_awarded REAL NOT NULL DEFAULT 0,
                days_awarded INTEGER NOT NULL DEFAULT 0,
                currency_code TEXT NOT NULL DEFAULT 'CLP',
                affected_activities TEXT NOT NULL DEFAULT '',
                wbs_code TEXT NOT NULL DEFAULT '',
                supporting_documents TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                response TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                row_version TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ccl_contract ON contract_claims(contract_id);
            CREATE INDEX IF NOT EXISTS idx_ccl_status ON contract_claims(status);
            CREATE INDEX IF NOT EXISTS idx_ccl_type ON contract_claims(type);

            CREATE TABLE IF NOT EXISTS contract_claim_events (
                id TEXT PRIMARY KEY,
                claim_id TEXT NOT NULL,
                event_type TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                event_date TEXT NOT NULL,
                actor TEXT NOT NULL DEFAULT '',
                details_json TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_cce_claim ON contract_claim_events(claim_id);

            CREATE TABLE IF NOT EXISTS contract_claim_evidence (
                id TEXT PRIMARY KEY,
                claim_id TEXT NOT NULL,
                document_reference TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                file_name TEXT NOT NULL DEFAULT '',
                uploaded_utc TEXT NOT NULL,
                uploaded_by TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_ccev_claim ON contract_claim_evidence(claim_id);

            CREATE TABLE IF NOT EXISTS contract_claim_evaluations (
                id TEXT PRIMARY KEY,
                claim_id TEXT NOT NULL,
                evaluator TEXT NOT NULL DEFAULT '',
                evaluation_date TEXT NOT NULL,
                evaluated_amount REAL NOT NULL DEFAULT 0,
                evaluated_days INTEGER NOT NULL DEFAULT 0,
                justification TEXT NOT NULL DEFAULT '',
                recommendation TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_ccel_claim ON contract_claim_evaluations(claim_id);

            CREATE TABLE IF NOT EXISTS contract_claim_responses (
                id TEXT PRIMARY KEY,
                claim_id TEXT NOT NULL,
                responder TEXT NOT NULL DEFAULT '',
                response_date TEXT NOT NULL,
                response_type TEXT NOT NULL DEFAULT '',
                response_detail TEXT NOT NULL DEFAULT '',
                offered_amount REAL NOT NULL DEFAULT 0,
                offered_days INTEGER NOT NULL DEFAULT 0,
                comment TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_ccrsp_claim ON contract_claim_responses(claim_id);

            CREATE TABLE IF NOT EXISTS contract_change_audit (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                entity_type TEXT NOT NULL DEFAULT '',
                entity_id TEXT NOT NULL DEFAULT '',
                action TEXT NOT NULL,
                actor TEXT NOT NULL DEFAULT '',
                timestamp_utc TEXT NOT NULL,
                details_json TEXT NOT NULL DEFAULT '',
                is_denied_attempt INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_cca_contract ON contract_change_audit(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cca_entity ON contract_change_audit(entity_type, entity_id);
            CREATE INDEX IF NOT EXISTS idx_cca_timestamp ON contract_change_audit(timestamp_utc);
            """);
    }

    internal static void Migration054_PaymentObservationsSnapshots(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS contract_payment_observations (
                id TEXT PRIMARY KEY,
                payment_certificate_id TEXT NOT NULL,
                payment_certificate_line_id TEXT,
                author TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT '',
                severity TEXT NOT NULL DEFAULT 'Info',
                status INTEGER NOT NULL DEFAULT 0,
                comment TEXT NOT NULL DEFAULT '',
                response TEXT NOT NULL DEFAULT '',
                responder TEXT NOT NULL DEFAULT '',
                resolved_utc TEXT,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cpo_certificate ON contract_payment_observations(payment_certificate_id);
            CREATE INDEX IF NOT EXISTS idx_cpo_line ON contract_payment_observations(payment_certificate_line_id);
            CREATE INDEX IF NOT EXISTS idx_cpo_status ON contract_payment_observations(status);
            CREATE TABLE IF NOT EXISTS contract_payment_snapshots (
                id TEXT PRIMARY KEY,
                payment_certificate_id TEXT NOT NULL,
                contract_id TEXT NOT NULL,
                project_path TEXT NOT NULL,
                asof_utc TEXT NOT NULL,
                snapshot_type TEXT NOT NULL DEFAULT 'Revision',
                snapshot_json TEXT NOT NULL,
                hash TEXT NOT NULL,
                version_label TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_cps_certificate ON contract_payment_snapshots(payment_certificate_id);
            CREATE INDEX IF NOT EXISTS idx_cps_contract ON contract_payment_snapshots(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cps_project ON contract_payment_snapshots(project_path);
            """);
    }

    internal static void Migration056_ContractAdministration(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS contract_adjustments (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                project_path TEXT NOT NULL DEFAULT '',
                visible_number INTEGER NOT NULL DEFAULT 0,
                period INTEGER NOT NULL DEFAULT 0,
                base_date TEXT NOT NULL,
                calculation_date TEXT NOT NULL,
                adjustment_type INTEGER NOT NULL DEFAULT 0,
                currency_code TEXT NOT NULL DEFAULT '',
                base_amount REAL NOT NULL DEFAULT 0,
                adjusted_amount REAL NOT NULL DEFAULT 0,
                status INTEGER NOT NULL DEFAULT 0,
                description TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                hash TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cadj_contract ON contract_adjustments(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cadj_project ON contract_adjustments(project_path);
            CREATE INDEX IF NOT EXISTS idx_cadj_status ON contract_adjustments(status);
            CREATE INDEX IF NOT EXISTS idx_cadj_period ON contract_adjustments(period);

            CREATE TABLE IF NOT EXISTS adjustment_formulas (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_af_contract ON adjustment_formulas(contract_id);

            CREATE TABLE IF NOT EXISTS adjustment_indexes (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL DEFAULT '',
                code TEXT NOT NULL DEFAULT '',
                value REAL NOT NULL DEFAULT 0,
                date TEXT NOT NULL,
                source TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ai_name ON adjustment_indexes(name);
            CREATE INDEX IF NOT EXISTS idx_ai_code ON adjustment_indexes(code);
            CREATE INDEX IF NOT EXISTS idx_ai_date ON adjustment_indexes(date);

            CREATE TABLE IF NOT EXISTS contract_guarantees (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                project_path TEXT NOT NULL DEFAULT '',
                visible_number INTEGER NOT NULL DEFAULT 0,
                guarantee_type INTEGER NOT NULL DEFAULT 0,
                number TEXT NOT NULL DEFAULT '',
                issuer TEXT NOT NULL DEFAULT '',
                institution TEXT NOT NULL DEFAULT '',
                beneficiary TEXT NOT NULL DEFAULT '',
                amount REAL NOT NULL DEFAULT 0,
                percentage REAL NOT NULL DEFAULT 0,
                currency_code TEXT NOT NULL DEFAULT '',
                start_date TEXT NOT NULL,
                expiry_date TEXT NOT NULL,
                status INTEGER NOT NULL DEFAULT 0,
                supporting_documents TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cguar_contract ON contract_guarantees(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cguar_project ON contract_guarantees(project_path);
            CREATE INDEX IF NOT EXISTS idx_cguar_status ON contract_guarantees(status);
            CREATE INDEX IF NOT EXISTS idx_cguar_expiry ON contract_guarantees(expiry_date);

            CREATE TABLE IF NOT EXISTS contract_penalties (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                project_path TEXT NOT NULL DEFAULT '',
                visible_number INTEGER NOT NULL DEFAULT 0,
                penalty_type INTEGER NOT NULL DEFAULT 0,
                cause TEXT NOT NULL DEFAULT '',
                date TEXT NOT NULL,
                amount REAL NOT NULL DEFAULT 0,
                percentage REAL NOT NULL DEFAULT 0,
                currency_code TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                observations TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cpen_contract ON contract_penalties(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cpen_project ON contract_penalties(project_path);
            CREATE INDEX IF NOT EXISTS idx_cpen_status ON contract_penalties(status);

            CREATE TABLE IF NOT EXISTS contract_invoices (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                project_path TEXT NOT NULL DEFAULT '',
                visible_number INTEGER NOT NULL DEFAULT 0,
                invoice_number TEXT NOT NULL DEFAULT '',
                date TEXT NOT NULL,
                supplier TEXT NOT NULL DEFAULT '',
                client TEXT NOT NULL DEFAULT '',
                currency_code TEXT NOT NULL DEFAULT '',
                subtotal REAL NOT NULL DEFAULT 0,
                tax_amount REAL NOT NULL DEFAULT 0,
                total REAL NOT NULL DEFAULT 0,
                status INTEGER NOT NULL DEFAULT 0,
                payment_certificate_reference TEXT NOT NULL DEFAULT '',
                contract_reference TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cinv_contract ON contract_invoices(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cinv_project ON contract_invoices(project_path);
            CREATE INDEX IF NOT EXISTS idx_cinv_status ON contract_invoices(status);

            CREATE TABLE IF NOT EXISTS contract_administration_audit (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                entity_type TEXT NOT NULL DEFAULT '',
                entity_id TEXT NOT NULL DEFAULT '',
                action TEXT NOT NULL,
                actor TEXT NOT NULL DEFAULT '',
                timestamp_utc TEXT NOT NULL,
                details_json TEXT NOT NULL DEFAULT '',
                is_denied_attempt INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_caa_contract ON contract_administration_audit(contract_id);
            CREATE INDEX IF NOT EXISTS idx_caa_entity ON contract_administration_audit(entity_type, entity_id);
            CREATE INDEX IF NOT EXISTS idx_caa_timestamp ON contract_administration_audit(timestamp_utc);
            """);
    }

    internal static void Migration057_ContractDashboard(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS contract_dashboard_snapshots (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                version INTEGER NOT NULL DEFAULT 0,
                status_date TEXT NOT NULL,
                status_label TEXT NOT NULL DEFAULT '',
                sha256 TEXT NOT NULL DEFAULT '',
                author TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cds_contract ON contract_dashboard_snapshots(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cds_version ON contract_dashboard_snapshots(contract_id, version);
            CREATE INDEX IF NOT EXISTS idx_cds_created ON contract_dashboard_snapshots(created_utc);

            CREATE TABLE IF NOT EXISTS contract_dashboard_alerts (
                id TEXT PRIMARY KEY,
                contract_id TEXT NOT NULL,
                code TEXT NOT NULL DEFAULT '',
                severity TEXT NOT NULL DEFAULT '',
                title TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                entity_type TEXT NOT NULL DEFAULT '',
                entity_id TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                responsible TEXT NOT NULL DEFAULT '',
                impact TEXT NOT NULL DEFAULT '',
                suggested_action TEXT NOT NULL DEFAULT '',
                source TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                resolved_utc TEXT,
                resolution TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cda_contract ON contract_dashboard_alerts(contract_id);
            CREATE INDEX IF NOT EXISTS idx_cda_status ON contract_dashboard_alerts(status);
            CREATE INDEX IF NOT EXISTS idx_cda_severity ON contract_dashboard_alerts(severity);
            CREATE INDEX IF NOT EXISTS idx_cda_created ON contract_dashboard_alerts(created_utc);

            CREATE TABLE IF NOT EXISTS contract_dashboard_alert_audit (
                id TEXT PRIMARY KEY,
                alert_id TEXT NOT NULL,
                old_status INTEGER NOT NULL DEFAULT 0,
                new_status INTEGER NOT NULL DEFAULT 0,
                actor TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                comment TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_cdaa_alert ON contract_dashboard_alert_audit(alert_id);
            CREATE INDEX IF NOT EXISTS idx_cdaa_created ON contract_dashboard_alert_audit(created_utc);
            """);
    }

    internal static void Migration058_Closeout(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS project_closeouts (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL,
                code TEXT NOT NULL DEFAULT '',
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                target_date TEXT NOT NULL DEFAULT '',
                status_date TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                readiness_pct REAL NOT NULL DEFAULT 0,
                risk_level TEXT NOT NULL DEFAULT '',
                active_checklist_id TEXT NOT NULL DEFAULT '',
                punch_item_count INTEGER NOT NULL DEFAULT 0,
                non_conformance_count INTEGER NOT NULL DEFAULT 0,
                pending_documents INTEGER NOT NULL DEFAULT 0,
                blockers INTEGER NOT NULL DEFAULT 0,
                comment TEXT NOT NULL DEFAULT '',
                version INTEGER NOT NULL DEFAULT 1,
                hash TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_co_project ON project_closeouts(project_id);
            CREATE INDEX IF NOT EXISTS idx_co_status ON project_closeouts(status);

            CREATE TABLE IF NOT EXISTS closeout_checklist_templates (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                version INTEGER NOT NULL DEFAULT 1,
                is_active INTEGER NOT NULL DEFAULT 1,
                is_obsolete INTEGER NOT NULL DEFAULT 0,
                project_type TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cct_active ON closeout_checklist_templates(is_active);

            CREATE TABLE IF NOT EXISTS closeout_checklists (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                template_id TEXT NOT NULL DEFAULT '',
                template_version INTEGER NOT NULL DEFAULT 1,
                name TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cc_co ON closeout_checklists(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_checklist_items (
                id TEXT PRIMARY KEY,
                checklist_id TEXT NOT NULL,
                code TEXT NOT NULL DEFAULT '',
                title TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                category TEXT NOT NULL DEFAULT '',
                is_mandatory INTEGER NOT NULL DEFAULT 0,
                is_blocking INTEGER NOT NULL DEFAULT 0,
                responsible TEXT NOT NULL DEFAULT '',
                due_date TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                evidence_required INTEGER NOT NULL DEFAULT 0,
                linked_evidence_ids TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                verified_by TEXT NOT NULL DEFAULT '',
                verification_date TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cci_cl ON closeout_checklist_items(checklist_id);
            CREATE INDEX IF NOT EXISTS idx_cci_status ON closeout_checklist_items(status);

            CREATE TABLE IF NOT EXISTS closeout_punch_items (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                project_id TEXT NOT NULL DEFAULT '',
                number INTEGER NOT NULL DEFAULT 0,
                title TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                category TEXT NOT NULL DEFAULT '',
                priority TEXT NOT NULL DEFAULT '',
                severity TEXT NOT NULL DEFAULT '',
                wbs_code TEXT NOT NULL DEFAULT '',
                activity_id TEXT NOT NULL DEFAULT '',
                contract_id TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                detected_date TEXT NOT NULL DEFAULT '',
                due_date TEXT NOT NULL DEFAULT '',
                corrected_date TEXT NOT NULL DEFAULT '',
                verified_date TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                estimated_cost_impact REAL NOT NULL DEFAULT 0,
                estimated_schedule_impact REAL NOT NULL DEFAULT 0,
                linked_evidence_ids TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cpi_co ON closeout_punch_items(closeout_id);
            CREATE INDEX IF NOT EXISTS idx_cpi_status ON closeout_punch_items(status);

            CREATE TABLE IF NOT EXISTS closeout_non_conformances (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                project_id TEXT NOT NULL DEFAULT '',
                origin TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                severity TEXT NOT NULL DEFAULT '',
                category TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                created_date TEXT NOT NULL DEFAULT '',
                due_date TEXT NOT NULL DEFAULT '',
                root_cause TEXT NOT NULL DEFAULT '',
                correction TEXT NOT NULL DEFAULT '',
                linked_evidence_ids TEXT NOT NULL DEFAULT '',
                associated_cost REAL NOT NULL DEFAULT 0,
                schedule_impact REAL NOT NULL DEFAULT 0,
                status INTEGER NOT NULL DEFAULT 0,
                verified_by TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL,
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cnc_co ON closeout_non_conformances(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_document_requirements (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT '',
                document_type TEXT NOT NULL DEFAULT '',
                is_required INTEGER NOT NULL DEFAULT 1,
                is_blocking INTEGER NOT NULL DEFAULT 0,
                min_version INTEGER NOT NULL DEFAULT 1,
                responsible TEXT NOT NULL DEFAULT '',
                due_date TEXT NOT NULL DEFAULT '',
                format TEXT NOT NULL DEFAULT '',
                reference TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                linked_document_id TEXT NOT NULL DEFAULT '',
                integrity_hash TEXT NOT NULL DEFAULT '',
                approved_by TEXT NOT NULL DEFAULT '',
                approved_date TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cdr_co ON closeout_document_requirements(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_documents (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                requirement_id TEXT NOT NULL DEFAULT '',
                document_type TEXT NOT NULL DEFAULT '',
                title TEXT NOT NULL DEFAULT '',
                file_path TEXT NOT NULL DEFAULT '',
                file_name TEXT NOT NULL DEFAULT '',
                version INTEGER NOT NULL DEFAULT 1,
                submitted_by TEXT NOT NULL DEFAULT '',
                submitted_utc TEXT NOT NULL,
                integrity_hash TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                approved_by TEXT NOT NULL DEFAULT '',
                approved_date TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cdoc_co ON closeout_documents(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_evidence (
                id TEXT PRIMARY KEY,
                evidence_type TEXT NOT NULL DEFAULT '',
                entity_type TEXT NOT NULL DEFAULT '',
                entity_id TEXT NOT NULL DEFAULT '',
                file_path TEXT NOT NULL DEFAULT '',
                file_name TEXT NOT NULL DEFAULT '',
                version INTEGER NOT NULL DEFAULT 1,
                uploaded_utc TEXT NOT NULL,
                uploaded_by TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                hash TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                metadata TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ce_entity ON closeout_evidence(entity_type, entity_id);

            CREATE TABLE IF NOT EXISTS closeout_meetings (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                meeting_date TEXT NOT NULL DEFAULT '',
                participants TEXT NOT NULL DEFAULT '',
                agenda TEXT NOT NULL DEFAULT '',
                agreements TEXT NOT NULL DEFAULT '',
                pending_items TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                due_date TEXT NOT NULL DEFAULT '',
                linked_document_ids TEXT NOT NULL DEFAULT '',
                minutes_path TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                created_by TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cm_co ON closeout_meetings(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_exceptions (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                blocker_code TEXT NOT NULL DEFAULT '',
                reason TEXT NOT NULL DEFAULT '',
                accepted_risk TEXT NOT NULL DEFAULT '',
                requested_by TEXT NOT NULL DEFAULT '',
                approved_by TEXT NOT NULL DEFAULT '',
                request_date TEXT NOT NULL DEFAULT '',
                expiry_date TEXT NOT NULL DEFAULT '',
                linked_evidence_ids TEXT NOT NULL DEFAULT '',
                condition TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cex_co ON closeout_exceptions(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_snapshots (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                status_date TEXT NOT NULL DEFAULT '',
                checklist_data TEXT NOT NULL DEFAULT '',
                punch_list_data TEXT NOT NULL DEFAULT '',
                non_conformance_data TEXT NOT NULL DEFAULT '',
                document_data TEXT NOT NULL DEFAULT '',
                reconciliation_data TEXT NOT NULL DEFAULT '',
                readiness_score TEXT NOT NULL DEFAULT '',
                blocker_data TEXT NOT NULL DEFAULT '',
                exception_data TEXT NOT NULL DEFAULT '',
                alerts_data TEXT NOT NULL DEFAULT '',
                source_hashes TEXT NOT NULL DEFAULT '',
                service_version TEXT NOT NULL DEFAULT '',
                created_by TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL,
                hash TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cs_co ON closeout_snapshots(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_alerts (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                code TEXT NOT NULL DEFAULT '',
                severity INTEGER NOT NULL DEFAULT 0,
                title TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                entity_type TEXT NOT NULL DEFAULT '',
                entity_id TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '',
                created_date TEXT NOT NULL DEFAULT '',
                impact TEXT NOT NULL DEFAULT '',
                suggested_action TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'Open',
                row_version TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ca_co ON closeout_alerts(closeout_id);

            CREATE TABLE IF NOT EXISTS closeout_audit (
                id TEXT PRIMARY KEY,
                closeout_id TEXT NOT NULL,
                action TEXT NOT NULL DEFAULT '',
                entity_type TEXT NOT NULL DEFAULT '',
                entity_id TEXT NOT NULL DEFAULT '',
                previous_value TEXT NOT NULL DEFAULT '',
                new_value TEXT NOT NULL DEFAULT '',
                performed_by TEXT NOT NULL DEFAULT '',
                performed_utc TEXT NOT NULL,
                comment TEXT NOT NULL DEFAULT '',
                json_data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_cau_co ON closeout_audit(closeout_id);
            CREATE INDEX IF NOT EXISTS idx_cau_action ON closeout_audit(action);
            """);
    }

    internal static void Migration059_CloseoutFinal(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS closeout_receptions (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL, closeout_id TEXT NOT NULL,
                reception_type INTEGER NOT NULL DEFAULT 0, number INTEGER NOT NULL DEFAULT 0,
                title TEXT NOT NULL DEFAULT '', scheduled_date TEXT NOT NULL DEFAULT '',
                actual_date TEXT NOT NULL DEFAULT '', client TEXT NOT NULL DEFAULT '',
                contractor TEXT NOT NULL DEFAULT '', inspector TEXT NOT NULL DEFAULT '',
                responsible_team TEXT NOT NULL DEFAULT '', scope TEXT NOT NULL DEFAULT '',
                observations TEXT NOT NULL DEFAULT '', document_refs TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0, approved_by TEXT NOT NULL DEFAULT '',
                approval_date TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '', modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '', row_version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS closeout_reception_inspections (
                id TEXT PRIMARY KEY, reception_id TEXT NOT NULL, inspection_date TEXT NOT NULL DEFAULT '',
                location TEXT NOT NULL DEFAULT '', scope TEXT NOT NULL DEFAULT '',
                attendees TEXT NOT NULL DEFAULT '', inspector TEXT NOT NULL DEFAULT '',
                result TEXT NOT NULL DEFAULT '', inspected_items TEXT NOT NULL DEFAULT '',
                observations TEXT NOT NULL DEFAULT '', photo_refs TEXT NOT NULL DEFAULT '',
                document_refs TEXT NOT NULL DEFAULT '', punch_item_ids TEXT NOT NULL DEFAULT '',
                non_conformance_ids TEXT NOT NULL DEFAULT '', decision TEXT NOT NULL DEFAULT '',
                approval_signature TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '', FOREIGN KEY(reception_id) REFERENCES closeout_receptions(id)
            );
            CREATE TABLE IF NOT EXISTS closeout_reception_observations (
                id TEXT PRIMARY KEY, reception_id TEXT NOT NULL, number INTEGER NOT NULL DEFAULT 0,
                description TEXT NOT NULL DEFAULT '', category TEXT NOT NULL DEFAULT '',
                severity TEXT NOT NULL DEFAULT '', responsible TEXT NOT NULL DEFAULT '',
                due_date TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT '',
                punch_item_id TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '', row_version TEXT NOT NULL,
                FOREIGN KEY(reception_id) REFERENCES closeout_receptions(id)
            );
            CREATE TABLE IF NOT EXISTS closeout_reception_records (
                id TEXT PRIMARY KEY, reception_id TEXT NOT NULL, project_id TEXT NOT NULL DEFAULT '',
                contract_id TEXT NOT NULL DEFAULT '', reception_type INTEGER NOT NULL DEFAULT 0,
                record_date TEXT NOT NULL DEFAULT '', parties TEXT NOT NULL DEFAULT '',
                delivered_scope TEXT NOT NULL DEFAULT '', observations TEXT NOT NULL DEFAULT '',
                reservations TEXT NOT NULL DEFAULT '', accepted_pending_items TEXT NOT NULL DEFAULT '',
                guarantee_refs TEXT NOT NULL DEFAULT '', documentation_refs TEXT NOT NULL DEFAULT '',
                approvers TEXT NOT NULL DEFAULT '', signatures TEXT NOT NULL DEFAULT '',
                version INTEGER NOT NULL DEFAULT 1, hash_sha256 TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT '', is_immutable INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL, created_by TEXT NOT NULL DEFAULT '',
                FOREIGN KEY(reception_id) REFERENCES closeout_receptions(id)
            );
            CREATE TABLE IF NOT EXISTS closeout_handover_records (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL DEFAULT '', closeout_id TEXT NOT NULL DEFAULT '',
                handover_date TEXT NOT NULL DEFAULT '', client TEXT NOT NULL DEFAULT '',
                contractor TEXT NOT NULL DEFAULT '', delivered_scope TEXT NOT NULL DEFAULT '',
                pending_items TEXT NOT NULL DEFAULT '', documentation_refs TEXT NOT NULL DEFAULT '',
                certificate_refs TEXT NOT NULL DEFAULT '', training_refs TEXT NOT NULL DEFAULT '',
                warranty_refs TEXT NOT NULL DEFAULT '', approvers TEXT NOT NULL DEFAULT '',
                signatures TEXT NOT NULL DEFAULT '', version INTEGER NOT NULL DEFAULT 1,
                hash_sha256 TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL, created_by TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS closeout_acceptance_certificates (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL DEFAULT '', contract_id TEXT NOT NULL DEFAULT '',
                reception_type INTEGER NOT NULL DEFAULT 0, certificate_date TEXT NOT NULL DEFAULT '',
                issued_by TEXT NOT NULL DEFAULT '', accepted_by TEXT NOT NULL DEFAULT '',
                scope TEXT NOT NULL DEFAULT '', observations TEXT NOT NULL DEFAULT '',
                conditions TEXT NOT NULL DEFAULT '', is_final INTEGER NOT NULL DEFAULT 0,
                hash_sha256 TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS closeout_asbuilt_deliverables (
                id TEXT PRIMARY KEY, closeout_id TEXT NOT NULL, doc_type TEXT NOT NULL DEFAULT '',
                title TEXT NOT NULL DEFAULT '', required_version TEXT NOT NULL DEFAULT '',
                delivered_version TEXT NOT NULL DEFAULT '', author TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '', delivery_date TEXT NOT NULL DEFAULT '',
                approval_date TEXT NOT NULL DEFAULT '', approved_by TEXT NOT NULL DEFAULT '',
                integrity_hash TEXT NOT NULL DEFAULT '', file_path TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '', status INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL, created_by TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL, modified_by TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS closeout_training_records (
                id TEXT PRIMARY KEY, closeout_id TEXT NOT NULL, topic TEXT NOT NULL DEFAULT '',
                instructor TEXT NOT NULL DEFAULT '', attendees TEXT NOT NULL DEFAULT '',
                training_date TEXT NOT NULL DEFAULT '', duration_minutes INTEGER NOT NULL DEFAULT 0,
                documentation_refs TEXT NOT NULL DEFAULT '', evaluation TEXT NOT NULL DEFAULT '',
                evidence_refs TEXT NOT NULL DEFAULT '', conformity TEXT NOT NULL DEFAULT '',
                pending_items TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS closeout_guarantee_releases (
                id TEXT PRIMARY KEY, closeout_id TEXT NOT NULL, contract_id TEXT NOT NULL DEFAULT '',
                guarantee_ref TEXT NOT NULL DEFAULT '', guarantee_type TEXT NOT NULL DEFAULT '',
                start_date TEXT NOT NULL DEFAULT '', expiry_date TEXT NOT NULL DEFAULT '',
                responsible TEXT NOT NULL DEFAULT '', coverage TEXT NOT NULL DEFAULT '',
                release_condition TEXT NOT NULL DEFAULT '', document_ref TEXT NOT NULL DEFAULT '',
                renewal_ref TEXT NOT NULL DEFAULT '', status INTEGER NOT NULL DEFAULT 0,
                requested_by TEXT NOT NULL DEFAULT '', requested_utc TEXT NOT NULL,
                released_amount REAL NOT NULL DEFAULT 0, currency_code TEXT NOT NULL DEFAULT '',
                release_reason TEXT NOT NULL DEFAULT '', alert TEXT NOT NULL DEFAULT '',
                modified_utc TEXT NOT NULL, modified_by TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS closeout_guarantee_release_approvals (
                id TEXT PRIMARY KEY, release_id TEXT NOT NULL, is_approved INTEGER NOT NULL DEFAULT 0,
                approved_by TEXT NOT NULL DEFAULT '', approval_date TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '', FOREIGN KEY(release_id) REFERENCES closeout_guarantee_releases(id)
            );
            CREATE TABLE IF NOT EXISTS closeout_retention_releases (
                id TEXT PRIMARY KEY, closeout_id TEXT NOT NULL, contract_id TEXT NOT NULL DEFAULT '',
                payment_certificate_ref TEXT NOT NULL DEFAULT '', retention_amount REAL NOT NULL DEFAULT 0,
                released_amount REAL NOT NULL DEFAULT 0, pending_amount REAL NOT NULL DEFAULT 0,
                currency_code TEXT NOT NULL DEFAULT '', release_condition TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0, requested_by TEXT NOT NULL DEFAULT '',
                requested_utc TEXT NOT NULL, is_partial INTEGER NOT NULL DEFAULT 0,
                approval_date TEXT NOT NULL DEFAULT '', approved_by TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '', modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '', row_version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS closeout_inventory_reconciliation (
                id TEXT PRIMARY KEY, closeout_id TEXT NOT NULL, project_id TEXT NOT NULL DEFAULT '',
                reconciliation_date TEXT NOT NULL DEFAULT '', total_purchased REAL NOT NULL DEFAULT 0,
                total_received REAL NOT NULL DEFAULT 0, total_consumed REAL NOT NULL DEFAULT 0,
                total_returned REAL NOT NULL DEFAULT 0, total_surplus REAL NOT NULL DEFAULT 0,
                total_differences REAL NOT NULL DEFAULT 0, currency_code TEXT NOT NULL DEFAULT '',
                differences_detail TEXT NOT NULL DEFAULT '', comment TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0, created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '', modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '', row_version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS closeout_surplus_disposals (
                id TEXT PRIMARY KEY, inventory_id TEXT NOT NULL, resource_code TEXT NOT NULL DEFAULT '',
                resource_name TEXT NOT NULL DEFAULT '', quantity REAL NOT NULL DEFAULT 0,
                unit TEXT NOT NULL DEFAULT '', location TEXT NOT NULL DEFAULT '',
                destination TEXT NOT NULL DEFAULT '', disposal_type INTEGER NOT NULL DEFAULT 0,
                cost REAL NOT NULL DEFAULT 0, responsible TEXT NOT NULL DEFAULT '',
                authorization TEXT NOT NULL DEFAULT '', evidence_ref TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL, created_by TEXT NOT NULL DEFAULT '',
                row_version TEXT NOT NULL, FOREIGN KEY(inventory_id) REFERENCES closeout_inventory_reconciliation(id)
            );
            CREATE TABLE IF NOT EXISTS closeout_lessons_learned (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL DEFAULT '', closeout_id TEXT NOT NULL,
                title TEXT NOT NULL DEFAULT '', description TEXT NOT NULL DEFAULT '',
                category INTEGER NOT NULL DEFAULT 0, event_description TEXT NOT NULL DEFAULT '',
                root_cause TEXT NOT NULL DEFAULT '', impact TEXT NOT NULL DEFAULT '',
                outcome TEXT NOT NULL DEFAULT '', recommendation TEXT NOT NULL DEFAULT '',
                applicability TEXT NOT NULL DEFAULT '', wbs_code TEXT NOT NULL DEFAULT '',
                activity_id TEXT NOT NULL DEFAULT '', contract_id TEXT NOT NULL DEFAULT '',
                supplier TEXT NOT NULL DEFAULT '', resource_code TEXT NOT NULL DEFAULT '',
                author TEXT NOT NULL DEFAULT '', reviewer TEXT NOT NULL DEFAULT '',
                lesson_date TEXT NOT NULL DEFAULT '', evidence_ids TEXT NOT NULL DEFAULT '',
                is_confidential INTEGER NOT NULL DEFAULT 0, status INTEGER NOT NULL DEFAULT 0,
                version INTEGER NOT NULL DEFAULT 1, created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '', modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT '', row_version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS closeout_lesson_evidence (
                id TEXT PRIMARY KEY, lesson_id TEXT NOT NULL, file_path TEXT NOT NULL DEFAULT '',
                file_name TEXT NOT NULL DEFAULT '', description TEXT NOT NULL DEFAULT '',
                uploaded_by TEXT NOT NULL DEFAULT '', uploaded_utc TEXT NOT NULL,
                hash TEXT NOT NULL DEFAULT '', FOREIGN KEY(lesson_id) REFERENCES closeout_lessons_learned(id)
            );
            CREATE TABLE IF NOT EXISTS closeout_final_snapshots (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL DEFAULT '', closeout_id TEXT NOT NULL,
                close_date TEXT NOT NULL DEFAULT '', project_data TEXT NOT NULL DEFAULT '',
                final_reconciliation TEXT NOT NULL DEFAULT '', kpi_data TEXT NOT NULL DEFAULT '',
                receptions TEXT NOT NULL DEFAULT '', contracts_data TEXT NOT NULL DEFAULT '',
                procurement_data TEXT NOT NULL DEFAULT '', evm_data TEXT NOT NULL DEFAULT '',
                documents TEXT NOT NULL DEFAULT '', guarantees TEXT NOT NULL DEFAULT '',
                retentions TEXT NOT NULL DEFAULT '', lessons TEXT NOT NULL DEFAULT '',
                alerts TEXT NOT NULL DEFAULT '', source_hashes TEXT NOT NULL DEFAULT '',
                engine_versions TEXT NOT NULL DEFAULT '', author TEXT NOT NULL DEFAULT '',
                comment TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                hash_sha256 TEXT NOT NULL DEFAULT '', is_official INTEGER NOT NULL DEFAULT 0,
                closeout_version INTEGER NOT NULL DEFAULT 1, row_version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS closeout_archive_manifests (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL, closeout_version TEXT NOT NULL DEFAULT '',
                archive_date TEXT NOT NULL DEFAULT '', responsible TEXT NOT NULL DEFAULT '',
                file_location TEXT NOT NULL DEFAULT '', documents_index TEXT NOT NULL DEFAULT '',
                snapshot_ref TEXT NOT NULL DEFAULT '', source_hashes TEXT NOT NULL DEFAULT '',
                sql_schema_version TEXT NOT NULL DEFAULT '', app_version TEXT NOT NULL DEFAULT '',
                observations TEXT NOT NULL DEFAULT '', retention_policy TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT '', created_utc TEXT NOT NULL,
                created_by TEXT NOT NULL DEFAULT '', modified_utc TEXT NOT NULL,
                modified_by TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_rcp_co ON closeout_receptions(closeout_id);
            CREATE INDEX IF NOT EXISTS idx_asb_co ON closeout_asbuilt_deliverables(closeout_id);
            CREATE INDEX IF NOT EXISTS idx_grl_co ON closeout_guarantee_releases(closeout_id);
            CREATE INDEX IF NOT EXISTS idx_rtl_co ON closeout_retention_releases(closeout_id);
            CREATE INDEX IF NOT EXISTS idx_lll_co ON closeout_lessons_learned(closeout_id);
            CREATE INDEX IF NOT EXISTS idx_fsn_co ON closeout_final_snapshots(closeout_id);
            """);
    }
}
