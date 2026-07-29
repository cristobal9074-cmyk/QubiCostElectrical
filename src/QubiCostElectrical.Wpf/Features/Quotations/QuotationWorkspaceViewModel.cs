using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using DomainTechnicalEvaluation = QubiCostElectrical.TechnicalEvaluation;
using DomainCommercialEvaluation = QubiCostElectrical.CommercialEvaluation;

namespace QubiCostElectrical.Wpf.Features.Quotations;

public sealed class QuotationWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<SupplierOffer> _offers = new();
    private readonly ObservableCollection<RequestForQuotation> _rfqs = new();
    private RequestForQuotation? _selectedRfq;
    private SupplierOffer? _selected;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _currency = "CLP";
    private decimal _unitPrice = 100m;
    private bool _isBusy;

    public QuotationWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _ = shell;
        _host = host;
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        RegisterCommand = new RelayCommand(async () => await RegisterAsync(), () => SelectedRfq != null && !_isBusy && !IsReadOnly);
        SubmitCommand = new RelayCommand(async () => await SubmitAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        EvaluateTechCommand = new RelayCommand(async () => await EvalTechAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        EvaluateCommercialCommand = new RelayCommand(async () => await EvalComAsync(), () => Selected != null && !_isBusy && !IsReadOnly);
        _ = LoadAsync();
    }

    public ObservableCollection<SupplierOffer> Offers => _offers;
    public ObservableCollection<RequestForQuotation> Rfqs => _rfqs;
    public RequestForQuotation? SelectedRfq { get => _selectedRfq; set { if (SetProperty(ref _selectedRfq, value)) { _ = LoadOffersAsync(); Raise(); } } }
    public SupplierOffer? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) Raise(); } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string Currency { get => _currency; set => SetProperty(ref _currency, value); }
    public decimal UnitPrice { get => _unitPrice; set => SetProperty(ref _unitPrice, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;
    public ICommand RefreshCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand EvaluateTechCommand { get; }
    public ICommand EvaluateCommercialCommand { get; }

    public async Task LoadAsync()
    {
        if (_isBusy || _host.Services.RfqService is null) return;
        _isBusy = true;
        try
        {
            var path = _host.Session.CurrentProjectPath ?? "project";
            _rfqs.Clear();
            foreach (var r in await _host.Services.RfqService.SearchAsync(path, null, 0, 100)) _rfqs.Add(r);
            StatusMessage = $"{_rfqs.Count} RFQ.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task LoadOffersAsync()
    {
        _offers.Clear();
        if (SelectedRfq is null || _host.Services.SupplierOfferService is null) return;
        foreach (var o in await _host.Services.SupplierOfferService.ListByRfqAsync(SelectedRfq.Id)) _offers.Add(o);
    }

    private async Task RegisterAsync()
    {
        if (SelectedRfq is null || _isBusy || IsReadOnly) return;
        _isBusy = true;
        try
        {
            var suppliers = await _host.Services.SupplierService.SearchAsync(null, SupplierStatus.Active, 0, 1);
            if (suppliers.Count == 0) { ErrorMessage = "Sin proveedores activos."; return; }
            var item = SelectedRfq.Items.FirstOrDefault();
            var offer = await _host.Services.SupplierOfferService.RegisterAsync(new SupplierOffer
            {
                RfqId = SelectedRfq.Id,
                SupplierId = suppliers[0].Id,
                CurrencyCode = Currency,
                TaxPercent = 19m,
                DiscountPercent = 0m,
                DeliveryLeadDays = 14,
                PaymentTerms = "30 días",
                Warranty = "12 meses",
                Lines =
                {
                    new OfferLine
                    {
                        LineId = Guid.NewGuid().ToString("N"),
                        OfferId = SupplierOfferId.New(),
                        RfqItemId = item?.ItemId,
                        Code = item?.Code ?? "ITEM",
                        Description = item?.Description ?? "Ítem",
                        Unit = item?.Unit ?? "UN",
                        Quantity = item?.Quantity ?? 1m,
                        UnitPriceOriginal = UnitPrice
                    }
                }
            }, Actor());
            StatusMessage = $"Oferta registrada v{offer.Version}.";
            await LoadOffersAsync();
            Selected = _offers.FirstOrDefault(x => x.Id.Value == offer.Id.Value);
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
            Selected = await _host.Services.SupplierOfferService.SubmitAsync(Selected.Id, Selected.RowVersion, Actor());
            StatusMessage = "Oferta enviada a evaluación.";
            await LoadOffersAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Raise(); }
    }

    private async Task EvalTechAsync()
    {
        if (Selected is null) return;
        try
        {
            await _host.Services.SupplierOfferService.SaveTechnicalAsync(new DomainTechnicalEvaluation
            {
                OfferId = Selected.Id,
                Compliance = TechnicalComplianceLevel.Compliant,
                DocumentComplete = true,
                Score = 80m
            }, Actor());
            StatusMessage = "Evaluación técnica guardada.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task EvalComAsync()
    {
        if (Selected is null) return;
        try
        {
            await _host.Services.SupplierOfferService.SaveCommercialAsync(new DomainCommercialEvaluation
            {
                OfferId = Selected.Id,
                Risk = CommercialRiskLevel.Low,
                Score = 75m,
                PaymentAssessment = "OK",
                WarrantyAssessment = "OK"
            }, Actor());
            StatusMessage = "Evaluación comercial guardada.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private string Actor() => _host.Session.ProjectInfo.UsuarioNombre ?? "System";
    private void Raise()
    {
        foreach (var c in new[] { RefreshCommand, RegisterCommand, SubmitCommand, EvaluateTechCommand, EvaluateCommercialCommand })
            ((RelayCommand)c).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsReadOnly));
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        Raise();
        _ = LoadAsync();
    }
}

public sealed class QuotationEditorViewModel
{
    public QuotationEditorViewModel(IShellNavigationHost shell, WpfAppHost host) => Workspace = new QuotationWorkspaceViewModel(shell, host);
    public QuotationWorkspaceViewModel Workspace { get; }
}
