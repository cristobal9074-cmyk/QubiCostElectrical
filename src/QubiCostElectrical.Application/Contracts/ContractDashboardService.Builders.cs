using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

partial class ContractDashboardService
{
    private ContractFinancialSummary BuildFinancialSummary(
        Contract contract, List<PaymentCertificate> certs, List<ContractAdvance> advances,
        List<ContractRetention> retentions, List<ChangeOrder> changeOrders,
        List<ContractAdjustment> adjustments, List<ContractClaim> claims,
        List<ContractPenalty> penalties)
    {
        var approvedOrders = changeOrders.Where(c => c.Status == ChangeOrderStatus.Implemented).ToList();
        var approvedAdjustments = adjustments.Where(a => a.Status is AdjustmentStatus.Applied or AdjustmentStatus.Closed).ToList();
        var approvedCerts = certs.Where(c => c.Status is PaymentCertificateStatus.Aprobado or PaymentCertificateStatus.Pagado).ToList();
        var recognizedClaims = claims.Where(c => c.Status == ClaimStatus.Accepted).ToList();

        var originalAmount = contract.OriginalAmount;
        var changeAmount = approvedOrders.Sum(o => o.ChangeAmount);
        var adjAmount = approvedAdjustments.Sum(a => a.Difference);
        var deductions = approvedOrders.Where(o => o.ChangeAmount < 0).Sum(o => Math.Abs(o.ChangeAmount));

        return new ContractFinancialSummary
        {
            OriginalAmount = originalAmount,
            CurrentAmount = originalAmount + changeAmount + adjAmount - deductions,
            ChangeOrdersAmount = changeAmount,
            AdjustmentsAmount = adjAmount,
            ApprovedDeductions = deductions,
            PresentedAmount = certs.Sum(c => c.MontoBruto),
            ApprovedAmount = approvedCerts.Sum(c => c.MontoLiquido),
            InvoicedAmount = approvedCerts.Where(c => c.Status == PaymentCertificateStatus.Pagado).Sum(c => c.MontoLiquido),
            PaidAmount = approvedCerts.Where(c => c.Status == PaymentCertificateStatus.Pagado).Sum(c => c.MontoLiquido),
            AdvanceOriginal = advances.Sum(a => a.Amount),
            AdvanceAmortized = advances.Sum(a => a.TotalAmortized),
            RetentionAccumulated = certs.Sum(c => c.RetentionApplications.Sum(r => r.AppliedAmount)),
            RetentionReleased = certs.Sum(c => c.RetentionApplications.Sum(r => r.ReleasedAmount)),
            ClaimsRequested = claims.Sum(c => c.AmountClaimed),
            ClaimsRecognized = recognizedClaims.Sum(c => c.AmountAwarded),
            PenaltiesProposed = penalties.Where(p => p.Status is PenaltyStatus.Proposed or PenaltyStatus.UnderReview).Sum(p => p.Amount),
            PenaltiesApplied = penalties.Where(p => p.Status == PenaltyStatus.Applied).Sum(p => p.Amount),
            BalanceToValue = originalAmount + changeAmount + adjAmount - deductions - approvedCerts.Sum(c => c.MontoAcumulado),
            ContractualBalance = originalAmount + changeAmount + adjAmount - deductions,
            CurrencyCode = contract.CurrencyCode,
            Quality = ContractDashboardDataQuality.Complete
        };
    }

    private static ContractTimeSummary BuildTimeSummary(
        Contract contract, List<ContractVersion> versions,
        List<ChangeOrder> changeOrders, List<ContractClaim> claims)
    {
        var latestVersion = versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        var approvedChangeDays = changeOrders
            .Where(c => c.Status == ChangeOrderStatus.Implemented)
            .Sum(c => c.AddedDays);
        var claimDaysRecognized = claims
            .Where(c => c.Status == ClaimStatus.Accepted)
            .Sum(c => c.DaysAwarded);

        var originalEndDate = DateOnly.FromDateTime(contract.EndDate);
        var currentEndDate = originalEndDate;
        var totalDuration = contract.ContractualDurationDays > 0
            ? contract.ContractualDurationDays
            : Math.Max(1, (contract.EndDate - contract.StartDate).Days);
        var daysElapsed = (DateTime.UtcNow - contract.StartDate).Days;
        var progressPct = totalDuration > 0
            ? Math.Min(100m, Math.Round((decimal)daysElapsed / totalDuration * 100, 1))
            : 0m;

        return new ContractTimeSummary
        {
            OriginalEndDate = originalEndDate,
            CurrentEndDate = currentEndDate,
            OriginalDurationDays = (contract.EndDate - contract.StartDate).Days,
            AddedDays = approvedChangeDays + claimDaysRecognized,
            ApprovedChangeDays = approvedChangeDays,
            ClaimDaysRequested = claims.Where(c => c.Status == ClaimStatus.Accepted).Sum(c => c.DaysClaimed),
            ClaimDaysRecognized = claimDaysRecognized,
            DaysElapsed = daysElapsed,
            ProgressPct = progressPct,
            Quality = ContractDashboardDataQuality.Complete
        };
    }

    private static ContractChangeSummary BuildChangeSummary(
        List<ChangeOrder> changeOrders, List<ExtraWork> extras)
    {
        return new ContractChangeSummary
        {
            OpenRequests = changeOrders.Count(c => c.Status == ChangeOrderStatus.Draft),
            PendingChanges = changeOrders.Count(c => c.Status == ChangeOrderStatus.UnderNegotiation),
            ApprovedChanges = changeOrders.Count(c => c.Status == ChangeOrderStatus.Implemented),
            ImplementedChanges = changeOrders.Count(c => c.Status == ChangeOrderStatus.Closed),
            PendingAmount = changeOrders.Where(c => c.Status is ChangeOrderStatus.Draft or ChangeOrderStatus.UnderNegotiation).Sum(c => c.ChangeAmount),
            ApprovedAmount = changeOrders.Where(c => c.Status == ChangeOrderStatus.Implemented).Sum(c => c.ChangeAmount),
            DaysRequested = changeOrders.Sum(c => c.AddedDays),
            DaysApproved = changeOrders.Where(c => c.Status == ChangeOrderStatus.Implemented).Sum(c => c.AddedDays),
            Quality = ContractDashboardDataQuality.Complete
        };
    }

    private static ContractPaymentSummary BuildPaymentSummary(List<PaymentCertificate> certs)
    {
        var approved = certs.Where(c => c.Status is PaymentCertificateStatus.Aprobado or PaymentCertificateStatus.Pagado).ToList();
        return new ContractPaymentSummary
        {
            PresentedAmount = certs.Sum(c => c.MontoBruto),
            ObservedAmount = 0,
            ApprovedAmount = approved.Sum(c => c.MontoLiquido),
            InvoicedAmount = approved.Where(c => c.Status == PaymentCertificateStatus.Pagado).Sum(c => c.MontoLiquido),
            PaidAmount = approved.Where(c => c.Status == PaymentCertificateStatus.Pagado).Sum(c => c.MontoLiquido),
            AccumulatedAmount = certs.Sum(c => c.MontoAcumulado),
            Quality = ContractDashboardDataQuality.Complete
        };
    }

    private static ContractRiskSummary BuildRiskSummary(
        List<ContractClaim> claims, List<ContractGuarantee> guarantees,
        List<ContractPenalty> penalties, List<ContractInvoice> invoices)
    {
        var now = DateTime.UtcNow;
        return new ContractRiskSummary
        {
            OpenClaims = claims.Count(c => c.Status is ClaimStatus.Submitted or ClaimStatus.Negotiation),
            NegotiatingClaims = claims.Count(c => c.Status == ClaimStatus.Negotiation),
            AcceptedClaims = claims.Count(c => c.Status == ClaimStatus.Accepted),
            ClaimedAmount = claims.Sum(c => c.AmountClaimed),
            RecognizedAmount = claims.Where(c => c.Status == ClaimStatus.Accepted).Sum(c => c.AmountAwarded),
            ExpiringGuarantees = guarantees.Count(g => g.ExpiryDate <= now.AddDays(30) && g.ExpiryDate > now && g.Status == GuaranteeStatus.Active),
            ExpiredGuarantees = guarantees.Count(g => g.ExpiryDate <= now && g.Status == GuaranteeStatus.Active),
            PendingPenalties = penalties.Count(p => p.Status is PenaltyStatus.Proposed or PenaltyStatus.UnderReview),
            OverdueInvoices = invoices.Count(i => i.Date < now.AddDays(-30) && i.Status == InvoiceStatus.Issued),
            Quality = ContractDashboardDataQuality.Complete
        };
    }

    private static ContractAdministrationSummary BuildAdminSummary(
        List<ContractAdjustment> adjustments, List<ContractPenalty> penalties,
        List<ContractGuarantee> guarantees, List<ContractInvoice> invoices)
    {
        var now = DateTime.UtcNow;
        return new ContractAdministrationSummary
        {
            ApprovedAdjustments = adjustments.Count(a => a.Status is AdjustmentStatus.Applied or AdjustmentStatus.Closed),
            ProposedPenalties = penalties.Count(p => p.Status is PenaltyStatus.Proposed or PenaltyStatus.UnderReview),
            AppliedPenalties = penalties.Count(p => p.Status == PenaltyStatus.Applied),
            ActiveGuarantees = guarantees.Count(g => g.Status == GuaranteeStatus.Active),
            ExpiringGuarantees = guarantees.Count(g => g.ExpiryDate <= now.AddDays(30) && g.ExpiryDate > now && g.Status == GuaranteeStatus.Active),
            ExpiredGuarantees = guarantees.Count(g => g.ExpiryDate <= now && g.Status == GuaranteeStatus.Active),
            PendingInvoices = invoices.Count(i => i.Status == InvoiceStatus.Issued),
            OverdueInvoices = invoices.Count(i => i.Date < now.AddDays(-30) && i.Status == InvoiceStatus.Issued),
            Quality = ContractDashboardDataQuality.Complete
        };
    }

    private static ContractEvmSummary? BuildEvmSummary(Contract contract, List<PaymentCertificate> certs)
    {
        var bac = contract.OriginalAmount;
        var approved = certs.Where(c => c.Status is PaymentCertificateStatus.Aprobado or PaymentCertificateStatus.Pagado).ToList();
        var earned = approved.Sum(c => c.MontoLiquido);
        if (bac <= 0) return null;

        var cpi = earned > 0 ? earned / bac : 1m;
        var eac = cpi > 0 ? bac / cpi : bac;
        return new ContractEvmSummary
        {
            Bac = bac,
            Eac = eac,
            Etc = eac - earned,
            Vac = bac - eac,
            Cpi = cpi,
            Spi = earned > 0 ? 1m : 1m,
            Quality = ContractDashboardDataQuality.Complete
        };
    }

    private static ContractDashboardDataQuality ComputeQuality(
        Contract contract, List<PaymentCertificate> certs,
        List<ContractVersion> versions, List<ContractAdvance> advances,
        List<ContractRetention> retentions)
    {
        if (contract == null) return ContractDashboardDataQuality.MissingStatusDate;
        if (versions.Count == 0) return ContractDashboardDataQuality.Partial;
        return ContractDashboardDataQuality.Complete;
    }

    private static string BuildSummary(Contract contract, ContractFinancialSummary fin, ContractTimeSummary time)
    {
        var diff = fin.CurrentAmount - fin.OriginalAmount;
        var pct = fin.OriginalAmount > 0 ? diff / fin.OriginalAmount * 100 : 0;
        var days = (time.CurrentEndDate.ToDateTime(TimeOnly.MinValue) - DateTime.UtcNow).Days;
        return $"Contrato {contract.VisibleNumber}: Monto vigente {fin.CurrentAmount:N0} ({pct:+0.0;-0.0;0.0}%), plazo hasta {time.CurrentEndDate:dd/MM/yyyy} ({days} días restantes)";
    }
}
