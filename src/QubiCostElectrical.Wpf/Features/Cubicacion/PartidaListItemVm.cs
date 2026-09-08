using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PartidaListItemVm : ViewModelBase
{
    private bool isActiva;
    private bool isTagged;
    private int recursoCount;
    private bool excluidoCubicacion;
    private string motivoNoCubicar;

    public PartidaListItemVm(ItemizadoRow row, int recursoCount = 0, bool hasIssues = false)
    {
        Id = row.Id;
        codigo = row.Item ?? "";
        descripcion = row.Descripcion ?? "";
        unidad = row.Unidad ?? "";
        EsPartida = !row.EsTitulo;
        excluidoCubicacion = row.ExcluidoCubicacion;
        motivoNoCubicar = row.MotivoNoCubicar ?? "";
        RecursoCount = recursoCount;
        cantidad = row.Cantidad;
        HasIssues = hasIssues;
    }

    private string codigo;
    private string descripcion;
    private decimal cantidad;
    private string unidad;

    public string Id { get; }
    public string Codigo
    {
        get => codigo;
        set
        {
            if (SetProperty(ref codigo, value ?? ""))
            {
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(ExclusionTooltip));
            }
        }
    }

    public string Descripcion
    {
        get => descripcion;
        set
        {
            if (SetProperty(ref descripcion, value ?? ""))
            {
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(ExclusionTooltip));
            }
        }
    }
    public bool IsTagged
    {
        get => isTagged;
        set => SetProperty(ref isTagged, value);
    }
    public string MotivoNoCubicar
    {
        get => motivoNoCubicar;
        set
        {
            if (SetProperty(ref motivoNoCubicar, value ?? ""))
            {
                OnPropertyChanged(nameof(ExclusionTooltip));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }
    public string Unidad
    {
        get => unidad;
        set => SetProperty(ref unidad, value);
    }
    public bool EsPartida { get; }
    public bool ExcluidoCubicacion
    {
        get => excluidoCubicacion;
        set
        {
            if (SetProperty(ref excluidoCubicacion, value))
            {
                OnPropertyChanged(nameof(ExclusionTooltip));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }
    public decimal Cantidad
    {
        get => cantidad;
        set => SetProperty(ref cantidad, value);
    }

    public int RecursoCount
    {
        get => recursoCount;
        set
        {
            if (SetProperty(ref recursoCount, value))
            {
                OnPropertyChanged(nameof(TieneCubicacion));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }

    public bool TieneCubicacion => recursoCount > 0;

    // Código no encontrado en el catálogo, o precio en cero, en al menos un recurso de la
    // partida. Antes solo se detectaba abriendo la partida y mirando fila por fila; con
    // proyectos grandes era fácil dejar partidas rotas sin darse cuenta.
    public bool HasIssues { get; }

    public bool IsActiva
    {
        get => isActiva;
        set => SetProperty(ref isActiva, value);
    }

    public string DisplayLabel => EsPartida
        ? (string.IsNullOrWhiteSpace(Codigo) ? Descripcion : $"{Codigo}  {Descripcion}")
        : $"  {Descripcion}";

    /// <summary>Solo muestra la observación si hay texto; la exclusión se ve por tachado, no por cartel.</summary>
    public string StatusBadgeText
    {
        get
        {
            var nota = MotivoNoCubicar.Trim();
            if (!string.IsNullOrEmpty(nota))
                return nota;
            if (HasIssues)
                return "Con observaciones";
            return TieneCubicacion ? "Cubicada" : "Sin cubicación";
        }
    }

    public string ExclusionTooltip
    {
        get
        {
            var motivo = MotivoNoCubicar.Trim();
            return string.IsNullOrEmpty(motivo)
                ? DisplayLabel
                : $"{DisplayLabel}\n{motivo}";
        }
    }
}
