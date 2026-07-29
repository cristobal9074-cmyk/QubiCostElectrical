using System;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class PaymentCertificateStatusTransitionsTests
{
    [Fact]
    public void CanTransition_BorradorToPresentado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Borrador, PaymentCertificateStatus.Presentado));
    }

    [Fact]
    public void CanTransition_BorradorToAnulado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Borrador, PaymentCertificateStatus.Anulado));
    }

    [Fact]
    public void CanTransition_BorradorToAprobado_ReturnsFalse()
    {
        Assert.False(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Borrador, PaymentCertificateStatus.Aprobado));
    }

    [Fact]
    public void CanTransition_PresentadoToEnRevision_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Presentado, PaymentCertificateStatus.EnRevision));
    }

    [Fact]
    public void CanTransition_PresentadoToObservado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Presentado, PaymentCertificateStatus.Observado));
    }

    [Fact]
    public void CanTransition_EnRevisionToPendienteAprobacion_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.EnRevision, PaymentCertificateStatus.PendienteAprobacion));
    }

    [Fact]
    public void CanTransition_EnRevisionToRechazado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.EnRevision, PaymentCertificateStatus.Rechazado));
    }

    [Fact]
    public void CanTransition_PendienteAprobacionToAprobado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.PendienteAprobacion, PaymentCertificateStatus.Aprobado));
    }

    [Fact]
    public void CanTransition_AprobadoToFacturado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Aprobado, PaymentCertificateStatus.Facturado));
    }

    [Fact]
    public void CanTransition_FacturadoToPagado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Facturado, PaymentCertificateStatus.Pagado));
    }

    [Fact]
    public void CanTransition_PagadoToCerrado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Pagado, PaymentCertificateStatus.Cerrado));
    }

    [Fact]
    public void CanTransition_CerradoToArchivado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Cerrado, PaymentCertificateStatus.Archivado));
    }

    [Fact]
    public void EnsureCanTransition_ValidTransition_DoesNotThrow()
    {
        PaymentCertificateStatusTransitions.EnsureCanTransition(
            PaymentCertificateStatus.Borrador, PaymentCertificateStatus.Presentado);
    }

    [Fact]
    public void EnsureCanTransition_InvalidTransition_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PaymentCertificateStatusTransitions.EnsureCanTransition(
                PaymentCertificateStatus.Borrador, PaymentCertificateStatus.Aprobado));

        Assert.Contains("inválida", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAllowedNext_Borrador_ReturnsPresentadoAndAnulado()
    {
        var allowed = PaymentCertificateStatusTransitions.GetAllowedNext(PaymentCertificateStatus.Borrador);

        Assert.Contains(PaymentCertificateStatus.Presentado, allowed);
        Assert.Contains(PaymentCertificateStatus.Anulado, allowed);
        Assert.Equal(2, allowed.Count);
    }

    [Fact]
    public void GetAllowedNext_Aprobado_ReturnsFacturadoCerradoAnuladoArchivado()
    {
        var allowed = PaymentCertificateStatusTransitions.GetAllowedNext(PaymentCertificateStatus.Aprobado);

        Assert.Contains(PaymentCertificateStatus.Facturado, allowed);
        Assert.Contains(PaymentCertificateStatus.Cerrado, allowed);
        Assert.Contains(PaymentCertificateStatus.Anulado, allowed);
        Assert.Contains(PaymentCertificateStatus.Archivado, allowed);
        Assert.Equal(4, allowed.Count);
    }

    [Fact]
    public void GetAllowedNext_Archivado_ReturnsEmpty()
    {
        var allowed = PaymentCertificateStatusTransitions.GetAllowedNext(PaymentCertificateStatus.Archivado);

        Assert.Empty(allowed);
    }

    [Fact]
    public void IsEditable_Borrador_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.IsEditable(PaymentCertificateStatus.Borrador));
    }

    [Fact]
    public void IsEditable_Presentado_ReturnsFalse()
    {
        Assert.False(PaymentCertificateStatusTransitions.IsEditable(PaymentCertificateStatus.Presentado));
    }

    [Fact]
    public void IsImmutable_Aprobado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.IsImmutable(PaymentCertificateStatus.Aprobado));
    }

    [Fact]
    public void IsImmutable_Pagado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.IsImmutable(PaymentCertificateStatus.Pagado));
    }

    [Fact]
    public void IsImmutable_Cerrado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.IsImmutable(PaymentCertificateStatus.Cerrado));
    }

    [Fact]
    public void IsImmutable_Anulado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.IsImmutable(PaymentCertificateStatus.Anulado));
    }

    [Fact]
    public void IsImmutable_Borrador_ReturnsFalse()
    {
        Assert.False(PaymentCertificateStatusTransitions.IsImmutable(PaymentCertificateStatus.Borrador));
    }

    [Fact]
    public void CanBeCorrected_Observado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanBeCorrected(PaymentCertificateStatus.Observado));
    }

    [Fact]
    public void CanBeCorrected_Rechazado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanBeCorrected(PaymentCertificateStatus.Rechazado));
    }

    [Fact]
    public void CanBeCorrected_Borrador_ReturnsFalse()
    {
        Assert.False(PaymentCertificateStatusTransitions.CanBeCorrected(PaymentCertificateStatus.Borrador));
    }

    [Fact]
    public void CanBeCorrected_Aprobado_ReturnsFalse()
    {
        Assert.False(PaymentCertificateStatusTransitions.CanBeCorrected(PaymentCertificateStatus.Aprobado));
    }

    [Fact]
    public void AllStatuses_Archivado_HasNoOutgoingTransitions()
    {
        var allowed = PaymentCertificateStatusTransitions.GetAllowedNext(PaymentCertificateStatus.Archivado);
        Assert.Empty(allowed);
    }

    [Fact]
    public void FullWorkflow_FromBorradorToArchivado_Succeeds()
    {
        var status = PaymentCertificateStatus.Borrador;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Presentado);
        status = PaymentCertificateStatus.Presentado;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.EnRevision);
        status = PaymentCertificateStatus.EnRevision;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.PendienteAprobacion);
        status = PaymentCertificateStatus.PendienteAprobacion;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Aprobado);
        status = PaymentCertificateStatus.Aprobado;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Facturado);
        status = PaymentCertificateStatus.Facturado;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Pagado);
        status = PaymentCertificateStatus.Pagado;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Cerrado);
        status = PaymentCertificateStatus.Cerrado;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Archivado);
        status = PaymentCertificateStatus.Archivado;

        Assert.Equal(PaymentCertificateStatus.Archivado, status);
    }

    [Fact]
    public void CorreccionWorkflow_BorradorToObservadoToCorregido_Succeeds()
    {
        var status = PaymentCertificateStatus.Borrador;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Presentado);
        status = PaymentCertificateStatus.Presentado;

        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Observado);
        status = PaymentCertificateStatus.Observado;

        Assert.True(PaymentCertificateStatusTransitions.CanBeCorrected(status));
        PaymentCertificateStatusTransitions.EnsureCanTransition(status, PaymentCertificateStatus.Corregido);
        status = PaymentCertificateStatus.Corregido;

        Assert.Equal(PaymentCertificateStatus.Corregido, status);
    }

    [Fact]
    public void CanTransition_ObservadoToArchivado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Observado, PaymentCertificateStatus.Archivado));
    }

    [Fact]
    public void CanTransition_RechazadoToAnulado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Rechazado, PaymentCertificateStatus.Anulado));
    }

    [Fact]
    public void CanTransition_CorregidoToPresentado_ReturnsTrue()
    {
        Assert.True(PaymentCertificateStatusTransitions.CanTransition(
            PaymentCertificateStatus.Corregido, PaymentCertificateStatus.Presentado));
    }
}
