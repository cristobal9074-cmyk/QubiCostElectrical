namespace QubiCostElectrical;

public enum PaymentCertificateStatus
{
    Borrador = 0,
    Presentado = 1,
    EnRevision = 2,
    Observado = 3,
    Corregido = 4,
    PendienteAprobacion = 5,
    Aprobado = 6,
    Rechazado = 7,
    Facturado = 8,
    Pagado = 9,
    Cerrado = 10,
    Anulado = 11,
    Archivado = 12
}

public enum AdvanceType
{
    Financiero = 0,
    Movilizacion = 1,
    Materiales = 2,
    Otro = 3
}

public enum RetentionType
{
    Contractual = 0,
    Garantia = 1,
    Calidad = 2,
    Tributaria = 3,
    FielCumplimiento = 4,
    Otro = 5
}

public enum PaymentObservationStatus
{
    Abierta = 0,
    Respondida = 1,
    Resuelta = 2,
    Rechazada = 3,
    Archivada = 4
}
