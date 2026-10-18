using System;
using System.Globalization;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

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

    public string RutaArchivoNombre
    {
        get => string.IsNullOrWhiteSpace(row.RutaArchivo) ? "" : System.IO.Path.GetFileName(row.RutaArchivo);
        set { }
    }
    public bool HasFile => !string.IsNullOrWhiteSpace(RutaArchivoNombre);

    public string EstadoLabel
    {
        get => EsPendienteMandante ? "Pendiente mandante" : (EsVigente ? "Vigente" : "Histórico");
        set { }
    }
    public bool IsEstadoVigente => EsVigente && !EsPendienteMandante;
    public bool IsEstadoPendiente => EsPendienteMandante;
    public bool IsEstadoHistorico => !EsVigente && !EsPendienteMandante;

    public bool IsTipoPlano => string.Equals(Tipo, AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoEspec => string.Equals(Tipo, AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoAddenda =>
        string.Equals(Tipo, AntecedenteTecnicoTipo.Addenda, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Tipo, AntecedenteTecnicoTipo.Aclaracion, StringComparison.OrdinalIgnoreCase);
    public bool IsTipoConsulta => string.Equals(Tipo, AntecedenteTecnicoTipo.Consulta, StringComparison.OrdinalIgnoreCase);

    public string FormatBadge
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(row.RutaArchivo))
            {
                var ext = System.IO.Path.GetExtension(row.RutaArchivo).TrimStart('.').ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(ext))
                    return ext.Length > 4 ? ext.Substring(0, 4) : ext;
            }
            if (IsTipoPlano) return "DWG";
            if (IsTipoEspec) return "PDF";
            if (IsTipoAddenda) return "ADD";
            if (IsTipoConsulta) return "RFI";
            return "DOC";
        }
        set { }
    }

    public bool IsFormatPdf => string.Equals(FormatBadge, "PDF", StringComparison.OrdinalIgnoreCase);
    public bool IsFormatDwg => string.Equals(FormatBadge, "DWG", StringComparison.OrdinalIgnoreCase) || string.Equals(FormatBadge, "DXF", StringComparison.OrdinalIgnoreCase);
    public bool IsFormatXlsx => string.Equals(FormatBadge, "XLSX", StringComparison.OrdinalIgnoreCase) || string.Equals(FormatBadge, "XLS", StringComparison.OrdinalIgnoreCase);

    public bool FileExistsOnDisk => !string.IsNullOrWhiteSpace(row.RutaArchivo) && System.IO.File.Exists(row.RutaArchivo);
    public bool IsBrokenLink => !string.IsNullOrWhiteSpace(row.RutaArchivo) && !FileExistsOnDisk;

    public void RefreshEstado()
    {
        OnPropertyChanged(nameof(EstadoLabel));
        OnPropertyChanged(nameof(IsEstadoVigente));
        OnPropertyChanged(nameof(IsEstadoPendiente));
        OnPropertyChanged(nameof(IsEstadoHistorico));
        OnPropertyChanged(nameof(FormatBadge));
        OnPropertyChanged(nameof(IsFormatPdf));
        OnPropertyChanged(nameof(IsFormatDwg));
        OnPropertyChanged(nameof(IsFormatXlsx));
        OnPropertyChanged(nameof(FileExistsOnDisk));
        OnPropertyChanged(nameof(IsBrokenLink));
    }
}

public sealed class PlanoEspecialidadFilterItem : ViewModelBase
{
    private bool isSelected;
    private int count;

    public string Disciplina { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Icon { get; init; } = "📐";

    public int Count
    {
        get => count;
        set => SetProperty(ref count, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public ICommand SelectCommand { get; init; } = null!;
}
