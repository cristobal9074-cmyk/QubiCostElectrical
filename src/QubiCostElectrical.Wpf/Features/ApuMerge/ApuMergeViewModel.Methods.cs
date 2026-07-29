using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;

namespace QubiCostElectrical.Wpf.Features.ApuMerge;

public sealed partial class ApuMergeViewModel
{
    public void Initialize(string apuId, string code)
    {
        ApuId = apuId;
        Code = code;
        LoadComparison();
    }

    private void LoadComparison()
    {
        conflicts.Clear();
        diffs.Clear();

        conflicts.Add(new ConflictVm
        {
            Dimension = "Componentes",
            LocalValue = "3 ítems",
            CatalogValue = "5 ítems",
            BaseValue = "4 ítems",
            Resolution = "KeepLocal"
        });
        conflicts.Add(new ConflictVm
        {
            Dimension = "Recursos",
            LocalValue = "8 refs",
            CatalogValue = "10 refs",
            BaseValue = "9 refs",
            Resolution = "KeepLocal"
        });

        diffs.Add(new DiffVm
        {
            Dimension = "Costo Directo",
            LocalValue = "$ 100.000",
            CatalogValue = "$ 120.000",
            Impact = "+$ 20.000"
        });
        diffs.Add(new DiffVm
        {
            Dimension = "Precio Final",
            LocalValue = "$ 150.000",
            CatalogValue = "$ 180.000",
            Impact = "+$ 30.000"
        });

        IsReady = true;
        StatusMessage = "Comparación cargada. Revise los conflictos y resuelva.";
    }

    private void ResolveKeepLocal(ConflictVm? c)
    {
        if (c != null) c.Resolution = "KeepLocal";
    }

    private void ResolveAcceptCatalog(ConflictVm? c)
    {
        if (c != null) c.Resolution = "AcceptCatalog";
    }

    private void ResolveKeepBase(ConflictVm? c)
    {
        if (c != null) c.Resolution = "KeepBase";
    }

    private async Task CreateMergeVersionAsync()
    {
        var versionRepo = host.Services.ApuVersionRepository;
        if (versionRepo == null)
        {
            StatusMessage = "Repositorio de versiones no disponible.";
            return;
        }

        try
        {
            var nextNumber = await versionRepo.GetNextVersionNumberAsync(ApuId);
            var mergeVersion = new ApuVersion
            {
                VersionId = Guid.NewGuid().ToString("N"),
                ApuId = ApuId,
                Code = Code,
                Name = $"{Code} (merge v{nextNumber})",
                VersionNumber = nextNumber,
                Status = ApuVersionStatus.Draft,
                Author = SessionManager.Current?.DisplayName ?? "merge",
                Notes = $"Merge creado desde catálogo. Conflictos: {conflicts.Count(c => c.Resolution != "KeepLocal")} resueltos.",
                CreatedAt = DateTime.UtcNow,
                EffectiveDate = DateTime.UtcNow
            };

            await versionRepo.SaveAsync(mergeVersion);
            StatusMessage = $"Versión merge {nextNumber} creada: {mergeVersion.VersionId}";
            IsReady = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al crear merge: {ex.Message}";
        }
    }
}
