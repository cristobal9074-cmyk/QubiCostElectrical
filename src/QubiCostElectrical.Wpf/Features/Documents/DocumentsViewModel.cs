using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Documents;

public sealed class AntecedenteRowVm : ViewModelBase
{
    private readonly AntecedenteTecnicoRow row;
    private readonly Action? markDirty;

    public AntecedenteRowVm(AntecedenteTecnicoRow row, Action? markDirty = null)
    {
        this.row = row;
        this.markDirty = markDirty;
    }

    public AntecedenteTecnicoRow Model => row;

    public string Tipo { get => row.Tipo; set { row.Tipo = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTipoPlano)); OnPropertyChanged(nameof(IsTipoEspec)); OnPropertyChanged(nameof(IsTipoAddenda)); OnPropertyChanged(nameof(IsTipoConsulta)); markDirty?.Invoke(); } }
    public string Codigo { get => row.Codigo; set { row.Codigo = value; OnPropertyChanged(); markDirty?.Invoke(); } }
    public string Revision { get => row.Revision; set { row.Revision = value; OnPropertyChanged(); markDirty?.Invoke(); } }
    public string Descripcion { get => row.Descripcion; set { row.Descripcion = value; OnPropertyChanged(); markDirty?.Invoke(); } }
    public string Disciplina { get => row.Disciplina; set { row.Disciplina = value; OnPropertyChanged(); markDirty?.Invoke(); } }

    public DateTime? FechaDocumento
    {
        get => row.FechaDocumento == DateTime.MinValue ? null : row.FechaDocumento;
        set { row.FechaDocumento = value ?? DateTime.Today; OnPropertyChanged(); OnPropertyChanged(nameof(FechaDocumentoText)); }
    }

    public string FechaDocumentoText
    {
        get => FechaDocumento?.ToString("dd/MM/yyyy") ?? "";
        set
        {
            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dt))
                FechaDocumento = dt;
            else if (string.IsNullOrWhiteSpace(value))
                FechaDocumento = null;
            OnPropertyChanged();
        }
    }

    public bool EsVigente { get => row.EsVigente; set { row.EsVigente = value; OnPropertyChanged(); RefreshEstado(); markDirty?.Invoke(); } }
    public bool EsPendienteMandante { get => row.EsPendienteMandante; set { row.EsPendienteMandante = value; OnPropertyChanged(); RefreshEstado(); markDirty?.Invoke(); } }
    public bool EsExclusion { get => row.EsExclusion; set { row.EsExclusion = value; OnPropertyChanged(); markDirty?.Invoke(); } }
    public bool EsSupuesto { get => row.EsSupuesto; set { row.EsSupuesto = value; OnPropertyChanged(); markDirty?.Invoke(); } }
    public bool IncluidoEnAlcance { get => row.IncluidoEnAlcance; set { row.IncluidoEnAlcance = value; OnPropertyChanged(); markDirty?.Invoke(); } }

    public string ResponsableRevision { get => row.ResponsableRevision; set { row.ResponsableRevision = value; OnPropertyChanged(); } }
    public string Notas { get => row.Notas; set { row.Notas = value; OnPropertyChanged(); } }
    public string RutaArchivo
    {
        get => row.RutaArchivo;
        set
        {
            row.RutaArchivo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RutaArchivoNombre));
            OnPropertyChanged(nameof(HasFile));
            markDirty?.Invoke();
        }
    }

    public string RutaArchivoNombre => string.IsNullOrWhiteSpace(row.RutaArchivo) ? "" : System.IO.Path.GetFileName(row.RutaArchivo);
    public bool HasFile => !string.IsNullOrWhiteSpace(RutaArchivoNombre);

    public string EstadoLabel => EsPendienteMandante ? "Pendiente mandante" : (EsVigente ? "Vigente" : "Histórico");
    public bool IsEstadoVigente => EsVigente && !EsPendienteMandante;
    public bool IsEstadoPendiente => EsPendienteMandante;
    public bool IsEstadoHistorico => !EsVigente && !EsPendienteMandante;

    public bool IsTipoPlano => string.Equals(Tipo, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoEspec => string.Equals(Tipo, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoAddenda =>
        string.Equals(Tipo, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Tipo, AntecedenteTecnicoTipo.Aclaracion, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoConsulta => string.Equals(Tipo, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase);

    public void RefreshEstado()
    {
        OnPropertyChanged(nameof(EstadoLabel));
        OnPropertyChanged(nameof(IsEstadoVigente));
        OnPropertyChanged(nameof(IsEstadoPendiente));
        OnPropertyChanged(nameof(IsEstadoHistorico));
    }
}

public sealed partial class DocumentsViewModel : ViewModelBase, IPageWithPendingChanges, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly IPermissionService permissions;
    private readonly IDocumentsWorkspaceService documents;
    private readonly List<AntecedenteRowVm> allRows = new();
    private AntecedenteRowVm? selectedRow;
    private string searchText = "";
    private string tipoFilter = "Todos";
    private bool sinFechaCorte = true;
    private DateTime? fechaCorte;
    private string summaryText = "";
    private string statusMessage = "";
    private bool showEmptyState = true;
    private bool isDirty;
}
