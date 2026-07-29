using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// Export (CSV)
// ──────────────────────────────────────────────────────────────
partial class ContractChangeService
{
    public async Task<string> ExportChangeRequestsCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractChange(_userPermissions.CurrentRole), "Export");
        var requests = await _changeRequestRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,Source,Title,Status,Urgency,CostImpact,DaysImpact,Requester,CreatedBy,CreatedUtc");
        foreach (var r in requests)
        {
            csv.AppendLine($"\"{r.Id.Value}\",\"{r.VisibleNumber}\",\"{r.Source}\",\"{r.Title}\",\"{r.Status}\",\"{r.Urgency}\",{r.PreliminaryCostImpact},{r.PreliminaryDaysImpact},\"{r.Requester}\",\"{r.CreatedBy}\",{r.CreatedUtc:O}");
        }
        return csv.ToString();
    }

    public async Task<string> ExportChangeOrdersCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractChange(_userPermissions.CurrentRole), "Export");
        var orders = await _changeOrderRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,Version,Status,ChangeAmount,AddedDays,CostImpact,ScheduleImpact,EffectiveDate,CreatedBy,CreatedUtc");
        foreach (var o in orders)
        {
            csv.AppendLine($"\"{o.Id.Value}\",\"{o.VisibleNumber}\",{o.Version},\"{o.Status}\",{o.ChangeAmount},{o.AddedDays},{o.CostImpact},{o.ScheduleImpactDays},{o.EffectiveDate:O},\"{o.CreatedBy}\",{o.CreatedUtc:O}");
        }
        return csv.ToString();
    }

    public async Task<string> ExportExtraWorksCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractChange(_userPermissions.CurrentRole), "Export");
        var works = await _extraWorkRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,Origin,Status,EstimatedCost,FinalCost,ValuationAmount,Responsible,CreatedBy,CreatedUtc");
        foreach (var w in works)
        {
            csv.AppendLine($"\"{w.Id.Value}\",\"{w.VisibleNumber}\",\"{w.Origin}\",\"{w.Status}\",{w.EstimatedCost},{w.FinalCost},{w.ValuationAmount},\"{w.Responsible}\",\"{w.CreatedBy}\",{w.CreatedUtc:O}");
        }
        return csv.ToString();
    }

    public async Task<string> ExportClaimsCsvAsync(ContractId contractId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanExportContractChange(_userPermissions.CurrentRole), "Export");
        var claims = await _claimRepo.GetByContractAsync(contractId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,VisibleNumber,Type,Status,AmountClaimed,DaysClaimed,AmountAwarded,DaysAwarded,Responsible,CreatedBy,CreatedUtc");
        foreach (var c in claims)
        {
            csv.AppendLine($"\"{c.Id.Value}\",\"{c.VisibleNumber}\",\"{c.Type}\",\"{c.Status}\",{c.AmountClaimed},{c.DaysClaimed},{c.AmountAwarded},{c.DaysAwarded},\"{c.Responsible}\",\"{c.CreatedBy}\",{c.CreatedUtc:O}");
        }
        return csv.ToString();
    }
}
