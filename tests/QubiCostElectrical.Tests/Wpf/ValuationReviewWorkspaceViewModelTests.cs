using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.PaymentCertificates;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ValuationReviewWorkspaceViewModelTests
{
    public ValuationReviewWorkspaceViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void ApproveCommand_WithNoCertificate_DoesNothing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new ValuationReviewWorkspaceViewModel(shell, host);

        Assert.Null(vm.Certificate);
        Assert.False(vm.ApproveCommand.CanExecute(null));
    }

    [Fact]
    public void ObserveCommand_WithNoCertificate_DoesNothing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new ValuationReviewWorkspaceViewModel(shell, host);

        Assert.Null(vm.Certificate);
        Assert.False(vm.ObserveCommand.CanExecute(null));
    }

    [Fact]
    public void RejectCommand_WithNoCertificate_DoesNothing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new ValuationReviewWorkspaceViewModel(shell, host);

        Assert.Null(vm.Certificate);
        Assert.False(vm.RejectCommand.CanExecute(null));
    }

    [Fact]
    public void BackCommand_NavigatesToWorkspace()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new ValuationReviewWorkspaceViewModel(shell, host);

        vm.BackCommand.Execute(null);

        Assert.Equal(ShellRoute.PaymentCertificateWorkspace, shell.CurrentRoute);
    }

    [Fact]
    public void IsReadOnly_ReflectsSessionState()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new ValuationReviewWorkspaceViewModel(shell, host);

        Assert.Equal(host.Session.IsReadOnly, vm.IsReadOnly);
    }

    [Fact]
    public void CertificateProperty_RaisesPropertyChanged()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new ValuationReviewWorkspaceViewModel(shell, host);

        bool changed = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Certificate)) changed = true; };

        vm.Certificate = CreateSampleCert();

        Assert.True(changed);
        Assert.NotNull(vm.Certificate);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static PaymentCertificate CreateSampleCert()
    {
        var now = DateTime.UtcNow;
        return new PaymentCertificate
        {
            Id = PaymentCertificateId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = "PC-REVIEW-001",
            RevisionNumber = 1,
            PeriodStart = new DateTime(2025, 1, 1),
            PeriodEnd = new DateTime(2025, 1, 31),
            CutOffDate = new DateTime(2025, 1, 15),
            PresentationDate = now,
            Currency = "CLP",
            ExchangeRate = 1m,
            Status = PaymentCertificateStatus.EnRevision,
            Presenter = "presenter1",
            SubtotalValorizado = 100_000m,
            MontoBruto = 100_000m,
            MontoLiquido = 119_000m,
            RowVersion = Guid.NewGuid().ToString("N"),
            CreatedUtc = now,
            CreatedBy = "testuser",
            ModifiedUtc = now,
            ModifiedBy = "testuser",
            Hash = "abc"
        };
    }

    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            return true;
        }

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }
}
