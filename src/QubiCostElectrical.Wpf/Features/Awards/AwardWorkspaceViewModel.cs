using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using DomainBidComparison = QubiCostElectrical.BidComparison;

namespace QubiCostElectrical.Wpf.Features.Awards;

public sealed class AwardWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<DomainBidComparison> _comparisons = new();
    private readonly ObservableCollection<AwardDecision> _awards = new();
    private DomainBidComparison? _selectedComparison;
    private AwardDecision? _selected;
    private string _reason = "Adjudicación confirmada";
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public AwardWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host;
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CreateFromRecCommand = new RelayCommand(async () => await CreateAsync(), () => SelectedComparison != null && !_isBusy && !IsReadOnly);
        SubmitCommand = new RelayCommand(async () => await SubmitAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        ApproveCommand = new RelayCommand(async () => await ApproveAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        RejectCommand = new RelayCommand(async () => await RejectAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        _ = LoadAsync();
    }

    public ObservableCollection<DomainBidComparison> Comparisons => _comparisons;
    public ObservableCollection<AwardDecision> Awards => _awards;
    public DomainBidComparison? SelectedComparison { get => _selectedComparison; set { if (SetProperty(ref _selectedComparison, value)) Raise(); } }
    public AwardDecision? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) Raise(); } }
    public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public ICommand RefreshCommand { get; }
    public ICommand CreateFromRecCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy || _host.Services.RfqService is null || _host.Services.BidComparisonService is null) return;
        _isBusy = true;
        try
        {
            _comparisons.Clear();
            _awards.Clear();
            var path = _host.Session.CurrentProjectPath ?? "project";
            foreach (var rfq in await _host.Services.RfqService.SearchAsync(path, null, 0, 50))
            {
                foreach (var c in await _host.Services.BidComparisonService.ListByRfqAsync(rfq.Id))
                    _comparisons.Add(c);
                if (_host.Services.AwardService is not null)
                {
                    // list via comparison rfq
                }
            }
            StatusMessage = $"{_comparisons.Count} comparación(es).";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task CreateAsync()
    {
        if (SelectedComparison is null || _host.Services.AwardService is null) return;
        _isBusy = true;
        try
        {
            var kind = SelectedComparison.Recommendation?.SuggestedKind ?? AwardKind.Total;
            var award = await _host.Services.AwardService.CreateFromRecommendationAsync(SelectedComparison.Id, kind, Reason, Actor());
            _awards.Insert(0, award);
            Selected = award;
            StatusMessage = $"Adjudicación borrador {award.Id.Value[..8]} · impacto {award.BudgetImpactBase:N0}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task SubmitAsync()
    {
        if (Selected is null) return;
        _isBusy = true;
        try
        {
            Selected = await _host.Services.AwardService.SubmitAsync(Selected.Id, Selected.RowVersion, Actor());
            StatusMessage = "Enviada a aprobación.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task ApproveAsync()
    {
        if (Selected is null) return;
        _isBusy = true;
        try
        {
            Selected = await _host.Services.AwardService.ApproveAsync(Selected.Id, Selected.RowVersion, Actor());
            StatusMessage = "Adjudicación aprobada.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task RejectAsync()
    {
        if (Selected is null) return;
        _isBusy = true;
        try
        {
            Selected = await _host.Services.AwardService.RejectAsync(Selected.Id, Selected.RowVersion, Actor(), "Rechazada");
            StatusMessage = "Adjudicación rechazada.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "System";
    private void Raise()
    {
        foreach (var c in new[] { RefreshCommand, CreateFromRecCommand, SubmitCommand, ApproveCommand, RejectCommand })
            ((RelayCommand)c).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsReadOnly));
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        Raise();
        _ = LoadAsync();
    }
}
