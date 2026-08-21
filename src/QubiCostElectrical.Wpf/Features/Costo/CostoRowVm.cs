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

public sealed class CostoRowVm
{
    public string Concepto { get; init; } = "";
    public decimal ValorUf { get; init; }
    public decimal ValorMoneda { get; init; }
    public string Explicacion { get; init; } = "";
    /// <summary>regular | total | subtotal | grand | coef</summary>
    public string Kind { get; init; } = "regular";

    public string ValorUfLabel => $"{ValorUf:N2} UF";
    public string ValorMonedaLabel { get; init; } = "";
    public bool IsEmphasis => Kind is "total" or "subtotal" or "grand";
    public bool IsGrand => Kind == "grand";
}
