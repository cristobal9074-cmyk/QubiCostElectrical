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

public sealed class CoefFactorVm : ViewModelBase
{
    private readonly Action<string, decimal> onCommit;
    private readonly Action<string, decimal> onDiscountCommit;
    private string valueText;
    private string discountText;

    public CoefFactorVm(
        string familyKey,
        string name,
        string map,
        string excelRef,
        decimal value,
        Action<string, decimal> onCommit,
        Action<string, decimal> onDiscountCommit)
    {
        FamilyKey = familyKey;
        Name = name;
        Map = map;
        ExcelRef = excelRef;
        this.onCommit = onCommit;
        this.onDiscountCommit = onDiscountCommit;
        valueText = value.ToString("N2", CultureInfo.CurrentCulture);
        discountText = "0";
    }

    public string FamilyKey { get; }
    public string Name { get; }
    public string Map { get; }
    public string ExcelRef { get; }

    public string EffectiveLabel
    {
        get => effectiveLabel;
        private set => SetProperty(ref effectiveLabel, value);
    }

    private string effectiveLabel = "";

    public void SetEffectiveSilent(decimal markup, decimal discountPct)
    {
        var eff = ExcelCostosBridge.PresentationFactor(markup, discountPct);
        EffectiveLabel = eff.ToString("N2", CultureInfo.CurrentCulture) + "× efectivo";
    }

    public string DiscountText
    {
        get => discountText;
        set
        {
            if (!SetProperty(ref discountText, value ?? ""))
                return;
            if (!TryParseDecimal(value, out var v))
                return;
            v = Math.Clamp(v, 0m, 50m);
            onDiscountCommit(FamilyKey, v);
        }
    }

    public void SetDiscountSilent(decimal pct)
    {
        discountText = pct.ToString("N0", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(DiscountText));
    }

    public string ValueText
    {
        get => valueText;
        set
        {
            if (!SetProperty(ref valueText, value ?? ""))
                return;
            if (!TryParseDecimal(value, out var v))
                return;
            v = Math.Clamp(v, 1m, 5m);
            onCommit(FamilyKey, v);
        }
    }

    public void SetValueSilent(decimal value)
    {
        valueText = value.ToString("N2", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(ValueText));
    }

}
