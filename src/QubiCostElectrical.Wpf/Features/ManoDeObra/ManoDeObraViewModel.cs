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
}

public sealed class MoCargoEditorVm : ViewModelBase
{
    private readonly Action onSalaryChanged;
    private decimal sueldoBase;
    private string sueldoEditText = "0";
    private string conLeyesLabel = "";

    public MoCargoEditorVm(MoCargoDef def, string hint, Action onSalaryChanged)
    {
        Id = def.Id;
        Tag = def.Tag;
        Nombre = def.Nombre;
        Hint = hint;
        this.onSalaryChanged = onSalaryChanged;
        sueldoBase = def.SueldoBase;
        sueldoEditText = FormatEdit(def.SueldoBase);
    }

    public string Id { get; }
    public string Tag { get; }
    public string Nombre { get; }
    public string Hint { get; }

    public decimal SueldoBase => sueldoBase;

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
            onSalaryChanged();
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
        OnPropertyChanged(nameof(SueldoEditText));
        OnPropertyChanged(nameof(SueldoBase));
        onSalaryChanged();
    }

    public void UpdateConLeyes(decimal leyesPct)
        => ConLeyesLabel = "$ " + Math.Round(sueldoBase * (1 + leyesPct)).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));

    private static string FormatEdit(decimal n)
        => Math.Round(n).ToString("N0", CultureInfo.GetCultureInfo("es-CL"));

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
