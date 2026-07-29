using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ContractInvoice
{
    public InvoiceId Id { get; init; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public int VisibleNumber { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public InvoiceStatus Status { get; set; }
    public string PaymentCertificateReference { get; set; } = string.Empty;
    public string ContractReference { get; set; } = string.Empty;
    public List<InvoiceLine> Lines { get; set; } = new();
    public string Comment { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
}

public sealed class InvoiceLine
{
    public InvoiceLineId Id { get; init; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Reference { get; set; } = string.Empty;
}
