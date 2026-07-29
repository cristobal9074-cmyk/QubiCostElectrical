namespace QubiCostElectrical;

public enum ContractType
{
    Principal = 0,
    Subcontrato = 1,
    OrdenServicio = 2,
    Convenio = 3,
    ContratoMarco = 4,
    AcuerdoComplementario = 5,
    Otro = 6
}

public enum ContractStatus
{
    Borrador = 0,
    PendienteRevision = 1,
    Aprobado = 2,
    Vigente = 3,
    Suspendido = 4,
    EnCierre = 5,
    Cerrado = 6,
    Terminado = 7,
    Rescindido = 8,
    Archivado = 9
}

public enum ContractPartyRole
{
    Cliente = 0,
    Mandante = 1,
    Contratista = 2,
    Subcontratista = 3,
    Proveedor = 4,
    Inspector = 5,
    Administrador = 6,
    RepresentanteLegal = 7,
    ResponsableTecnico = 8
}
