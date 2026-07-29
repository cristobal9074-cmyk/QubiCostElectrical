using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

public sealed class ProcurementValidationIssue
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ProcurementValidationSeverity Severity { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string? Field { get; init; }
    public string? EntityId { get; init; }
}

public static class ProcurementValidation
{
    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static List<ProcurementValidationIssue> ValidateSupplier(Supplier s, IEnumerable<string> existingCodes, string? excludeId = null)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (string.IsNullOrWhiteSpace(s.Code))
            issues.Add(Err("DuplicateSupplierCode", "Código de proveedor requerido.", "Supplier", "Code", s.Id.Value));
        else if (existingCodes.Any(c => string.Equals(c, s.Code, StringComparison.OrdinalIgnoreCase)
            && (excludeId is null || !string.Equals(excludeId, s.Id.Value, StringComparison.Ordinal))))
            issues.Add(Err("DuplicateSupplierCode", $"Código duplicado: {s.Code}.", "Supplier", "Code", s.Id.Value));
        if (string.IsNullOrWhiteSpace(s.LegalName))
            issues.Add(Err("SupplierNotFound", "Razón social requerida.", "Supplier", "LegalName", s.Id.Value));
        if (!string.IsNullOrWhiteSpace(s.Email) && !EmailRx.IsMatch(s.Email))
            issues.Add(Err("InvalidSupplierContact", "Correo de proveedor inválido.", "Supplier", "Email", s.Id.Value));
        return issues;
    }

    public static List<ProcurementValidationIssue> ValidateContact(SupplierContact c, IReadOnlyList<SupplierContact> siblings)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (string.IsNullOrWhiteSpace(c.Name))
            issues.Add(Err("InvalidSupplierContact", "Nombre de contacto requerido.", "Contact", "Name", c.ContactId));
        if (!string.IsNullOrWhiteSpace(c.Email) && !EmailRx.IsMatch(c.Email))
            issues.Add(Err("InvalidSupplierContact", "Correo de contacto inválido.", "Contact", "Email", c.ContactId));
        if (c.IsPrimary && siblings.Any(x => x.IsPrimary && x.ContactId != c.ContactId && x.IsActive))
            issues.Add(Err("InvalidSupplierContact", "Ya existe un contacto principal activo.", "Contact", "IsPrimary", c.ContactId));
        if (siblings.Any(x => x.ContactId != c.ContactId && !string.IsNullOrWhiteSpace(c.Email)
            && string.Equals(x.Email, c.Email, StringComparison.OrdinalIgnoreCase)))
            issues.Add(Warn("InvalidSupplierContact", "Correo duplicado entre contactos.", "Contact", "Email", c.ContactId));
        return issues;
    }

    public static List<ProcurementValidationIssue> ValidateRequisition(PurchaseRequisition r)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (r.RequiredDate.Date < r.CreatedUtc.Date)
            issues.Add(Err("InvalidRequiredDate", "Fecha requerida anterior a la creación.", "Requisition", "RequiredDate", r.Id.Value));
        if (r.Items.Count == 0 && r.Status != PurchaseRequisitionStatus.Draft)
            issues.Add(Err("RequisitionNotFound", "Requerimiento sin ítems.", "Requisition", "Items", r.Id.Value));
        foreach (var item in r.Items)
            issues.AddRange(ValidateItem(item, r));
        return issues;
    }

    public static List<ProcurementValidationIssue> ValidateItem(PurchaseRequisitionItem item, PurchaseRequisition r)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (item.RequiredQuantity <= 0m)
            issues.Add(Err("InvalidRequiredQuantity", "Cantidad requerida debe ser > 0.", "Item", "RequiredQuantity", item.ItemId));
        if (item.CoveredQuantity > item.RequiredQuantity)
            issues.Add(Err("InvalidRequiredQuantity", "Cantidad cubierta supera la requerida.", "Item", "CoveredQuantity", item.ItemId));
        if (item.RequiredDate.Date < r.CreatedUtc.Date)
            issues.Add(Err("InvalidRequiredDate", "Fecha de ítem anterior a creación.", "Item", "RequiredDate", item.ItemId));
        if (string.IsNullOrWhiteSpace(item.Description) && string.IsNullOrWhiteSpace(item.Code))
            issues.Add(Err("InvalidBudgetLink", "Ítem sin código ni descripción.", "Item", "Description", item.ItemId));
        return issues;
    }

    public static List<ProcurementValidationIssue> ValidateRfq(RequestForQuotation rfq)
    {
        var issues = new List<ProcurementValidationIssue>();
        if (rfq.DeadlineUtc.Date < DateTime.UtcNow.Date && rfq.Status is RfqStatus.Draft or RfqStatus.PendingApproval)
            issues.Add(Warn("InvalidRfqDeadline", "Fecha límite en el pasado.", "Rfq", "DeadlineUtc", rfq.Id.Value));
        if (rfq.Items.Count == 0)
            issues.Add(Err("RfqNotFound", "RFQ sin ítems.", "Rfq", "Items", rfq.Id.Value));
        if (rfq.Invitations.Count == 0 && rfq.Status is RfqStatus.Issued or RfqStatus.Open)
            issues.Add(Err("NoSuppliersSelected", "RFQ emitida sin proveedores.", "Rfq", "Invitations", rfq.Id.Value));
        var dup = rfq.Invitations.GroupBy(i => i.SupplierId.Value).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            issues.Add(Err("DuplicateSupplierInvitation", "Proveedor invitado duplicado.", "Invitation", "SupplierId", dup.Key));
        return issues;
    }

    public static bool IsEligible(Supplier s) =>
        s.Status is SupplierStatus.Active or SupplierStatus.Prospect;

    private static ProcurementValidationIssue Err(string code, string msg, string entity, string field, string? id) =>
        new() { Code = code, Message = msg, Severity = ProcurementValidationSeverity.Error, EntityType = entity, Field = field, EntityId = id };

    private static ProcurementValidationIssue Warn(string code, string msg, string entity, string field, string? id) =>
        new() { Code = code, Message = msg, Severity = ProcurementValidationSeverity.Warning, EntityType = entity, Field = field, EntityId = id };
}
