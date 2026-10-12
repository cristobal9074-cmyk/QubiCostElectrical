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
    public string UfFigura => $"{ValorUf:N2}";
    /// <summary>Valor primario en la moneda del proyecto (primera columna del análisis financiero).</summary>
    public string CurrencyFigure { get; init; } = "";
    /// <summary>La columna de moneda extranjera no existe cuando el proyecto está en CLP.</summary>
    public bool ShowCurrencyColumn { get; init; } = true;
    public string ValorMonedaLabel { get; init; } = "";
    public string ParticipacionLabel { get; init; } = "";
    /// <summary>Participación numérica sobre la venta (para barras proporcionales de la cascada).</summary>
    public double Share { get; init; }
    public bool IsEnInforme { get; init; } = true;
    public bool TieneOrigen { get; init; }
    public string OrigenTexto { get; init; } = "";
    /// <summary>directo | cierre — separa composición de recargos/margen en el rediseño 2.0.</summary>
    public string Seccion { get; init; } = "directo";
    public bool EsCierre => Seccion == "cierre";
    public bool EsDirecto => Seccion != "cierre";
    public bool IsEmphasis => Kind is "total" or "subtotal" or "grand";
    public bool IsGrand => Kind == "grand";
    public bool IsSection => Kind == "section";
    public bool IsQuiet => Kind == "regular" && ValorUf == 0m;
}

public sealed class CostoGrupoVm : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string Titulo { get; init; } = "";
    public string ClpLabel { get; init; } = "";
    public string UfLabel { get; init; } = "";
    public string Explicacion { get; init; } = "";
    public ObservableCollection<CostoRowVm> Lineas { get; } = new();

    private bool isExpanded;
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (SetProperty(ref isExpanded, value))
                OnPropertyChanged(nameof(Chevron));
        }
    }

    public string Chevron => IsExpanded ? "\u25BE" : "\u25B8";
}
