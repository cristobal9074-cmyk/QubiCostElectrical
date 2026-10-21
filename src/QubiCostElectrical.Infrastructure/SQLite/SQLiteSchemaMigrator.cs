using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

public static class SQLiteSchemaMigrator
{
    public static int LatestSchemaVersion => ProductVersionInfo.DatabaseSchemaVersion;

    public static int PublishedStepCount => Steps.Count;

    private sealed record MigrationStep(int Version, string Name, Action<SqliteConnection, SqliteTransaction> Up);

    private static readonly IReadOnlyList<MigrationStep> Steps = new List<MigrationStep>
    {
        new(1,  "Base inicial: proyecto, itemizado, materiales, cubicación, plantillas y calculadora", SqliteMigrationSteps.Migration001_BaseInicial),
        new(2,  "Versiones de presupuesto",                                                            SqliteMigrationSteps.Migration002_Versiones),
        new(3,  "APU por partida",                                                                     SqliteMigrationSteps.Migration003_Apu),
        new(4,  "Órdenes de cambio",                                                                   SqliteMigrationSteps.Migration004_OrdenesCambio),
        new(5,  "Migrador versionado y columnas ejecutivas de proyecto",                               SqliteMigrationSteps.Migration005_CompatibilidadVersionada),
        new(6,  "Carta de presentación para PDF de oferta",                                            SqliteMigrationSteps.Migration006_CartaPresentacion),
        new(7,  "Motor único de costos y políticas de margen",                                         SqliteMigrationSteps.Migration007_MotorCostos),
        new(8,  "Política profesional Cubicación/APU para BudgetEngine",                               SqliteMigrationSteps.Migration008_ApuCostingMode),
        new(9,  "APU profesional: estado, proveedor, fuente y fecha de precio",                        SqliteMigrationSteps.Migration009_ApuProfesional),
        new(10, "Trazabilidad profesional de precios",                                                 SqliteMigrationSteps.Migration010_TrazabilidadPrecios),
        new(11, "Catálogo profesional de materiales: subcategorías, instalación y especificación técnica", SqliteMigrationSteps.Migration011_CatalogoMaterialesPro),
        new(12, "Índices de rendimiento para búsquedas grandes",                                       SqliteMigrationSteps.Migration012_PerformanceIndexes),
        new(13, "Índices profesionales adicionales de cubicación y catálogo",                          SqliteMigrationSteps.Migration013_ProfessionalSearchIndexes),
        new(14, "Tablas propias para las 11 colecciones embebidas en ProjectInfoRow",                  SqliteMigrationSteps.Migration014_SeparateCollections),
        new(15, "Eliminar tablas de features sin uso real (workspace_layouts, partida_schedules, cubicacion_attachments, offer_digital_signatures)", SqliteMigrationSteps.Migration015_DropUnusedCollections),
        new(16, "Zonas de proyecto y libro de medición por celda",                                     SqliteMigrationSteps.Migration016_ZonasYMediciones),
        new(17, "Cubicación de Servicios Comunes: cuadros de carga SC y configuración",                SqliteMigrationSteps.Migration017_CubicacionSC),
        new(18, "Registro central de antecedentes técnicos del presupuesto",                           SqliteMigrationSteps.Migration018_AntecedentesTecnicos),
        new(19, "Conjuntos eléctricos reutilizables con APU integrado",                                SqliteMigrationSteps.Migration019_ConjuntosElectricos),
        new(20, "Capas de cubicación: recurso, zona y valorización",                                   SqliteMigrationSteps.Migration020_CubicacionComposicion),
        new(21, "Registro de auditoría colaborativa del proyecto",                                       SqliteMigrationSteps.Migration021_ProjectAuditLog),
        new(22, "Comentarios de celda de cubicación: referencia estable a CubicacionRow.Id",             SqliteMigrationSteps.Migration022_CubicacionCellCommentsRowId),
        new(23, "Hoja de Cubicación: ítems y filas persistentes con el proyecto",                       SqliteMigrationSteps.Migration023_HojaCubicacion),
        new(24, "Solicitudes de materiales y EPP del proyecto",                                         SqliteMigrationSteps.Migration024_Solicitudes),
        new(25, "Columnas mat_clp/mo_clp en materiales y cubicación (fuente CLP del precio)",           SqliteMigrationSteps.Migration025_MatClpMoClpColumns),
        new(26, "MO dotación (directa e indirecta) y gastos operacionales",                              SqliteMigrationSteps.Migration026_MoDotacionGastosOperacionales),
        new(27, "Presupuesto EPP editable del proyecto",                                                 SqliteMigrationSteps.Migration027_EppBudget),
        new(28, "Proyección de mano de obra por tarea",                                                  SqliteMigrationSteps.Migration028_MoProjectionTasks),
        new(29, "Descuentos negociados por equipo/material específico",                                  SqliteMigrationSteps.Migration029_NegotiatedDiscounts),
        new(30, "Política de cierre comercial del proyecto",                                             SqliteMigrationSteps.Migration030_ClosingPolicy),
        new(31, "Dotación indirecta por rol, dedicación y meses",                                        SqliteMigrationSteps.Migration031_StaffingPlan),
        new(32, "Contratos: tabla raíz y 6 tablas hijas (partes, hitos, alcance, documentos, versiones, auditoría)", SqliteMigrationSteps.Migration032_Contracts),
        new(33, "Estados de pago: cronograma mensual de cobros y configuración",                    SqliteMigrationSteps.Migration033_EstadosPago),
    };

    public static void MigrateToLatest(SqliteConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        int userVersion = SqliteMigratorHelpers.GetUserVersion(connection);
        int currentVersion = userVersion == 0 ? SqliteMigrationLedger.DetectLegacySchemaVersion(connection) : userVersion;

        // Un archivo guardado por una versión más nueva no se abre ni se toca.
        // La versión nueva abre el archivo viejo. La versión vieja no abre el archivo nuevo.
        if (currentVersion > LatestSchemaVersion)
        {
            throw new InvalidDataException(
                $"Este archivo fue guardado con una versión más nueva de Qubi Cost Electrical (esquema v{currentVersion}). "
                + $"Esta versión soporta hasta el esquema v{LatestSchemaVersion}. "
                + "Instala la versión más nueva para abrirlo. El archivo no fue modificado.");
        }

        bool legacyWithoutPragma = userVersion == 0 && currentVersion > 0;
        bool needsMigration = legacyWithoutPragma || currentVersion < LatestSchemaVersion;
        string? backupPath = null;
        if (needsMigration)
            backupPath = SqliteMigrationLedger.TryCreateMigrationBackup(connection, currentVersion, LatestSchemaVersion);

        SqliteMigrationLedger.CreateMigrationLedger(connection, null);

        // Compatibilidad hacia atrás: las bases anteriores a esta versión usaban app_meta.schema_version,
        // pero PRAGMA user_version quedaba en 0. Si detectamos tablas antiguas, las marcamos como migradas
        // hasta su nivel real y desde ahí aplicamos solamente lo nuevo.
        if (legacyWithoutPragma)
        {
            SqliteMigrationLedger.BackfillMigrationLedger(connection, currentVersion, Steps.Select(s => (s.Version, s.Name)));
            SqliteMigratorHelpers.SetUserVersion(connection, currentVersion, null);
            SqliteMigrationLedger.UpsertMetaIfPossible(connection, null, "schema_version", currentVersion.ToString());
        }

        foreach (var step in Steps.Where(x => x.Version > currentVersion && x.Version <= LatestSchemaVersion).OrderBy(x => x.Version))
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                SqliteMigrationLedger.CreateMigrationLedger(connection, transaction);
                step.Up(connection, transaction);
                SqliteMigrationLedger.RecordMigration(connection, transaction, step.Version, step.Name);
                SqliteMigratorHelpers.SetUserVersion(connection, step.Version, transaction);
                SqliteMigrationLedger.UpsertMetaIfPossible(connection, transaction, "schema_version", step.Version.ToString());
                transaction.Commit();
            }
            catch (Exception ex)
            {
                AppLog.Critical("SQLiteMigración", $"Falló la migración a v{step.Version} ('{step.Name}'); se revierte la transacción", ex);
                transaction.Rollback();

                // Auto-recuperación atómica: si falla la migración, restaurar el archivo original desde el respaldo preventivo
                bool restored = false;
                if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
                {
                    restored = SqliteMigrationLedger.TryRestoreFromBackup(connection, backupPath);
                }

                throw new InvalidOperationException(
                    $"No se pudo actualizar la base de datos del proyecto a la versión {step.Version} ({step.Name}). "
                    + (restored
                        ? "La base de datos fue auto-restaurada de forma segura a su estado previo al intento de migración. "
                        : "Se intentó crear un respaldo previo en la carpeta .schema_backups junto al archivo .db. ")
                    + "Contacta soporte con el log de la aplicación.",
                    ex);
            }
        }

        // Validación de salud post-migración obligatoria: previene abrir o salvar una base corrupta en obra
        if (needsMigration)
        {
            string integrity = SqliteMigratorHelpers.ExecuteScalarText(connection, "PRAGMA integrity_check;");
            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
            {
                AppLog.Critical("SQLiteMigración", $"Integrity check falló tras migrar: '{integrity}'");
                if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
                {
                    SqliteMigrationLedger.TryRestoreFromBackup(connection, backupPath);
                }
                throw new InvalidOperationException(
                    $"La base de datos quedó en estado inconsistente tras la migración ({integrity}). "
                    + "Se auto-restauró automáticamente el respaldo previo para proteger los datos.");
            }
        }
    }

}
