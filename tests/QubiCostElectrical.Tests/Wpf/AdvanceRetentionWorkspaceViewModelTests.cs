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
public sealed class AdvanceRetentionWorkspaceViewModelTests
{
    public AdvanceRetentionWorkspaceViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void BackCommand_NavigatesToWorkspace()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        vm.BackCommand.Execute(null);

        Assert.Equal(ShellRoute.PaymentCertificateWorkspace, shell.CurrentRoute);
    }

    [Fact]
    public void IsReadOnly_ReflectsSessionState()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        Assert.Equal(host.Session.IsReadOnly, vm.IsReadOnly);
    }

    [Fact]
    public void AddAdvanceCommand_WithoutContract_DoesNothing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        // Sin CurrentContractId (no se ha llamado LoadForContractAsync)
        Assert.False(vm.AddAdvanceCommand.CanExecute(null));
    }

    [Fact]
    public void ReleaseRetentionCommand_WithoutSelection_DoesNothing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        Assert.Null(vm.SelectedRetention);
        Assert.False(vm.ReleaseRetentionCommand.CanExecute(null));
    }

    [Fact]
    public void AdvancesCollection_InitiallyEmpty()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        Assert.Empty(vm.Advances);
    }

    [Fact]
    public void RetentionsCollection_InitiallyEmpty()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        Assert.Empty(vm.Retentions);
    }

    [Fact]
    public void SelectedAdvance_RaisesPropertyChanged()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        bool changed = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SelectedAdvance)) changed = true; };

        vm.SelectedAdvance = CreateSampleAdvance();
        Assert.True(changed);
    }

    [Fact]
    public void SelectedRetention_RaisesPropertyChanged()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdvanceRetentionWorkspaceViewModel(shell, host);

        bool changed = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SelectedRetention)) changed = true; };

        vm.SelectedRetention = CreateSampleRetention();
        Assert.True(changed);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static ContractAdvance CreateSampleAdvance()
    {
        return new ContractAdvance
        {
            Id = ContractAdvanceId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = "ANT-001",
            Amount = 50_000m,
            Currency = "CLP",
            Date = DateTime.UtcNow,
            AmortizationPercentage = 10m,
            InitialBalance = 50_000m,
            CurrentBalance = 50_000m,
            TotalAmortized = 0m,
            Status = "Active",
            DocumentReference = "",
            Comment = "",
            CreatedBy = "testuser",
            CreatedUtc = DateTime.UtcNow,
            AdvanceType = AdvanceType.Financiero
        };
    }

    private static ContractRetention CreateSampleRetention()
    {
        return new ContractRetention
        {
            Id = ContractRetentionId.New(),
            ContractId = ContractId.New(),
            RetentionType = RetentionType.Contractual,
            BaseType = "Subtotal",
            Percentage = 5m,
            FixedAmount = 0m,
            LimitAmount = 0m,
            ExpiryDate = null,
            ReleaseCondition = "",
            Status = "Active",
            Observation = ""
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
