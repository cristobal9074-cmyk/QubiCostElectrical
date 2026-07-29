using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.ApuCatalog;

public sealed partial class ApuCatalogViewModel
{
    public async Task SearchAsync()
    {
        var repo = host.Services.ApuCatalogRepository;
        if (repo == null)
        {
            StatusMessage = "Catálogo no disponible.";
            return;
        }

        searchCts?.Cancel();
        searchCts = new CancellationTokenSource();
        var ct = searchCts.Token;

        IsSearching = true;
        entries.Clear();

        try
        {
            var result = await repo.SearchAsync(new ApuCatalogSearchRequest
            {
                Query = SearchText,
                Category = string.IsNullOrWhiteSpace(CategoryFilter) ? string.Empty : CategoryFilter,
                MaxResults = pageSize,
                Offset = 0
            });

            if (ct.IsCancellationRequested) return;

            foreach (var entry in result.Entries)
                entries.Add(new ApuCatalogEntryVm
                {
                    ApuId = entry.ApuId,
                    Code = entry.Code,
                    Name = entry.Name,
                    Category = entry.Category ?? string.Empty,
                    Unit = entry.Unit,
                    VersionCount = entry.VersionCount,
                    ActiveVersionNumber = entry.ActiveVersionNumber,
                    ActiveVersionStatus = entry.ActiveVersionStatus.ToString(),
                    LastPrice = entry.LastPrice,
                    IsActive = entry.IsActive,
                    Owner = entry.Owner
                });

            TotalCount = result.FilteredCount;
            CurrentPage = 0;
            HasMore = result.FilteredCount > pageSize;
            StatusMessage = $"{result.FilteredCount} resultados";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Búsqueda cancelada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task LoadPageAsync(int page)
    {
        if (page < 0) return;
        var repo = host.Services.ApuCatalogRepository;
        if (repo == null) return;

        searchCts?.Cancel();
        searchCts = new CancellationTokenSource();
        var ct = searchCts.Token;

        IsSearching = true;

        try
        {
            var result = await repo.SearchAsync(new ApuCatalogSearchRequest
            {
                Query = SearchText,
                Category = string.IsNullOrWhiteSpace(CategoryFilter) ? string.Empty : CategoryFilter,
                MaxResults = pageSize,
                Offset = page * pageSize
            });

            if (ct.IsCancellationRequested) return;

            entries.Clear();
            foreach (var entry in result.Entries)
                entries.Add(new ApuCatalogEntryVm
                {
                    ApuId = entry.ApuId,
                    Code = entry.Code,
                    Name = entry.Name,
                    Category = entry.Category,
                    Unit = entry.Unit,
                    VersionCount = entry.VersionCount,
                    ActiveVersionNumber = entry.ActiveVersionNumber,
                    ActiveVersionStatus = entry.ActiveVersionStatus.ToString(),
                    LastPrice = entry.LastPrice,
                    IsActive = entry.IsActive,
                    Owner = entry.Owner
                });

            CurrentPage = page;
            HasMore = (page + 1) * pageSize < result.FilteredCount;
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsSearching = false;
        }
    }

    private void CompareEntry(ApuCatalogEntryVm? entry)
    {
        if (entry == null) return;
        shell.RequestNavigate(ShellRoute.ApuMerge);
    }

    private void Clear()
    {
        entries.Clear();
        SearchText = string.Empty;
        TotalCount = 0;
        CurrentPage = 0;
        HasMore = false;
        StatusMessage = string.Empty;
    }
}

public sealed record ApuMergeNavContext(string ApuId, string Code);
