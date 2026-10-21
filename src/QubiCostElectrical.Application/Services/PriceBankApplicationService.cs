using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Resumen de una importación de precios de proyecto al banco central.
/// </summary>
public sealed record PriceBankImportResult(int MaterialesProcesados, int EntradasGuardadas);

public enum PriceBankApplyStatus
{
    Applied,
    NothingToApply,
    BlockedByFrozenPrices
}

public sealed record PriceBankApplyResult(PriceBankApplyStatus Status, int AppliedCount);

/// <summary>
/// Orquesta el banco de precios reutilizable: combina la lógica pura (<see cref="PriceBankService"/>)
/// con la persistencia (<see cref="IPriceBankStore"/>). Es el punto de entrada para la UI.
/// </summary>
public sealed class PriceBankApplicationService
{
    private readonly IPriceBankStore _repository;
    private readonly PriceBankService _domain;
    private readonly ProjectPriceFreezeService _freeze;

    public PriceBankApplicationService(
        IPriceBankStore repository,
        PriceBankService? domain = null,
        ProjectPriceFreezeService? freeze = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _domain = domain ?? new PriceBankService();
        _freeze = freeze ?? new ProjectPriceFreezeService();
    }

    public PriceBankImportResult ImportFromProject(IEnumerable<MaterialRow>? materiales, string proyectoOrigen = "")
    {
        var list = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();
        var entries = _domain.Consolidate(list, proyectoOrigen);
        int saved = entries.Count == 0 ? 0 : _repository.UpsertMany(entries);
        return new PriceBankImportResult(list.Count, saved);
    }

    public IReadOnlyList<PriceBankPriceUpdate> PreviewProjectUpdates(
        IEnumerable<MaterialRow>? projectMaterials,
        decimal valorUfProyecto = 0m)
    {
        var bank = _repository.GetAll()
            .GroupBy(e => e.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return _domain.BuildProjectUpdates(projectMaterials, bank, valorUfProyecto);
    }

    public PriceBankApplyResult ApplyBankPricesToProject(
        IList<MaterialRow>? projectMaterials,
        ProjectInfoRow? proyecto,
        IReadOnlyList<PriceBankPriceUpdate>? selectedUpdates = null,
        decimal valorUfProyecto = 0m,
        bool allowWhenFrozen = false)
    {
        if (projectMaterials == null || projectMaterials.Count == 0)
            return new PriceBankApplyResult(PriceBankApplyStatus.NothingToApply, 0);

        if (_freeze.IsFrozen(proyecto) && !allowWhenFrozen)
            return new PriceBankApplyResult(PriceBankApplyStatus.BlockedByFrozenPrices, 0);

        var updates = (selectedUpdates ?? PreviewProjectUpdates(projectMaterials, valorUfProyecto))
            .ToDictionary(u => u.Codigo, StringComparer.OrdinalIgnoreCase);
        if (updates.Count == 0)
            return new PriceBankApplyResult(PriceBankApplyStatus.NothingToApply, 0);

        int applied = 0;
        foreach (var material in projectMaterials)
        {
            string code = _domain.NormalizeCode(material.Codigo);
            if (!updates.TryGetValue(code, out var update)) continue;

            // El precio fuente que se aplica es CLP (bank.MatClp/MoClp); MatUf/MoUf se derivan al
            // instante con el valor de UF del proyecto — así RecalcularUf nunca revierte esto.
            material.MatClp = update.MatClpNuevo;
            material.MoClp = update.MoClpNuevo;
            decimal valorUfEfectivo = valorUfProyecto > 0m ? valorUfProyecto : update.UfUsadaClpNuevo;
            if (valorUfEfectivo > 0m)
                material.RecalcularUf(valorUfEfectivo);
            else
            {
                material.MatUf = update.MatUfNuevo;
                material.MoUf = update.MoUfNuevo;
            }
            material.FechaPrecio = update.FechaPrecioNuevo;
            material.Proveedor = update.ProveedorNuevo;
            material.MonedaPrecio = update.MonedaNueva;
            material.OrigenPrecio = "Banco de precios";
            material.UltimaActualizacionPrecio = AppClock.Now;
            if (_freeze.IsFrozen(proyecto))
                material.PrecioCongelado = _freeze.SnapshotMaterial(material, proyecto!);
            applied++;
        }

        return new PriceBankApplyResult(PriceBankApplyStatus.Applied, applied);
    }

    public IReadOnlyList<PriceBankEntry> Search(string text) => _repository.Search(text);

    public int Count() => _repository.Count();

    /// <summary>Elimina TODOS los precios del banco central. Irreversible — la UI debe confirmar.</summary>
    public int DeleteAll() => _repository.DeleteAll();
}
