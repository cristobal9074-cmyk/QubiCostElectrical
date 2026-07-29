using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class MedicionLineVm : ViewModelBase
{
    public MeasurementLineRow Source { get; }

    internal Action? ParcialChanged;

    public MedicionLineVm(MeasurementLineRow source) => Source = source;

    public string Comentario
    {
        get => Source.Comentario;
        set { if (Source.Comentario != value) { Source.Comentario = value; OnPropertyChanged(); } }
    }

    public string Plano
    {
        get => Source.Plano;
        set { if (Source.Plano != value) { Source.Plano = value; OnPropertyChanged(); } }
    }

    public string RevisionPlano
    {
        get => Source.RevisionPlano;
        set { if (Source.RevisionPlano != value) { Source.RevisionPlano = value; OnPropertyChanged(); } }
    }

    public string Piso
    {
        get => Source.Piso;
        set { if (Source.Piso != value) { Source.Piso = value; OnPropertyChanged(); } }
    }

    public string Sector
    {
        get => Source.Sector;
        set { if (Source.Sector != value) { Source.Sector = value; OnPropertyChanged(); } }
    }

    public decimal Longitud
    {
        get => Source.Longitud;
        set
        {
            if (Source.Longitud == value) return;
            Source.Longitud = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Parcial));
            ParcialChanged?.Invoke();
        }
    }

    public bool IsDeduccion
    {
        get => Source.Signo < 0;
        set
        {
            Source.Signo = value ? -1 : 1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Parcial));
            ParcialChanged?.Invoke();
        }
    }

    public decimal Parcial => MeasurementLineService.ComputeParcial(Source);
}
