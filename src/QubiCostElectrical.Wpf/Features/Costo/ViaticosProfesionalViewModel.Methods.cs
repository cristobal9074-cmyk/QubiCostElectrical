using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed partial class ViaticosProfesionalViewModel : ViewModelBase
{
    public ViaticosProfesionalViewModel(decimal valorUfClp, decimal picoDirecta, decimal rolesIndirectos, int mesesObra, bool ocultarEquivalenciaUf = false)
    {
        muestraEquivalenciaUf = !ocultarEquivalenciaUf;
        valorUf = valorUfClp > 0 ? valorUfClp : PriceConversion.DefaultValorUfClp;
        dotDir = Math.Max(0, picoDirecta);
        dotInd = Math.Max(0, rolesIndirectos);
        meses = mesesObra > 0 ? mesesObra : 12;
        mesesArr = meses;
        SyncFromMoCommand = new RelayCommand(SyncFromMoInternal);
        LoadDemoCommand = new RelayCommand(LoadDemo);
        SelectTabCommand = new RelayCommand<string>(SelectTab);
        ToggleChipDirCommand = new RelayCommand(() => { UseDir = !UseDir; });
        ToggleChipIndCommand = new RelayCommand(() => { UseInd = !UseInd; });
        ToggleMealDesCommand = new RelayCommand(() => { MealDes = !MealDes; });
        ToggleMealAlmCommand = new RelayCommand(() => { MealAlm = !MealAlm; });
        ToggleMealCenCommand = new RelayCommand(() => { MealCen = !MealCen; });
        Recalc();
    }

    public ObservableCollection<ViaticosLineaVm> Lineas { get; } = new();
    public IReadOnlyList<GastoOperacionalItemRow> ResultItems { get; private set; } = Array.Empty<GastoOperacionalItemRow>();

    public ICommand SyncFromMoCommand { get; }
    public ICommand LoadDemoCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand ToggleChipDirCommand { get; }
    public ICommand ToggleChipIndCommand { get; }
    public ICommand ToggleMealDesCommand { get; }
    public ICommand ToggleMealAlmCommand { get; }
    public ICommand ToggleMealCenCommand { get; }

    public Action? SyncFromMoRequested { get; set; }

    public bool IsTabPasajes => selectedTab == "pasajes";
    public bool IsTabAlim => selectedTab == "alim";
    public bool IsTabArriendo => selectedTab == "arriendo";
    public bool IsTabResumen => selectedTab == "resumen";

    public bool UseDir
    {
        get => useDir;
        set { if (SetProperty(ref useDir, value)) Recalc(); }
    }

    public bool UseInd
    {
        get => useInd;
        set { if (SetProperty(ref useInd, value)) Recalc(); }
    }

    public string Zona
    {
        get => zona;
        set
        {
            if (!SetProperty(ref zona, value ?? "regional"))
                return;
            if (zona == "remoto") InclArriendo = true;
            if (zona == "local") InclArriendo = false;
            Recalc();
        }
    }

    public decimal DotDir { get => dotDir; set { if (SetProperty(ref dotDir, Math.Max(0, value))) Recalc(); } }
    public decimal DotInd { get => dotInd; set { if (SetProperty(ref dotInd, Math.Max(0, value))) Recalc(); } }
    public decimal Meses { get => meses; set { if (SetProperty(ref meses, Math.Max(0, value))) Recalc(); } }
    public decimal DiasMes { get => diasMes; set { if (SetProperty(ref diasMes, Math.Max(0, value))) Recalc(); } }
    public decimal FactorPct { get => factorPct; set { if (SetProperty(ref factorPct, Math.Max(0, value))) Recalc(); } }
    public decimal ValorUf { get => valorUf; set { if (SetProperty(ref valorUf, Math.Max(1, value))) Recalc(); } }
    public decimal ReglaDiaPct
    {
        get => reglaDiaPct;
        set
        {
            if (SetProperty(ref reglaDiaPct, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(ReglaDiaKey));
                Recalc();
            }
        }
    }

    /// <summary>Clave de combo (100 | 75) para binding WPF con Tag string.</summary>
    public string ReglaDiaKey
    {
        get => ((int)reglaDiaPct).ToString();
        set
        {
            if (!decimal.TryParse(value, out var v))
                return;
            ReglaDiaPct = v;
        }
    }

    public bool InclPasDir { get => inclPasDir; set { if (SetProperty(ref inclPasDir, value)) Recalc(); } }
    public bool InclPasInd { get => inclPasInd; set { if (SetProperty(ref inclPasInd, value)) Recalc(); } }
    public decimal TarifaPasDir { get => tarifaPasDir; set { if (SetProperty(ref tarifaPasDir, Math.Max(0, value))) Recalc(); } }
    public decimal TarifaPasInd { get => tarifaPasInd; set { if (SetProperty(ref tarifaPasInd, Math.Max(0, value))) Recalc(); } }

    public bool EmpresaProveAlim { get => empresaProveAlim; set { if (SetProperty(ref empresaProveAlim, value)) Recalc(); } }
    public decimal VDesayuno { get => vDesayuno; set { if (SetProperty(ref vDesayuno, Math.Max(0, value))) Recalc(); } }
    public decimal VAlmuerzo { get => vAlmuerzo; set { if (SetProperty(ref vAlmuerzo, Math.Max(0, value))) Recalc(); } }
    public decimal VCena { get => vCena; set { if (SetProperty(ref vCena, Math.Max(0, value))) Recalc(); } }
    public bool MealDes { get => mealDes; set { if (SetProperty(ref mealDes, value)) { OnPropertyChanged(nameof(MealDesLabel)); Recalc(); } } }
    public bool MealAlm { get => mealAlm; set { if (SetProperty(ref mealAlm, value)) { OnPropertyChanged(nameof(MealAlmLabel)); Recalc(); } } }
    public bool MealCen { get => mealCen; set { if (SetProperty(ref mealCen, value)) { OnPropertyChanged(nameof(MealCenLabel)); Recalc(); } } }
    public bool AlimDir { get => alimDir; set { if (SetProperty(ref alimDir, value)) Recalc(); } }
    public bool AlimInd { get => alimInd; set { if (SetProperty(ref alimInd, value)) Recalc(); } }
    public decimal DiasVisitaInd { get => diasVisitaInd; set { if (SetProperty(ref diasVisitaInd, Math.Max(0, value))) Recalc(); } }

    public string MealDesLabel => Fmt(VDesayuno);
    public string MealAlmLabel => Fmt(VAlmuerzo);
    public string MealCenLabel => Fmt(VCena);

    public bool InclArriendo
    {
        get => inclArriendo;
        set { if (SetProperty(ref inclArriendo, value)) Recalc(); }
    }

    public string ModalidadArriendo
    {
        get => modalidadArriendo;
        set
        {
            if (!SetProperty(ref modalidadArriendo, value ?? "mes"))
                return;
            HintArriendo = modalidadArriendo switch
            {
                "noche" => "CLP / noche-persona",
                "campamento" => "CLP / mes global",
                _ => "CLP / mes-cupo",
            };
            Recalc();
        }
    }

    public decimal TarifaArr { get => tarifaArr; set { if (SetProperty(ref tarifaArr, Math.Max(0, value))) Recalc(); } }
    public decimal CuposArr { get => cuposArr; set { if (SetProperty(ref cuposArr, Math.Max(0, value))) Recalc(); } }
    public decimal MesesArr { get => mesesArr; set { if (SetProperty(ref mesesArr, Math.Max(0, value))) Recalc(); } }
    public bool InclOtros { get => inclOtros; set { if (SetProperty(ref inclOtros, value)) Recalc(); } }
    public decimal PctOtros { get => pctOtros; set { if (SetProperty(ref pctOtros, Math.Max(0, value))) Recalc(); } }

    public string EqPasajes { get => eqPasajes; private set => SetProperty(ref eqPasajes, value); }
    public string EqAlim { get => eqAlim; private set => SetProperty(ref eqAlim, value); }
    public string EqArr { get => eqArr; private set => SetProperty(ref eqArr, value); }
    public string SubPasajesLabel { get => subPasajesLabel; private set => SetProperty(ref subPasajesLabel, value); }
    public string SubAlimLabel { get => subAlimLabel; private set => SetProperty(ref subAlimLabel, value); }
    public string SubArrLabel { get => subArrLabel; private set => SetProperty(ref subArrLabel, value); }
    public string KpiPasajes { get => kpiPasajes; private set => SetProperty(ref kpiPasajes, value); }
    public string KpiAlim { get => kpiAlim; private set => SetProperty(ref kpiAlim, value); }
    public string KpiArriendo { get => kpiArriendo; private set => SetProperty(ref kpiArriendo, value); }
    public string KpiOtros { get => kpiOtros; private set => SetProperty(ref kpiOtros, value); }
    public string KpiTotal { get => kpiTotal; private set => SetProperty(ref kpiTotal, value); }
    public string FootTotalLabel { get => footTotalLabel; private set => SetProperty(ref footTotalLabel, value); }
    public string FootUfLabel { get => footUfLabel; private set => SetProperty(ref footUfLabel, value); }

    /// <summary>Falso cuando el proyecto está en CLP: no se expone la equivalencia en UF.</summary>
    public bool MuestraEquivalenciaUf => muestraEquivalenciaUf;
    public string BadgeAlim { get => badgeAlim; private set => SetProperty(ref badgeAlim, value); }
    public string BadgeArr { get => badgeArr; private set => SetProperty(ref badgeArr, value); }
    public string HintArriendo { get => hintArriendo; private set => SetProperty(ref hintArriendo, value); }
    public bool BadgeAlimIsOff { get => badgeAlimIsOff; private set => SetProperty(ref badgeAlimIsOff, value); }
    public bool BadgeArrIsOn { get => badgeArrIsOn; private set => SetProperty(ref badgeArrIsOn, value); }

    public void ApplyMoSnapshot(decimal picoDirecta, decimal rolesIndirectos, int mesesObra, decimal valorUfClp)
    {
        suppressRecalc = true;
        DotDir = Math.Max(0, picoDirecta);
        DotInd = Math.Max(0, rolesIndirectos);
        if (mesesObra > 0)
        {
            Meses = mesesObra;
            MesesArr = mesesObra;
        }
        if (valorUfClp > 0)
            ValorUf = valorUfClp;
        suppressRecalc = false;
        Recalc();
    }

    private bool suppressRecalc;

    private void SelectTab(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab) || selectedTab == tab)
            return;
        selectedTab = tab;
        OnPropertyChanged(nameof(IsTabPasajes));
        OnPropertyChanged(nameof(IsTabAlim));
        OnPropertyChanged(nameof(IsTabArriendo));
        OnPropertyChanged(nameof(IsTabResumen));
    }

    private void SyncFromMoInternal() => SyncFromMoRequested?.Invoke();

    private void LoadDemo()
    {
        suppressRecalc = true;
        DotDir = 77;
        DotInd = 6;
        Meses = 12;
        DiasMes = 20;
        FactorPct = 70;
        Zona = "regional";
        InclArriendo = false;
        EmpresaProveAlim = false;
        CuposArr = 8;
        UseDir = true;
        UseInd = true;
        MesesArr = 12;
        suppressRecalc = false;
        Recalc();
    }

    private void Recalc()
    {
        if (suppressRecalc)
            return;

        var factor = FactorPct / 100m;
        var uf = Math.Max(1m, ValorUf);
        var diasObra = Meses * DiasMes;
        var persEf = DotDir * factor;
        var regla = ReglaDiaPct / 100m;
        var lines = new List<ViaticosLineaVm>();

        decimal totPas = 0, totAlim = 0, totArr = 0, totOtros = 0;

        if (InclPasDir && UseDir && persEf > 0)
        {
            var cant = persEf * diasObra * regla;
            var tot = cant * TarifaPasDir;
            totPas += tot;
            lines.Add(Line("Pasajes / movilización diaria MO directa", "dir", "día", cant, TarifaPasDir, tot, true));
        }

        if (InclPasInd && UseInd && DotInd > 0)
        {
            var cant = DotInd * Meses;
            var tot = cant * TarifaPasInd;
            totPas += tot;
            lines.Add(Line("Movilización terreno MO indirecta", "ind", "mes", cant, TarifaPasInd, tot, true));
        }

        var vDes = MealDes ? VDesayuno : 0;
        var vAlm = MealAlm ? VAlmuerzo : 0;
        var vCen = MealCen ? VCena : 0;
        var diariaAlim = EmpresaProveAlim ? 0 : vDes + vAlm + vCen;

        if (diariaAlim > 0 && AlimDir && UseDir && persEf > 0)
        {
            var cant = persEf * diasObra * regla;
            var tot = cant * diariaAlim;
            totAlim += tot;
            lines.Add(Line("Alimentación / colación MO directa", "dir", "día", cant, diariaAlim, tot, true));
        }
        else if (EmpresaProveAlim && AlimDir && UseDir)
        {
            lines.Add(Line("Alimentación MO directa (empresa provee)", "dir", "día", 0, 0, 0, false));
        }

        if (diariaAlim > 0 && AlimInd && UseInd && DotInd > 0)
        {
            var cant = DotInd * DiasVisitaInd * Meses;
            var tot = cant * diariaAlim;
            totAlim += tot;
            lines.Add(Line("Alimentación visitas MO indirecta", "ind", "día", cant, diariaAlim, tot, true));
        }

        if (InclArriendo)
        {
            var cupos = CuposArr > 0 ? CuposArr : (UseDir ? Math.Ceiling(persEf * 0.15m) : 0);
            var mArr = MesesArr > 0 ? MesesArr : Meses;
            decimal cant;
            string un, concepto;
            switch (ModalidadArriendo)
            {
                case "noche":
                    cant = cupos * mArr * DiasMes;
                    un = "noche";
                    concepto = "Arriendo noche-persona";
                    break;
                case "campamento":
                    cant = mArr;
                    un = "mes";
                    concepto = "Aporte campamento / módulo";
                    break;
                default:
                    cant = cupos * mArr;
                    un = "mes";
                    concepto = "Arriendo mensual (cupos)";
                    break;
            }

            var tot = cant * TarifaArr;
            totArr += tot;
            lines.Add(Line(concepto, "dir", un, cant, TarifaArr, tot, tot > 0));
        }

        var baseInc = totAlim + totArr;
        if (InclOtros && baseInc > 0)
        {
            totOtros = baseInc * (PctOtros / 100m);
            lines.Add(Line("Incidentales / imprevistos menores", "ind", "GL", 1, totOtros, totOtros, true));
        }

        var total = totPas + totAlim + totArr + totOtros;

        EqPasajes =
            $"Directa: {persEf:N1} pers × {diasObra:N0} días × {Fmt(TarifaPasDir)} · Indirecta: {DotInd:N0} roles × {Meses:N0} m × {Fmt(TarifaPasInd)}";
        SubPasajesLabel = Fmt(totPas);

        var mealParts = new List<string>();
        if (MealDes) mealParts.Add("des");
        if (MealAlm) mealParts.Add("alm");
        if (MealCen) mealParts.Add("cen");
        EqAlim = EmpresaProveAlim
            ? "Empresa provee alimentación → viático comida = $ 0 (evita doble pago)"
            : $"Diaria {Fmt(diariaAlim)} ({(mealParts.Count > 0 ? string.Join("+", mealParts) : "ninguna")}) × días-persona efectivos";
        SubAlimLabel = Fmt(totAlim);
        BadgeAlim = EmpresaProveAlim ? "Empresa provee (descuento)" : "Viático de comida";
        BadgeAlimIsOff = EmpresaProveAlim;

        EqArr = InclArriendo
            ? $"{ModalidadArriendoLabel()} · tarifa {Fmt(TarifaArr)}"
            : "Arriendo desactivado (típico en faena local)";
        SubArrLabel = Fmt(totArr + totOtros);
        BadgeArr = InclArriendo ? "Incluido" : "No aplica";
        BadgeArrIsOn = InclArriendo;

        KpiPasajes = Fmt(totPas);
        KpiAlim = Fmt(totAlim);
        KpiArriendo = Fmt(totArr);
        KpiOtros = Fmt(totOtros);
        KpiTotal = Fmt(total);
        FootTotalLabel = Fmt(total);
        FootUfLabel = muestraEquivalenciaUf ? $"{total / uf:N2} UF" : "";

        OnPropertyChanged(nameof(MealDesLabel));
        OnPropertyChanged(nameof(MealAlmLabel));
        OnPropertyChanged(nameof(MealCenLabel));

        Lineas.Clear();
        foreach (var l in lines)
            Lineas.Add(l);

        ResultItems = lines
            .Where(l => l.Activo && l.TotalClp > 0)
            .Select(l => new GastoOperacionalItemRow
            {
                Concepto = l.Concepto,
                Unidad = l.Unidad,
                Cantidad = Math.Round(l.Cantidad, 2),
                PrecioUnitarioClp = Math.Round(l.PrecioUnitarioClp, 0),
            })
            .ToList();
    }

    private string ModalidadArriendoLabel() => ModalidadArriendo switch
    {
        "noche" => "Arriendo por noche-persona",
        "campamento" => "Aporte campamento (CLP/mes global)",
        _ => "Arriendo mensual (casa / módulo)",
    };

    private static ViaticosLineaVm Line(string c, string g, string un, decimal cant, decimal pu, decimal tot, bool ok) =>
        new()
        {
            Concepto = c,
            Grupo = g,
            Unidad = un,
            Cantidad = cant,
            PrecioUnitarioClp = pu,
            TotalClp = tot,
            Activo = ok,
        };

    private static string Fmt(decimal n) => CurrencyFormat.Clp(Math.Round(Math.Max(0, n)));
}
