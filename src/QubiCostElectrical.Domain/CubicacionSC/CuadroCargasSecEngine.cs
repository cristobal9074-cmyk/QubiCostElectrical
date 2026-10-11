using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace QubiCostElectrical;

public sealed record CircuitoSecSummary(
    int Numero,
    string Descripcion,
    string Tipo,
    string Sistema,
    string Fase,
    decimal PotenciaW,
    decimal PotenciaKw,
    decimal CorrienteA,
    string Disyuntor,
    string Diferencial,
    string Conductor,
    string Canalizacion
);

public sealed record TableroSecSummary(
    string TableroId,
    string TableroNombre,
    string Descripcion,
    int TotalCircuitos,
    int CircuitosActivos,
    int CircuitosReserva,
    decimal PotenciaTotalInstaladaKw,
    decimal FactorSimultaneidad,
    decimal PotenciaTotalDemandaKw,
    decimal PotenciaFaseRKw,
    decimal PotenciaFaseSKw,
    decimal PotenciaFaseTKw,
    decimal CorrienteFaseRA,
    decimal CorrienteFaseSA,
    decimal CorrienteFaseTA,
    decimal CorrienteMaximaA,
    decimal CorrienteDemandaA,
    decimal PorcentajeDesbalance,
    bool ConformeSec,
    string DisyuntorGeneralSugerido,
    string DiferencialGeneralSugerido,
    string AlimentadorSugerido,
    string DuctoAlimentadorSugerido,
    IReadOnlyList<CircuitoSecSummary> Circuitos
);

public static partial class CuadroCargasSecEngine
{
    private static readonly Regex WattageRegex = new(@"(\d+(?:[.,]\d+)?)\s*W\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static decimal EstimateCircuitPowerW(CircuitoSCRow c)
    {
        if (c == null) return 0m;
        if (c.PotenciaW > 0m) return c.PotenciaW;
        if (!c.EsActivo && !c.EsReservaEquipada) return 0m;
        if (c.EsReservaEquipada) return 500m;

        decimal sum = 0m;
        bool hasArtifacts = false;

        if (c.Artefactos != null && c.Artefactos.Count > 0)
        {
            foreach (var a in c.Artefactos)
            {
                if (a.Cantidad <= 0m) continue;
                hasArtifacts = true;
                decimal unitW = ParseArtifactWattage(a.Nombre, a.Categoria);
                sum += unitW * a.Cantidad;
            }
        }

        if (c.SensoresMovimiento > 0m)
        {
            sum += c.SensoresMovimiento * 10m;
            hasArtifacts = true;
        }

        if (c.Senaleticas > 0m)
        {
            sum += c.Senaleticas * 15m;
            hasArtifacts = true;
        }

        if (c.CintaLedMts > 0m)
        {
            sum += c.CintaLedMts * 14.4m;
            hasArtifacts = true;
        }

        if (hasArtifacts && sum > 0m)
            return Math.Round(sum, 1);

        return (c.Tipo ?? "").Trim().ToUpperInvariant() switch
        {
            "A" => 1000m,
            "E" => 1500m,
            "CL" => 2200m,
            "C" => 1500m,
            "F" => 3000m,
            _ => 1000m
        };
    }

    private static decimal ParseArtifactWattage(string? name, string? category)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 100m;

        var m = WattageRegex.Match(name);
        if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedW) && parsedW > 0m)
            return parsedW;

        string upper = name.Trim().ToUpperInvariant();
        if (upper.StartsWith("ENCH S10")) return 150m;
        if (upper.StartsWith("ENCH D10")) return 300m;
        if (upper.StartsWith("ENCH T10")) return 450m;
        if (upper.StartsWith("ENCH S16")) return 250m;
        if (upper.StartsWith("ENCH D16")) return 500m;
        if (upper.StartsWith("ENCH T16")) return 750m;
        if (upper.StartsWith("ENCH MONO")) return 1500m;
        if (upper.StartsWith("ENCH TRIFA")) return 3000m;
        if (upper.StartsWith("INT 9/")) return 0m;
        if (upper.Contains("DOWNLIGHT") || upper.Contains("LUMINARIA") || upper.Contains("FOCO")) return 50m;
        if (upper.Contains("SENSOR")) return 10m;
        if (upper.Contains("SEÑAL") || upper.Contains("SENAL")) return 15m;
        if (upper.Contains("CINTA")) return 14.4m;

        string cat = (category ?? "").Trim().ToUpperInvariant();
        if (cat.Contains("ENCHUFE")) return 150m;
        if (cat.Contains("LUMINARIA")) return 50m;
        if (cat.Contains("INTERRUPTOR")) return 0m;

        return 100m;
    }

    public static string ResolveCircuitPhase(CircuitoSCRow c, int monoSequenceIndex)
    {
        if (c == null) return "R";
        if (string.Equals(c.Sistema, "T", StringComparison.OrdinalIgnoreCase))
            return "R-S-T";

        if (!string.IsNullOrWhiteSpace(c.FaseRst))
        {
            string f = c.FaseRst.Trim().ToUpperInvariant();
            if (f is "R" or "S" or "T" or "R-S-T")
                return f;
        }

        return (monoSequenceIndex % 3) switch
        {
            0 => "R",
            1 => "S",
            _ => "T"
        };
    }

    public static string ResolveDisyuntor(CircuitoSCRow c, decimal corrienteA)
    {
        if (c == null) return "1x16A Curva C";
        bool isTrifasico = string.Equals(c.Sistema, "T", StringComparison.OrdinalIgnoreCase);

        if (isTrifasico)
        {
            if (corrienteA <= 16m) return "3x16A Curva C";
            if (corrienteA <= 20m) return "3x20A Curva C";
            if (corrienteA <= 25m) return "3x25A Curva C";
            if (corrienteA <= 32m) return "3x32A Curva C";
            if (corrienteA <= 40m) return "3x40A Curva C";
            if (corrienteA <= 50m) return "3x50A Curva C";
            return "3x63A Curva C";
        }

        string tipo = (c.Tipo ?? "A").Trim().ToUpperInvariant();
        if (tipo == "A" && corrienteA <= 8m)
            return "1x10A Curva C";

        if (corrienteA <= 16m) return "1x16A Curva C";
        if (corrienteA <= 20m) return "1x20A Curva C";
        if (corrienteA <= 25m) return "1x25A Curva C";
        if (corrienteA <= 32m) return "1x32A Curva C";
        return "1x40A Curva C";
    }

    public static string ResolveDiferencial(CircuitoSCRow c)
    {
        if (c == null) return "2x25A 30mA";
        return string.Equals(c.Sistema, "T", StringComparison.OrdinalIgnoreCase)
            ? "4x25A 30mA"
            : "2x25A 30mA";
    }

    public static TableroSecSummary CalculateTableroSummary(CuadroCargaSCRow board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var circuitosSummary = new List<CircuitoSecSummary>();
        decimal pR = 0m;
        decimal pS = 0m;
        decimal pT = 0m;
        int monoIndex = 0;

        foreach (var c in board.Circuitos.OrderBy(x => x.Numero))
        {
            decimal pW = EstimateCircuitPowerW(c);
            decimal pKw = Math.Round(pW / 1000m, 3);
            string fase = ResolveCircuitPhase(c, monoIndex);
            if (string.Equals(c.Sistema, "T", StringComparison.OrdinalIgnoreCase))
            {
                pR += pKw / 3m;
                pS += pKw / 3m;
                pT += pKw / 3m;
            }
            else
            {
                monoIndex++;
                if (fase == "R") pR += pKw;
                else if (fase == "S") pS += pKw;
                else if (fase == "T") pT += pKw;
                else
                {
                    pR += pKw / 3m;
                    pS += pKw / 3m;
                    pT += pKw / 3m;
                }
            }

            decimal corrienteA = string.Equals(c.Sistema, "T", StringComparison.OrdinalIgnoreCase)
                ? (pW > 0m ? Math.Round(pW / (1.73205m * 380m * 0.93m), 1) : 0m)
                : (pW > 0m ? Math.Round(pW / (220m * 0.93m), 1) : 0m);

            string disyuntor = ResolveDisyuntor(c, corrienteA);
            string diferencial = ResolveDiferencial(c);
            string conductor = $"{c.NumConductores:0}x{c.SeccionDerivacion} {c.ConductorDerivacion}";
            string canalizacion = c.EsPorBandeja
                ? $"Bandeja {c.TipoBandeja} {c.AnchoBandeja}mm"
                : $"Tubería {c.TipoTuberia} {c.SeccionTuberia}";

            circuitosSummary.Add(new CircuitoSecSummary(
                c.Numero,
                c.Descripcion,
                c.Tipo,
                c.Sistema,
                fase,
                pW,
                pKw,
                corrienteA,
                disyuntor,
                diferencial,
                conductor,
                canalizacion
            ));
        }

        decimal pInstaladaKw = Math.Round(pR + pS + pT, 2);

        decimal pDemandaKw = pInstaladaKw <= 3.0m
            ? pInstaladaKw
            : Math.Round(3.0m + ((pInstaladaKw - 3.0m) * 0.70m), 2);

        decimal factorSimultaneidad = pInstaladaKw > 0m
            ? Math.Round(pDemandaKw / pInstaladaKw, 2)
            : 1.0m;

        decimal iR = Math.Round((pR * 1000m) / (220m * 0.93m), 1);
        decimal iS = Math.Round((pS * 1000m) / (220m * 0.93m), 1);
        decimal iT = Math.Round((pT * 1000m) / (220m * 0.93m), 1);
        decimal iMax = Math.Max(iR, Math.Max(iS, iT));
        decimal iDemanda = Math.Round(iMax * factorSimultaneidad, 1);

        decimal pProm = (pR + pS + pT) / 3m;
        decimal desbalancePct = 0m;
        if (pProm > 0m && board.Circuitos.Count(x => x.EsActivo || x.EsReservaEquipada) > 1)
        {
            decimal maxDev = Math.Max(Math.Abs(pR - pProm), Math.Max(Math.Abs(pS - pProm), Math.Abs(pT - pProm)));
            desbalancePct = Math.Round((maxDev / pProm) * 100m, 1);
        }

        bool conformeSec = desbalancePct <= 10.0m;

        bool isBoardTrifasico = pT > 0m || board.Circuitos.Any(x => string.Equals(x.Sistema, "T", StringComparison.OrdinalIgnoreCase));
        var (disyuntorGeneral, diferencialGeneral) = SelectMainProtections(iDemanda, isBoardTrifasico);
        var (alimentador, ducto) = SelectFeederAndDuct(iDemanda, isBoardTrifasico);

        return new TableroSecSummary(
            board.Id,
            board.Nombre,
            board.Descripcion,
            board.Circuitos.Count,
            board.Circuitos.Count(x => x.EsActivo),
            board.Circuitos.Count(x => x.EsReservaEquipada),
            pInstaladaKw,
            factorSimultaneidad,
            pDemandaKw,
            Math.Round(pR, 2),
            Math.Round(pS, 2),
            Math.Round(pT, 2),
            iR,
            iS,
            iT,
            iMax,
            iDemanda,
            desbalancePct,
            conformeSec,
            disyuntorGeneral,
            diferencialGeneral,
            alimentador,
            ducto,
            circuitosSummary
        );
    }

    public static void AutoBalancearFases(CuadroCargaSCRow board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var monoCircuits = board.Circuitos
            .Where(c => !string.Equals(c.Sistema, "T", StringComparison.OrdinalIgnoreCase) && (c.EsActivo || c.EsReservaEquipada))
            .ToList();

        if (monoCircuits.Count == 0)
            return;

        decimal pR = 0m;
        decimal pS = 0m;
        decimal pT = 0m;

        foreach (var c in board.Circuitos.Where(c => string.Equals(c.Sistema, "T", StringComparison.OrdinalIgnoreCase) && (c.EsActivo || c.EsReservaEquipada)))
        {
            decimal p = EstimateCircuitPowerW(c);
            pR += p / 3m;
            pS += p / 3m;
            pT += p / 3m;
            c.FaseRst = "R-S-T";
        }

        var items = monoCircuits
            .Select(c => new { Circuit = c, Power = EstimateCircuitPowerW(c) })
            .OrderByDescending(x => x.Power)
            .ToList();

        // 1. Asignación voraz inicial (Greedy Partition)
        foreach (var item in items)
        {
            if (pR <= pS && pR <= pT)
            {
                item.Circuit.FaseRst = "R";
                pR += item.Power;
            }
            else if (pS <= pR && pS <= pT)
            {
                item.Circuit.FaseRst = "S";
                pS += item.Power;
            }
            else
            {
                item.Circuit.FaseRst = "T";
                pT += item.Power;
            }
        }

        // 2. Optimización por intercambios locales (2-opt swap & move) para minimizar el desbalance
        decimal pProm = (pR + pS + pT) / 3m;
        if (pProm > 0m && items.Count > 1)
        {
            bool improved = true;
            int maxIterations = 50;
            while (improved && maxIterations-- > 0)
            {
                improved = false;
                decimal currentMaxDev = Math.Max(Math.Abs(pR - pProm), Math.Max(Math.Abs(pS - pProm), Math.Abs(pT - pProm)));

                // Intentar mover un circuito de fase
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    string currentPhase = item.Circuit.FaseRst ?? "R";
                    string[] otherPhases = currentPhase switch
                    {
                        "R" => new[] { "S", "T" },
                        "S" => new[] { "R", "T" },
                        _ => new[] { "R", "S" }
                    };

                    foreach (var targetPhase in otherPhases)
                    {
                        decimal newR = pR + (targetPhase == "R" ? item.Power : (currentPhase == "R" ? -item.Power : 0m));
                        decimal newS = pS + (targetPhase == "S" ? item.Power : (currentPhase == "S" ? -item.Power : 0m));
                        decimal newT = pT + (targetPhase == "T" ? item.Power : (currentPhase == "T" ? -item.Power : 0m));
                        decimal newMaxDev = Math.Max(Math.Abs(newR - pProm), Math.Max(Math.Abs(newS - pProm), Math.Abs(newT - pProm)));

                        if (newMaxDev < currentMaxDev - 0.001m)
                        {
                            item.Circuit.FaseRst = targetPhase;
                            pR = newR;
                            pS = newS;
                            pT = newT;
                            currentMaxDev = newMaxDev;
                            improved = true;
                            break;
                        }
                    }
                    if (improved) break;
                }

                if (improved) continue;

                // Intentar intercambiar dos circuitos de distintas fases
                for (int i = 0; i < items.Count && !improved; i++)
                {
                    for (int j = i + 1; j < items.Count && !improved; j++)
                    {
                        var a = items[i];
                        var b = items[j];
                        if (a.Circuit.FaseRst == b.Circuit.FaseRst) continue;

                        string phaseA = a.Circuit.FaseRst!;
                        string phaseB = b.Circuit.FaseRst!;
                        decimal diff = a.Power - b.Power;

                        decimal newR = pR + (phaseA == "R" ? -diff : (phaseB == "R" ? diff : 0m));
                        decimal newS = pS + (phaseA == "S" ? -diff : (phaseB == "S" ? diff : 0m));
                        decimal newT = pT + (phaseA == "T" ? -diff : (phaseB == "T" ? diff : 0m));
                        decimal newMaxDev = Math.Max(Math.Abs(newR - pProm), Math.Max(Math.Abs(newS - pProm), Math.Abs(newT - pProm)));

                        if (newMaxDev < currentMaxDev - 0.001m)
                        {
                            a.Circuit.FaseRst = phaseB;
                            b.Circuit.FaseRst = phaseA;
                            pR = newR;
                            pS = newS;
                            pT = newT;
                            currentMaxDev = newMaxDev;
                            improved = true;
                        }
                    }
                }
            }
        }
    }

    private static (string Disyuntor, string Diferencial) SelectMainProtections(decimal corrienteA, bool isTrifasico)
    {
        decimal target = Math.Max(corrienteA * 1.15m, 16m);
        int rating = target switch
        {
            <= 16m => 16,
            <= 20m => 20,
            <= 25m => 25,
            <= 32m => 32,
            <= 40m => 40,
            <= 50m => 50,
            <= 63m => 63,
            <= 80m => 80,
            <= 100m => 100,
            <= 125m => 125,
            <= 160m => 160,
            <= 200m => 200,
            <= 250m => 250,
            _ => 315
        };

        if (isTrifasico)
        {
            int diffRating = rating <= 25 ? 25 : rating <= 40 ? 40 : 63;
            return ($"3x{rating}A Curva C", $"4x{diffRating}A 300mA");
        }
        else
        {
            int diffRating = rating <= 25 ? 25 : rating <= 40 ? 40 : 63;
            return ($"1x{rating}A Curva C", $"2x{diffRating}A 30mA");
        }
    }

    private static (string Alimentador, string Ducto) SelectFeederAndDuct(decimal corrienteA, bool isTrifasico)
    {
        decimal target = Math.Max(corrienteA, 10m);

        if (target <= 16m)
            return (isTrifasico ? "3x2.5 + 1x2.5 + 1x2.5 mm² RZ1-K" : "2x2.5 + 1x2.5 mm² EVA", "Tubería EMT 20mm");
        if (target <= 22m)
            return (isTrifasico ? "3x4.0 + 1x4.0 + 1x4.0 mm² RZ1-K" : "2x4.0 + 1x4.0 mm² EVA", "Tubería EMT 20mm");
        if (target <= 29m)
            return (isTrifasico ? "3x6.0 + 1x6.0 + 1x6.0 mm² RZ1-K" : "2x6.0 + 1x6.0 mm² EVA", "Tubería EMT 25mm");
        if (target <= 40m)
            return (isTrifasico ? "3x10 + 1x10 + 1x10 mm² RZ1-K" : "2x10 + 1x10 mm² EVA", "Tubería EMT 25mm");
        if (target <= 54m)
            return (isTrifasico ? "3x16 + 1x16 + 1x16 mm² RZ1-K" : "2x16 + 1x16 mm² EVA", "Tubería EMT 32mm");
        if (target <= 71m)
            return (isTrifasico ? "3x25 + 1x25 + 1x16 mm² RZ1-K" : "2x25 + 1x16 mm² EVA", "Tubería EMT 40mm");
        if (target <= 89m)
            return (isTrifasico ? "3x35 + 1x35 + 1x16 mm² RZ1-K" : "2x35 + 1x16 mm² EVA", "Tubería EMT 40mm");
        if (target <= 110m)
            return (isTrifasico ? "3x50 + 1x50 + 1x25 mm² RZ1-K" : "2x50 + 1x25 mm² EVA", "Tubería EMT 50mm");
        if (target <= 140m)
            return (isTrifasico ? "3x70 + 1x70 + 1x35 mm² RZ1-K" : "2x70 + 1x35 mm² EVA", "Tubería EMT 50mm");
        if (target <= 175m)
            return (isTrifasico ? "3x95 + 1x95 + 1x50 mm² RZ1-K" : "2x95 + 1x50 mm² EVA", "Tubería EMT 63mm");
        if (target <= 205m)
            return (isTrifasico ? "3x120 + 1x120 + 1x70 mm² RZ1-K" : "2x120 + 1x70 mm² EVA", "Tubería EMT 63mm");
        if (target <= 240m)
            return (isTrifasico ? "3x150 + 1x150 + 1x70 mm² RZ1-K" : "2x150 + 1x70 mm² EVA", "Tubería EMT 75mm");

        return (isTrifasico ? "3x185 + 1x185 + 1x95 mm² RZ1-K" : "2x185 + 1x95 mm² EVA", "Tubería EMT 90mm");
    }
}
