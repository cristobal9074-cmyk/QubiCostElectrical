using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// Impact Analysis & Audit
// ──────────────────────────────────────────────────────────────
partial class ContractChangeService
{
    public async Task<ImpactAnalysisResult> CalculateImpactAsync(ChangeOrderId orderId, CancellationToken ct = default)
    {
        var order = await _changeOrderRepo.GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        var result = new ImpactAnalysisResult
        {
            CostImpact = order.CostImpact,
            ScheduleImpactDays = order.ScheduleImpactDays,
            AffectedActivities = string.IsNullOrWhiteSpace(order.AffectedActivities)
                ? new List<string>()
                : order.AffectedActivities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            AffectedMilestones = string.IsNullOrWhiteSpace(order.AffectedMilestones)
                ? new List<string>()
                : order.AffectedMilestones.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            MayChangeCriticalPath = order.ScheduleImpactDays > 0,
            ProposedAmount = order.ChangeAmount,
            ProposedDurationDays = order.AddedDays,
            DataQuality = "Estimated"
        };

        try
        {
            // Placeholder: In a full implementation, query contract, budget, planning and EVM data.
        }
        catch
        {
            result.Warnings.Add("No se pudieron obtener datos completos del contrato, presupuesto o planificación.");
            result.DataQuality = "Limited";
        }

        await RecordAuditAsync(order.ContractId, "ChangeOrder", orderId.Value, "IMPACTO", "system",
            $"Análisis de impacto calculado. Costo: {order.CostImpact:C}, Días: {order.ScheduleImpactDays}", ct: ct);

        return result;
    }

    public async Task<ImpactAnalysisResult> CalculateChangeRequestImpactAsync(ChangeRequestId requestId, CancellationToken ct = default)
    {
        var request = await _changeRequestRepo.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        var result = new ImpactAnalysisResult
        {
            CostImpact = request.PreliminaryCostImpact,
            ScheduleImpactDays = request.PreliminaryDaysImpact,
            AffectedActivities = string.IsNullOrWhiteSpace(request.AffectedActivities)
                ? new List<string>()
                : request.AffectedActivities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            MayChangeCriticalPath = request.PreliminaryDaysImpact > 0,
            ProposedAmount = request.PreliminaryCostImpact,
            ProposedDurationDays = request.PreliminaryDaysImpact,
            DataQuality = "Preliminary",
            Warnings = new List<string> { "Impacto preliminar basado en la solicitud. Se requiere análisis detallado." }
        };

        return result;
    }

    // ── Audit ────────────────────────────────────────────────────

    public async Task<List<ContractChangeAuditEvent>> GetContractChangeAuditLogAsync(ContractId contractId, int skip, int take, CancellationToken ct = default)
    {
        Ensure(_permissions.CanViewContractChangeAudit(_userPermissions.CurrentRole), "ViewAudit");
        return await _auditRepo.GetByContractAsync(contractId, skip, take, ct);
    }

    public async Task<List<ContractChangeAuditEvent>> GetEntityAuditLogAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        Ensure(_permissions.CanViewContractChangeAudit(_userPermissions.CurrentRole), "ViewAudit");
        return await _auditRepo.GetByEntityAsync(entityType, entityId, ct);
    }
}
