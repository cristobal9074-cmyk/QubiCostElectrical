using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Procurement;

public sealed class ProcurementValidationAndConsolidationTests
{
    [Fact]
    public void ValidateSupplier_RequiresCodeAndLegalName()
    {
        var s = new Supplier { Id = SupplierId.New(), Code = "", LegalName = "" };
        var issues = ProcurementValidation.ValidateSupplier(s, Array.Empty<string>());
        Assert.Contains(issues, i => i.Code == "DuplicateSupplierCode");
        Assert.Contains(issues, i => i.Field == "LegalName");
    }

    [Fact]
    public void ValidateSupplier_DuplicateCode()
    {
        var id = SupplierId.New();
        var s = new Supplier { Id = id, Code = "A1", LegalName = "Acme" };
        var issues = ProcurementValidation.ValidateSupplier(s, new[] { "A1" });
        Assert.Contains(issues, i => i.Code == "DuplicateSupplierCode");
    }

    [Fact]
    public void ValidateSupplier_InvalidEmail()
    {
        var s = new Supplier { Id = SupplierId.New(), Code = "B", LegalName = "B", Email = "bad" };
        Assert.Contains(ProcurementValidation.ValidateSupplier(s, Array.Empty<string>()), i => i.Code == "InvalidSupplierContact");
    }

    [Fact]
    public void ValidateContact_PrimaryDuplicate()
    {
        var sid = SupplierId.New();
        var siblings = new List<SupplierContact>
        {
            new() { ContactId = "1", SupplierId = sid, Name = "A", IsPrimary = true, IsActive = true, Email = "a@x.com" }
        };
        var c = new SupplierContact { ContactId = "2", SupplierId = sid, Name = "B", IsPrimary = true, IsActive = true, Email = "b@x.com" };
        Assert.Contains(ProcurementValidation.ValidateContact(c, siblings), i => i.Severity == ProcurementValidationSeverity.Error);
    }

    [Fact]
    public void ValidateContact_InvalidEmail()
    {
        var c = new SupplierContact { ContactId = "1", SupplierId = SupplierId.New(), Name = "A", Email = "x" };
        Assert.Contains(ProcurementValidation.ValidateContact(c, Array.Empty<SupplierContact>()), i => i.Code == "InvalidSupplierContact");
    }

    [Fact]
    public void ValidateItem_ZeroQuantity()
    {
        var r = BasePr();
        var item = new PurchaseRequisitionItem { ItemId = "i1", RequisitionId = r.Id, Description = "x", RequiredQuantity = 0, RequiredDate = r.RequiredDate };
        Assert.Contains(ProcurementValidation.ValidateItem(item, r), i => i.Code == "InvalidRequiredQuantity");
    }

    [Fact]
    public void ValidateItem_CoveredExceeds()
    {
        var r = BasePr();
        var item = new PurchaseRequisitionItem { ItemId = "i1", RequisitionId = r.Id, Description = "x", RequiredQuantity = 1, CoveredQuantity = 2, RequiredDate = r.RequiredDate };
        Assert.Contains(ProcurementValidation.ValidateItem(item, r), i => i.Code == "InvalidRequiredQuantity");
    }

    [Fact]
    public void ValidateItem_DateBeforeCreation()
    {
        var r = BasePr();
        var item = new PurchaseRequisitionItem { ItemId = "i1", RequisitionId = r.Id, Description = "x", RequiredQuantity = 1, RequiredDate = r.CreatedUtc.Date.AddDays(-2) };
        Assert.Contains(ProcurementValidation.ValidateItem(item, r), i => i.Code == "InvalidRequiredDate");
    }

    [Fact]
    public void ValidateRequisition_DateInvalid()
    {
        var r = BasePr();
        r.RequiredDate = r.CreatedUtc.Date.AddDays(-1);
        Assert.Contains(ProcurementValidation.ValidateRequisition(r), i => i.Code == "InvalidRequiredDate");
    }

    [Fact]
    public void ValidateRfq_NoItems()
    {
        var rfq = new RequestForQuotation { Id = RequestForQuotationId.New(), DeadlineUtc = DateTime.UtcNow.AddDays(5) };
        Assert.Contains(ProcurementValidation.ValidateRfq(rfq), i => i.Code == "RfqNotFound");
    }

    [Fact]
    public void ValidateRfq_DuplicateInvitation()
    {
        var sid = SupplierId.New();
        var rfq = new RequestForQuotation
        {
            Id = RequestForQuotationId.New(),
            DeadlineUtc = DateTime.UtcNow.AddDays(5),
            Items = { new RfqItem { ItemId = "1", RfqId = RequestForQuotationId.New(), Description = "a", Quantity = 1 } },
            Invitations =
            {
                new RfqSupplierInvitation { InvitationId = "a", RfqId = RequestForQuotationId.New(), SupplierId = sid },
                new RfqSupplierInvitation { InvitationId = "b", RfqId = RequestForQuotationId.New(), SupplierId = sid }
            }
        };
        Assert.Contains(ProcurementValidation.ValidateRfq(rfq), i => i.Code == "DuplicateSupplierInvitation");
    }

    [Theory]
    [InlineData(SupplierStatus.Active, true)]
    [InlineData(SupplierStatus.Prospect, true)]
    [InlineData(SupplierStatus.Inactive, false)]
    [InlineData(SupplierStatus.Blocked, false)]
    [InlineData(SupplierStatus.Suspended, false)]
    [InlineData(SupplierStatus.Archived, false)]
    public void IsEligible_ByStatus(SupplierStatus status, bool expected) =>
        Assert.Equal(expected, ProcurementValidation.IsEligible(new Supplier { Status = status }));

    [Fact]
    public void Consolidate_MergesCompatible()
    {
        var pr = BasePr();
        var items = new[]
        {
            Item(pr, "1", "C1", "UN", "spec", 2),
            Item(pr, "2", "C1", "UN", "spec", 3)
        };
        var result = ProcurementConsolidationEngine.Consolidate(items);
        Assert.Single(result.Lines);
        Assert.Equal(5m, result.Lines[0].TotalQuantity);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Consolidate_ConflictOnSpec()
    {
        var pr = BasePr();
        var items = new[]
        {
            Item(pr, "1", "C1", "UN", "A", 1),
            Item(pr, "2", "C1", "UN", "B", 1)
        };
        var result = ProcurementConsolidationEngine.Consolidate(items);
        Assert.Empty(result.Lines);
        Assert.Single(result.Conflicts);
        Assert.Contains("incompatibles", result.Conflicts[0].Message);
    }

    [Fact]
    public void Consolidate_SkipsCancelled()
    {
        var pr = BasePr();
        var items = new[]
        {
            Item(pr, "1", "C1", "UN", "s", 1),
            new PurchaseRequisitionItem { ItemId = "2", RequisitionId = pr.Id, Code = "C1", Unit = "UN", TechnicalSpec = "s", RequiredQuantity = 9, Status = RequisitionItemStatus.Cancelled, RequiredDate = pr.RequiredDate }
        };
        Assert.Equal(1m, ProcurementConsolidationEngine.Consolidate(items).Lines[0].TotalQuantity);
    }

    [Fact]
    public void Consolidate_DateWarning()
    {
        var pr = BasePr();
        var a = Item(pr, "1", "C1", "UN", "s", 1);
        a.RequiredDate = DateTime.Today.AddDays(10);
        var b = Item(pr, "2", "C1", "UN", "s", 1);
        b.RequiredDate = DateTime.Today.AddDays(20);
        var line = ProcurementConsolidationEngine.Consolidate(new[] { a, b }).Lines.Single();
        Assert.NotEmpty(line.Warnings);
    }

    [Theory]
    [InlineData("MAT")]
    [InlineData("EQ")]
    [InlineData("HER")]
    [InlineData("SUB")]
    [InlineData("TRA")]
    [InlineData("SER")]
    [InlineData("ARR")]
    [InlineData("ING")]
    [InlineData("OTR")]
    public void SupplyCategoryCatalog_Contains(string code) =>
        Assert.Contains(SupplyCategoryCatalog.Defaults, c => c.Code == code);

    [Fact]
    public void PendingQuantity_Clamps()
    {
        var item = new PurchaseRequisitionItem { ItemId = "1", RequisitionId = PurchaseRequisitionId.New(), RequiredQuantity = 5, CoveredQuantity = 8 };
        Assert.Equal(0m, item.PendingQuantity);
    }

    private static PurchaseRequisition BasePr() => new()
    {
        Id = PurchaseRequisitionId.New(),
        CreatedUtc = DateTime.UtcNow.Date,
        RequiredDate = DateTime.Today.AddDays(14),
        ProjectPath = "p"
    };

    private static PurchaseRequisitionItem Item(PurchaseRequisition pr, string id, string code, string unit, string spec, decimal qty) =>
        new()
        {
            ItemId = id,
            RequisitionId = pr.Id,
            Code = code,
            Description = code,
            Unit = unit,
            TechnicalSpec = spec,
            RequiredQuantity = qty,
            RequiredDate = pr.RequiredDate,
            CurrencyCode = "CLP"
        };
}

public sealed class ProcurementNeedDatePolicyTests
{
    [Theory]
    [InlineData(30, 14, 5, 19)]
    [InlineData(20, 10, 5, 15)]
    [InlineData(15, 7, 3, 10)]
    [InlineData(10, 0, 0, 0)]
    [InlineData(60, 20, 10, 30)]
    public void SuggestRequestDate_Formula(int requiredOffset, int lead, int safety, int expectedOffset)
    {
        var policy = new ProcurementNeedDatePolicy { DefaultLeadTimeDays = lead, SafetyMarginDays = safety };
        var req = new DateTime(2026, 7, 1).AddDays(requiredOffset);
        var suggested = policy.SuggestRequestDate(req, lead);
        Assert.Equal(req.AddDays(-(lead + safety)), suggested);
        Assert.Equal(expectedOffset, (req - suggested).TotalDays);
    }

    [Fact]
    public void SuggestRequestDate_UsesDefaultLead()
    {
        var policy = new ProcurementNeedDatePolicy { DefaultLeadTimeDays = 14, SafetyMarginDays = 5 };
        var req = new DateTime(2026, 9, 1);
        Assert.Equal(new DateTime(2026, 8, 13), policy.SuggestRequestDate(req, null));
    }
}
