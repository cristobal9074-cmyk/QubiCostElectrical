using System;
using System.IO;

namespace QubiCostElectrical;

/// <summary>
/// Sembrado idempotente que antes vivía dentro de <c>AppServicesBuilder.Build()</c> (I/O de
/// disco en la construcción del grafo). Ahora es un paso explícito de arranque.
/// </summary>
public static class StartupSeeder
{
    public static void SeedPriceBank(IPriceBankStore store)
    {
        try
        {
            string? seedFile = PriceBankSeeder.FindSeedFile(AppContext.BaseDirectory);
            if (seedFile != null)
            {
                int inserted = store.SeedIfEmpty(PriceBankSeeder.ParseFile(seedFile));
                if (inserted > 0)
                    AppLog.Info("BancoPrecios", $"Banco de precios sembrado con {inserted} materiales desde '{Path.GetFileName(seedFile)}'.");
                return;
            }

            var embedded = PriceBankSeeder.GetEmbeddedEntries();
            if (embedded.Count > 0)
            {
                int inserted = store.SeedIfEmpty(embedded);
                if (inserted > 0)
                    AppLog.Info("BancoPrecios", $"Banco de precios sembrado con {inserted} materiales desde banco inicial embebido.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("BancoPrecios", "No se pudo sembrar el banco de precios inicial.", ex);
        }
    }
}
