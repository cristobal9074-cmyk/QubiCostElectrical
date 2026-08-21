using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Prices;

public sealed class PriceBankEntryVm : ViewModelBase
{
    private readonly PriceBankEntry source;
    private string originalCodigo;
    private bool hasChanges;
    private decimal valorUfClp;

    public PriceBankEntryVm(PriceBankEntry source, decimal valorUfClp = 0m)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        originalCodigo = NormalizeCode(source.Codigo);
        hasChanges = false;
        this.valorUfClp = valorUfClp;

        // Sanación de entradas ya contaminadas (pesos en columna MoUf)
        if (source.MoClp == 0m && source.MoUf > 15m)
        {
            source.MoClp = source.MoUf;
            source.RecalcularUf(valorUfClp);
        }

        // Migración perezosa por campo: MAT y MO independientes. Nunca MoUf=1 → ×ValorUF.
        if (source.MatClp == 0m && source.MatUf > 0m && valorUfClp > 0m)
            source.MatClp = PriceConversion.LegacyUfFieldToClp(source.MatUf, valorUfClp, isMo: false);
        if (source.MoClp == 0m && source.MoUf > 0m && valorUfClp > 0m)
            source.MoClp = PriceConversion.LegacyUfFieldToClp(source.MoUf, valorUfClp, isMo: true);

        source.RecalcularUf(valorUfClp);
    }

    /// <summary>
    /// Valor de la UF del proyecto activo, usado para derivar MatUf/MoUf desde MatClp/MoClp.
    /// Al cambiar, recalcula y refresca la fila (ver PriceBankViewModel).
    /// </summary>
    public decimal ValorUfClp
    {
        get => valorUfClp;
        set
        {
            if (valorUfClp == value) return;
            valorUfClp = value;
            source.RecalcularUf(valorUfClp);
            OnPropertyChanged(nameof(PrecioUfCalculado));
            OnPropertyChanged(nameof(MoUfCalculado));
        }
    }

    public PriceBankEntry Source => source;

    public bool HasChanges
    {
        get => hasChanges;
        private set => SetProperty(ref hasChanges, value);
    }

    public string OriginalCodigo
    {
        get => originalCodigo;
        private set => originalCodigo = NormalizeCode(value);
    }

    public string Codigo
    {
        get => source.Codigo;
        set
        {
            string normalized = NormalizeCode(value);
            if (string.Equals(source.Codigo, normalized, StringComparison.OrdinalIgnoreCase))
                return;
            source.Codigo = normalized;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string Descripcion
    {
        get => source.Descripcion;
        set
        {
            if (source.Descripcion == value) return;
            source.Descripcion = value ?? "";
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string Unidad
    {
        get => source.Unidad;
        set
        {
            if (source.Unidad == value) return;
            source.Unidad = value ?? "";
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string Categoria
    {
        get => source.Categoria;
        set
        {
            if (source.Categoria == value) return;
            source.Categoria = value ?? "";
            MarkChanged();
            OnPropertyChanged();
        }
    }

    // Precio fuente: SIEMPRE en pesos chilenos (columna "PRECIO CLP" de la lista de precios).
    // Al editarlo se recalcula MatUf al instante con el valor de UF vigente.
    public decimal PrecioClp
    {
        get => source.MatClp;
        set
        {
            if (source.MatClp == value) return;
            source.MatClp = value;
            source.RecalcularUf(valorUfClp);
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(PrecioUfCalculado));
        }
    }

    // Mano de obra fuente: SIEMPRE en pesos chilenos (columna "MO").
    public decimal MoClp
    {
        get => source.MoClp;
        set
        {
            if (source.MoClp == value) return;
            source.MoClp = value;
            source.RecalcularUf(valorUfClp);
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MoUfCalculado));
        }
    }

    // UF derivada, de solo lectura — el motor (APU/presupuesto/resumen/itemizado) sigue
    // consumiendo MatUf/MoUf sin cambios; esto es solo lo que se le muestra al usuario.
    public decimal PrecioUfCalculado => source.MatUf;
    public decimal MoUfCalculado => source.MoUf;

    // Compatibilidad interna: algunos consumidores (PriceBankService, exportadores) siguen
    // leyendo MatUf/MoUf directo del modelo — no de este VM — así que no se tocan.

    public string MonedaPrecio
    {
        get => source.MonedaPrecio;
        set
        {
            if (source.MonedaPrecio == value) return;
            source.MonedaPrecio = string.IsNullOrWhiteSpace(value) ? Moneda.UF : value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string Proveedor
    {
        get => source.Proveedor;
        set
        {
            if (source.Proveedor == value) return;
            source.Proveedor = value ?? "";
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string Marca
    {
        get => source.Marca;
        set
        {
            if (source.Marca == value) return;
            source.Marca = value ?? "";
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public DateTime FechaPrecio
    {
        get => source.FechaPrecio;
        set
        {
            if (source.FechaPrecio == value) return;
            source.FechaPrecio = value == default ? DateTime.Today : value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(VigenciaLabel));
            OnPropertyChanged(nameof(VigenciaKind));
        }
    }

    // Columna "VIGENTE": a los 6 meses (180 días) de la fecha de precio, hay que revisarlo.
    public string VigenciaKind => source.RequiereRevisionPrecio ? "Revisar" : "Vigente";

    public string VigenciaLabel => VigenciaKind == "Revisar" ? "REVISAR" : "Vigente";

    public string OrigenPrecio
    {
        get => source.OrigenPrecio;
        set
        {
            if (source.OrigenPrecio == value) return;
            source.OrigenPrecio = value ?? "";
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public string ProyectoOrigen
    {
        get => source.ProyectoOrigen;
        set
        {
            if (source.ProyectoOrigen == value) return;
            source.ProyectoOrigen = value ?? "";
            MarkChanged();
            OnPropertyChanged();
        }
    }

    public DateTime ActualizadoEn => source.ActualizadoEn;

    public string EstadoLabel => HasChanges ? "Pendiente" : "Guardado";

    public void AcceptChanges()
    {
        OriginalCodigo = source.Codigo;
        HasChanges = false;
        OnPropertyChanged(nameof(EstadoLabel));
    }

    public void MarkSavedAs(string? newCodigo = null)
    {
        if (!string.IsNullOrWhiteSpace(newCodigo))
            source.Codigo = NormalizeCode(newCodigo);
        AcceptChanges();
    }

    private void MarkChanged()
    {
        HasChanges = true;
        OnPropertyChanged(nameof(EstadoLabel));
    }

    private static string NormalizeCode(string? value) => (value ?? "").Trim().ToUpperInvariant();
}

public sealed class PriceBankUpdateVm
{
    public PriceBankUpdateVm(PriceBankPriceUpdate source)
    {
        Source = source;
    }

    public PriceBankPriceUpdate Source { get; }
    public string Codigo => Source.Codigo;
    public string Descripcion => Source.Descripcion;
    public decimal MatUfAnterior => Source.MatUfAnterior;
    public decimal MatUfNuevo => Source.MatUfNuevo;
    public decimal MatUfDelta => Source.MatUfNuevo - Source.MatUfAnterior;
    public decimal MoUfAnterior => Source.MoUfAnterior;
    public decimal MoUfNuevo => Source.MoUfNuevo;
    public decimal MoUfDelta => Source.MoUfNuevo - Source.MoUfAnterior;
    public decimal MatClpAnterior => Source.MatClpAnterior;
    public decimal MatClpNuevo => Source.MatClpNuevo;
    public decimal MatClpDelta => Source.MatClpNuevo - Source.MatClpAnterior;
    public decimal MoClpAnterior => Source.MoClpAnterior;
    public decimal MoClpNuevo => Source.MoClpNuevo;
    public decimal MoClpDelta => Source.MoClpNuevo - Source.MoClpAnterior;
    public string ProveedorAnterior => Source.ProveedorAnterior;
    public string ProveedorNuevo => Source.ProveedorNuevo;
    public string MonedaAnterior => Source.MonedaAnterior;
    public string MonedaNueva => Source.MonedaNueva;
    public string FechaAnterior => Source.FechaPrecioAnterior == default ? "—" : Source.FechaPrecioAnterior.ToString("dd/MM/yyyy");
    public string FechaNuevo => Source.FechaPrecioNuevo == default ? "—" : Source.FechaPrecioNuevo.ToString("dd/MM/yyyy");
}
