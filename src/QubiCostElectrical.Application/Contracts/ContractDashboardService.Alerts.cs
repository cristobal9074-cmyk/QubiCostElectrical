using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

partial class ContractDashboardService
{
    private List<ContractAlert> BuildAlerts(
        Contract contract, List<PaymentCertificate> certs,
        List<ContractGuarantee> guarantees, List<ContractPenalty> penalties,
        List<ContractInvoice> invoices, List<ContractAdjustment> adjustments,
        List<ChangeOrder> changeOrders, List<ContractClaim> claims,
        List<ContractVersion> versions)
    {
        var alerts = new List<ContractAlert>();
        var now = DateTime.UtcNow;
        var alertId = 0;

        foreach (var co in changeOrders.Where(c => c.Status is ChangeOrderStatus.UnderNegotiation && Math.Abs(c.ChangeAmount) > 100000))
        {
            alerts.Add(MakeAlert(ref alertId, "CHG-HIGH", ContractAlertSeverity.High, contract, "Cambio pendiente con impacto alto", co, $"Monto: {co.ChangeAmount:N0}", "Revisar y resolver cambio", "ChangeOrder"));
        }

        foreach (var cl in claims.Where(c => c.Status is ClaimStatus.Submitted))
        {
            alerts.Add(MakeAlert(ref alertId, "CLM-NORESP", ContractAlertSeverity.Medium, contract, "Claim sin respuesta", cl, $"Monto solicitado: {cl.AmountClaimed:N0}", "Evaluar y responder claim", "Claim"));
        }

        foreach (var cert in certs.Where(c => c.Status == PaymentCertificateStatus.Borrador && c.PeriodEnd < now.AddDays(-15)))
        {
            alerts.Add(MakeAlert(ref alertId, "PAY-OVD", ContractAlertSeverity.Medium, contract, "Estado de pago atrasado", cert, $"Período: {cert.PeriodEnd:dd/MM/yyyy}", "Completar y presentar estado de pago", "PaymentCertificate"));
        }

        foreach (var g in guarantees.Where(g => g.ExpiryDate <= now.AddDays(30) && g.ExpiryDate > now && g.Status == GuaranteeStatus.Active))
        {
            alerts.Add(MakeAlert(ref alertId, "GNT-EXPIRING", ContractAlertSeverity.Medium, contract, "Garantía próxima a vencer", g, $"Vence: {g.ExpiryDate:dd/MM/yyyy}", "Renovar o prorrogar garantía", "Guarantee"));
        }

        foreach (var g in guarantees.Where(g => g.ExpiryDate <= now && g.Status == GuaranteeStatus.Active))
        {
            alerts.Add(MakeAlert(ref alertId, "GNT-EXPIRED", ContractAlertSeverity.High, contract, "Garantía vencida", g, $"Vencida desde: {g.ExpiryDate:dd/MM/yyyy}", "Gestionar renovación urgente", "Guarantee"));
        }

        foreach (var p in penalties.Where(p => p.Status is PenaltyStatus.Proposed or PenaltyStatus.UnderReview))
        {
            alerts.Add(MakeAlert(ref alertId, "PNL-PEND", ContractAlertSeverity.Medium, contract, "Multa pendiente de decisión", p, $"Monto: {p.Amount:N0}", "Revisar y resolver multa", "Penalty"));
        }

        foreach (var inv in invoices.Where(i => i.Date < now.AddDays(-30) && i.Status == InvoiceStatus.Issued))
        {
            alerts.Add(MakeAlert(ref alertId, "INV-OVERDUE", ContractAlertSeverity.Medium, contract, "Factura vencida", inv, $"Fecha: {inv.Date:dd/MM/yyyy}", "Gestionar pago de factura", "Invoice"));
        }

        foreach (var adj in adjustments.Where(a => a.Status == AdjustmentStatus.Draft))
        {
            alerts.Add(MakeAlert(ref alertId, "ADJ-PEND", ContractAlertSeverity.Low, contract, "Reajuste pendiente", adj, $"Período: {adj.Period}", "Completar y aplicar reajuste", "Adjustment"));
        }

        if (certs.All(c => c.Amortizations == null || c.Amortizations.Count == 0))
        {
            alerts.Add(new()
            {
                AlertId = $"ALERT-{++alertId}", Code = "ADV-NOAMORT", Severity = ContractAlertSeverity.Medium,
                Title = "Anticipo sin amortización", Description = "No se registran amortizaciones de anticipo en estados de pago",
                ContractId = contract.Id, EntityType = "Contract", EntityId = contract.Id.Value,
                Date = now, Responsible = "", Impact = "Saldo de anticipo no recuperado",
                SuggestedAction = "Verificar y registrar amortización", Source = "Advance",
                Status = ContractAlertStatus.New, CreatedUtc = now, CreatedBy = "System"
            });
        }

        if (versions.Count > 0)
        {
            var latest = versions.Last();
            var expectedHash = ContractVersion.ComputeHash(latest.SnapshotJson);
            if (!string.Equals(expectedHash, latest.Hash, StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add(new()
                {
                    AlertId = $"ALERT-{++alertId}", Code = "INT-INVALID", Severity = ContractAlertSeverity.Critical,
                    Title = "Integridad del contrato comprometida", Description = $"La versión {latest.VersionNumber} no coincide con su hash",
                    ContractId = contract.Id, EntityType = "ContractVersion", EntityId = latest.Id.Value,
                    Date = now, Responsible = "", Impact = "Información contractual no confiable",
                    SuggestedAction = "Verificar y restaurar versión desde backup", Source = "Integrity",
                    Status = ContractAlertStatus.New, CreatedUtc = now, CreatedBy = "System"
                });
            }
        }

        return alerts;
    }

    private static ContractAlert MakeAlert(ref int id, string code, ContractAlertSeverity severity,
        Contract contract, string title, object entity, string detail, string action, string source)
    {
        id++;
        return new ContractAlert
        {
            AlertId = $"ALERT-{id}",
            Code = code,
            Severity = severity,
            Title = title,
            Description = detail,
            ContractId = contract.Id,
            EntityType = entity.GetType().Name,
            EntityId = entity.GetType().GetProperty("Id")?.GetValue(entity)?.ToString() ?? "",
            Date = DateTime.UtcNow,
            Responsible = "",
            Impact = detail,
            SuggestedAction = action,
            Source = source,
            Status = ContractAlertStatus.New,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "System"
        };
    }

    private static List<ContractDashboardSeries> BuildSeries(
        List<PaymentCertificate> certs, List<ChangeOrder> changeOrders, List<ContractClaim> claims)
    {
        var series = new List<ContractDashboardSeries>();

        var paymentPoints = certs
            .OrderBy(c => c.PeriodEnd)
            .Select(c => new ContractDashboardTrendData
            {
                PeriodLabel = c.PeriodEnd.ToString("MM/yyyy"),
                PeriodDate = DateOnly.FromDateTime(c.PeriodEnd),
                Value = c.MontoAcumulado
            }).ToList();

        if (paymentPoints.Count > 0)
            series.Add(new() { SeriesCode = "PAY-ACC", Title = "Valorización acumulada", Points = paymentPoints });

        return series;
    }
}
