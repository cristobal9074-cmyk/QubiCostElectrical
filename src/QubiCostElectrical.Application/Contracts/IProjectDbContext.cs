namespace QubiCostElectrical;

/// <summary>
/// Servicios cuya BD SQLite es la del proyecto abierto (`.qce`). Se reconstruyen cuando cambia
/// la ruta del proyecto. Antes esto era la closure `RewireContractsIfProjectChanged` dentro de
/// `WpfAppHostBuilder`; ahora es un singleton con una sola suscripción a `Session.Changed`.
/// </summary>
public interface IProjectDbContext
{
    string? CurrentProjectPath { get; }

    /// <summary>Servicio de contratos ligado al proyecto abierto. <c>null</c> si no hay proyecto.</summary>
    IContractService? Contracts { get; }
}
