using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace QubiCostElectrical;

/// <summary>Exportación tabular de compras sin recalcular montos ni estados.</summary>
public static class ProcurementExportHelper
{
    public static string SupplierToCsv(Supplier s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var sb = new StringBuilder();
        sb.AppendLine("Campo,Valor");
        Row(sb, "Codigo", s.Code);
        Row(sb, "RazonSocial", s.LegalName);
        Row(sb, "NombreComercial", s.TradeName);
        Row(sb, "Rut", s.TaxId);
        Row(sb, "Pais", s.Country);
        Row(sb, "Ciudad", s.City);
        Row(sb, "Direccion", s.Address);
        Row(sb, "Correo", s.Email);
        Row(sb, "Telefono", s.Phone);
        Row(sb, "Estado", s.Status.ToString());
        Row(sb, "PlazoHabitualDias", s.UsualLeadTimeDays.ToString(CultureInfo.InvariantCulture));
        Row(sb, "CondicionesPago", s.UsualPaymentTerms);
        return sb.ToString();
    }

    public static string SuppliersToCsv(IEnumerable<Supplier> suppliers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Codigo,RazonSocial,NombreComercial,Estado,Ciudad,Correo,Telefono");
        foreach (var s in suppliers)
            sb.AppendLine(string.Join(',', Esc(s.Code), Esc(s.LegalName), Esc(s.TradeName), Esc(s.Status.ToString()), Esc(s.City), Esc(s.Email), Esc(s.Phone)));
        return sb.ToString();
    }

    public static string RequisitionToCsv(PurchaseRequisition r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var sb = new StringBuilder();
        sb.AppendLine("Campo,Valor");
        Row(sb, "Numero", r.Number);
        Row(sb, "Estado", r.Status.ToString());
        Row(sb, "Prioridad", r.Priority.ToString());
        Row(sb, "Solicitante", r.RequestedBy);
        Row(sb, "FechaRequerida", r.RequiredDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Row(sb, "Moneda", r.CurrencyCode);
        Row(sb, "Items", r.Items.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();
        sb.AppendLine("ItemId,Codigo,Descripcion,Unidad,Cantidad,Pendiente,Fecha,Criticidad,Estado");
        foreach (var i in r.Items)
            sb.AppendLine(string.Join(',', Esc(i.ItemId), Esc(i.Code), Esc(i.Description), Esc(i.Unit),
                i.RequiredQuantity.ToString(CultureInfo.InvariantCulture),
                i.PendingQuantity.ToString(CultureInfo.InvariantCulture),
                i.RequiredDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Esc(i.Criticidad.ToString()), Esc(i.Status.ToString())));
        return sb.ToString();
    }

    public static string RequisitionsToCsv(IEnumerable<PurchaseRequisition> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Numero,Estado,Prioridad,Solicitante,FechaRequerida,Items,CostoPresupuestado");
        foreach (var r in list)
        {
            var cost = r.Items.Sum(i => i.RequiredQuantity * i.BudgetedUnitPrice);
            sb.AppendLine(string.Join(',', Esc(r.Number), Esc(r.Status.ToString()), Esc(r.Priority.ToString()),
                Esc(r.RequestedBy), r.RequiredDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.Items.Count.ToString(CultureInfo.InvariantCulture),
                cost.ToString(CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }

    public static string RfqToCsv(RequestForQuotation rfq)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        var sb = new StringBuilder();
        sb.AppendLine("Campo,Valor");
        Row(sb, "Numero", rfq.Number);
        Row(sb, "Titulo", rfq.Title);
        Row(sb, "Estado", rfq.Status.ToString());
        Row(sb, "FechaLimite", rfq.DeadlineUtc.ToString("O", CultureInfo.InvariantCulture));
        Row(sb, "Moneda", rfq.ComparisonCurrency);
        Row(sb, "LugarEntrega", rfq.DeliveryPlace);
        sb.AppendLine();
        sb.AppendLine("ItemId,Codigo,Descripcion,Unidad,Cantidad,FechaRequerida");
        foreach (var i in rfq.Items)
            sb.AppendLine(string.Join(',', Esc(i.ItemId), Esc(i.Code), Esc(i.Description), Esc(i.Unit),
                i.Quantity.ToString(CultureInfo.InvariantCulture),
                i.RequiredDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        return sb.ToString();
    }

    public static string InvitationsToCsv(IEnumerable<RfqSupplierInvitation> invitations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("InvitationId,SupplierId,ContactId,Estado,Canal,EnviadoUtc");
        foreach (var i in invitations)
            sb.AppendLine(string.Join(',', Esc(i.InvitationId), Esc(i.SupplierId.Value), Esc(i.ContactId ?? ""),
                Esc(i.Status.ToString()), Esc(i.Channel),
                i.SentUtc?.ToString("O", CultureInfo.InvariantCulture) ?? ""));
        return sb.ToString();
    }

    public static string ResponsesToCsv(IEnumerable<QuotationResponse> responses)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ResponseId,RfqId,SupplierId,Moneda,PlazoDias,Estado,RecibidoUtc");
        foreach (var r in responses)
            sb.AppendLine(string.Join(',', Esc(r.ResponseId), Esc(r.RfqId.Value), Esc(r.SupplierId.Value),
                Esc(r.CurrencyCode), r.DeliveryLeadDays.ToString(CultureInfo.InvariantCulture),
                Esc(r.Status.ToString()), r.ReceivedUtc.ToString("O", CultureInfo.InvariantCulture)));
        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string field, string value) =>
        sb.AppendLine($"{Esc(field)},{Esc(value)}");

    private static string Esc(string? v)
    {
        v ??= string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
