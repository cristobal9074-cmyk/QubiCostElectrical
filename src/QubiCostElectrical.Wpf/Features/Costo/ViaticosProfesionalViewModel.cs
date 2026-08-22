using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class ViaticosLineaVm
{
    public string Concepto { get; init; } = "";
    public string Grupo { get; init; } = "dir";
    public string GrupoLabel => Grupo == "dir" ? "Directa" : "Indirecta";
    public string Unidad { get; init; } = "";
    public decimal Cantidad { get; init; }
    public decimal PrecioUnitarioClp { get; init; }
    public decimal TotalClp { get; init; }
    public bool Activo { get; init; }
    public string CantidadLabel => Cantidad > 0 ? $"{Math.Round(Cantidad):N0}" : "—";
    public string PrecioLabel => CurrencyFormat.Clp(Math.Round(PrecioUnitarioClp));
    public string TotalLabel => CurrencyFormat.Clp(Math.Round(TotalClp));
    public string EstadoLabel => Activo ? "Activo" : "Omitido";
}

/// <summary>Calculadora profesional de viáticos → GO 3.3 (pasajes, alimentación, arriendo).</summary>
public sealed partial class ViaticosProfesionalViewModel : ViewModelBase
{
    private string selectedTab = "pasajes";
    private bool useDir = true;
    private bool useInd = true;
    private string zona = "regional";
    private decimal dotDir;
    private decimal dotInd;
    private decimal meses = 12;
    private decimal diasMes = 20;
    private decimal factorPct = 70;
    private decimal valorUf;
    private decimal reglaDiaPct = 100;

    private bool inclPasDir = true;
    private bool inclPasInd = true;
    private decimal tarifaPasDir = 20000;
    private decimal tarifaPasInd = 80000;

    private bool empresaProveAlim;
    private decimal vDesayuno = 3500;
    private decimal vAlmuerzo = 7500;
    private decimal vCena = 6500;
    private bool mealDes = true;
    private bool mealAlm = true;
    private bool mealCen = true;
    private bool alimDir = true;
    private bool alimInd = true;
    private decimal diasVisitaInd = 8;

    private bool inclArriendo;
    private string modalidadArriendo = "mes";
    private decimal tarifaArr = 450000;
    private decimal cuposArr;
    private decimal mesesArr = 12;
    private bool inclOtros = true;
    private decimal pctOtros = 5;

    private string eqPasajes = "—";
    private string eqAlim = "—";
    private string eqArr = "—";
    private string subPasajesLabel = "$ 0";
    private string subAlimLabel = "$ 0";
    private string subArrLabel = "$ 0";
    private string kpiPasajes = "$ 0";
    private string kpiAlim = "$ 0";
    private string kpiArriendo = "$ 0";
    private string kpiOtros = "$ 0";
    private string kpiTotal = "$ 0";
    private string footTotalLabel = "$ 0";
    private string footUfLabel = "0,00 UF";
    private string badgeAlim = "Viático de comida";
    private string badgeArr = "Según zona";
    private string hintArriendo = "CLP / mes-cupo";
    private bool badgeAlimIsOff;
    private bool badgeArrIsOn;
}
