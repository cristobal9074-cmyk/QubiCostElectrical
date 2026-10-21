using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Coordina la política de cierre comercial, simulador de negociación, descuentos acordados y dotación indirecta.
/// </summary>
internal sealed class CostoClosingPolicyCoordinator
{
    private static readonly HashSet<string> AbsoluteAmountParams = new(StringComparer.Ordinal)
    {
        nameof(ClosingPolicySelection.TargetPriceClp), nameof(ClosingPolicySelection.FeeClp),
        nameof(ClosingPolicySelection.TargetCostClp), nameof(ClosingPolicySelection.GainshareCapClp)
    };

    private readonly CostoViewModel _vm;

    public CostoClosingPolicyCoordinator(CostoViewModel vm)
    {
        _vm = vm;
    }

    public void RefreshNegotiatedDiscountsSection(ProjectFileData data)
    {
        _vm.NegotiatedDiscounts.Clear();
        foreach (var row in data.NegotiatedDiscounts ?? new List<NegotiatedDiscountRow>())
            _vm.NegotiatedDiscounts.Add(new NegotiatedDiscountVm(row, OnNegotiatedDiscountsChanged, () => _vm.CanEdit));
    }

    public void OnNegotiatedDiscountsChanged() => PersistNegotiatedDiscounts(markDirty: true);

    public void PersistNegotiatedDiscounts(bool markDirty)
    {
        if (!_vm.CanEdit || _vm.ShowEmptyState)
            return;

        _vm.Session.Data.NegotiatedDiscounts = _vm.NegotiatedDiscounts.Select(x => x.ToRow()).ToList();
        if (!markDirty)
            return;

        _vm.SuppressNegotiatedDiscountsRefresh = true;
        try
        {
            _vm.Session.MarkDirty(ProjectDirtyScope.NegotiatedDiscounts);
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressNegotiatedDiscountsRefresh = false;
        }
    }

    public void AddNegotiatedDiscount()
    {
        if (!_vm.CanEdit)
            return;
        _vm.NegotiatedDiscounts.Add(new NegotiatedDiscountVm(new NegotiatedDiscountRow(), OnNegotiatedDiscountsChanged, () => _vm.CanEdit));
        PersistNegotiatedDiscounts(markDirty: true);
    }

    public void RemoveNegotiatedDiscount(NegotiatedDiscountVm? item)
    {
        if (!_vm.CanEdit || item == null)
            return;
        _vm.NegotiatedDiscounts.Remove(item);
        PersistNegotiatedDiscounts(markDirty: true);
    }

    public void RefreshStaffingPlanSection(ProjectFileData data)
    {
        _vm.StaffingPlan.Clear();
        foreach (var row in data.StaffingPlan ?? new List<StaffingRoleRow>())
            _vm.StaffingPlan.Add(new StaffingRoleVm(row, OnStaffingPlanChanged, () => _vm.CanEdit));
        UpdateStaffingPlanTotal();
    }

    public void UpdateStaffingPlanTotal()
    {
        _vm.TotalStaffingPlanLabel = CurrencyFormat.Clp(_vm.StaffingPlan.Sum(x => x.TotalClp));
    }

    public void OnStaffingPlanChanged()
    {
        UpdateStaffingPlanTotal();
        PersistStaffingPlan(markDirty: true);
    }

    public void PersistStaffingPlan(bool markDirty)
    {
        if (!_vm.CanEdit || _vm.ShowEmptyState)
            return;

        _vm.Session.Data.StaffingPlan = _vm.StaffingPlan.Select(x => x.ToRow()).ToList();
        if (!markDirty)
            return;

        _vm.SuppressStaffingPlanRefresh = true;
        try
        {
            _vm.Session.MarkDirty(ProjectDirtyScope.StaffingPlan);
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressStaffingPlanRefresh = false;
        }
    }

    public void AddStaffingRole()
    {
        if (!_vm.CanEdit)
            return;
        _vm.StaffingPlan.Add(new StaffingRoleVm(new StaffingRoleRow { DedicacionPct = 100m, Meses = 1 }, OnStaffingPlanChanged, () => _vm.CanEdit));
        PersistStaffingPlan(markDirty: true);
    }

    public void RemoveStaffingRole(StaffingRoleVm? item)
    {
        if (!_vm.CanEdit || item == null)
            return;
        _vm.StaffingPlan.Remove(item);
        UpdateStaffingPlanTotal();
        PersistStaffingPlan(markDirty: true);
    }

    public void RefreshClosingPolicySection(CostBreakdown cost, string moneda, decimal valorUf, decimal tipoCambio)
    {
        var selection = _vm.Session.Data.ClosingPolicy ?? ClosingPolicySelection.Default();
        _vm.ClosingPolicySection.SetSilent(selection);
        // El motor ya aplicó la política sobre el breakdown; leer su resultado en vez de recalcular.
        var result = cost.ClosingPolicyResult ?? ClosingPolicyEngine.Apply(cost, selection);
        var svc = _vm.CurrencySvc;
        var ventaPair = svc.FormatMoneyPairFromClp(result.VentaFinalClp, moneda, valorUf, tipoCambio);
        var margenPair = svc.FormatMoneyPairFromClp(result.MargenClp, moneda, valorUf, tipoCambio);
        var margenLabel = margenPair.Secondary.Length == 0
            ? $"{margenPair.Primary} · {result.MargenPct:N1}%"
            : $"{margenPair.Primary} ({margenPair.Secondary}) · {result.MargenPct:N1}%";
        _vm.ClosingPolicySection.SetResult(result, ventaPair.Primary, ventaPair.Secondary, margenLabel);
        _vm.ClosingPolicySection.SetSafeguards(selection, cost.CostoDirectoClp);
    }

    public void CommitClosingPolicyType(ClosingPolicyType policy)
    {
        if (!_vm.CanEdit) return;
        var selection = _vm.Session.Data.ClosingPolicy ??= ClosingPolicySelection.Default();
        if (selection.Policy == policy) return;
        var policyName = ClosingPolicyOptionVm.All.FirstOrDefault(o => o.Value == policy)?.DisplayName ?? policy.ToString();
        if (!_vm.ConfirmAction("Cambiar política de cierre",
            $"¿Desea cambiar la política de cierre comercial a «{policyName}»?\n\nEsto afectará la fórmula de cálculo de la venta final y los márgenes del proyecto."))
        {
            _vm.ClosingPolicySection.SetSilent(selection);
            return;
        }
        _vm.Undo?.PushUndoPoint(_vm.Session, $"Cambiar política de cierre a {policyName}");
        _vm.Audit?.RecordLifecycle("Política de cierre", $"{selection.Policy} → {policy}");
        selection.Policy = policy;
        _vm.Session.MarkDirty(ProjectDirtyScope.ClosingPolicy);
        _vm.RefreshCostOnly();
    }

    public void CommitClosingPolicyParam(string paramName, decimal value)
    {
        if (!_vm.CanEdit) return;
        var selection = _vm.Session.Data.ClosingPolicy ??= ClosingPolicySelection.Default();
        switch (paramName)
        {
            case nameof(ClosingPolicySelection.TargetPriceClp): selection.TargetPriceClp = value; break;
            case nameof(ClosingPolicySelection.LumpSumMinRiskPct): selection.LumpSumMinRiskPct = value; break;
            case nameof(ClosingPolicySelection.FeeClp): selection.FeeClp = value; break;
            case nameof(ClosingPolicySelection.FeePct): selection.FeePct = value; break;
            case nameof(ClosingPolicySelection.TargetCostClp): selection.TargetCostClp = value; break;
            case nameof(ClosingPolicySelection.GainsharePct): selection.GainsharePct = value; break;
            case nameof(ClosingPolicySelection.GainshareCapClp): selection.GainshareCapClp = value; break;
        }
        if (AbsoluteAmountParams.Contains(paramName))
        {
            selection.ParamsStampedAt = AppClock.Now;
            try { selection.ParamsStampedCostoEjecucionClp = _vm.Dashboard.BuildCost(_vm.Session.Data).CostoDirectoUf; }
            catch { /* breakdown no disponible: se sella sin costo de referencia */ }
        }
        _vm.Session.MarkDirty(ProjectDirtyScope.ClosingPolicy);
        _vm.RefreshCostOnly();
    }

    public void ApplyNegotiatedTargetPrice(decimal targetPrice)
    {
        _vm.Undo?.PushUndoPoint(_vm.Session, "Aplicar precio objetivo negociado");
        CommitClosingPolicyType(ClosingPolicyType.TargetPrice);
        CommitClosingPolicyParam(nameof(ClosingPolicySelection.TargetPriceClp), targetPrice);
    }
}
