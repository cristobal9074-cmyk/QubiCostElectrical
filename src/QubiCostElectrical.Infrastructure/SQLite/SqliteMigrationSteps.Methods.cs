using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

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

    internal static void Migration028_MoProjectionTasks(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS mo_projection_tasks (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    internal static void Migration029_NegotiatedDiscounts(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS negotiated_discounts (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    internal static void Migration030_ClosingPolicy(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS closing_policy (
                id          INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    internal static void Migration031_StaffingPlan(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS staffing_plan (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }

    // Contratos: tabla raíz "contracts" + 6 tablas hijas. El esquema no existía en la fuente
    // decompilada (sus repositorios SqliteContract*Repository asumían tablas que ninguna
    // migración de origen creaba); fue diseñado aquí a partir de las columnas que esos
    // repositorios realmente leen/escriben, siguiendo el patrón "id/json/updated_at" ya
    // usado en Migration029_NegotiatedDiscounts / Migration031_StaffingPlan. Todo con
    // CREATE TABLE IF NOT EXISTS para permanecer idempotente y compatible entre forks.
    internal static void Migration032_Contracts(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS contracts (
                id              TEXT NOT NULL PRIMARY KEY,
                project_path    TEXT NOT NULL,
                visible_number  TEXT NOT NULL,
                status          INTEGER NOT NULL DEFAULT 0,
                updated_utc     TEXT NOT NULL,
                json_data       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_contracts_project_path ON contracts(project_path);
            CREATE INDEX IF NOT EXISTS ix_contracts_visible_number ON contracts(visible_number);

            CREATE TABLE IF NOT EXISTS contract_parties (
                id              TEXT NOT NULL PRIMARY KEY,
                contract_id     TEXT NOT NULL,
                role            INTEGER NOT NULL DEFAULT 0,
                json_data       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_contract_parties_contract_id ON contract_parties(contract_id);

            CREATE TABLE IF NOT EXISTS contract_milestones (
                id              TEXT NOT NULL PRIMARY KEY,
                contract_id     TEXT NOT NULL,
                code            TEXT NOT NULL,
                json_data       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_contract_milestones_contract_id ON contract_milestones(contract_id);

            CREATE TABLE IF NOT EXISTS contract_scope_links (
                id              TEXT NOT NULL PRIMARY KEY,
                contract_id     TEXT NOT NULL,
                scope_type      TEXT NOT NULL,
                ref_id          TEXT NOT NULL,
                json_data       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_contract_scope_links_contract_id ON contract_scope_links(contract_id);

            CREATE TABLE IF NOT EXISTS contract_documents (
                id              TEXT NOT NULL PRIMARY KEY,
                contract_id     TEXT NOT NULL,
                doc_type        TEXT NOT NULL,
                json_data       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_contract_documents_contract_id ON contract_documents(contract_id);

            CREATE TABLE IF NOT EXISTS contract_versions (
                id              TEXT NOT NULL PRIMARY KEY,
                contract_id     TEXT NOT NULL,
                version_number  INTEGER NOT NULL DEFAULT 0,
                is_approved     INTEGER NOT NULL DEFAULT 0,
                hash            TEXT NOT NULL DEFAULT '',
                json_data       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_contract_versions_contract_id ON contract_versions(contract_id);

            CREATE TABLE IF NOT EXISTS contract_audit (
                id              TEXT NOT NULL PRIMARY KEY,
                contract_id     TEXT NOT NULL,
                action          TEXT NOT NULL,
                actor           TEXT NOT NULL,
                utc_ts          TEXT NOT NULL,
                json_data       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_contract_audit_contract_id ON contract_audit(contract_id);
            """);
    }

    // Estados de pago (fase Ejecución): filas mensuales del cronograma de cobros + fila de
    // configuración (id='config') en la misma tabla. La tabla legacy "estados_pago"
    // (Migration014, feature retirada sin datos activos) queda intacta por separado.
    internal static void Migration033_EstadosPago(SqliteConnection connection, SqliteTransaction transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS estados_pago_mes (
                id          TEXT NOT NULL PRIMARY KEY,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                anio        INTEGER NOT NULL DEFAULT 0,
                mes         INTEGER NOT NULL DEFAULT 0,
                estado      TEXT NOT NULL DEFAULT '',
                json        TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);
    }
}
