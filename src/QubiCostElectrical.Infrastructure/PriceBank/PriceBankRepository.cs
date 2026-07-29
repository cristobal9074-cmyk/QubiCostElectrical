using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Adaptador del banco de precios sobre <see cref="PriceBankStore"/> (SQLite central).
/// Permite que la capa Application use el banco sin conocer SQLite.
/// </summary>
public sealed class PriceBankRepository : IPriceBankRepository
{
    private readonly IPriceBankStore store;

    public PriceBankRepository(IPriceBankStore? store = null)
    {
        this.store = store ?? new PriceBankStore();
    }

    public IReadOnlyList<PriceBankEntry> GetAll() => store.GetAll();

    public PriceBankEntry? GetByCodigo(string codigo) => store.GetByCodigo(codigo);

    public IReadOnlyList<PriceBankEntry> Search(string text) => store.Search(text);

    public void Upsert(PriceBankEntry entry) => store.Upsert(entry);

    public bool Delete(string codigo) => store.Delete(codigo);

    public int DeleteAll() => store.DeleteAll();

    public int UpsertMany(IEnumerable<PriceBankEntry> entries) => store.UpsertMany(entries);

    public int Count() => store.Count();

    public int SeedIfEmpty(IEnumerable<PriceBankEntry> entries) => store.SeedIfEmpty(entries);
}
