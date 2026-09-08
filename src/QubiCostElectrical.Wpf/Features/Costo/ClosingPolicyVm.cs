using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class ClosingPolicyOptionVm
{
    public ClosingPolicyType Value { get; init; }
    public string DisplayName { get; init; } = "";

    public static readonly IReadOnlyList<ClosingPolicyOptionVm> All =
    [
        new() { Value = ClosingPolicyType.CostPlusMarkup, DisplayName = "Costo + Markup (motor)" },
        new() { Value = ClosingPolicyType.TargetPrice, DisplayName = "Precio Objetivo" },
        new() { Value = ClosingPolicyType.LumpSumEpc, DisplayName = "Suma Alzada / EPC" },
        new() { Value = ClosingPolicyType.CostPlusFee, DisplayName = "Costo + Fee" },
        new() { Value = ClosingPolicyType.UnitPricePure, DisplayName = "Precio Unitario Puro" },
        new() { Value = ClosingPolicyType.GainsharePainshare, DisplayName = "Gainshare / Painshare" }
    ];
}

/// <summary>
/// Sección "Política de cierre comercial" de la pantalla Costo. Un único cierre por proyecto
/// (no una colección): permite elegir la política y sus parámetros, y muestra el resultado de
/// aplicar ClosingPolicyEngine sobre el CostBreakdown ya calculado por la pantalla.
/// </summary>
public sealed class ClosingPolicySectionVm : ViewModelBase
{
    private readonly Action<ClosingPolicyType> onPolicyCommit;
    private readonly Action<string, decimal> onParamCommit;

    private ClosingPolicyOptionVm? selectedOption;
    private string targetPriceClpText = "0";
    private string lumpSumMinRiskPctText = "15";
    private string feeClpText = "0";
    private string feePctText = "8";
    private string targetCostClpText = "0";
    private string gainsharePctText = "50";
    private string gainshareCapClpText = "0";
    private bool showTargetPrice;
    private bool showLumpSum;
    private bool showCostPlusFee;
    private bool showGainshare;
    private string ventaFinalLabel = "";
    private string ventaFinalSubLabel = "";
    private string margenLabel = "";
    private string explicacion = "";
    private bool hasAdvertencias;
    private string stampNote = "";
    private bool hasStampNote;
    private string policyWarning = "";
    private bool hasPolicyWarning;

    public ClosingPolicySectionVm(Action<ClosingPolicyType> onPolicyCommit, Action<string, decimal> onParamCommit)
    {
        this.onPolicyCommit = onPolicyCommit;
        this.onParamCommit = onParamCommit;
        selectedOption = ClosingPolicyOptionVm.All[0];
    }

    public IReadOnlyList<ClosingPolicyOptionVm> Options => ClosingPolicyOptionVm.All;

    public ObservableCollection<string> Advertencias { get; } = new();

    public ClosingPolicyOptionVm? SelectedOption
    {
        get => selectedOption;
        set
        {
            // WPF puede empujar temporalmente null (ItemsSource reseteado, etc.); nunca dejamos
            // los flags Show* desalineados con la selección real — caemos a la primera opción.
            var resolved = value ?? Options[0];
            if (!SetProperty(ref selectedOption, resolved))
                return;
            UpdateVisibility(resolved.Value);
            if (value != null)
                onPolicyCommit(resolved.Value);
        }
    }

    public bool ShowTargetPrice { get => showTargetPrice; private set => SetProperty(ref showTargetPrice, value); }
    public bool ShowLumpSum { get => showLumpSum; private set => SetProperty(ref showLumpSum, value); }
    public bool ShowCostPlusFee { get => showCostPlusFee; private set => SetProperty(ref showCostPlusFee, value); }
    public bool ShowGainshare { get => showGainshare; private set => SetProperty(ref showGainshare, value); }

    public string VentaFinalLabel { get => ventaFinalLabel; set => SetProperty(ref ventaFinalLabel, value); }
    public string VentaFinalSubLabel { get => ventaFinalSubLabel; set => SetProperty(ref ventaFinalSubLabel, value); }
    public string MargenLabel { get => margenLabel; set => SetProperty(ref margenLabel, value); }
    public string Explicacion { get => explicacion; set => SetProperty(ref explicacion, value); }
    public bool HasAdvertencias { get => hasAdvertencias; set => SetProperty(ref hasAdvertencias, value); }
    public string StampNote { get => stampNote; set => SetProperty(ref stampNote, value); }
    public bool HasStampNote { get => hasStampNote; set => SetProperty(ref hasStampNote, value); }
    public string PolicyWarning { get => policyWarning; set => SetProperty(ref policyWarning, value); }
    public bool HasPolicyWarning { get => hasPolicyWarning; set => SetProperty(ref hasPolicyWarning, value); }

    /// <summary>Actualiza los avisos de salvaguarda: parámetro obsoleto y política que ignora factores.</summary>
    public void SetSafeguards(ClosingPolicySelection selection, decimal currentCostoEjecucionUf)
    {
        selection ??= ClosingPolicySelection.Default();

        StampNote = "";
        HasStampNote = false;
        if (selection.ParamsStampedAt is { } stamped && selection.ParamsStampedCostoEjecucionClp > 0m)
        {
            decimal drift = Math.Abs(currentCostoEjecucionUf - selection.ParamsStampedCostoEjecucionClp)
                / selection.ParamsStampedCostoEjecucionClp;
            if (drift > 0.05m)
            {
                StampNote = $"Parámetro fijado el {stamped:d}, cuando el costo de ejecución era "
                    + $"{selection.ParamsStampedCostoEjecucionClp:N2} UF (hoy {currentCostoEjecucionUf:N2} UF). Revíselo.";
                HasStampNote = true;
            }
        }

        PolicyWarning = selection.Policy switch
        {
            ClosingPolicyType.CostPlusFee =>
                "Esta política ignora los factores por familia y el GG/utilidad configurados.",
            ClosingPolicyType.TargetPrice =>
                "El precio objetivo reemplaza la venta por factores; GG y utilidad quedan como margen residual.",
            ClosingPolicyType.LumpSumEpc =>
                "La venta se cierra sobre el piso comercial más la contingencia mínima; los factores por familia no aplican.",
            _ => ""
        };
        HasPolicyWarning = PolicyWarning.Length > 0;
    }

    public string TargetPriceClpText
    {
        get => targetPriceClpText;
        set => CommitParamText(ref targetPriceClpText, value, nameof(ClosingPolicySelection.TargetPriceClp));
    }

    public string LumpSumMinRiskPctText
    {
        get => lumpSumMinRiskPctText;
        set => CommitParamText(ref lumpSumMinRiskPctText, value, nameof(ClosingPolicySelection.LumpSumMinRiskPct));
    }

    public string FeeClpText
    {
        get => feeClpText;
        set => CommitParamText(ref feeClpText, value, nameof(ClosingPolicySelection.FeeClp));
    }

    public string FeePctText
    {
        get => feePctText;
        set => CommitParamText(ref feePctText, value, nameof(ClosingPolicySelection.FeePct));
    }

    public string TargetCostClpText
    {
        get => targetCostClpText;
        set => CommitParamText(ref targetCostClpText, value, nameof(ClosingPolicySelection.TargetCostClp));
    }

    public string GainsharePctText
    {
        get => gainsharePctText;
        set => CommitParamText(ref gainsharePctText, value, nameof(ClosingPolicySelection.GainsharePct));
    }

    public string GainshareCapClpText
    {
        get => gainshareCapClpText;
        set => CommitParamText(ref gainshareCapClpText, value, nameof(ClosingPolicySelection.GainshareCapClp));
    }

    private static readonly HashSet<string> PctParams = new(StringComparer.Ordinal)
    {
        nameof(ClosingPolicySelection.LumpSumMinRiskPct),
        nameof(ClosingPolicySelection.FeePct),
        nameof(ClosingPolicySelection.GainsharePct)
    };

    private void CommitParamText(ref string field, string? value, string paramName)
    {
        var normalized = value ?? "";
        if (field == normalized)
            return;
        field = normalized;
        OnPropertyChanged(paramName + "Text");
        if (!TryParseDecimal(value, out var v))
            return;
        // Los % se clampean a [0,100] igual que en CoefFactorVm/NegotiatedDiscountVm/StaffingRoleVm;
        // los montos CLP nunca deberían ser negativos (fee, precio objetivo, tope de reparto, etc.).
        v = PctParams.Contains(paramName) ? ClampPct(v) : Math.Max(0m, v);
        onParamCommit(paramName, v);
    }

    private void UpdateVisibility(ClosingPolicyType policy)
    {
        ShowTargetPrice = policy == ClosingPolicyType.TargetPrice;
        ShowLumpSum = policy == ClosingPolicyType.LumpSumEpc;
        ShowCostPlusFee = policy == ClosingPolicyType.CostPlusFee;
        ShowGainshare = policy == ClosingPolicyType.GainsharePainshare;
    }

    /// <summary>Repuebla los campos desde la selección persistida sin disparar commits.</summary>
    public void SetSilent(ClosingPolicySelection selection)
    {
        selectedOption = Options.FirstOrDefault(o => o.Value == selection.Policy) ?? Options[0];
        OnPropertyChanged(nameof(SelectedOption));
        UpdateVisibility(selectedOption.Value);

        // Formato "F0"/"F1" (sin separador de miles) a propósito: TryParseDecimal solo entiende
        // "," o "." como separador decimal, no separadores de agrupación — con "N0" el valor
        // reformateado ("150,000,000") deja de parsear y la siguiente edición se pierde en silencio.
        targetPriceClpText = selection.TargetPriceClp.ToString("F0", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(TargetPriceClpText));
        lumpSumMinRiskPctText = selection.LumpSumMinRiskPct.ToString("F1", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(LumpSumMinRiskPctText));
        feeClpText = selection.FeeClp.ToString("F0", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(FeeClpText));
        feePctText = selection.FeePct.ToString("F1", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(FeePctText));
        targetCostClpText = selection.TargetCostClp.ToString("F0", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(TargetCostClpText));
        gainsharePctText = selection.GainsharePct.ToString("F1", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(GainsharePctText));
        gainshareCapClpText = selection.GainshareCapClp.ToString("F0", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(GainshareCapClpText));
    }

    public void SetResult(ClosingPolicyResult result, string ventaFinalSubLabel)
    {
        VentaFinalLabel = $"{result.VentaFinalClp:N2} UF";
        VentaFinalSubLabel = ventaFinalSubLabel;
        MargenLabel = $"{result.MargenClp:N2} UF · {result.MargenPct:N1}%";
        Explicacion = result.Explicacion;
        Advertencias.Clear();
        foreach (var w in result.Advertencias)
            Advertencias.Add(w);
        HasAdvertencias = Advertencias.Count > 0;
    }
}
