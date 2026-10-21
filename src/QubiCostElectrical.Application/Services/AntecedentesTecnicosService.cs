using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class AntecedentesTecnicosSummary
{
    public int Total { get; init; }
    public int Planos { get; init; }
    public int PendientesMandante { get; init; }
    public int Exclusiones { get; init; }
    public int Supuestos { get; init; }
    public int Inclusiones { get; init; }
    public bool TieneFechaCorte { get; init; }
}

public sealed class AntecedentesTecnicosService
{
    public int CountConsultasAbiertas(IEnumerable<AntecedenteTecnicoRow>? rows)
        => (rows ?? Enumerable.Empty<AntecedenteTecnicoRow>())
            .Count(x => IsTipo(x, AntecedenteTecnicoTipo.Consulta) && x.EsPendienteMandante);

    public AntecedentesTecnicosSummary Summarize(ProjectInfoRow? proyecto)
    {
        var rows = proyecto?.AntecedentesTecnicos ?? new List<AntecedenteTecnicoRow>();
        return new AntecedentesTecnicosSummary
        {
            Total = rows.Count,
            Planos = rows.Count(x => IsTipo(x, AntecedenteTecnicoTipo.Plano)),
            PendientesMandante = rows.Count(x => x.EsPendienteMandante),
            Exclusiones = rows.Count(x => x.EsExclusion),
            Supuestos = rows.Count(x => x.EsSupuesto),
            Inclusiones = rows.Count(x => x.IncluidoEnAlcance),
            TieneFechaCorte = proyecto?.FechaCorteAntecedentes != null && proyecto.FechaCorteAntecedentes != DateTime.MinValue
        };
    }

    public string? GetVigenteRevision(IEnumerable<AntecedenteTecnicoRow>? rows, string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        return (rows ?? Enumerable.Empty<AntecedenteTecnicoRow>())
            .Where(x => IsTipo(x, AntecedenteTecnicoTipo.Plano) && SameCode(x.Codigo, codigo) && x.EsVigente)
            .OrderByDescending(x => x.FechaDocumento)
            .Select(x => x.Revision?.Trim())
            .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
    }

    public bool MatchesRegistro(IEnumerable<AntecedenteTecnicoRow>? rows, string codigo, string revision)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return true;
        var list = (rows ?? Enumerable.Empty<AntecedenteTecnicoRow>())
            .Where(x => IsTipo(x, AntecedenteTecnicoTipo.Plano) && SameCode(x.Codigo, codigo))
            .ToList();
        if (list.Count == 0) return string.IsNullOrWhiteSpace(revision);

        string rev = (revision ?? "").Trim();
        if (string.IsNullOrWhiteSpace(rev))
            return list.Any(x => x.EsVigente);

        return list.Any(x => SameRevision(x.Revision, rev));
    }

    public IReadOnlyList<(MeasurementLineRow Line, string Problema)> FindMedicionesSinRegistro(
        IEnumerable<MeasurementLineRow>? mediciones,
        IEnumerable<AntecedenteTecnicoRow>? antecedentes)
    {
        var result = new List<(MeasurementLineRow, string)>();
        foreach (var line in mediciones ?? Enumerable.Empty<MeasurementLineRow>())
        {
            string plano = (line.Plano ?? "").Trim();
            if (string.IsNullOrWhiteSpace(plano)) continue;

            string revision = (line.RevisionPlano ?? "").Trim();
            if (!MatchesRegistro(antecedentes, plano, revision))
            {
                string vigente = GetVigenteRevision(antecedentes, plano) ?? "";
                string msg = string.IsNullOrWhiteSpace(vigente)
                    ? $"Plano «{plano}» no está en antecedentes técnicos."
                    : $"Revisión «{revision}» no coincide con la vigente ({vigente}) del plano «{plano}».";
                result.Add((line, msg));
            }
        }

        return result;
    }

    public void EnsureSingleVigenteRevision(IList<AntecedenteTecnicoRow> rows, AntecedenteTecnicoRow changed)
    {
        if (!changed.EsVigente || !IsTipo(changed, AntecedenteTecnicoTipo.Plano)) return;
        string code = NormalizeCode(changed.Codigo);
        if (string.IsNullOrWhiteSpace(code)) return;

        foreach (var row in rows)
        {
            if (ReferenceEquals(row, changed)) continue;
            if (IsTipo(row, AntecedenteTecnicoTipo.Plano) && SameCode(row.Codigo, code))
                row.EsVigente = false;
        }
    }

    public string BuildExclusionsDraft(IEnumerable<AntecedenteTecnicoRow>? rows)
    {
        var lines = (rows ?? Enumerable.Empty<AntecedenteTecnicoRow>())
            .Where(x => x.EsExclusion)
            .Select(x =>
            {
                string head = string.IsNullOrWhiteSpace(x.Codigo) ? x.Tipo : $"{x.Tipo} {x.Codigo}".Trim();
                if (!string.IsNullOrWhiteSpace(x.Revision))
                    head += $" Rev. {x.Revision}";
                string body = string.IsNullOrWhiteSpace(x.Descripcion) ? x.Notas : x.Descripcion;
                return string.IsNullOrWhiteSpace(body) ? head : $"{head}: {body}";
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return lines.Count == 0 ? "" : string.Join(Environment.NewLine, lines);
    }

    public IReadOnlyList<OfferPlanReferenceRow> GetPlanosVigentesParaOferta(IEnumerable<AntecedenteTecnicoRow>? rows)
    {
        return (rows ?? Enumerable.Empty<AntecedenteTecnicoRow>())
            .Where(x => IsTipo(x, AntecedenteTecnicoTipo.Plano) && x.EsVigente && !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => NormalizeCode(x.Codigo), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.FechaDocumento).First())
            .Select(x => new OfferPlanReferenceRow
            {
                Codigo = x.Codigo.Trim(),
                Revision = x.Revision?.Trim() ?? "",
                Descripcion = x.Descripcion?.Trim() ?? "",
                Disciplina = string.IsNullOrWhiteSpace(x.Disciplina) ? "Electricidad" : x.Disciplina.Trim()
            })
            .OrderBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsTipo(AntecedenteTecnicoRow row, string tipo)
        => string.Equals((row.Tipo ?? "").Trim(), tipo, StringComparison.OrdinalIgnoreCase);

    private static bool SameCode(string? a, string? b)
        => string.Equals(NormalizeCode(a), NormalizeCode(b), StringComparison.OrdinalIgnoreCase);

    private static bool SameRevision(string? a, string? b)
        => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCode(string? value) => (value ?? "").Trim();
}
