using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IPaymentCertificateRepository
{
    Task SaveAsync(PaymentCertificate certificate, CancellationToken ct = default);
    Task<PaymentCertificate?> GetByIdAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task<PaymentCertificate?> GetByVisibleNumberAsync(string visibleNumber, CancellationToken ct = default);
    Task<List<PaymentCertificate>> SearchAsync(string projectPath, string? text, PaymentCertificateStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
    Task<List<PaymentCertificate>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
}

public interface IPaymentCertificateLineRepository
{
    Task SaveAsync(PaymentCertificateLine line, CancellationToken ct = default);
    Task SaveRangeAsync(IEnumerable<PaymentCertificateLine> lines, CancellationToken ct = default);
    Task<List<PaymentCertificateLine>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
}

public interface IContractAdvanceRepository
{
    Task SaveAsync(ContractAdvance advance, CancellationToken ct = default);
    Task<ContractAdvance?> GetByIdAsync(ContractAdvanceId id, CancellationToken ct = default);
    Task<List<ContractAdvance>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
}

public interface IAdvanceAmortizationRepository
{
    Task SaveAsync(AdvanceAmortization amortization, CancellationToken ct = default);
    Task SaveRangeAsync(IEnumerable<AdvanceAmortization> amortizations, CancellationToken ct = default);
    Task<List<AdvanceAmortization>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
}

public interface IContractRetentionRepository
{
    Task SaveAsync(ContractRetention retention, CancellationToken ct = default);
    Task<ContractRetention?> GetByIdAsync(ContractRetentionId id, CancellationToken ct = default);
    Task<List<ContractRetention>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
}

public interface IRetentionApplicationRepository
{
    Task SaveAsync(RetentionApplication application, CancellationToken ct = default);
    Task SaveRangeAsync(IEnumerable<RetentionApplication> applications, CancellationToken ct = default);
    Task<List<RetentionApplication>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task DeleteByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
}

public interface IRetentionReleaseRepository
{
    Task SaveAsync(RetentionRelease release, CancellationToken ct = default);
    Task<List<RetentionRelease>> GetByRetentionAsync(ContractRetentionId id, CancellationToken ct = default);
    Task<List<RetentionRelease>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
}

public interface IPaymentObservationRepository
{
    Task SaveAsync(PaymentObservation observation, CancellationToken ct = default);
    Task<List<PaymentObservation>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task<PaymentObservation?> GetByIdAsync(PaymentObservationId id, CancellationToken ct = default);
}

public interface IPaymentCertificateRevisionRepository
{
    Task SaveAsync(PaymentCertificateRevision revision, CancellationToken ct = default);
    Task<List<PaymentCertificateRevision>> GetByCertificateAsync(PaymentCertificateId id, CancellationToken ct = default);
    Task<PaymentCertificateRevision?> GetByIdAsync(PaymentCertificateRevisionId id, CancellationToken ct = default);
}

public interface IPaymentCertificateAuditRepository
{
    Task RecordAsync(PaymentCertificateAuditEvent auditEvent, CancellationToken ct = default);
    Task<List<PaymentCertificateAuditEvent>> GetByCertificateAsync(PaymentCertificateId id, int limit, CancellationToken ct = default);
}
