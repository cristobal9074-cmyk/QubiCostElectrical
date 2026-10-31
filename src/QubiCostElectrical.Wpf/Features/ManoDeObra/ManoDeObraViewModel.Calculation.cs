using System;
using System.Windows;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public sealed partial class ManoDeObraViewModel
{
    public void Recalcular()
    {
        if (CargosDef.Length == 0)
            return;

        var n = totalMeses;
        var vUf = valorUf;
        var leyes = leyesSocialesPct / 100m;
        var directa = CargosDef.Where(c => c.EsDirecta).ToArray();
        var indirecta = CargosDef.Where(c => !c.EsDirecta).ToArray();

        foreach (var c in directa)
        {
            if (c.DotPico <= 0 && peakHeadcount > 0)
                c.DotPico = peakHeadcount;
        }

        var rampUp = Math.Max(1, (int)Math.Round(n * 0.25));
        var peak = Math.Max(1, (int)Math.Round(n * 0.40));
        var rampDown = Math.Max(0, n - rampUp - peak);
        var peakInicio = rampUp + 1;
        var peakFin = rampUp + peak;

        var meses = new MoMesInfo[n];
        var dotacionPorCargo = new Dictionary<string, int[]>();
        foreach (var c in CargosDef)
            dotacionPorCargo[c.Id] = new int[n];

        decimal totalBase = 0, totalLeyes = 0, totalMo = 0, totalDir = 0, totalInd = 0;

        for (var i = 0; i < n; i++)
        {
            double frac;
            string fase;
            if (i < rampUp)
            {
                frac = (i + 1.0) / rampUp;
                fase = "ramp-up";
            }
            else if (i < rampUp + peak)
            {
                frac = 1.0;
                fase = "peak";
            }
            else
            {
                frac = 1.0 - ((i - rampUp - peak + 1.0) / (rampDown + 1));
                fase = "ramp-down";
            }

            int head = curvePreset is "fixed" or "flat"
                ? peakHeadcount
                : (int)Math.Round(frac * peakHeadcount);

            foreach (var c in CargosDef)
            {
                var key = $"{c.Id}_{i}";
                if (cellOverrides.TryGetValue(key, out var count))
                {
                    dotacionPorCargo[c.Id][i] = count;
                }
                else if (c.EsDirecta)
                {
                    dotacionPorCargo[c.Id][i] = head;
                }
                else
                {
                    dotacionPorCargo[c.Id][i] = c.DotPico;
                }
            }

            decimal baseDirMes = directa.Sum(c => c.SueldoBase * dotacionPorCargo[c.Id][i]);
            decimal baseIndMes = indirecta.Sum(c => c.SueldoBase * c.FraccionObra * dotacionPorCargo[c.Id][i]);
            var leyesDirMes = baseDirMes * leyes;
            var leyesIndMes = baseIndMes * leyes;
            var baseMes = baseDirMes + baseIndMes;
            var leyesMes = leyesDirMes + leyesIndMes;
            var totalMes = baseMes + leyesMes;
            var dotDir = directa.Sum(c => dotacionPorCargo[c.Id][i]);
            var dotInd = indirecta.Sum(c => dotacionPorCargo[c.Id][i]);

            totalBase += baseMes;
            totalLeyes += leyesMes;
            totalMo += totalMes;
            totalDir += baseDirMes + leyesDirMes;
            totalInd += baseIndMes + leyesIndMes;

            meses[i] = new MoMesInfo
            {
                Numero = i + 1,
                Fase = fase,
                DotDir = dotDir,
                DotInd = dotInd,
                CostoStr = Fmt(totalMes),
                CostoTotal = totalMes,
            };
        }

        Meses = meses;

        var dotRows = new List<MoTableRow>();
        var picoDir = directa.Sum(c => c.DotPico);
        dotRows.Add(new MoTableRow
        {
            IsGroupHeader = true,
            GroupLabel = "MO Directa",
            GroupBadge = $"{picoDir} pico",
            GroupBadgeType = "dir",
        });

        foreach (var c in directa)
        {
            var dots = dotacionPorCargo[c.Id];
            decimal totalCargo = 0;
            for (var i = 0; i < n; i++)
                totalCargo += c.SueldoBase * dots[i] * (1 + leyes);

            dotRows.Add(new MoTableRow
            {
                Tag = c.Tag,
                CargoId = c.Id,
                Nombre = c.Nombre,
                EsDirecta = true,
                DotPico = c.DotPico,
                DotacionStr = c.DotPico.ToString(CultureInfo.InvariantCulture),
                SueldoBaseStr = Fmt(c.SueldoBase),
                FraccionStr = "—",
                LeyesStr = Fmt(c.SueldoBase * leyes),
                TotalMensualStr = Fmt(c.SueldoBase * (1 + leyes)),
                Dotaciones = dots,
                TotalStr = Fmt(totalCargo),
            });
        }

        var subDirDots = new int[n];
        decimal subDirTotal = 0;
        for (var i = 0; i < n; i++)
        {
            subDirDots[i] = directa.Sum(c => dotacionPorCargo[c.Id][i]);
            subDirTotal += directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * (1 + leyes));
        }

        dotRows.Add(new MoTableRow
        {
            IsSubtotal = true,
            EsDirecta = true,
            Tag = "",
            Nombre = "Subtotal MO Directa",
            DotPico = picoDir,
            DotacionStr = picoDir.ToString(CultureInfo.InvariantCulture),
            SueldoBaseStr = Fmt(directa.Sum(c => c.SueldoBase)),
            FraccionStr = "—",
            LeyesStr = Fmt(directa.Sum(c => c.SueldoBase) * leyes),
            TotalMensualStr = Fmt(directa.Sum(c => c.SueldoBase) * (1 + leyes)),
            Dotaciones = subDirDots,
            TotalStr = Fmt(subDirTotal),
        });

        var picoInd = indirecta.Sum(c => c.DotPico);
        dotRows.Add(new MoTableRow
        {
            IsGroupHeader = true,
            GroupLabel = "MO Indirecta",
            GroupBadge = $"{picoInd} roles fijos",
            GroupBadgeType = "ind",
        });

        foreach (var c in indirecta)
        {
            var dots = dotacionPorCargo[c.Id];
            var baseCobrada = c.SueldoBase * c.FraccionObra;
            decimal totalCargo = 0;
            for (var i = 0; i < n; i++)
                totalCargo += baseCobrada * dots[i] * (1 + leyes);

            var dotStr = (c.FraccionObra > 0m && c.FraccionObra < 1m)
                ? c.FraccionObra.ToString("0.##", CultureInfo.GetCultureInfo("es-CL"))
                : (c.DotPico * c.FraccionObra).ToString("0.##", CultureInfo.GetCultureInfo("es-CL"));
            if (string.IsNullOrWhiteSpace(dotStr) || dotStr == "0")
                dotStr = c.FraccionObra == 0m ? "0" : c.DotPico.ToString(CultureInfo.InvariantCulture);

            dotRows.Add(new MoTableRow
            {
                Tag = c.Tag,
                CargoId = c.Id,
                Nombre = c.Nombre,
                EsDirecta = false,
                DotPico = c.DotPico,
                DotacionStr = dotStr,
                SueldoBaseStr = Fmt(baseCobrada),
                FraccionObra = c.FraccionObra,
                FraccionStr = FraccionStr(c.FraccionObra),
                LeyesStr = Fmt(baseCobrada * leyes),
                TotalMensualStr = Fmt(baseCobrada * (1 + leyes)),
                Dotaciones = dots,
                TotalStr = Fmt(totalCargo),
            });
        }

        var subIndDots = new int[n];
        decimal subIndTotal = 0;
        for (var i = 0; i < n; i++)
        {
            subIndDots[i] = indirecta.Sum(c => dotacionPorCargo[c.Id][i]);
            subIndTotal += indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra * (1 + leyes));
        }

        dotRows.Add(new MoTableRow
        {
            IsSubtotal = true,
            EsDirecta = false,
            Tag = "",
            Nombre = "Subtotal MO Indirecta",
            DotPico = picoInd,
            DotacionStr = picoInd.ToString(CultureInfo.InvariantCulture),
            SueldoBaseStr = Fmt(indirecta.Sum(c => c.SueldoBase * c.FraccionObra)),
            FraccionStr = "—",
            LeyesStr = Fmt(indirecta.Sum(c => c.SueldoBase * c.FraccionObra) * leyes),
            TotalMensualStr = Fmt(indirecta.Sum(c => c.SueldoBase * c.FraccionObra) * (1 + leyes)),
            Dotaciones = subIndDots,
            TotalStr = Fmt(subIndTotal),
        });

        var grandDots = new int[n];
        for (var i = 0; i < n; i++)
            grandDots[i] = subDirDots[i] + subIndDots[i];

        var basePorMesDir = directa.Sum(c => c.SueldoBase);
        var basePorMesInd = indirecta.Sum(c => c.SueldoBase * c.FraccionObra);
        dotRows.Add(new MoTableRow
        {
            IsGrandTotal = true,
            Tag = "",
            Nombre = "Total dotación",
            DotPico = grandDots.Length > 0 ? grandDots.Max() : 0,
            DotacionStr = (grandDots.Length > 0 ? grandDots.Max() : 0).ToString(CultureInfo.InvariantCulture),
            SueldoBaseStr = Fmt(basePorMesDir + basePorMesInd),
            FraccionStr = "—",
            LeyesStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
            TotalMensualStr = Fmt((basePorMesDir + basePorMesInd) * (1 + leyes)),
            Dotaciones = grandDots,
            TotalStr = Fmt(totalMo),
        });

        DotacionRows = dotRows.ToArray();
        lastTotalMoClp = totalMo;
        lastTotalDirClp = totalDir;
        hasRecalculated = true;

        var costRows = new List<MoCostRow>
        {
            new()
            {
                Label = "MO Directa (base + leyes)",
                Tag = "dir",
                BaseStr = Fmt(basePorMesDir),
                LeyesStr = Fmt(basePorMesDir * leyes),
                TotalStr = Fmt(basePorMesDir * (1 + leyes)),
                CostosStr = meses.Select((_, i) => Fmt(directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * (1 + leyes)))).ToArray(),
                TotalFinalStr = Fmt(totalDir),
            },
            new()
            {
                Label = "MO Indirecta (base + leyes)",
                Tag = "ind",
                BaseStr = Fmt(basePorMesInd),
                LeyesStr = Fmt(basePorMesInd * leyes),
                TotalStr = Fmt(basePorMesInd * (1 + leyes)),
                CostosStr = meses.Select((_, i) => Fmt(indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra * (1 + leyes)))).ToArray(),
                TotalFinalStr = Fmt(totalInd),
            },
            new()
            {
                IsSubtotal = true,
                Label = "Sueldos base",
                Tag = "base",
                BaseStr = Fmt(basePorMesDir + basePorMesInd),
                LeyesStr = "—",
                TotalStr = Fmt(basePorMesDir + basePorMesInd),
                CostosStr = meses.Select((_, i) =>
                {
                    var b = directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase)
                          + indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra);
                    return Fmt(b);
                }).ToArray(),
                TotalFinalStr = Fmt(totalBase),
            },
            new()
            {
                IsSubtotal = true,
                Label = $"Leyes sociales ({Math.Round(leyesSocialesPct)}%)",
                Tag = "leyes",
                BaseStr = "—",
                LeyesStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
                TotalStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
                CostosStr = meses.Select((_, i) =>
                {
                    var b = directa.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase)
                          + indirecta.Sum(c => dotacionPorCargo[c.Id][i] * c.SueldoBase * c.FraccionObra);
                    return Fmt(b * leyes);
                }).ToArray(),
                TotalFinalStr = Fmt(totalLeyes),
            },
            new()
            {
                IsGrandTotal = true,
                Label = "Total mes",
                BaseStr = Fmt(basePorMesDir + basePorMesInd),
                LeyesStr = Fmt((basePorMesDir + basePorMesInd) * leyes),
                TotalStr = Fmt((basePorMesDir + basePorMesInd) * (1 + leyes)),
                CostosStr = meses.Select(m => m.CostoStr).ToArray(),
                TotalFinalStr = Fmt(totalMo),
            },
        };

        CostRows = costRows.ToArray();

        var pctBase = totalMo > 0 ? totalBase / totalMo * 100 : 0;
        var pctLeyes = totalMo > 0 ? totalLeyes / totalMo * 100 : 0;
        var pctDir = totalMo > 0 ? totalDir / totalMo * 100 : 0;
        var pctInd = totalMo > 0 ? totalInd / totalMo * 100 : 0;
        var mesProm = n > 0 ? totalMo / n : 0;
        var promSueldoDir = directa.Length > 0 ? directa.Average(c => c.SueldoBase) : 0;
        var promSueldoInd = indirecta.Length > 0 ? indirecta.Average(c => c.SueldoBase * c.FraccionObra) : 0;
        var picoDotDir = subDirDots.Length > 0 ? subDirDots.Max() : 0;

        KpiTotalMo = Fmt(totalMo);
        KpiTotalMoSub = $"{n} meses - pico {peakHeadcount} pers.";
        KpiBase = Fmt(totalBase);
        KpiBasePct = $"{pctBase:N1}% del total MO";
        KpiLeyes = Fmt(totalLeyes);
        KpiLeyesPct = $"{pctLeyes:N1}% del total MO";
        KpiDirecta = Fmt(totalDir);
        KpiDirectaSub = $"{directa.Length} cargos - variable";
        KpiDirectaPct = $"{pctDir:N1}% del total";
        KpiIndirecta = Fmt(totalInd);
        KpiIndirectaSub = $"{indirecta.Length} roles - fijo";
        KpiIndirectaPct = $"{pctInd:N1}% del total";
        KpiBaseTrend = "Base imponible";
        KpiLeyesTrend = $"{Math.Round(leyesSocialesPct)}% leyes";

        ExDirPico = $"{picoDotDir} pers.";
        ExDirSueldoProm = FmtClp(promSueldoDir);
        ExDirLeyVal = FmtClp(promSueldoDir * leyes);
        ExIndDur = $"{n} meses";
        ExIndRoles = $"{indirecta.Length}";
        ExIndSueldoProm = FmtClp(promSueldoInd);

        PeakLabel = $"M{peakInicio} – M{peakFin}";
        ChipMeses = $"{n} meses";
        ChipUf = FmtClp(vUf);
        CostHint = (viewInUf ? "UF" : "CLP") + $" - incluye {Math.Round(leyesSocialesPct)}% leyes";

        TotalBarLabel = $"Costo total mano de obra - {n} meses";
        TotalBarSub = $"Directa {pctDir:N1}% - Indirecta {pctInd:N1}% - Curva {(curvePreset == "flat" ? "plana" : "campana")} - Pico M{peakInicio}–M{peakFin}";
        TotalBarValue = Fmt(totalMo);
        TotalBarUf = viewInUf ? FmtClp(totalMo) : (MuestraUf ? FmtUf(totalMo, vUf) : "");

        SumBase = Fmt(totalBase);
        SumLeyes = Fmt(totalLeyes);
        SumDirecta = Fmt(totalDir);
        SumIndirecta = Fmt(totalInd);
        SumPromMes = Fmt(mesProm);
        SumRight = viewInUf
            ? $"{FmtUf(totalMo, vUf)}  -  {FmtClp(mesProm)}/mes"
            : $"{FmtClp(totalMo)}  -  {FmtClp(mesProm)}/mes";

        FootDir = $"{FmtClp(promSueldoDir)} - {FmtClp(promSueldoDir * (1 + leyes))}";
        FootInd = $"{FmtClp(promSueldoInd)} - {FmtClp(promSueldoInd * (1 + leyes))}";

        foreach (var ed in DirectaEditors.Concat(IndirectaEditors))
            ed.UpdateConLeyes(leyes);

        RebuildChart(meses);

        OnPropertyChanged(nameof(CargosDef));
        OnPropertyChanged(nameof(Meses));
        OnPropertyChanged(nameof(DotacionRows));
        OnPropertyChanged(nameof(CostRows));
    }

    private bool isPersonasMode = true;
    public bool IsPersonasMode
    {
        get => isPersonasMode;
        set
        {
            if (SetProperty(ref isPersonasMode, value))
            {
                OnPropertyChanged(nameof(IsDiasHombreMode));
                RebuildChart(Meses);
            }
        }
    }

    public bool IsDiasHombreMode
    {
        get => !isPersonasMode;
        set => IsPersonasMode = !value;
    }

    private Thickness chartMeanLineMargin;
    public Thickness ChartMeanLineMargin
    {
        get => chartMeanLineMargin;
        private set => SetProperty(ref chartMeanLineMargin, value);
    }

    private Thickness chartMeanBadgeMargin;
    public Thickness ChartMeanBadgeMargin
    {
        get => chartMeanBadgeMargin;
        private set => SetProperty(ref chartMeanBadgeMargin, value);
    }

    private string chartMeanLabel = "Media: 0 pers.";
    public string ChartMeanLabel
    {
        get => chartMeanLabel;
        private set => SetProperty(ref chartMeanLabel, value);
    }

    private bool hasChartData;
    public bool HasChartData
    {
        get => hasChartData;
        private set => SetProperty(ref hasChartData, value);
    }

    private void RebuildChart(MoMesInfo[] meses)
    {
        ChartBars.Clear();
        if (meses == null || meses.Length == 0)
        {
            HasChartData = false;
            return;
        }

        HasChartData = true;
        const double DhFactor = 22.0;
        double avgDot = meses.Average(m => (double)m.DotTotal);
        int maxDot = Math.Max(1, meses.Max(m => m.DotTotal));

        double meanY = maxDot > 0 ? (100.0 * avgDot / maxDot) : 0;
        meanY = Math.Max(4, Math.Min(116, meanY));

        ChartMeanLineMargin = new Thickness(0, 0, 0, meanY);
        ChartMeanBadgeMargin = new Thickness(0, 0, 4, Math.Max(0, meanY - 8));

        if (IsPersonasMode)
        {
            ChartMeanLabel = $"Media: {avgDot:F1} pers.";
            foreach (var m in meses)
            {
                ChartBars.Add(new MoChartBarVm
                {
                    Label = $"M{m.Numero}",
                    DirPct = 100.0 * m.DotDir / maxDot,
                    IndPct = 100.0 * m.DotInd / maxDot,
                    Tip = $"Mes {m.Numero} - {m.DotTotal} pers. ({m.DotDir} dir + {m.DotInd} ind) - {Fmt(m.CostoTotal)}",
                });
            }
        }
        else
        {
            double avgDh = avgDot * DhFactor;
            ChartMeanLabel = $"Media: {avgDh:F0} DH";
            foreach (var m in meses)
            {
                double dirDh = m.DotDir * DhFactor;
                double indDh = m.DotInd * DhFactor;
                double totDh = m.DotTotal * DhFactor;
                ChartBars.Add(new MoChartBarVm
                {
                    Label = $"M{m.Numero}",
                    DirPct = 100.0 * m.DotDir / maxDot,
                    IndPct = 100.0 * m.DotInd / maxDot,
                    Tip = $"Mes {m.Numero} - {totDh:N0} DH ({dirDh:N0} dir + {indDh:N0} ind) - {Fmt(m.CostoTotal)}",
                });
            }
        }

        OnPropertyChanged(nameof(ChartMeanLabel));
        OnPropertyChanged(nameof(ChartMeanLineMargin));
        OnPropertyChanged(nameof(ChartMeanBadgeMargin));
        OnPropertyChanged(nameof(HasChartData));
    }
}