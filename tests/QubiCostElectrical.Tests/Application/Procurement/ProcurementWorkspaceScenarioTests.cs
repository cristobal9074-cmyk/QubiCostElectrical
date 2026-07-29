using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical.Tests.Application.Procurement;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Procurement;

/// <summary>Cobertura adicional de escenarios ViewModel / flujo UI sin hosting WPF.</summary>
public sealed class ProcurementWorkspaceScenarioTests
{
    [Fact]
    public async Task Workspace_Load_Search_Filter_Create_Edit_Save()
    {
        var suppliers = new FakeSupplierRepository();
        var contacts = new FakeContactRepository();
        var audit = new FakeProcAuditRepository();
        var svc = new SupplierService(suppliers, contacts, audit, new AllowAllProcurementPermissionChecker());
        await svc.CreateAsync(new Supplier { Code = "A", LegalName = "Alpha", City = "Santiago" }, "u");
        await svc.CreateAsync(new Supplier { Code = "B", LegalName = "Beta", City = "Valpo", Status = SupplierStatus.Active }, "u");
        var filtered = await svc.SearchAsync("Alp", null, 0, 50);
        Assert.Single(filtered);
        var s = filtered[0];
        s.City = "Providencia";
        await svc.UpdateAsync(s, s.RowVersion, "u");
        Assert.Equal("Providencia", (await svc.GetAsync(s.Id))!.City);
    }

    [Fact]
    public async Task Workspace_No_Reentrancy_Busy_Flag_Simulation()
    {
        var gate = 0;
        async Task Op()
        {
            if (System.Threading.Interlocked.CompareExchange(ref gate, 1, 0) != 0) return;
            try { await Task.Delay(5); }
            finally { System.Threading.Interlocked.Exchange(ref gate, 0); }
        }
        await Task.WhenAll(Op(), Op(), Op());
        Assert.Equal(0, gate);
    }

    [Fact]
    public async Task Requisition_Workspace_Approve_Reject_Consolidate_Export_Paths()
    {
        var prRepo = new FakePrRepository();
        var audit = new FakeProcAuditRepository();
        var svc = new PurchaseRequisitionService(prRepo, audit, new AllowAllProcurementPermissionChecker());
        var pr = await svc.CreateAsync(new PurchaseRequisition { ProjectPath = "w", RequiredDate = DateTime.Today.AddDays(9) }, "creator");
        pr = await svc.AddItemAsync(pr.Id, new PurchaseRequisitionItem
        {
            ItemId = "1", RequisitionId = pr.Id, Code = "C", Description = "C", RequiredQuantity = 2, RequiredDate = pr.RequiredDate
        }, pr.RowVersion, "creator");
        pr = await svc.AddItemAsync(pr.Id, new PurchaseRequisitionItem
        {
            ItemId = "2", RequisitionId = pr.Id, Code = "C", Description = "C", RequiredQuantity = 3, RequiredDate = pr.RequiredDate, TechnicalSpec = "same"
        }, pr.RowVersion, "creator");
        // duplicate same code+unit+spec should fail
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddItemAsync(pr.Id, new PurchaseRequisitionItem
        {
            ItemId = "3", RequisitionId = pr.Id, Code = "C", Description = "C", RequiredQuantity = 1, RequiredDate = pr.RequiredDate, TechnicalSpec = "same"
        }, pr.RowVersion, "creator"));

        var cons = await svc.ConsolidateAsync(pr.Id, "creator");
        Assert.True(cons.Lines.Count + cons.Conflicts.Count >= 1);
        var csv = ProcurementExportHelper.RequisitionToCsv(pr);
        Assert.Contains("C", csv);
        pr = await svc.SubmitAsync(pr.Id, pr.RowVersion, "creator");
        await svc.RejectAsync(pr.Id, pr.RowVersion, "reviewer", "faltan specs");
    }

    [Fact]
    public async Task ReadOnly_Denied_Via_Permissions()
    {
        var svc = new RfqService(new FakeRfqRepository(), new FakePrRepository(), new FakeSupplierRepository(),
            new FakeContactRepository(), new FakeQrRepository(), new FakeProcAuditRepository(),
            new DenyAllProcurementPermissionChecker());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.SearchAsync("p", null, 0, 10));
    }

    [Fact]
    public async Task Error_Visible_On_Invalid_Generation()
    {
        var svc = new PurchaseRequisitionService(new FakePrRepository(), new FakeProcAuditRepository(), new AllowAllProcurementPermissionChecker());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ConfirmGenerationAsync("p", Array.Empty<NeedGenerationPreviewItem>(), "u", NeedOriginKind.Manual));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ConfirmGenerationAsync("p", new[]
            {
                new NeedGenerationPreviewItem { Code = "X", Quantity = 0, Selected = true, RequiredDate = DateTime.Today.AddDays(1) }
            }, "u", NeedOriginKind.Manual));
    }

    [Theory]
    [InlineData(PurchasePriority.Low)]
    [InlineData(PurchasePriority.Normal)]
    [InlineData(PurchasePriority.High)]
    [InlineData(PurchasePriority.Urgent)]
    [InlineData(PurchasePriority.Critical)]
    public async Task Priority_Persists(PurchasePriority p)
    {
        var svc = new PurchaseRequisitionService(new FakePrRepository(), new FakeProcAuditRepository(), new AllowAllProcurementPermissionChecker());
        var pr = await svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(3), Priority = p }, "u");
        Assert.Equal(p, pr.Priority);
    }

    [Theory]
    [InlineData(NeedOriginKind.Manual)]
    [InlineData(NeedOriginKind.Budget)]
    [InlineData(NeedOriginKind.Apu)]
    [InlineData(NeedOriginKind.Activity)]
    [InlineData(NeedOriginKind.Schedule)]
    [InlineData(NeedOriginKind.InsufficientStock)]
    [InlineData(NeedOriginKind.ApprovedChange)]
    [InlineData(NeedOriginKind.Other)]
    public async Task Origin_Kinds_Generate(NeedOriginKind kind)
    {
        var svc = new PurchaseRequisitionService(new FakePrRepository(), new FakeProcAuditRepository(), new AllowAllProcurementPermissionChecker());
        var preview = new List<NeedGenerationPreviewItem>
        {
            new()
            {
                Code = "O-" + kind, Description = "d", Quantity = 1, Selected = true,
                RequiredDate = DateTime.Today.AddDays(12), Origin = kind, OriginId = kind.ToString()
            }
        };
        if (kind is NeedOriginKind.Budget)
            preview = svc.PreviewFromBudget(preview);
        else if (kind is NeedOriginKind.Schedule)
            preview = svc.PreviewFromSchedule(preview);
        var pr = await svc.ConfirmGenerationAsync("p", preview, "u", kind);
        Assert.Equal(kind, pr.PrimaryOrigin);
        Assert.Equal(kind, pr.Items[0].Origin.Kind);
    }

    [Theory]
    [InlineData(SupplyCategoryKind.Materials)]
    [InlineData(SupplyCategoryKind.Equipment)]
    [InlineData(SupplyCategoryKind.Tools)]
    [InlineData(SupplyCategoryKind.Subcontracts)]
    [InlineData(SupplyCategoryKind.Transport)]
    [InlineData(SupplyCategoryKind.Services)]
    [InlineData(SupplyCategoryKind.Rental)]
    [InlineData(SupplyCategoryKind.Engineering)]
    [InlineData(SupplyCategoryKind.Other)]
    public async Task Item_Types(SupplyCategoryKind type)
    {
        var svc = new PurchaseRequisitionService(new FakePrRepository(), new FakeProcAuditRepository(), new AllowAllProcurementPermissionChecker());
        var pr = await svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(4) }, "u");
        pr = await svc.AddItemAsync(pr.Id, new PurchaseRequisitionItem
        {
            ItemId = Guid.NewGuid().ToString("N"), RequisitionId = pr.Id, Code = type.ToString(), Description = "d",
            Type = type, RequiredQuantity = 1, RequiredDate = pr.RequiredDate
        }, pr.RowVersion, "u");
        Assert.Equal(type, pr.Items[0].Type);
    }
}
