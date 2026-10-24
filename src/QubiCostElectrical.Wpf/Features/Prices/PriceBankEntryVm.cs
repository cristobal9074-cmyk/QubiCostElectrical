using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Prices;

public sealed class PriceBankEntryVm : ViewModelBase
{
    private readonly PriceBankEntry source;
    private readonly Func<bool>? canEdit;
    private string originalCodigo;
    private bool hasChanges;
    private decimal valorUfClp;
    private bool modoRendimiento;
    private decimal tarifaDhClp;
    private decimal indiceMoFa;

    public PriceBankEntryVm(PriceBankEntry source, decimal valorUfClp = 0m, Func<bool>? canEdit = null, bool modoRendimiento = false, decimal tarifaDhClp = 0m, decimal indiceMoFa = 0m)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.canEdit = canEdit;
        originalCodigo = NormalizeCode(source.Codigo);
        hasChanges = false;
        this.valorUfClp = valorUfClp;
        this.modoRendimiento = modoRendimiento;
        this.tarifaDhClp = tarifaDhClp;
        this.indiceMoFa = Math.Max(0m, indiceMoFa);

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

    private bool AllowEdit => canEdit?.Invoke() ?? true;

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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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

    /// <summary>Rendimiento de instalación en unidades por día-hombre (REND). 0 = sin rendimiento.</summary>
    public decimal RendimientoDh
    {
        get => source.RendimientoDh;
        set
        {
            if (!AllowEdit) return;
            if (source.RendimientoDh == value) return;
            source.RendimientoDh = value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MoDerivadaClp));
        }
    }

    /// <summary>Rendimiento por día-pareja (informativo). 0 = no aplica.</summary>
    public decimal DiaXPareja
    {
        get => source.DiaXPareja;
        set
        {
            if (!AllowEdit) return;
            if (source.DiaXPareja == value) return;
            source.DiaXPareja = value;
            MarkChanged();
            OnPropertyChanged();
        }
    }

    /// <summary>Fórmula de M.O.: "" = fija, "Porcentaje" = Material × % × FA (columna M.O. modo).</summary>
    public string MoFormulaTipo
    {
        get => source.MoFormulaTipo;
        set
        {
            if (!AllowEdit) return;
            string normalized = value ?? "";
            if (string.Equals(source.MoFormulaTipo, normalized, StringComparison.OrdinalIgnoreCase))
                return;
            source.MoFormulaTipo = normalized;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MoDerivadaClp));
        }
    }

    /// <summary>Base de M.O. para la fórmula Factor (multiplica al FA).</summary>
    public decimal MoFactorBase
    {
        get => source.MoFactorBase;
        set
        {
            if (!AllowEdit) return;
            if (source.MoFactorBase == value) return;
            source.MoFactorBase = value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MoDerivadaClp));
        }
    }

    /// <summary>Porcentaje de M.O. sobre el material como número (2.5 = 2,5%). Columna % M.O.</summary>
    public decimal MoPorcentajeMaterial
    {
        get => source.MoPorcentajeMaterial;
        set
        {
            if (!AllowEdit) return;
            if (source.MoPorcentajeMaterial == value) return;
            source.MoPorcentajeMaterial = value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MoDerivadaClp));
        }
    }

    /// <summary>Activa la fórmula M.O. = Precio × % M.O. × FA para este material (columna M.O. por %).</summary>
    public bool MoEsPorcentaje
    {
        get => string.Equals(source.MoFormulaTipo, MaterialMoResolver.MoFormulaPorcentaje, StringComparison.OrdinalIgnoreCase);
        set
        {
            if (!AllowEdit) return;
            string nuevo = value ? MaterialMoResolver.MoFormulaPorcentaje : "";
            if (string.Equals(source.MoFormulaTipo, nuevo, StringComparison.OrdinalIgnoreCase))
                return;
            source.MoFormulaTipo = nuevo;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MoDerivadaClp));
        }
    }

    /// <summary>
    /// MO efectiva mostrada, con la misma precedencia que MaterialMoResolver: modo rendimiento
    /// (Tarifa D/H ÷ Rendimiento) &gt; fórmula porcentaje (Material × % × FA) &gt; M.O. directo.
    /// </summary>
    public decimal MoDerivadaClp
    {
        get
        {
            if (modoRendimiento && tarifaDhClp > 0m && source.RendimientoDh > 0m)
                return Math.Round(tarifaDhClp / source.RendimientoDh, 2, MidpointRounding.AwayFromZero);
            if (string.Equals(source.MoFormulaTipo, MaterialMoResolver.MoFormulaPorcentaje, StringComparison.OrdinalIgnoreCase)
                && source.MoPorcentajeMaterial > 0m)
                return Math.Round(source.MatClp * (source.MoPorcentajeMaterial / 100m) * indiceMoFa, 2, MidpointRounding.AwayFromZero);
            if (string.Equals(source.MoFormulaTipo, MaterialMoResolver.MoFormulaFactor, StringComparison.OrdinalIgnoreCase))
            {
                decimal baseMo = source.MoFactorBase > 0m ? source.MoFactorBase : source.MoClp;
                decimal fa = indiceMoFa > 0m ? indiceMoFa : 1m;
                return Math.Round(baseMo * fa, 2, MidpointRounding.AwayFromZero);
            }
            return source.MoClp;
        }
    }

    // Compatibilidad interna: algunos consumidores (PriceBankService, exportadores) siguen
    // leyendo MatUf/MoUf directo del modelo — no de este VM — así que no se tocan.

    public string MonedaPrecio
    {
        get => source.MonedaPrecio;
        set
        {
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
            if (!AllowEdit) return;
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
    public bool IsDeltaPositive => MatClpDelta > 0;
    public bool IsDeltaNegative => MatClpDelta < 0;
    public string FormattedDelta => MatClpDelta > 0 ? $"+{MatClpDelta:N0}" : $"{MatClpDelta:N0}";
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
