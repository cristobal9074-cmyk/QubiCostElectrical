using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public static class SQLiteSchemaMigrator
{
    public static int LatestSchemaVersion => ProductVersionInfo.DatabaseSchemaVersion;

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
        new(28, "Recursos profesionales, historial de precios y tipos de cambio",                         SqliteMigrationSteps.Migration028_ProfessionalResources),
        new(29, "Versionado APU, snapshots SHA-256 y catálogo de APU",                                    SqliteMigrationSteps.Migration029_ApuVersioning),
        new(30, "Configuración económica, escenarios y motor de cálculo",                                 SqliteMigrationSteps.Migration030_EconomicConfig),
        new(31, "Planificación: cronogramas, WBS y actividades",                                           SqliteMigrationSteps.Migration031_PlanningCore),
        new(32, "Planificación: calendarios laborales, patrones semanales y excepciones",                   SqliteMigrationSteps.Migration032_PlanningCalendars),
        new(33, "Planificación: restricciones, responsables, vínculos presupuestarios y auditoría",        SqliteMigrationSteps.Migration033_PlanningLinksAndAudit),
        new(34, "Planificación: dependencias y CPM",                                                       SqliteMigrationSteps.Migration034_PlanningDependenciesAndCpm),
        new(35, "Planificación: líneas base y snapshots del cronograma",                                   SqliteMigrationSteps.Migration035_PlanningBaselines),
        new(36, "Planificación: fecha de estado, avance real e intervalos",                                SqliteMigrationSteps.Migration036_PlanningProgressCore),
        new(37, "Planificación: snapshots de seguimiento de avance",                                       SqliteMigrationSteps.Migration037_PlanningProgressSnapshots),
        new(38, "Planificación: causas de atraso y evidencias de avance",                                  SqliteMigrationSteps.Migration038_PlanningProgressDelayEvidence),
        new(39, "Planificación: Earned Value Management (EVM)",                                            SqliteMigrationSteps.Migration039_PlanningEarnedValue),
        new(40, "Planificación: dashboard ejecutivo, alertas y preferencias",                              SqliteMigrationSteps.Migration040_PlanningDashboard),
        new(41, "Compras: proveedores y contactos",                                                        SqliteMigrationSteps.Migration041_ProcurementSuppliers),
        new(42, "Compras: requerimientos e ítems",                                                         SqliteMigrationSteps.Migration042_ProcurementRequisitions),
        new(43, "Compras: RFQ, invitaciones y respuestas",                                                 SqliteMigrationSteps.Migration043_ProcurementRfq),
        new(44, "Compras: documentos y auditoría",                                                         SqliteMigrationSteps.Migration044_ProcurementAuditDocuments),
        new(45, "Compras: ofertas detalladas e ítems",                                                     SqliteMigrationSteps.Migration045_ProcurementOffers),
        new(46, "Compras: evaluaciones y comparaciones",                                                   SqliteMigrationSteps.Migration046_ProcurementComparisons),
        new(47, "Compras: adjudicaciones",                                                                 SqliteMigrationSteps.Migration047_ProcurementAwards),
        new(48, "Compras: órdenes de compra",                                                              SqliteMigrationSteps.Migration048_PurchaseOrders),
        new(49, "Compras: recepciones y devoluciones",                                                     SqliteMigrationSteps.Migration049_GoodsReceiptsReturns),
        new(50, "Compras: inventario de proyecto",                                                         SqliteMigrationSteps.Migration050_ProjectInventory),
        new(51, "Compras: snapshots de compromiso y dashboards",                                           SqliteMigrationSteps.Migration051_ProcurementCommitments),
        new(52, "Contratos: certificados de pago, líneas, revisiones y auditoría",                         SqliteMigrationSteps.Migration052_PaymentCertificates),
        new(53, "Contratos: anticipos, amortizaciones, retenciones, aplicaciones y liberaciones",          SqliteMigrationSteps.Migration053_ContractAdvancesRetentions),
        new(54, "Contratos: observaciones de pago y snapshots contractuales",                              SqliteMigrationSteps.Migration054_PaymentObservationsSnapshots),
        new(55, "Contratos: solicitudes de cambio, órdenes de cambio, obras extraordinarias y reclamos",  SqliteMigrationSteps.Migration055_ContractChanges),
        new(56, "Contratos: reajustes, fórmulas polinomiales, índices, garantías, multas, facturación informativa y auditoría", SqliteMigrationSteps.Migration056_ContractAdministration),
        new(57, "Contratos: dashboard ejecutivo contractual con KPIs, alertas, snapshots y comparación", SqliteMigrationSteps.Migration057_ContractDashboard),
        new(58, "Cierre: preparación, checklist, punch list, NC, documentos, evidencias, alertas y snapshots", SqliteMigrationSteps.Migration058_Closeout),
        new(59, "Cierre final: recepciones, actas, As Built, garantías, retenciones, inventario, lecciones, snapshot y archivo", SqliteMigrationSteps.Migration059_CloseoutFinal),
    };

    public static void MigrateToLatest(SqliteConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        int userVersion = SqliteMigratorHelpers.GetUserVersion(connection);
        int currentVersion = userVersion == 0 ? SqliteMigrationLedger.DetectLegacySchemaVersion(connection) : userVersion;

        if (currentVersion > LatestSchemaVersion)
            throw new InvalidDataException($"La base SQLite tiene esquema v{currentVersion}, pero esta aplicación soporta hasta v{LatestSchemaVersion}. Abre el proyecto con una versión más nueva del programa.");

        bool legacyWithoutPragma = userVersion == 0 && currentVersion > 0;
        bool needsMigration = legacyWithoutPragma || currentVersion < LatestSchemaVersion;
        if (needsMigration)
            SqliteMigrationLedger.TryCreateMigrationBackup(connection, currentVersion, LatestSchemaVersion);

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
                throw new InvalidOperationException(
                    $"No se pudo actualizar la base de datos del proyecto a la versión {step.Version} ({step.Name}). "
                    + "Se intentó crear un respaldo previo en la carpeta .schema_backups junto al archivo .db. "
                    + "Si el problema persiste, restaura ese respaldo o contacta soporte con el log de la aplicación.",
                    ex);
            }
        }
    }

    public static int GetCurrentUserVersion(SqliteConnection connection)
    {
        return SqliteMigratorHelpers.GetUserVersion(connection);
    }

    public static string GetSchemaSummary(SqliteConnection connection)
    {
        int version = SqliteMigratorHelpers.GetUserVersion(connection);
        string applied = SqliteMigratorHelpers.TableExists(connection, "schema_migrations")
            ? SqliteMigratorHelpers.ExecuteScalarText(connection, "SELECT COUNT(*) FROM schema_migrations;")
            : "0";
        return $"SQLite schema v{version}/{LatestSchemaVersion} · migraciones aplicadas: {applied}";
    }
}
