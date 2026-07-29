using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class PaymentCertificateStatusTransitions
{
    private static readonly Dictionary<PaymentCertificateStatus, HashSet<PaymentCertificateStatus>> ValidTransitions = new()
    {
        [PaymentCertificateStatus.Borrador] = new()
        {
            PaymentCertificateStatus.Presentado,
            PaymentCertificateStatus.Anulado
        },
        [PaymentCertificateStatus.Presentado] = new()
        {
            PaymentCertificateStatus.EnRevision,
            PaymentCertificateStatus.Observado,
            PaymentCertificateStatus.Anulado
        },
        [PaymentCertificateStatus.EnRevision] = new()
        {
            PaymentCertificateStatus.Observado,
            PaymentCertificateStatus.PendienteAprobacion,
            PaymentCertificateStatus.Rechazado,
            PaymentCertificateStatus.Anulado
        },
        [PaymentCertificateStatus.Observado] = new()
        {
            PaymentCertificateStatus.Corregido,
            PaymentCertificateStatus.Anulado,
            PaymentCertificateStatus.Archivado
        },
        [PaymentCertificateStatus.Corregido] = new()
        {
            PaymentCertificateStatus.EnRevision,
            PaymentCertificateStatus.Presentado,
            PaymentCertificateStatus.Anulado
        },
        [PaymentCertificateStatus.PendienteAprobacion] = new()
        {
            PaymentCertificateStatus.Aprobado,
            PaymentCertificateStatus.Rechazado,
            PaymentCertificateStatus.Observado,
            PaymentCertificateStatus.Anulado
        },
        [PaymentCertificateStatus.Aprobado] = new()
        {
            PaymentCertificateStatus.Facturado,
            PaymentCertificateStatus.Cerrado,
            PaymentCertificateStatus.Anulado,
            PaymentCertificateStatus.Archivado
        },
        [PaymentCertificateStatus.Rechazado] = new()
        {
            PaymentCertificateStatus.Corregido,
            PaymentCertificateStatus.Archivado,
            PaymentCertificateStatus.Anulado
        },
        [PaymentCertificateStatus.Facturado] = new()
        {
            PaymentCertificateStatus.Pagado,
            PaymentCertificateStatus.Anulado,
            PaymentCertificateStatus.Archivado
        },
        [PaymentCertificateStatus.Pagado] = new()
        {
            PaymentCertificateStatus.Cerrado,
            PaymentCertificateStatus.Anulado,
            PaymentCertificateStatus.Archivado
        },
        [PaymentCertificateStatus.Cerrado] = new()
        {
            PaymentCertificateStatus.Archivado,
            PaymentCertificateStatus.Anulado
        },
        [PaymentCertificateStatus.Anulado] = new()
        {
            PaymentCertificateStatus.Archivado
        },
        [PaymentCertificateStatus.Archivado] = new()
        {
        }
    };

    public static bool CanTransition(PaymentCertificateStatus from, PaymentCertificateStatus to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static void EnsureCanTransition(PaymentCertificateStatus from, PaymentCertificateStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException(
                $"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<PaymentCertificateStatus> GetAllowedNext(PaymentCertificateStatus current)
    {
        return ValidTransitions.TryGetValue(current, out var allowed)
            ? allowed
            : new HashSet<PaymentCertificateStatus>();
    }

    public static bool IsEditable(PaymentCertificateStatus status) => status == PaymentCertificateStatus.Borrador;

    public static bool IsImmutable(PaymentCertificateStatus status) =>
        status is PaymentCertificateStatus.Aprobado
            or PaymentCertificateStatus.Pagado
            or PaymentCertificateStatus.Cerrado
            or PaymentCertificateStatus.Anulado;

    public static bool CanBeCorrected(PaymentCertificateStatus status) =>
        status is PaymentCertificateStatus.Observado or PaymentCertificateStatus.Rechazado;
}
