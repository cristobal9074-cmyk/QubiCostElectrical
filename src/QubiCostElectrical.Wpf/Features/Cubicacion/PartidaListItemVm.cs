using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PartidaListItemVm : ViewModelBase
{
    private bool isActiva;
    private int recursoCount;

    public PartidaListItemVm(ItemizadoRow row, int recursoCount = 0, bool hasIssues = false)
    {
        Id = row.Id;
        Codigo = row.Item ?? "";
        Descripcion = row.Descripcion ?? "";
        unidad = row.Unidad ?? "";
        EsPartida = !row.EsTitulo;
        ExcluidoCubicacion = row.ExcluidoCubicacion;
        RecursoCount = recursoCount;
        cantidad = row.Cantidad;
        HasIssues = hasIssues;
    }

    private decimal cantidad;
    private string unidad;

    public string Id { get; }
    public string Codigo { get; }
    public string Descripcion { get; }
    public string Unidad
    {
        get => unidad;
        set => SetProperty(ref unidad, value);
    }
    public bool EsPartida { get; }
    public bool ExcluidoCubicacion { get; }
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
                OnPropertyChanged(nameof(TieneCubicacion));
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
}
