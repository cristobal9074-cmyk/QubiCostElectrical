using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed partial class CostoViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly DashboardPresenter dashboard;
    private readonly ICurrencyConversionService currencySvc;
    private readonly ProjectAuditRecorder? audit;

    private bool showEmptyState = true;
    private bool isLoading;
    private bool showFamilias;
    private bool showMoSection;
    private bool showGoSection;
    private bool showOrigenes;
    private string costDirectoLabel = "";
    private string costDirectoSubLabel = "";
    private string totalVentaLabel = "";
    private string totalVentaSubLabel = "";
    private string margenLabel = "";
    private string ufM2Label = "";
    private string ufM2SubLabel = "";
    private string resumenFooterText = "";
    private string valorUfDiaLabel = "";
    private string famCostoLabel = "—";
    private string famVentaLabel = "—";
    private string famDeltaLabel = "—";
    private string chainIvaLabel = "—";
    private string chainTotalLabel = "—";
    private string currency = "CLP";
    private string totalMoDirectaLabel = "";
    private string totalMoIndirectaLabel = "";
    private string totalMoLabel = "";
    private string totalMoSubLabel = "";
    private decimal moDirectaPct;
    private decimal moIndirectaPct;
    private int totalMoDotacion;
    private int totalMoDirectaDotacion;
    private string totalGoLabel = "";
    private string totalGoUfLabel = "";
    private string totalGoUfSubLabel = "";
    private string goComposeHint = "sin montos aún";
    private string goFooterSubLabel = "Listo para cargar números reales";
    private string goCategoriaCountLabel = "0 categorías";
    private string goItemCountLabel = "0";
    private string currencyColumnHeader = "Valor (CLP)";
    private bool canEdit;
    private bool suppressGoRefresh;
    private bool suppressCoefRefresh;
    private bool suppressNegotiatedDiscountsRefresh;
    private bool suppressStaffingPlanRefresh;
    private string totalStaffingPlanLabel = "";
    private decimal currentValorUf = PriceConversion.DefaultValorUfClp;
    private string gastosGeneralesText = "";
    private string utilidadText = "";
    private string valorAgregadoEquiposText = "";
    private string ferreteriaPctText = "5,0";
    private bool ferreteriaActiva;
    private string utilidadPolicyLabel = "Utilidad / margen s/ venta";
    private string coefFactorMedioLabel = "1,00×";
    private string coefActivasLabel = "0/9";
    private bool coefAppliedFlash;
    private bool politicaCostosExcel;
    private string costosPresetHint = "Sin preset guardado en este proyecto";
    private CostoRowVm? ventaCierre;
    private CostoRowVm? ufMetroCierre;
    public ClosingPolicySectionVm ClosingPolicySection { get; }
    public CommercialNegotiationVm NegotiationSimulator { get; }

    public ClosingPolicyType CurrentPolicy => session.Data.ClosingPolicy?.Policy ?? ClosingPolicyType.CostPlusMarkup;
    public bool IsGastosGeneralesEditable => CanEdit && CurrentPolicy != ClosingPolicyType.CostPlusFee;
    public bool IsUtilidadEditable => CanEdit && CurrentPolicy == ClosingPolicyType.CostPlusMarkup;
    public bool IsFactoresFamiliaEditable => CanEdit && (CurrentPolicy == ClosingPolicyType.CostPlusMarkup || CurrentPolicy == ClosingPolicyType.UnitPricePure);

    public string GastosGeneralesPolicyHint => CurrentPolicy switch
    {
        ClosingPolicyType.CostPlusFee => "Inactivo: en Costo + Fee no se aplica GG sobre compras directas",
        ClosingPolicyType.TargetPrice => "Informativo: GG absorbido dentro del precio objetivo",
        _ => ""
    };

    public string UtilidadPolicyHint => CurrentPolicy switch
    {
        ClosingPolicyType.CostPlusFee => "Sustituido por Fee de administración pactado",
        ClosingPolicyType.TargetPrice => "Margen residual calculado (Precio Objetivo − Costo Directo)",
        ClosingPolicyType.LumpSumEpc => "Margen de piso comercial; riesgo cubierto por contingencia",
        _ => ""
    };

    private static readonly string[] GoPalette =
    [
        "Brush.BrandStroke",
        "Brush.SuccessBright",
        "Brush.AmberRisk",
        "Brush.Violet",
        "Brush.Pink",
        "Brush.Slate"
    ];
}
