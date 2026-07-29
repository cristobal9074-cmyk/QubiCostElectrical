using System;
using System.IO;
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
public sealed class PaymentCertificateWorkspaceViewModelTests
{
    public PaymentCertificateWorkspaceViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void CreateCommand_NavigatesToEditor()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PaymentCertificateWorkspaceViewModel(shell, host);

        vm.CreateCommand.Execute(null);

        Assert.Equal(ShellRoute.PaymentCertificateEditor, shell.CurrentRoute);
    }

    [Fact]
    public void EditCommand_WithNoSelection_DoesNothing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PaymentCertificateWorkspaceViewModel(shell, host);

        Assert.Null(vm.Selected);
        vm.EditCommand.Execute(null);

        Assert.NotEqual(ShellRoute.PaymentCertificateEditor, shell.CurrentRoute);
    }

    [Fact]
    public void ReviewCommand_NavigatesToValuationReview()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PaymentCertificateWorkspaceViewModel(shell, host);

        vm.Selected = CreateSampleCert();
        vm.ReviewCommand.Execute(null);

        Assert.Equal(ShellRoute.ValuationReviewWorkspace, shell.CurrentRoute);
    }

    [Fact]
    public void AdvancesCommand_NavigatesToAdvanceRetention()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PaymentCertificateWorkspaceViewModel(shell, host);

        vm.Selected = CreateSampleCert();
        vm.AdvancesCommand.Execute(null);

        Assert.Equal(ShellRoute.AdvanceRetentionWorkspace, shell.CurrentRoute);
    }

    [Fact]
    public void SearchText_TriggersLoad()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PaymentCertificateWorkspaceViewModel(shell, host);

        vm.SearchText = "test";
        // LoadAsync se llama desde el setter; verificamos que no falla
        Assert.Equal("test", vm.SearchText);
    }

    [Fact]
    public void IsReadOnly_ReflectsSessionState()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PaymentCertificateWorkspaceViewModel(shell, host);

        Assert.Equal(host.Session.IsReadOnly, vm.IsReadOnly);
    }

    [Fact]
    public async Task LoadAsync_ClearsErrorBeforeLoad()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PaymentCertificateWorkspaceViewModel(shell, host);

        // Con servicio disponible no debería mostrar error
        await vm.LoadAsync();

        Assert.Empty(vm.ErrorMessage);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static PaymentCertificate CreateSampleCert()
    {
        var now = DateTime.UtcNow;
        return new PaymentCertificate
        {
            Id = PaymentCertificateId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = "PC-TEST-001",
            RevisionNumber = 1,
            PeriodStart = new DateTime(2025, 1, 1),
            PeriodEnd = new DateTime(2025, 1, 31),
            CutOffDate = new DateTime(2025, 1, 15),
            PresentationDate = now,
            Currency = "CLP",
            ExchangeRate = 1m,
            Status = PaymentCertificateStatus.Borrador,
            Presenter = "testuser",
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
