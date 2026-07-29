using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QubiCostElectrical;

public sealed class PaymentCertificate
{
    public PaymentCertificateId Id { get; init; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public string VisibleNumber { get; set; } = string.Empty;
    public int RevisionNumber { get; set; } = 1;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime CutOffDate { get; set; }
    public DateTime PresentationDate { get; set; }
    public string Currency { get; set; } = "CLP";
    public decimal ExchangeRate { get; set; } = 1.0m;
    public PaymentCertificateStatus Status { get; set; } = PaymentCertificateStatus.Borrador;
    
    public string Presenter { get; set; } = string.Empty;
    public string Revisor { get; set; } = string.Empty;
    public string Aprobador { get; set; } = string.Empty;
    public DateTime? ApprovedAtUtc { get; set; }
    public string Comment { get; set; } = string.Empty;

    // Totals
    public decimal SubtotalValorizado { get; set; }
    public decimal Reajuste { get; set; }
    public decimal AnticipoAmortizado { get; set; }
    public decimal RetencionesAplicadas { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Multas { get; set; }
    public decimal Impuestos { get; set; }
    public decimal MontoBruto { get; set; }
    public decimal MontoLiquido { get; set; }
    
    // Historical Totals
    public decimal MontoAnterior { get; set; }
    public decimal MontoAcumulado { get; set; }
    public decimal SaldoContractual { get; set; }
    public decimal SaldoPorValorizar { get; set; }

    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// Hash de integridad del certificado. Se excluye de la serialización
    /// para evitar el problema circular al calcular el hash (ComputeHash
    /// serializa el objeto sin incluir Hash).
    /// </summary>
    [JsonIgnore]
    public string Hash { get; set; } = string.Empty;

    public string CompatibilityMetadata { get; set; } = string.Empty;

    // Collections
    public List<PaymentCertificateLine> Lines { get; } = new();
    public List<PaymentObservation> Observations { get; } = new();
    public List<AdvanceAmortization> Amortizations { get; } = new();
    public List<RetentionApplication> RetentionApplications { get; } = new();

    public static string ComputeHash(string snapshotJson)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
