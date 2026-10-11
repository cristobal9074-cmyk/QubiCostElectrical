using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public sealed class MoCargoDef
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Tag { get; set; } = "";
    public bool EsDirecta { get; set; }
    public int DotPico { get; set; }
    public decimal SueldoBase { get; set; }
    /// <summary>Solo indirecta: fracción del sueldo cobrada en esta obra (0..1; 1 = 100%).</summary>
    public decimal FraccionObra { get; set; } = 1m;
}

public sealed class MoCargoEditorVm : ViewModelBase
{
    private readonly Action onEditorChanged;
    private decimal sueldoBase;
    private decimal fraccionObra;
    private decimal leyesPct;
    private string sueldoEditText = "0";
    private string fraccionEditText = "";
    private string conLeyesLabel = "";

    public MoCargoEditorVm(MoCargoDef def, string hint, Action onEditorChanged)
    {
        Id = def.Id;
        Tag = def.Tag;
        Nombre = def.Nombre;
        Hint = hint;
        this.onEditorChanged = onEditorChanged;
        sueldoBase = def.SueldoBase;
        sueldoEditText = FormatEdit(def.SueldoBase);
        fraccionObra = ClampFraccion(def.FraccionObra);
        fraccionEditText = FormatFraccion(fraccionObra);
    }

    public string Id { get; }
    public string Tag { get; }
    public string Nombre { get; }
    public string Hint { get; }

    public decimal SueldoBase => sueldoBase;

    /// <summary>Fracción efectiva 0..1 (directa siempre 1).</summary>
    public decimal FraccionObra => fraccionObra;

    public string SueldoEditText
    {
        get => sueldoEditText;
        set
        {
            if (!SetProperty(ref sueldoEditText, value ?? ""))
                return;
            var parsed = ParseMoney(value);
            if (parsed == sueldoBase)
                return;
            sueldoBase = parsed;
            OnPropertyChanged(nameof(SueldoBase));
            onEditorChanged();
        }
    }

    public string FraccionEditText
    {
        get => fraccionEditText;
        set
        {
            if (!SetProperty(ref fraccionEditText, value ?? ""))
                return;
            var parsed = ParseFraccion(value, fraccionObra);
            if (parsed == fraccionObra)
                return;
            fraccionObra = parsed;
            OnPropertyChanged(nameof(FraccionObra));
            UpdateConLeyes(leyesPct);
            onEditorChanged();
        }
    }

    public string ConLeyesLabel
    {
        get => conLeyesLabel;
        private set => SetProperty(ref conLeyesLabel, value);
    }

    public void CommitEdit()
    {
        sueldoBase = ParseMoney(sueldoEditText);
        sueldoEditText = FormatEdit(sueldoBase);
        fraccionObra = ParseFraccion(fraccionEditText, fraccionObra);
        fraccionEditText = FormatFraccion(fraccionObra);
        OnPropertyChanged(nameof(SueldoEditText));
        OnPropertyChanged(nameof(SueldoBase));
        OnPropertyChanged(nameof(FraccionEditText));
        OnPropertyChanged(nameof(FraccionObra));
        onEditorChanged();
    }

    public void UpdateConLeyes(decimal pct)
    {
        leyesPct = pct;
        var cobrado = sueldoBase * fraccionObra;
        ConLeyesLabel = "$ " + Math.Round(cobrado * (1 + pct)).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));
    }

    private static string FormatEdit(decimal n)
        => Math.Round(n).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));

    private static string FormatFraccion(decimal n)
        => n.ToString("N2", CultureInfo.GetCultureInfo("es-CL"));

    private static decimal ClampFraccion(decimal n)
        => Math.Clamp(n, 0m, 1m);

    /// <summary>Acepta coma o punto decimal (0,3 / 0.3) y recorta a 0..1. Vacío = conserva el valor actual.</summary>
    private static decimal ParseFraccion(string? raw, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        var text = raw.Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
            ? ClampFraccion(n)
            : fallback;
    }

    private static decimal ParseMoney(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0m;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? Math.Max(0m, n)
            : 0m;
    }
}

public sealed class MoMesInfo
{
    public int Numero { get; init; }
    public string Fase { get; init; } = "";
    public bool EsPeak => Fase == "peak";
    public int DotDir { get; init; }
    public int DotInd { get; init; }
    public int DotTotal => DotDir + DotInd;
    public string CostoStr { get; init; } = "";
    public decimal CostoTotal { get; init; }
}

public sealed class MoChartBarVm
{
    public string Label { get; init; } = "";
    public double DirPct { get; init; }
    public double IndPct { get; init; }
    public string Tip { get; init; } = "";
}

public sealed class MoTableRow
{
    public bool IsGroupHeader { get; init; }
    public bool IsSubtotal { get; init; }
    public bool IsGrandTotal { get; init; }
    public string GroupLabel { get; init; } = "";
    public string GroupBadge { get; init; } = "";
    public string GroupBadgeType { get; init; } = "dir";
    public string Tag { get; init; } = "";
    public string Nombre { get; init; } = "";
    public bool EsDirecta { get; init; }
    public int DotPico { get; init; }
    public string SueldoBaseStr { get; init; } = "";
    public string FraccionStr { get; init; } = "";
    public string LeyesStr { get; init; } = "";
    public string TotalMensualStr { get; init; } = "";
    public int[] Dotaciones { get; init; } = Array.Empty<int>();
    public string TotalStr { get; init; } = "";
}

public sealed class MoCostRow
{
    public bool IsSubtotal { get; init; }
    public bool IsGrandTotal { get; init; }
    public string Label { get; init; } = "";
    public string Tag { get; init; } = "";
    public string BaseStr { get; init; } = "";
    public string LeyesStr { get; init; } = "";
    public string TotalStr { get; init; } = "";
    public string[] CostosStr { get; init; } = Array.Empty<string>();
    public string TotalFinalStr { get; init; } = "";
}

public sealed class MoProjectionTaskVm
{
    public string Id { get; init; } = "";
    public string Nombre { get; set; } = "";
    public int[] Cantidades { get; set; } = Array.Empty<int>();
}

public sealed partial class ManoDeObraViewModel : ViewModelBase, IRefreshablePage
{
}
