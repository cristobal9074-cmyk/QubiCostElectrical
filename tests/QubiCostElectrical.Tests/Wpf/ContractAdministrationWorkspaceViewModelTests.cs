using System;
using System.Threading.Tasks;
using QubiCostElectrical.Wpf.Features.ContractAdministration;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class FakeShell : IShellNavigationHost
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

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class AdjustmentWorkspaceViewModelTests
{
    public AdjustmentWorkspaceViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdjustmentWorkspaceViewModel(shell, host);
        Assert.NotNull(vm);
    }

    [Fact]
    public void IsReadOnly_ReflectsSessionState()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdjustmentWorkspaceViewModel(shell, host);
        Assert.Equal(host.Session.IsReadOnly, vm.IsReadOnly);
    }

    [Fact]
    public void SearchText_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdjustmentWorkspaceViewModel(shell, host);

        vm.SearchText = "test-filter";
        Assert.Equal("test-filter", vm.SearchText);
    }

    [Fact]
    public async Task LoadAsync_SetsErrorWhenServiceMissing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdjustmentWorkspaceViewModel(shell, host);

        await vm.LoadAsync();

        Assert.NotEmpty(vm.ErrorMessage);
    }

    [Fact]
    public void SelectedItem_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new AdjustmentWorkspaceViewModel(shell, host);

        var item = CreateSampleAdjustment();
        vm.Selected = item;

        Assert.Same(item, vm.Selected);
    }

    private static ContractAdjustment CreateSampleAdjustment()
    {
        return new ContractAdjustment
        {
            Id = AdjustmentId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = 1,
            Period = 1,
            AdjustmentType = AdjustmentType.IPC,
            BaseAmount = 100_000m,
            AdjustedAmount = 105_000m,
            CurrencyCode = "CLP",
            Status = AdjustmentStatus.Draft,
            Description = "Test adjustment",
            Responsible = "testuser",
            RowVersion = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "testuser"
        };
    }
}

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class GuaranteeWorkspaceViewModelTests
{
    public GuaranteeWorkspaceViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new GuaranteeWorkspaceViewModel(shell, host);
        Assert.NotNull(vm);
    }

    [Fact]
    public void IsReadOnly_ReflectsSessionState()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new GuaranteeWorkspaceViewModel(shell, host);
        Assert.Equal(host.Session.IsReadOnly, vm.IsReadOnly);
    }

    [Fact]
    public void SearchText_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new GuaranteeWorkspaceViewModel(shell, host);

        vm.SearchText = "search-guarantee";
        Assert.Equal("search-guarantee", vm.SearchText);
    }

    [Fact]
    public async Task LoadAsync_SetsErrorWhenServiceMissing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new GuaranteeWorkspaceViewModel(shell, host);

        await vm.LoadAsync();

        Assert.NotEmpty(vm.ErrorMessage);
    }

    [Fact]
    public void SelectedItem_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new GuaranteeWorkspaceViewModel(shell, host);

        var item = CreateSampleGuarantee();
        vm.Selected = item;

        Assert.Same(item, vm.Selected);
    }

    private static ContractGuarantee CreateSampleGuarantee()
    {
        return new ContractGuarantee
        {
            Id = GuaranteeId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = 1,
            GuaranteeType = GuaranteeType.Performance,
            Number = "G-001",
            Issuer = "Banco Test",
            Institution = "Test Institution",
            Beneficiary = "Empresa",
            Amount = 50_000m,
            Percentage = 10m,
            CurrencyCode = "CLP",
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            Status = GuaranteeStatus.Active,
            Responsible = "testuser",
            RowVersion = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "testuser"
        };
    }
}

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PenaltyWorkspaceViewModelTests
{
    public PenaltyWorkspaceViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PenaltyWorkspaceViewModel(shell, host);
        Assert.NotNull(vm);
    }

    [Fact]
    public void IsReadOnly_ReflectsSessionState()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PenaltyWorkspaceViewModel(shell, host);
        Assert.Equal(host.Session.IsReadOnly, vm.IsReadOnly);
    }

    [Fact]
    public void SearchText_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PenaltyWorkspaceViewModel(shell, host);

        vm.SearchText = "penalty-filter";
        Assert.Equal("penalty-filter", vm.SearchText);
    }

    [Fact]
    public async Task LoadAsync_SetsErrorWhenServiceMissing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PenaltyWorkspaceViewModel(shell, host);

        await vm.LoadAsync();

        Assert.NotEmpty(vm.ErrorMessage);
    }

    [Fact]
    public void SelectedItem_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new PenaltyWorkspaceViewModel(shell, host);

        var item = CreateSamplePenalty();
        vm.Selected = item;

        Assert.Same(item, vm.Selected);
    }

    private static ContractPenalty CreateSamplePenalty()
    {
        return new ContractPenalty
        {
            Id = PenaltyId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = 1,
            PenaltyType = PenaltyType.Delay,
            Cause = "Retraso en entrega",
            Amount = 10_000m,
            Percentage = 5m,
            CurrencyCode = "CLP",
            Status = PenaltyStatus.Draft,
            Date = DateTime.UtcNow,
            Responsible = "testuser",
            Observations = "Test observation",
            RowVersion = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "testuser"
        };
    }
}

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class InvoiceWorkspaceViewModelTests
{
    public InvoiceWorkspaceViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new InvoiceWorkspaceViewModel(shell, host);
        Assert.NotNull(vm);
    }

    [Fact]
    public void IsReadOnly_ReflectsSessionState()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new InvoiceWorkspaceViewModel(shell, host);
        Assert.Equal(host.Session.IsReadOnly, vm.IsReadOnly);
    }

    [Fact]
    public void SearchText_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new InvoiceWorkspaceViewModel(shell, host);

        vm.SearchText = "invoice-search";
        Assert.Equal("invoice-search", vm.SearchText);
    }

    [Fact]
    public async Task LoadAsync_SetsErrorWhenServiceMissing()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new InvoiceWorkspaceViewModel(shell, host);

        await vm.LoadAsync();

        Assert.NotEmpty(vm.ErrorMessage);
    }

    [Fact]
    public void SelectedItem_CanBeSet()
    {
        var host = WpfAppHost.Current;
        var shell = new FakeShell();
        var vm = new InvoiceWorkspaceViewModel(shell, host);

        var item = CreateSampleInvoice();
        vm.Selected = item;

        Assert.Same(item, vm.Selected);
    }

    private static ContractInvoice CreateSampleInvoice()
    {
        return new ContractInvoice
        {
            Id = InvoiceId.New(),
            ContractId = ContractId.New(),
            VisibleNumber = 1,
            InvoiceNumber = "FAC-001",
            Supplier = "Proveedor Test",
            Client = "Cliente Test",
            Subtotal = 100_000m,
            TaxAmount = 19_000m,
            Total = 119_000m,
            CurrencyCode = "CLP",
            Status = InvoiceStatus.Draft,
            Date = DateTime.UtcNow,
            ContractReference = "CT-001",
            RowVersion = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "testuser"
        };
    }
}
