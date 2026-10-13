#nullable enable
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ItemizadoPartidaVm : ViewModelBase
{
    private string itemCode = "";
    private string descripcion = "";
    private string unidad = "";
    private decimal cantidad;
    private decimal materialTotal;
    private decimal manoObraTotal;
    private decimal costoDirecto;
    private bool isCurrentPartida;

    public bool EsSeccion { get; init; }

    public string Id { get; init; } = "";

    public bool IsCurrentPartida
    {
        get => isCurrentPartida;
        set => SetProperty(ref isCurrentPartida, value);
    }

    public string ItemCode
    {
        get => itemCode;
        set
        {
            if (SetProperty(ref itemCode, value))
            {
                OnChanged?.Invoke(this);
            }
        }
    }

    public string Descripcion
    {
        get => descripcion;
        set
        {
            if (SetProperty(ref descripcion, value))
            {
                OnChanged?.Invoke(this);
            }
        }
    }

    public string Unidad
    {
        get => unidad;
        set
        {
            if (SetProperty(ref unidad, value))
            {
                OnChanged?.Invoke(this);
            }
        }
    }

    public decimal Cantidad
    {
        get => cantidad;
        set
        {
            if (SetProperty(ref cantidad, value))
            {
                OnPropertyChanged(nameof(CantidadDisplay));
                OnPropertyChanged(nameof(CantidadEditString));
                OnChanged?.Invoke(this);
            }
        }
    }

    public string CantidadEditString
    {
        get => Cantidad.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Cantidad = 0m;
                return;
            }
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
                decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out parsed))
            {
                Cantidad = parsed;
            }
        }
    }

    public decimal MaterialTotal
    {
        get => materialTotal;
        set
        {
            if (SetProperty(ref materialTotal, value))
            {
                OnPropertyChanged(nameof(MaterialDisplay));
                OnPropertyChanged(nameof(TieneCubicacion));
                OnPropertyChanged(nameof(EstadoBadgeText));
                OnPropertyChanged(nameof(EstadoBadgeBackground));
                OnPropertyChanged(nameof(EstadoBadgeBorder));
                OnPropertyChanged(nameof(EstadoBadgeForeground));
                OnPropertyChanged(nameof(EstadoBadgeDot));
            }
        }
    }

    public decimal ManoObraTotal
    {
        get => manoObraTotal;
        set
        {
            if (SetProperty(ref manoObraTotal, value))
            {
                OnPropertyChanged(nameof(ManoObraDisplay));
                OnPropertyChanged(nameof(TieneCubicacion));
                OnPropertyChanged(nameof(EstadoBadgeText));
                OnPropertyChanged(nameof(EstadoBadgeBackground));
                OnPropertyChanged(nameof(EstadoBadgeBorder));
                OnPropertyChanged(nameof(EstadoBadgeForeground));
                OnPropertyChanged(nameof(EstadoBadgeDot));
            }
        }
    }

    public decimal CostoDirecto
    {
        get => costoDirecto;
        set
        {
            if (SetProperty(ref costoDirecto, value))
            {
                OnPropertyChanged(nameof(CostoDirectoDisplay));
            }
        }
    }

    public string CantidadDisplay => Cantidad.ToString("#,##0.##");

    public string MaterialDisplay
    {
        get
        {
            if (!(MaterialTotal > 0m))
            {
                return "";
            }
            return $"$ {MaterialTotal:N0}";
        }
    }

    public string ManoObraDisplay
    {
        get
        {
            if (!(ManoObraTotal > 0m))
            {
                return "";
            }
            return $"$ {ManoObraTotal:N0}";
        }
    }

    public string CostoDirectoDisplay
    {
        get
        {
            if (!(CostoDirecto > 0m))
            {
                return "";
            }
            return $"$ {CostoDirecto:N0}";
        }
    }

    public bool TieneCubicacion
    {
        get
        {
            if (!(MaterialTotal > 0m))
            {
                return ManoObraTotal > 0m;
            }
            return true;
        }
    }

    public string EstadoBadgeText => EsSeccion ? "CAPÍTULO" : (TieneCubicacion ? "Cubicado" : "Pendiente");
    public string EstadoBadgeBackground => EsSeccion ? "#F1F5F9" : (TieneCubicacion ? "#ECFDF5" : "#FFFBEB");
    public string EstadoBadgeBorder => EsSeccion ? "#E2E8F0" : (TieneCubicacion ? "#A7F3D0" : "#FDE68A");
    public string EstadoBadgeForeground => EsSeccion ? "#64748B" : (TieneCubicacion ? "#047857" : "#B45309");
    public string EstadoBadgeDot => EsSeccion ? "#94A3B8" : (TieneCubicacion ? "#10B981" : "#F59E0B");

    public Action<ItemizadoPartidaVm>? OnChanged { get; set; }
}
