using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

partial class ContractDashboardService
{
    private static List<ContractDashboardKpi> BuildKpis(
        Contract contract, ContractFinancialSummary fin, ContractTimeSummary time,
        ContractChangeSummary chg, ContractPaymentSummary pay,
        ContractRiskSummary risk, ContractAdministrationSummary admin,
        ContractEvmSummary? evm)
    {
        var kpis = new List<ContractDashboardKpi>();
        var diff = fin.CurrentAmount - fin.OriginalAmount;
        var pct = fin.OriginalAmount > 0 ? diff / fin.OriginalAmount * 100 : 0;

        kpis.Add(new() { Code = "CTR-ORG", Title = "Monto original", DisplayValue = fin.OriginalAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Contrato", SortOrder = 1 });
        kpis.Add(new() { Code = "CTR-CUR", Title = "Monto vigente", DisplayValue = fin.CurrentAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Contrato", SortOrder = 2 });
        kpis.Add(new() { Code = "CTR-DIFF", Title = "Diferencia absoluta", DisplayValue = diff.ToString("N0"), Unit = fin.CurrencyCode, Status = diff > 0 ? ContractDashboardKpiStatus.Warning : ContractDashboardKpiStatus.Good, Category = "Contrato", SortOrder = 3 });
        kpis.Add(new() { Code = "CTR-PCT", Title = "Variación %", DisplayValue = $"{pct:N2}%", Status = Math.Abs(pct) > 10 ? ContractDashboardKpiStatus.Warning : ContractDashboardKpiStatus.Good, Category = "Contrato", SortOrder = 4 });
        kpis.Add(new() { Code = "CTR-ORIG-DT", Title = "Fecha original término", DisplayValue = time.OriginalEndDate.ToString("dd/MM/yyyy"), Category = "Contrato", SortOrder = 5 });
        kpis.Add(new() { Code = "CTR-CUR-DT", Title = "Fecha vigente término", DisplayValue = time.CurrentEndDate.ToString("dd/MM/yyyy"), Category = "Contrato", SortOrder = 6 });

        kpis.Add(new() { Code = "PAY-PRES", Title = "Monto presentado", DisplayValue = pay.PresentedAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Estados de pago", SortOrder = 10 });
        kpis.Add(new() { Code = "PAY-OBS", Title = "Monto observado", DisplayValue = pay.ObservedAmount.ToString("N0"), Unit = fin.CurrencyCode, Status = pay.ObservedAmount > 0 ? ContractDashboardKpiStatus.Warning : ContractDashboardKpiStatus.Good, Category = "Estados de pago", SortOrder = 11 });
        kpis.Add(new() { Code = "PAY-APP", Title = "Monto aprobado", DisplayValue = pay.ApprovedAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Estados de pago", SortOrder = 12 });
        kpis.Add(new() { Code = "PAY-INV", Title = "Monto facturado", DisplayValue = pay.InvoicedAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Estados de pago", SortOrder = 13 });
        kpis.Add(new() { Code = "PAY-BAL", Title = "Saldo por valorizar", DisplayValue = fin.BalanceToValue.ToString("N0"), Unit = fin.CurrencyCode, Category = "Estados de pago", SortOrder = 14 });

        kpis.Add(new() { Code = "ADV-ORIG", Title = "Anticipo original", DisplayValue = fin.AdvanceOriginal.ToString("N0"), Unit = fin.CurrencyCode, Category = "Anticipos y retenciones", SortOrder = 20 });
        kpis.Add(new() { Code = "ADV-AMORT", Title = "Anticipo amortizado", DisplayValue = fin.AdvanceAmortized.ToString("N0"), Unit = fin.CurrencyCode, Category = "Anticipos y retenciones", SortOrder = 21 });
        kpis.Add(new() { Code = "RET-ACC", Title = "Retención acumulada", DisplayValue = fin.RetentionAccumulated.ToString("N0"), Unit = fin.CurrencyCode, Category = "Anticipos y retenciones", SortOrder = 22 });
        kpis.Add(new() { Code = "RET-REL", Title = "Retención liberada", DisplayValue = fin.RetentionReleased.ToString("N0"), Unit = fin.CurrencyCode, Category = "Anticipos y retenciones", SortOrder = 23 });

        kpis.Add(new() { Code = "CHG-OPEN", Title = "Solicitudes abiertas", DisplayValue = chg.OpenRequests.ToString(), Category = "Cambios", SortOrder = 30, Status = chg.OpenRequests > 0 ? ContractDashboardKpiStatus.Warning : ContractDashboardKpiStatus.Good });
        kpis.Add(new() { Code = "CHG-PEND", Title = "Cambios pendientes", DisplayValue = chg.PendingChanges.ToString(), Category = "Cambios", SortOrder = 31 });
        kpis.Add(new() { Code = "CHG-APP", Title = "Cambios aprobados", DisplayValue = chg.ApprovedChanges.ToString(), Category = "Cambios", SortOrder = 32 });
        kpis.Add(new() { Code = "CHG-AMT", Title = "Monto cambios aprobados", DisplayValue = chg.ApprovedAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Cambios", SortOrder = 33 });

        kpis.Add(new() { Code = "CLM-OPEN", Title = "Claims abiertos", DisplayValue = risk.OpenClaims.ToString(), Category = "Claims", SortOrder = 40, Status = risk.OpenClaims > 0 ? ContractDashboardKpiStatus.Critical : ContractDashboardKpiStatus.Good });
        kpis.Add(new() { Code = "CLM-AMT", Title = "Monto solicitado", DisplayValue = risk.ClaimedAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Claims", SortOrder = 41 });
        kpis.Add(new() { Code = "CLM-REC", Title = "Monto reconocido", DisplayValue = risk.RecognizedAmount.ToString("N0"), Unit = fin.CurrencyCode, Category = "Claims", SortOrder = 42 });

        kpis.Add(new() { Code = "ADJ-APP", Title = "Reajustes aprobados", DisplayValue = admin.ApprovedAdjustments.ToString(), Category = "Administración", SortOrder = 50 });
        kpis.Add(new() { Code = "PNL-PROP", Title = "Multas propuestas", DisplayValue = admin.ProposedPenalties.ToString(), Category = "Administración", SortOrder = 51, Status = admin.ProposedPenalties > 0 ? ContractDashboardKpiStatus.Warning : ContractDashboardKpiStatus.Good });
        kpis.Add(new() { Code = "PNL-APP", Title = "Multas aplicadas", DisplayValue = admin.AppliedPenalties.ToString(), Category = "Administración", SortOrder = 52 });
        kpis.Add(new() { Code = "GNT-ACT", Title = "Garantías activas", DisplayValue = admin.ActiveGuarantees.ToString(), Category = "Administración", SortOrder = 53 });
        kpis.Add(new() { Code = "GNT-EXP", Title = "Garantías por vencer", DisplayValue = admin.ExpiringGuarantees.ToString(), Category = "Administración", SortOrder = 54, Status = admin.ExpiringGuarantees > 0 ? ContractDashboardKpiStatus.Warning : ContractDashboardKpiStatus.Good });
        kpis.Add(new() { Code = "INV-PEND", Title = "Facturas pendientes", DisplayValue = admin.PendingInvoices.ToString(), Category = "Administración", SortOrder = 55 });
        kpis.Add(new() { Code = "INV-OVD", Title = "Facturas vencidas", DisplayValue = admin.OverdueInvoices.ToString(), Category = "Administración", SortOrder = 56, Status = admin.OverdueInvoices > 0 ? ContractDashboardKpiStatus.Critical : ContractDashboardKpiStatus.Good });

        if (evm != null)
        {
            kpis.Add(new() { Code = "EVM-BAC", Title = "BAC", DisplayValue = evm.Bac.ToString("N0"), Unit = fin.CurrencyCode, Category = "EVM y forecast", SortOrder = 60 });
            kpis.Add(new() { Code = "EVM-EAC", Title = "EAC", DisplayValue = evm.Eac.ToString("N0"), Unit = fin.CurrencyCode, Category = "EVM y forecast", SortOrder = 61 });
            kpis.Add(new() { Code = "EVM-VAC", Title = "VAC", DisplayValue = evm.Vac.ToString("N0"), Unit = fin.CurrencyCode, Status = evm.Vac < 0 ? ContractDashboardKpiStatus.Critical : ContractDashboardKpiStatus.Good, Category = "EVM y forecast", SortOrder = 62 });
            kpis.Add(new() { Code = "EVM-CPI", Title = "CPI", DisplayValue = evm.Cpi.ToString("N2"), Category = "EVM y forecast", SortOrder = 63 });
        }

        return kpis;
    }
}
