using System;
using System.IO;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Sembrado e inicialización idempotente de arranque.
/// Incluye auto-migración de bancos de precios legados/hermanos y auto-respaldo para
/// garantizar que la lista de precios del usuario nunca se pierda entre actualizaciones.
/// </summary>
public static class StartupSeeder
{
    public static void SeedPriceBank(IPriceBankStore store)
    {
        try
        {
            // 1. Si la base está vacía, buscar e importar desde la base legada/hermana más rica
            TryMigrateLegacyPriceBank(store);

            // 2. Si ya contiene materiales (existentes o migrados), asegurar respaldo y salir
            if (store.Count() > 0)
            {
                TryBackup(store);
                return;
            }

            // 3. Fallback: sembrar desde archivo externo si existe en la carpeta de ejecución
            string? seedFile = PriceBankSeeder.FindSeedFile(AppContext.BaseDirectory);
            if (seedFile != null)
            {
                int inserted = store.SeedIfEmpty(PriceBankSeeder.ParseFile(seedFile));
                if (inserted > 0)
                {
                    AppLog.Info("BancoPrecios", $"Banco de precios sembrado con {inserted} materiales desde '{Path.GetFileName(seedFile)}'.");
                    TryBackup(store);
                }
                return;
            }

            // 4. Fallback final: recurso embebido
            var embedded = PriceBankSeeder.GetEmbeddedEntries();
            if (embedded.Count > 0)
            {
                int inserted = store.SeedIfEmpty(embedded);
                if (inserted > 0)
                {
                    AppLog.Info("BancoPrecios", $"Banco de precios sembrado con {inserted} materiales desde banco inicial embebido.");
                    TryBackup(store);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("BancoPrecios", "No se pudo sembrar el banco de precios inicial.", ex);
        }
    }

    public static void TryMigrateLegacyPriceBank(IPriceBankStore targetStore)
    {
        try
        {
            if (targetStore.Count() > 0) return;

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidatePaths =
            [
                Path.Combine(local, ProductIdentity.AppDataFolderName, "dev", "banco_precios.db"),
                ProductPaths.PriceBankBackupFile(),
                Path.Combine(local, "QubiCostElectricalPRO", "banco_precios.db"),
                Path.Combine(local, "PresupuestoElectricoPRO", "banco_precios.db"),
                Path.Combine(local, "QubiCostElectrical-Development", "banco_precios.db")
            ];

            string? bestPath = null;
            int maxEntries = 0;

            foreach (var candidate in candidatePaths)
            {
                if (!File.Exists(candidate)) continue;

                // Evitar leer la misma base objetivo si coincide
                if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(ProductPaths.PriceBankDatabaseFile()), StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var testStore = new PriceBankStore(candidate);
                    int count = testStore.Count();
                    if (count > maxEntries)
                    {
                        maxEntries = count;
                        bestPath = candidate;
                    }
                }
                catch
                {
                    // Archivo bloqueado o corrupto: continuar buscando
                }
            }

            if (bestPath != null && maxEntries > 0)
            {
                var sourceStore = new PriceBankStore(bestPath);
                var entries = sourceStore.GetAll();
                if (entries.Count > 0)
                {
                    targetStore.UpsertMany(entries);
                    AppLog.Info("BancoPrecios", $"Migrados automáticamente {entries.Count} materiales desde '{bestPath}' al banco de precios.");
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warning("BancoPrecios", $"No se pudo completar la migración de banco legado: {ex.Message}", ex);
        }
    }

    public static void TryBackup(IPriceBankStore store)
    {
        try
        {
            if (store.Count() == 0) return;
            string dbFile = ProductPaths.PriceBankDatabaseFile();
            if (!File.Exists(dbFile)) return;

            string backupFile = ProductPaths.PriceBankBackupFile();
            string? dir = Path.GetDirectoryName(backupFile);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(backupFile) || File.GetLastWriteTimeUtc(dbFile) > File.GetLastWriteTimeUtc(backupFile))
            {
                File.Copy(dbFile, backupFile, true);
                AppLog.Info("BancoPrecios", $"Respaldo del banco de precios sincronizado en '{backupFile}'.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warning("BancoPrecios", $"No se pudo sincronizar respaldo del banco de precios: {ex.Message}", ex);
        }
    }
}
