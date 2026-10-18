using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public sealed record DashboardNextStepResult(
    string NextStepTitle,
    string NextStepDetail,
    string SubtitleText,
    string PrimaryCtaLabel,
    ShellRoute PrimaryCtaRoute,
    bool HasTopAlert,
    string TopAlertTitle,
    string TopAlertDetail);

public sealed record DashboardReadinessResult(
    DashboardReadinessContract Contract,
    string PartidasReadyLabel,
    double PartidasReadyPercent,
    int BlockingAlertCount,
    double RingPercent,
    IReadOnlyList<DashboardOfferReadinessItemVm> Items);

public sealed record DashboardPriceValidityResult(
    string Label,
    string Caption,
    double Percent);

public sealed record DashboardVersionComparisonResult(
    bool HasComparison,
    string Label,
    string Caption,
    double DeltaPercent);

public sealed record DashboardCostDistributionResult(
    GridLength MaterialShareWidth,
    GridLength LaborShareWidth,
    GridLength GoShareWidth,
    GridLength MarginShareWidth,
    IReadOnlyList<DashboardCostShareVm> Items);

/// <summary>
/// Coordina la preparación de la oferta, derivación de alertas, flujo de producción,
/// veredictos, validez de precios, comparación entre versiones y cálculo de KPIs ejecutivos.
/// </summary>
public sealed class DashboardReadinessCoordinator
{
    private readonly BudgetProjectSession session;
    private readonly IShellNavigationHost shell;
    private readonly ICurrencyConversionService currency;

    public DashboardReadinessCoordinator(BudgetProjectSession session, IShellNavigationHost shell, ICurrencyConversionService currency)
    {
        this.session = session;
        this.shell = shell;
        this.currency = currency;
    }

    public List<DashboardWorkflowStepVm> BuildWorkflow(FinalReviewSummary review, bool showEmptyState)
    {
        var project = session.ProjectInfo;
        int totalPartidas = session.Itemizado.Count(x => !x.EsTitulo);
        bool hasName = !string.IsNullOrWhiteSpace(project.Nombre) && project.Nombre != "Proyecto sin nombre";
        bool s1 = hasName;
        bool s2 = totalPartidas > 0;
        bool s3 = review.EmptyPartidas == 0 && totalPartidas > 0;
        bool s4 = review.MaterialsWithoutPrice == 0 && totalPartidas > 0;
        bool s5 = review.BlockingCount == 0 && review.CriticalCount == 0;
        bool s6 = review.ReadyToExport;

        var steps = new[]
        {
            ("Proyecto",  project.Nombre,                                                     s1, !s1,       ShellRoute.Project),
            ("Itemizado", $"{totalPartidas:N0} partidas",                                     s2, s1 && !s2, ShellRoute.Itemized),
            ("Cubicación",$"{Math.Max(0, totalPartidas - review.EmptyPartidas):N0} cubiertas",s3, s2 && !s3, ShellRoute.Takeoff),
            ("Precios",   review.MaterialsWithoutPrice > 0 ? $"{review.MaterialsWithoutPrice:N0} sin precio" : "Precios vigentes", s4, s3 && !s4, ShellRoute.Prices),
            ("Revisión",  DashboardCockpitComposer.BlockingLabel(review.BlockingCount),        s5, s4 && !s5, ShellRoute.Review),
            ("Exportar",  review.ReadyToExport ? "Listo" : "Bloqueado",                      s6, s5 && !s6, ShellRoute.OfferDocuments),
        };

        var list = new List<DashboardWorkflowStepVm>(steps.Length);
        for (int i = 0; i < steps.Length; i++)
        {
            var (title, detail, complete, current, route) = steps[i];
            var r = route;
            list.Add(new DashboardWorkflowStepVm
            {
                Index = i + 1,
                Title = title,
                Detail = detail,
                IsComplete = complete,
                IsCurrent = current,
                IsLast = (i == steps.Length - 1),
                NavigateCommand = new RelayCommand(() => shell.RequestNavigate(r), () => !showEmptyState)
            });
        }
        return list;
    }

    public List<DashboardAlertVm> BuildAlerts(FinalReviewSummary review)
    {
        var list = new List<DashboardAlertVm>();
        foreach (var issue in review.Issues.OrderByDescending(x => x.Severity).Take(12))
        {
            var category = issue.Category;
            list.Add(new DashboardAlertVm
            {
                Title = issue.Problem,
                Detail = issue.Recommendation,
                SeverityLabel = issue.Severity switch
                {
                    FinalReviewSeverity.Blocker => "Bloqueante",
                    FinalReviewSeverity.Critical => "Crítico",
                    FinalReviewSeverity.Warning => "Mayor",
                    _ => "Info"
                },
                Category = category,
                NavigateCommand = new RelayCommand(() =>
                {
                    var target = ShellRouteMapper.FromReviewTarget(FinalReviewRouteResolver.Resolve(category));
                    shell.RequestNavigate(target);
                })
            });
        }
        return list;
    }

    public DashboardNextStepResult DetermineNextStep(FinalReviewSummary review, IReadOnlyList<DashboardWorkflowStepVm> workflowSteps, string partidasReadyLabel)
    {
        var current = workflowSteps.FirstOrDefault(s => s.IsCurrent)
                      ?? workflowSteps.LastOrDefault(s => !s.IsComplete)
                      ?? workflowSteps.LastOrDefault();

        string nextStepTitle;
        string nextStepDetail;
        string primaryCtaLabel;
        ShellRoute primaryCtaRoute;
        string subtitleText;

        if (current == null)
        {
            nextStepTitle = "Siguiente paso";
            nextStepDetail = partidasReadyLabel;
            primaryCtaLabel = "Continuar";
            primaryCtaRoute = ShellRoute.Takeoff;
            subtitleText = "Revise el flujo de producción para avanzar.";
        }
        else
        {
            nextStepTitle = current.IsComplete && workflowSteps.All(s => s.IsComplete)
                ? "Proyecto listo para exportar"
                : $"Siguiente paso: {current.Title.ToLowerInvariant()}";
            nextStepDetail = $"{partidasReadyLabel} · {DashboardCockpitComposer.BlockingAlertPhrase(review.BlockingCount)}";
            subtitleText = review.BlockingCount > 0
                ? "Hay decisiones pendientes que afectan la oferta."
                : review.ReadyToExport
                    ? "El proyecto está listo para cerrar y exportar."
                    : "El proyecto avanza; continúe con el paso actual del flujo.";
            primaryCtaLabel = current.Title switch
            {
                "Proyecto" => "Completar proyecto",
                "Itemizado" => "Ir al itemizado",
                "Cubicación" => "Continuar cubicación",
                "APU/Costos" => "Completar APU",
                "Revisión" => "Ir a revisión",
                "Exportar" => "Cerrar oferta",
                _ => $"Continuar {current.Title.ToLowerInvariant()}"
            };
            primaryCtaRoute = current.Title switch
            {
                "Proyecto" => ShellRoute.Project,
                "Itemizado" => ShellRoute.Itemized,
                "Cubicación" => ShellRoute.Takeoff,
                "APU/Costos" => ShellRoute.Apu,
                "Revisión" => ShellRoute.Review,
                "Exportar" => ShellRoute.OfferDocuments,
                _ => ShellRoute.Takeoff
            };
        }

        var top = review.Issues.OrderByDescending(x => x.Severity).FirstOrDefault();
        bool hasTopAlert = top != null;
        string topAlertTitle = top?.Problem ?? "";
        string topAlertDetail = top?.Recommendation ?? "";

        return new DashboardNextStepResult(
            nextStepTitle,
            nextStepDetail,
            subtitleText,
            primaryCtaLabel,
            primaryCtaRoute,
            hasTopAlert,
            topAlertTitle,
            topAlertDetail);
    }

    public DashboardReadinessResult BuildReadiness(FinalReviewSummary review, bool showEmptyState)
    {
        int totalPartidas = session.Itemizado.Count(x => !x.EsTitulo);
        var contract = DashboardReadinessContract.Build(
            totalPartidas,
            review.EmptyPartidas,
            review.MaterialsWithoutPrice,
            review.BlockingCount,
            review.CriticalCount);

        string readyLabel = $"{contract.Listas:N0} de {contract.TotalPartidas:N0} partidas listas";
        var items = new List<DashboardOfferReadinessItemVm>();
        foreach (var check in DashboardCockpitComposer.BuildReadinessChecks(contract))
        {
            var route = check.Label switch
            {
                "Datos del proyecto" => ShellRoute.Project,
                "Itemizado importado" => ShellRoute.Itemized,
                "Cubicación" => ShellRoute.Takeoff,
                _ => ShellRoute.Prices
            };
            items.Add(new DashboardOfferReadinessItemVm
            {
                Label = check.Label,
                Detail = check.Detail,
                IsComplete = check.Ok,
                IsWarning = check.Warn,
                StatusBrush = check.Ok ? DashboardKpiBrushes.Green : DashboardKpiBrushes.AmberRisk,
                NavigateCommand = new RelayCommand(() => shell.RequestNavigate(route), () => !showEmptyState)
            });
        }

        return new DashboardReadinessResult(
            contract,
            readyLabel,
            contract.ListasPercent,
            review.BlockingCount,
            contract.RingPercent,
            items);
    }

    public DashboardPriceValidityResult BuildPriceValidity()
    {
        var priced = session.Materiales.Where(m => m.FechaPrecio != default).ToList();
        if (priced.Count == 0)
        {
            return new DashboardPriceValidityResult("Sin datos", "Asigne fechas en banco de precios", 0);
        }

        int fresh = priced.Count(m => !m.RequiereRevisionPrecio);
        double percent = Math.Min(100, fresh * 100d / priced.Count);
        string label = $"{fresh:N0}/{priced.Count:N0} vigentes";
        string caption = fresh == priced.Count
            ? "Precios dentro del umbral de 6 meses"
            : $"{priced.Count - fresh:N0} requieren revisión";

        return new DashboardPriceValidityResult(label, caption, percent);
    }

    public DashboardVersionComparisonResult BuildVersionComparison()
    {
        var versions = session.Versiones
            .OrderBy(v => v.FechaCreacion)
            .ThenBy(v => v.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (versions.Count < 2)
        {
            return new DashboardVersionComparisonResult(
                false,
                "Sin versiones comparables",
                "Guarde al menos dos versiones del presupuesto",
                0);
        }

        var previous = versions[^2];
        var current = versions[^1];
        string label = $"{previous.Codigo} → {current.Codigo}";

        if (previous.MontoVentaUf <= 0m)
        {
            return new DashboardVersionComparisonResult(
                true,
                label,
                "Versión anterior sin monto registrado",
                0);
        }

        var deltaPct = (current.MontoVentaUf - previous.MontoVentaUf) / previous.MontoVentaUf * 100m;
        double directCostDeltaPercent = Math.Min(100, Math.Abs((double)deltaPct));
        string caption = deltaPct switch
        {
            > 0.05m => $"+{deltaPct:N1}% en monto de venta",
            < -0.05m => $"{deltaPct:N1}% en monto de venta",
            _ => "Sin variación relevante entre versiones"
        };

        return new DashboardVersionComparisonResult(
            true,
            label,
            caption,
            directCostDeltaPercent);
    }

    public DashboardCostDistributionResult BuildCostDistribution(
        decimal totalVenta, decimal materials, decimal labor, decimal go, decimal margin,
        string costMaterials, string costLabor, string costGo, string costMargin)
    {
        var safeTotal = totalVenta <= 0m ? 1m : totalVenta;
        var matPct = Math.Max(0m, materials / safeTotal * 100m);
        var moPct = Math.Max(0m, labor / safeTotal * 100m);
        var goPct = Math.Max(0m, go / safeTotal * 100m);
        var mgPct = Math.Max(0m, margin / safeTotal * 100m);

        var materialWidth = new GridLength((double)Math.Max(matPct, 0.1m), GridUnitType.Star);
        var laborWidth = new GridLength((double)Math.Max(moPct, 0.1m), GridUnitType.Star);
        var goWidth = new GridLength((double)Math.Max(goPct, 0.1m), GridUnitType.Star);
        var marginWidth = new GridLength((double)Math.Max(mgPct, 0.1m), GridUnitType.Star);

        var list = new List<DashboardCostShareVm>(4);
        list.Add(new DashboardCostShareVm
        {
            Label = "Materiales",
            AmountLabel = costMaterials,
            PercentageLabel = $"{matPct:N0}%",
            PercentageValue = (double)matPct,
            Accent = DashboardExecutiveKpiAccent.Blue,
            AccentBrush = DashboardKpiBrushes.Blue,
            AccentSoftBrush = DashboardKpiBrushes.BlueSoft
        });
        list.Add(new DashboardCostShareVm
        {
            Label = "Mano de obra",
            AmountLabel = costLabor,
            PercentageLabel = $"{moPct:N0}%",
            PercentageValue = (double)moPct,
            Accent = DashboardExecutiveKpiAccent.RiskLow,
            AccentBrush = DashboardKpiBrushes.Green,
            AccentSoftBrush = DashboardKpiBrushes.GreenSoft
        });
        list.Add(new DashboardCostShareVm
        {
            Label = "Gastos Op. (Faena)",
            AmountLabel = costGo,
            PercentageLabel = $"{goPct:N0}%",
            PercentageValue = (double)goPct,
            Accent = DashboardExecutiveKpiAccent.Orange,
            AccentBrush = DashboardKpiBrushes.Amber,
            AccentSoftBrush = DashboardKpiBrushes.AmberSoft
        });
        list.Add(new DashboardCostShareVm
        {
            Label = "Margen Bruto",
            AmountLabel = costMargin,
            PercentageLabel = $"{mgPct:N0}%",
            PercentageValue = (double)mgPct,
            Accent = DashboardExecutiveKpiAccent.Primary,
            AccentBrush = DashboardKpiBrushes.BlueDark,
            AccentSoftBrush = DashboardKpiBrushes.BlueSoft
        });

        return new DashboardCostDistributionResult(materialWidth, laborWidth, goWidth, marginWidth, list);
    }

    public static string GetCategoryForPartida(string item, string name)
    {
        string text = (name ?? "").ToLowerInvariant();
        if (text.Contains("conductor") || text.Contains("cable") || text.Contains("tubería") || text.Contains("tuberia") ||
            text.Contains("pvc") || text.Contains("canaliz") || text.Contains("artefacto") || text.Contains("led") ||
            text.Contains("enchufe") || text.Contains("interruptor"))
        {
            return "Materiales";
        }
        else if (text.Contains("montaje") || text.Contains("conexionado") || text.Contains("instalac") || text.Contains("mano de obra"))
        {
            return "Mano de Obra";
        }
        else
        {
            return "Otros";
        }
    }

    public List<DashboardKpiCardVm> BuildExecutiveKpis(
        DashboardPageState state,
        decimal valorUf,
        decimal totalVentaClp,
        bool hasVersionComparison,
        double versionDeltaPercent,
        int readinessPendientes,
        double partidasReadyPercent,
        string margenRealPctLabel,
        string costMargin,
        DashboardParetoSummary concentracion)
    {
        var project = state.Project;
        bool isClpPref = string.Equals(project.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);

        string FormatClp(decimal clp) => $"$ {clp:N0}";
        string FormatMoney(decimal clp) =>
            currency.FormatMoneyFromClp(clp, project.MonedaPresupuesto, valorUf, project.TipoCambioUsdClp);

        var cards = new List<DashboardKpiCardVm>();

        foreach (var kpi in state.Executive.Kpis)
        {
            string val, cap;
            if (kpi.Title == "TOTAL VENTA")
            {
                val = FormatMoney(totalVentaClp);
                cap = isClpPref ? "Valor oficial" : $"{FormatClp(totalVentaClp)} · Valor oficial";
            }
            else if (kpi.Title.EndsWith("/ m²", StringComparison.OrdinalIgnoreCase) || kpi.Title.Contains("m²"))
            {
                if (project.MetrosCuadrados > 0)
                {
                    decimal clpM2 = totalVentaClp / project.MetrosCuadrados;
                    decimal ufM2 = PriceConversion.ClpToUf(clpM2, valorUf);
                    val = isClpPref ? $"$ {clpM2:N0} / m²" : $"{ufM2:N3} UF / m²";
                    cap = isClpPref ? "Total venta / m²" : $"$ {clpM2:N0} CLP/m² · Superficie";
                }
                else
                {
                    val = "Configurar m²";
                    cap = "Costo comercial por superficie";
                }
            }
            else
            {
                val = kpi.ValueKind == DashboardExecutiveKpiValueKind.Money ? FormatMoney(kpi.MoneyValueUf ?? 0m) : (kpi.TextValue ?? "");
                cap = kpi.Caption;
            }

            int pendientes = kpi.Title == "PARTIDAS LISTAS" ? readinessPendientes : 0;
            string chip = kpi.Title switch
            {
                "TOTAL VENTA" => hasVersionComparison
                    ? $"{versionDeltaPercent:+0.0;-0.0;0.0}% vs v. anterior"
                    : "Valor oficial",
                "PARTIDAS LISTAS" => pendientes > 0 ? $"{pendientes} pendientes" : "Todo listo",
                _ when kpi.Title.Contains("m²") => project.MetrosCuadrados > 0 ? $"{project.MetrosCuadrados:N0} m² útiles" : "Configurar m²",
                _ => kpi.Accent == DashboardExecutiveKpiAccent.RiskLow ? "Riesgo bajo" : (kpi.TextValue ?? "")
            };
            string chipKind = kpi.Accent switch
            {
                DashboardExecutiveKpiAccent.RiskHigh => "danger",
                DashboardExecutiveKpiAccent.RiskMedium or DashboardExecutiveKpiAccent.Orange => "warn",
                DashboardExecutiveKpiAccent.RiskLow => "up",
                _ => "mute"
            };
            if (kpi.Title == "TOTAL VENTA" && hasVersionComparison)
                chipKind = versionDeltaPercent >= 0 ? "up" : "danger";
            double gauge = kpi.Title == "PARTIDAS LISTAS" ? partidasReadyPercent : 0;
            string iconKind = kpi.Title == "TOTAL VENTA" ? "total"
                : kpi.Title == "PARTIDAS LISTAS" ? "listas"
                : kpi.Title.Contains("m²") ? "m2"
                : "riesgo";

            cards.Add(new DashboardKpiCardVm
            {
                Title = kpi.Title,
                Value = val,
                Caption = cap,
                IsPrimary = kpi.IsPrimary,
                Accent = kpi.Accent,
                AccentBrush = DashboardKpiBrushes.ForAccent(kpi.Accent),
                AccentSoftBrush = DashboardKpiBrushes.ForAccentSoft(kpi.Accent),
                ChipLabel = chip,
                ChipKind = chipKind,
                IconKind = iconKind,
                ShowUfUpdateButton = kpi.Title == "TOTAL VENTA" && !isClpPref,
                GaugePercent = gauge,
                GaugeFillWidth = new GridLength(Math.Max(0.001, gauge), GridUnitType.Star),
                GaugeRemainWidth = new GridLength(Math.Max(0.001, 100 - gauge), GridUnitType.Star)
            });
        }

        // 5.º y 6.º KPI: las dos lecturas que faltaban (margen real y concentración de costo).
        double margenPct = (double)state.Cost.MargenRealPct;
        var margenAccent = margenPct >= 10d
            ? DashboardExecutiveKpiAccent.RiskLow
            : (margenPct > 0d ? DashboardExecutiveKpiAccent.RiskMedium : DashboardExecutiveKpiAccent.RiskHigh);
        cards.Add(new DashboardKpiCardVm
        {
            Title = "MARGEN REAL",
            Value = margenRealPctLabel,
            Caption = $"{costMargin} · margen sobre la venta neta",
            Accent = margenAccent,
            AccentBrush = DashboardKpiBrushes.ForAccent(margenAccent),
            AccentSoftBrush = DashboardKpiBrushes.ForAccentSoft(margenAccent),
            ChipLabel = margenPct >= 10d ? "Saludable" : (margenPct > 0d ? "Ajustado" : "Bajo costo"),
            ChipKind = margenPct >= 10d ? "up" : (margenPct > 0d ? "warn" : "danger"),
            IconKind = "margen"
        });

        cards.Add(new DashboardKpiCardVm
        {
            Title = "CONCENTRACIÓN A",
            Value = $"{concentracion.PctA:N1}%",
            Caption = concentracion.CountA > 0
                ? $"{concentracion.CountA} partidas cubren el 80% del costo directo"
                : "Aún sin partidas críticas Tipo A",
            Accent = DashboardExecutiveKpiAccent.Blue,
            AccentBrush = DashboardKpiBrushes.BlueDark,
            AccentSoftBrush = DashboardKpiBrushes.BlueSoft,
            ChipLabel = concentracion.CountA <= 15 ? "Foco" : "Dispersa",
            ChipKind = concentracion.CountA <= 15 ? "mute" : "warn",
            IconKind = "concentracion"
        });

        return cards;
    }
}
