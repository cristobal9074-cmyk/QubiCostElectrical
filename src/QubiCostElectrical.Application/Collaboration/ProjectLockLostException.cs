namespace QubiCostElectrical;

/// <summary>Se perdió el lease de edición exclusiva antes o durante la escritura del .qce.</summary>
public sealed class ProjectLockLostException : InvalidOperationException
{
    public ProjectLockLostException()
        : this("Se perdió el bloqueo de edición. El proyecto quedó en solo lectura; no se puede guardar sobre el archivo compartido.")
    {
    }

    public ProjectLockLostException(string message)
        : base(message)
    {
    }
}
