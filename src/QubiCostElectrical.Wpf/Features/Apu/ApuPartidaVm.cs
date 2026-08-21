using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Apu;

public sealed class ApuPartidaVm : ViewModelBase
{
    private bool isSelected;
    private bool tieneApu;
    private string estado = "Sin APU";
    private decimal precioUnitUf;

    public ApuPartidaVm(ItemizadoRow row, ApuRow? apu)
    {
        ItemId = row.Id ?? "";
        CodigoItem = row.Item ?? "";
        Descripcion = row.Descripcion ?? "";
        Unidad = row.Unidad ?? "";
        Cantidad = row.Cantidad;
        EsTitulo = row.EsTitulo;
        ApplyApu(apu);
    }

    public string ItemId { get; }
    public string CodigoItem { get; }
    public string Descripcion { get; }
    public string Unidad { get; }
    public decimal Cantidad { get; }
    public bool EsTitulo { get; }

    public bool TieneApu
    {
        get => tieneApu;
        private set => SetProperty(ref tieneApu, value);
    }

    public string Estado
    {
        get => estado;
        private set => SetProperty(ref estado, value);
    }

    public decimal PrecioUnitUf
    {
        get => precioUnitUf;
        private set => SetProperty(ref precioUnitUf, value);
    }

    public string DisplayLabel => string.IsNullOrWhiteSpace(CodigoItem)
        ? Descripcion
        : $"{CodigoItem}  {Descripcion}";

    public string EstadoColor => Estado switch
    {
        "OK" => "#1E7A4B",
        "Falta precio" => "#B23434",
        "Revisar rendimiento" => "#9A6A10",
        _ => "#5C6A7C"
    };

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public void Refresh(ApuRow? apu)
    {
        ApplyApu(apu);
        OnPropertyChanged(nameof(EstadoColor));
    }

    private void ApplyApu(ApuRow? apu)
    {
        if (apu != null)
        {
            TieneApu = true;
            Estado = ApuFromCubicacionService.GetProfessionalStatus(apu);
            PrecioUnitUf = apu.PrecioUnitUf;
        }
        else
        {
            TieneApu = false;
            Estado = "Sin APU";
            PrecioUnitUf = 0m;
        }
    }
}
