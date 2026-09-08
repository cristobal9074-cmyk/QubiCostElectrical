using System.Text;

namespace QubiCostElectrical;

public interface IDocumentsWorkspaceService
{
    AntecedentesTecnicosSummary Summarize();
    int CountConsultasAbiertas();
    IReadOnlyList<AntecedenteTecnicoRow> GetRows();
    IReadOnlyList<AntecedenteTecnicoRow> Filter(string? search, string? tipoFilter);
    AntecedenteTecnicoRow AddNew(string tipo);
    void Remove(AntecedenteTecnicoRow row);
    AntecedenteTecnicoRow Duplicate(AntecedenteTecnicoRow row);
    void MarkVigente(AntecedenteTecnicoRow row);
    void SetFechaCorte(DateTime? fecha);
    DateTime? GetFechaCorte();
    void SaveChanges();
    void DiscardChanges();
    int SyncPlanosToOffer();
    int AppendExclusionsToOffer(bool replace);
}

public sealed class DocumentsWorkspaceService : IDocumentsWorkspaceService
{
    private readonly BudgetProjectSession session;
    private readonly AntecedentesTecnicosService antecedentes = new();
    private List<AntecedenteTecnicoRow>? workingCopy;

    public DocumentsWorkspaceService(BudgetProjectSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    private ProjectInfoRow Project => session.ProjectInfo;

    private List<AntecedenteTecnicoRow> WorkingRows
    {
        get
        {
            workingCopy ??= (Project.AntecedentesTecnicos ?? new List<AntecedenteTecnicoRow>())
                .Select(CloneRow)
                .ToList();
            return workingCopy;
        }
    }

    public AntecedentesTecnicosSummary Summarize() => antecedentes.Summarize(Project);

    public int CountConsultasAbiertas() => antecedentes.CountConsultasAbiertas(WorkingRows);

    public IReadOnlyList<AntecedenteTecnicoRow> GetRows() => WorkingRows.AsReadOnly();

    public IReadOnlyList<AntecedenteTecnicoRow> Filter(string? search, string? tipoFilter)
    {
        IEnumerable<AntecedenteTecnicoRow> query = WorkingRows;
        if (!string.IsNullOrWhiteSpace(tipoFilter) && !tipoFilter.Equals("Todos", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => string.Equals(x.Tipo, tipoFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            query = query.Where(x =>
                (x.Codigo ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Descripcion ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Revision ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Notas ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(x => x.FechaDocumento).ThenBy(x => x.Codigo).ToList();
    }

    public AntecedenteTecnicoRow AddNew(string tipo)
    {
        session.EnsureWritable("agregar antecedentes técnicos");
        var row = new AntecedenteTecnicoRow
        {
            Tipo = string.IsNullOrWhiteSpace(tipo) ? AntecedenteTecnicoTipo.Plano : tipo.Trim(),
            FechaDocumento = DateTime.Today,
            EsVigente = true
        };
        WorkingRows.Add(row);
        return row;
    }

    public void Remove(AntecedenteTecnicoRow row)
    {
        session.EnsureWritable("eliminar antecedentes técnicos");
        WorkingRows.Remove(row);
    }

    public AntecedenteTecnicoRow Duplicate(AntecedenteTecnicoRow row)
    {
        session.EnsureWritable("duplicar antecedentes técnicos");
        var copy = CloneRow(row);
        copy.Id = Guid.NewGuid().ToString("N");
        copy.RegistradoEn = AppClock.Now;
        WorkingRows.Add(copy);
        return copy;
    }

    public void MarkVigente(AntecedenteTecnicoRow row)
    {
        session.EnsureWritable("marcar antecedentes técnicos");
        row.EsVigente = true;
        antecedentes.EnsureSingleVigenteRevision(WorkingRows, row);
    }

    public void SetFechaCorte(DateTime? fecha)
    {
        session.EnsureWritable("cambiar la fecha de corte de antecedentes");
        Project.FechaCorteAntecedentes = fecha;
    }

    public DateTime? GetFechaCorte() => Project.FechaCorteAntecedentes;

    public void SaveChanges()
    {
        session.EnsureWritable("guardar antecedentes técnicos");
        Project.AntecedentesTecnicos = WorkingRows.Select(CloneRow).ToList();
        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        workingCopy = null;
    }

    public void DiscardChanges() => workingCopy = null;

    public int SyncPlanosToOffer()
    {
        session.EnsureWritable("sincronizar planos a la oferta");
        Project.OfertaPdf ??= new OfferPdfSettings();
        var incoming = antecedentes.GetPlanosVigentesParaOferta(WorkingRows);
        int added = 0;
        foreach (var plano in incoming)
        {
            bool exists = Project.OfertaPdf.Planos.Any(x =>
                SameCode(x.Codigo, plano.Codigo) && SameRevision(x.Revision, plano.Revision));
            if (exists) continue;
            Project.OfertaPdf.Planos.Add(plano);
            added++;
        }

        return added;
    }

    public int AppendExclusionsToOffer(bool replace)
    {
        session.EnsureWritable("actualizar exclusiones de la oferta");
        Project.OfertaPdf ??= new OfferPdfSettings();
        string draft = antecedentes.BuildExclusionsDraft(WorkingRows);
        if (string.IsNullOrWhiteSpace(draft)) return 0;

        if (replace || string.IsNullOrWhiteSpace(Project.OfertaPdf.ExclusionsText))
        {
            Project.OfertaPdf.ExclusionsText = draft;
            return draft.Split('\n').Length;
        }

        var sb = new StringBuilder(Project.OfertaPdf.ExclusionsText.Trim());
        sb.AppendLine();
        sb.Append(draft);
        Project.OfertaPdf.ExclusionsText = sb.ToString();
        return draft.Split('\n').Length;
    }

    private static bool SameCode(string? a, string? b)
        => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool SameRevision(string? a, string? b)
        => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static AntecedenteTecnicoRow CloneRow(AntecedenteTecnicoRow source) => new()
    {
        Id = source.Id,
        Tipo = source.Tipo,
        Codigo = source.Codigo,
        Revision = source.Revision,
        Descripcion = source.Descripcion,
        Disciplina = source.Disciplina,
        FechaDocumento = source.FechaDocumento,
        EsVigente = source.EsVigente,
        IncluidoEnAlcance = source.IncluidoEnAlcance,
        EsExclusion = source.EsExclusion,
        EsSupuesto = source.EsSupuesto,
        EsPendienteMandante = source.EsPendienteMandante,
        ResponsableRevision = source.ResponsableRevision,
        Notas = source.Notas,
        RutaArchivo = source.RutaArchivo,
        RegistradoEn = source.RegistradoEn
    };
}
