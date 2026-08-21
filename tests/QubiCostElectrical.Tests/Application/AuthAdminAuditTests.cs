using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class AuthAdminAuditTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteAuthStore _store;
    private readonly AuthService _auth;

    public AuthAdminAuditTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"qce-auth-test-{Guid.NewGuid():N}.db");
        _store = new SqliteAuthStore($"Data Source={_dbPath}");
        _auth = new AuthService(_store);
    }

    [Fact]
    public async Task CreateUserAsync_persists_admin_audit_entry()
    {
        var created = await _auth.CreateUserAsync("est@empresa.cl", "2580", "Estimador Uno", UserRole.Estimator, "admin@empresa.cl");
        Assert.True(created.IsSuccess);

        var audit = await _auth.GetAdminAuditAsync(10);
        Assert.Contains(audit, e => e.Action == "CreateUser" && e.TargetEmail == "est@empresa.cl");
    }

    [Fact]
    public async Task UnlockUserAsync_clears_lock_and_logs_audit()
    {
        var created = await _auth.CreateUserAsync("bloq@empresa.cl", "2580", "Usuario Bloq", UserRole.Viewer, "admin@empresa.cl");
        Assert.True(created.IsSuccess);

        for (int i = 0; i < 5; i++)
            _store.RecordFailedAttempt("bloq@empresa.cl");

        var locked = _store.FindByEmail("bloq@empresa.cl");
        Assert.NotNull(locked);
        Assert.True(locked!.LockedUntil.HasValue);

        var unlocked = await _auth.UnlockUserAsync(created.Value!.Id, "admin@empresa.cl");
        Assert.True(unlocked.IsSuccess);
        Assert.Null(unlocked.Value!.LockedUntil);
        Assert.Equal(0, unlocked.Value.FailedAttempts);

        var audit = await _auth.GetAdminAuditAsync(10);
        Assert.Contains(audit, e => e.Action == "UnlockUser" && e.TargetEmail == "bloq@empresa.cl");
    }

    [Fact]
    public async Task ResetUserPinAsync_rejects_weak_pin_under_policy()
    {
        var created = await _auth.CreateUserAsync("pin@empresa.cl", "2580", "PIN Test", UserRole.Estimator, "admin@empresa.cl");
        Assert.True(created.IsSuccess);

        var weak = await _auth.ResetUserPinAsync(created.Value!.Id, "1234", "admin@empresa.cl");
        Assert.False(weak.IsSuccess);
        Assert.Equal(AuthAdminError.WeakPin, weak.Error);
    }

    public void Dispose()
    {
        _auth.Dispose();
        _store.Dispose();
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
        }
    }
}
