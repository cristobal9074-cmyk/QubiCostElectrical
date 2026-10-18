using System;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Infrastructure;

public sealed class ProjectDbContext : IProjectDbContext, IDisposable
{
    private readonly BudgetProjectSession _session;
    private readonly IPermissionService _permissions;
    private readonly object _gate = new();
    private string? _wiredPath;
    private IContractService? _contracts;

    public ProjectDbContext(BudgetProjectSession session, IPermissionService permissions)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _session.Changed += OnSessionChanged;
        Rewire();
    }

    public string? CurrentProjectPath => _session.CurrentProjectPath;

    public IContractService? Contracts
    {
        get { lock (_gate) return _contracts; }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Rewire();

    private void Rewire()
    {
        var path = _session.CurrentProjectPath;
        lock (_gate)
        {
            if (string.Equals(path, _wiredPath, StringComparison.OrdinalIgnoreCase))
                return;
            _wiredPath = path;

            // Los `SqliteContract*Repository` son sin estado (abren una conexión por operación y
            // la cierran con `using`), así que no retienen handles. `ContractService` hoy tampoco
            // es `IDisposable`; el descarte defensivo cubre el caso de que alguno de ellos pase a
            // retener recursos sin que haya que volver a tocar este punto.
            (_contracts as IDisposable)?.Dispose();

            if (string.IsNullOrWhiteSpace(path))
            {
                _contracts = null;
                return;
            }

            _contracts = new ContractService(
                new SqliteContractRepository(path),
                new SqliteContractVersionRepository(path),
                new SqliteContractPartyRepository(path),
                new SqliteContractMilestoneRepository(path),
                new SqliteContractScopeLinkRepository(path),
                new SqliteContractDocumentRepository(path),
                new SqliteContractAuditRepository(path),
                _permissions);
        }
    }

    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        lock (_gate)
        {
            (_contracts as IDisposable)?.Dispose();
            _contracts = null;
        }
    }
}
