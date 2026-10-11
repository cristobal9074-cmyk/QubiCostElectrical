using System;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public enum NegotiationRiskLevel
{
    Healthy, // > 15%
    Warning, // 10% - 15%
    Critical // < 10%
}

/// <summary>
/// Simulador interactivo de negociación comercial y cierre de oferta.
/// Permite simular descuentos porcentuales o precios objetivo pactados con el mandante,
/// evaluando en tiempo real el margen de ganancia resultante y el semáforo de viabilidad.
/// </summary>
public sealed class CommercialNegotiationVm : ViewModelBase
{
    private readonly Action<decimal> onApplyAsTargetPrice;
    private readonly Func<bool> canEdit;

    private decimal baseDirectCostClp;
    private decimal baseSaleClp;
    private decimal ufRate = 39000m;
    private decimal discountPct = 0m;
    private bool isUpdatingInternal;

    public CommercialNegotiationVm(Action<decimal> onApplyAsTargetPrice, Func<bool>? canEdit = null)
    {
        this.onApplyAsTargetPrice = onApplyAsTargetPrice ?? throw new ArgumentNullException(nameof(onApplyAsTargetPrice));
        this.canEdit = canEdit ?? (() => true);

        ApplyTargetPriceCommand = new RelayCommand(ApplyTargetPrice, () => this.canEdit() && BaseSaleClp > 0);
        SetDiscountPresetCommand = new RelayCommand<string>(SetDiscountPreset, _ => this.canEdit());
    }

    public ICommand ApplyTargetPriceCommand { get; }
    public ICommand SetDiscountPresetCommand { get; }

    public decimal BaseDirectCostClp
    {
        get => baseDirectCostClp;
        private set { if (SetProperty(ref baseDirectCostClp, value)) Recompute(); }
    }

    public decimal BaseSaleClp
    {
        get => baseSaleClp;
        private set { if (SetProperty(ref baseSaleClp, value)) Recompute(); }
    }

    public decimal UfRate
    {
        get => ufRate;
        private set { if (SetProperty(ref ufRate, value)) Recompute(); }
    }

    public decimal DiscountPct
    {
        get => discountPct;
        set
        {
            var clamped = Math.Clamp(Math.Round(value, 1), 0m, 30m);
            if (SetProperty(ref discountPct, clamped))
            {
                if (!isUpdatingInternal)
                    Recompute();
            }
        }
    }

    public string NegotiatedPriceClpText
    {
        get => $"$ {NegotiatedPriceClp:N0}";
        set
        {
            if (isUpdatingInternal) return;
            var cleaned = (value ?? "").Replace("$", "").Replace(".", "").Trim();
            if (decimal.TryParse(cleaned, out var parsed) && BaseSaleClp > 0)
            {
                var discount = ((BaseSaleClp - parsed) / BaseSaleClp) * 100m;
                DiscountPct = Math.Clamp(discount, 0m, 30m);
            }
        }
    }

    public decimal NegotiatedPriceClp => Math.Max(0m, Math.Round(BaseSaleClp * (1m - (DiscountPct / 100m))));
    public decimal NegotiatedPriceUf => PriceConversion.ClpToUf(NegotiatedPriceClp, UfRate);

    public decimal DiscountAmountClp => Math.Max(0m, BaseSaleClp - NegotiatedPriceClp);
    public decimal DiscountAmountUf => PriceConversion.ClpToUf(DiscountAmountClp, UfRate);

    public decimal ResultingProfitClp => NegotiatedPriceClp - BaseDirectCostClp;
    public decimal ResultingProfitUf => PriceConversion.ClpToUf(ResultingProfitClp, UfRate);

    public decimal ResultingMarginPct => NegotiatedPriceClp > 0
        ? Math.Round(((NegotiatedPriceClp - BaseDirectCostClp) / NegotiatedPriceClp) * 100m, 1)
        : 0m;

    public NegotiationRiskLevel RiskLevel
    {
        get
        {
            if (ResultingMarginPct >= 15m) return NegotiationRiskLevel.Healthy;
            if (ResultingMarginPct >= 10m) return NegotiationRiskLevel.Warning;
            return NegotiationRiskLevel.Critical;
        }
    }

    public string RiskBadgeText => RiskLevel switch
    {
        NegotiationRiskLevel.Healthy => "Margen Saludable",
        NegotiationRiskLevel.Warning => "Margen Acotado",
        _ => "Margen Crítico"
    };

    public string RiskExplanationText => RiskLevel switch
    {
        NegotiationRiskLevel.Healthy => "Zona de negociación segura. Rentabilidad holgada para absorber imprevistos de faena.",
        NegotiationRiskLevel.Warning => "Precaución: margen estrecho (10% - 15%). Requiere control riguroso de rendimientos y compras.",
        _ => "Alerta: margen inferior al 10%. Alto riesgo de pérdida operativa si surgen atrasos o mermas imprevistas."
    };

    public Brush RiskBrush => RiskLevel switch
    {
        NegotiationRiskLevel.Healthy => ThemeOrFallback("Brush.SuccessBright", "#22C55E"),
        NegotiationRiskLevel.Warning => ThemeOrFallback("Brush.AmberRisk", "#F59E0B"),
        _ => ThemeOrFallback("Brush.Danger2", "#EF4444")
    };

    public Brush RiskBackgroundBrush => RiskLevel switch
    {
        NegotiationRiskLevel.Healthy => ThemeOrFallback("Brush.SuccessSoft",
            () => new SolidColorBrush(Color.FromArgb(30, 34, 197, 94))),
        NegotiationRiskLevel.Warning => ThemeOrFallback("Brush.WarningSoft",
            () => new SolidColorBrush(Color.FromArgb(30, 245, 158, 11))),
        _ => ThemeOrFallback("Brush.RedSoft",
            () => new SolidColorBrush(Color.FromArgb(30, 239, 68, 68)))
    };

    private static Brush ThemeOrFallback(string resourceKey, string fallbackHex)
    {
        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        brush.Freeze();
        return brush;
    }

    private static Brush ThemeOrFallback(string resourceKey, Func<SolidColorBrush> fallbackFactory)
    {
        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;

        var brush = fallbackFactory();
        if (brush.CanFreeze)
            brush.Freeze();
        return brush;
    }

    public void UpdateBase(decimal directCostClp, decimal saleClp, decimal currentUf)
    {
        isUpdatingInternal = true;
        try
        {
            BaseDirectCostClp = directCostClp;
            BaseSaleClp = saleClp;
            if (currentUf > 0)
                UfRate = currentUf;
        }
        finally
        {
            isUpdatingInternal = false;
            Recompute();
        }
    }

    private void SetDiscountPreset(string? pctStr)
    {
        if (decimal.TryParse(pctStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pct))
        {
            DiscountPct = pct;
        }
    }

    private void ApplyTargetPrice()
    {
        if (NegotiatedPriceClp > 0)
        {
            onApplyAsTargetPrice(NegotiatedPriceClp);
        }
    }

    private void Recompute()
    {
        OnPropertyChanged(nameof(NegotiatedPriceClp));
        OnPropertyChanged(nameof(NegotiatedPriceClpText));
        OnPropertyChanged(nameof(NegotiatedPriceUf));
        OnPropertyChanged(nameof(DiscountAmountClp));
        OnPropertyChanged(nameof(DiscountAmountUf));
        OnPropertyChanged(nameof(ResultingProfitClp));
        OnPropertyChanged(nameof(ResultingProfitUf));
        OnPropertyChanged(nameof(ResultingMarginPct));
        OnPropertyChanged(nameof(RiskLevel));
        OnPropertyChanged(nameof(RiskBadgeText));
        OnPropertyChanged(nameof(RiskExplanationText));
        OnPropertyChanged(nameof(RiskBrush));
        OnPropertyChanged(nameof(RiskBackgroundBrush));
        (ApplyTargetPriceCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
