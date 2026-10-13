using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionRowVm : ViewModelBase, IGridCellValidationSource
{
    public CubicacionRowVm(CubicacionRow source, bool isSessionBacked = true, Func<bool>? canEdit = null)
    {
        Source = source;
        this.isSessionBacked = isSessionBacked;
        this.canEdit = canEdit;
        tipo = source.Tipo ?? "MAT";
        codigo = source.Codigo ?? "";
        descripcion = source.Descripcion ?? "";
        unidad = source.Unidad ?? "";
        cantidad = source.Cantidad;
        matUf = source.MatUf;
        moUf = source.MoUf;
        area = string.IsNullOrWhiteSpace(source.Area) ? "General" : source.Area;
        notaInterna = source.NotaInterna ?? "";
        SectorValues = new ObservableCollection<string>(Enumerable.Repeat(string.Empty, CubicacionViewModel.SectorWindowSize));
        SectorValues.CollectionChanged += OnSectorValuesChanged;
        // Valor MOSTRADO en la celda cuando no se está editando: el RESULTADO calculado de la
        // fórmula (ej. "2+3" → "5"), no el texto de la fórmula — igual que una celda de Excel.
        // SectorValues sigue guardando el texto crudo, usado solo mientras se edita la celda.
        SectorDisplayValues = new ObservableCollection<string>(Enumerable.Repeat(string.Empty, CubicacionViewModel.SectorWindowSize));
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    internal Action<CubicacionRowVm, int, string>? SectorExpressionChanged { get; set; }
    internal Action<CubicacionRowVm, HashSet<int>>? SectorBatchExpressionChanged { get; set; }
    // Propagación maestro→esclavo: se dispara cuando cambia la CANTIDAD (columna CANT) de una
    // fila maestro. Cableado en CubicacionSectorService.InitializeSectorRow.
    internal Action<CubicacionRowVm>? CantidadChanged { get; set; }
    // Suprime CantidadChanged durante los recálculos internos de una fila con zonas
    // (ApplySectorExpression / ApplyBatchSectorExpressions): ahí la propagación la hace el
    // camino de zona con el valor por casilla, no el total.
    internal bool SuppressCantidadPropagation;

    private bool _isBatchUpdating;
    private HashSet<int>? _batchUpdatedSlots;

    public void BeginBatchUpdate()
    {
        _isBatchUpdating = true;
        _batchUpdatedSlots = new HashSet<int>();
        isLoadingSectorWindow = true; // Evita disparar CollectionChanged individuales
    }

    public void EndBatchUpdate()
    {
        _isBatchUpdating = false;
        isLoadingSectorWindow = false;

        if (_batchUpdatedSlots != null && _batchUpdatedSlots.Count > 0 && SectorBatchExpressionChanged != null)
        {
            SectorBatchExpressionChanged(this, _batchUpdatedSlots);
        }
        _batchUpdatedSlots = null;
    }

    internal CubicacionRow Source { get; }

    public ObservableCollection<string> SectorValues { get; }
    public ObservableCollection<string> SectorDisplayValues { get; }

    internal void SetSectorDisplayValue(int slot, string display)
    {
        if (slot >= 0 && slot < SectorDisplayValues.Count)
            SectorDisplayValues[slot] = display;
    }

    public string Id => Source.Id;
    public string ItemId => Source.ItemId;

    public bool IsSessionBacked
    {
        get => isSessionBacked;
        internal set => SetProperty(ref isSessionBacked, value);
    }

    /// <summary>
    /// Número correlativo de la fila (1-based). Lo asigna el ViewModel cada vez que
    /// se agregan, insertan, eliminan o mueven filas. Solo se muestra en filas con contenido
    /// real (IsSessionBacked); las filas placeholder muestran cadena vacía.
    /// </summary>
    public int RowNumber
    {
        get => rowNumber;
        internal set
        {
            if (SetProperty(ref rowNumber, value))
                OnPropertyChanged(nameof(RowNumberDisplay));
        }
    }

    /// <summary>Número correlativo para mostrar en la columna #. Siempre visible en todas las filas.</summary>
    public string RowNumberDisplay => rowNumber > 0 ? rowNumber.ToString() : "";

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    // Resalte transitorio (no persiste, no es la selección real de la grilla) mientras se
    // "pickea" esta fila con ↓/↑ desde la Cantidad oficial de la partida en modo Detalle.
    private bool isFormulaPickTarget;
    public bool IsFormulaPickTarget
    {
        get => isFormulaPickTarget;
        set => SetProperty(ref isFormulaPickTarget, value);
    }

    // Sectores absolutos (0-299) apuntados al "pickear" referencias entre casillas con las
    // flechas desde otra celda de la partida (ver HandleSectorPick) — puede haber MÁS DE UNO
    // por fila (ej. sumar varias casillas de la MISMA fila, horizontal: F4C1+F4C2+F4C3). A
    // diferencia de IsFormulaPickTarget (resalta toda la fila), esto marca las CELDAS exactas
    // para que el usuario distinga cuáles casillas van a quedar referenciadas.
    private readonly HashSet<int> formulaPickTargetOffsets = new();
    public IReadOnlyCollection<int> FormulaPickTargetOffsets => formulaPickTargetOffsets;

    public void AddFormulaPickTargetOffset(int offset)
    {
        if (formulaPickTargetOffsets.Add(offset))
            OnPropertyChanged(nameof(FormulaPickTargetOffsets));
    }

    public void ClearFormulaPickTargetOffsets()
    {
        if (formulaPickTargetOffsets.Count == 0)
            return;
        formulaPickTargetOffsets.Clear();
        OnPropertyChanged(nameof(FormulaPickTargetOffsets));
    }

    public string Tipo
    {
        get => tipo;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref tipo, value))
                Source.Tipo = value;
        }
    }

    public string Codigo
    {
        get => codigo;
        set
        {
            if (!AllowEdit) return;
            var normalized = CodeHelper.Normalize(value);
            string old = codigo;
            if (SetProperty(ref codigo, normalized))
            {
                Source.Codigo = normalized;
                OnPropertyChanged(nameof(HasContent));
                if (string.IsNullOrWhiteSpace(normalized) && !string.IsNullOrWhiteSpace(old))
                {
                    Descripcion = "";
                    Unidad = "";
                    MatClp = 0m;
                    MoClp = 0m;
                }
            }
        }
    }

    public string Descripcion
    {
        get => descripcion;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref descripcion, value))
                Source.Descripcion = value;
        }
    }

    public string Unidad
    {
        get => unidad;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref unidad, value))
                Source.Unidad = value;
        }
    }

    public decimal Cantidad
    {
        get => cantidad;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref cantidad, value))
            {
                Source.Cantidad = value;
                if (!SuppressCantidadPropagation)
                    CantidadChanged?.Invoke(this);
                OnPropertyChanged(nameof(UfTotal));
                OnPropertyChanged(nameof(CantidadDisplay));
                OnPropertyChanged(nameof(SubtotalClp));
                OnPropertyChanged(nameof(SubtotalClpDisplay));
                OnPropertyChanged(nameof(MoSubtotalClp));
                OnPropertyChanged(nameof(MoSubtotalClpDisplay));
                OnPropertyChanged(nameof(UfSubtotalCalculado));
                OnPropertyChanged(nameof(UfSubtotalCalculadoDisplay));
            }
        }
    }

    public decimal MatUf
    {
        get => matUf;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref matUf, value))
            {
                Source.MatUf = value;
                OnPropertyChanged(nameof(UfUnit));
                OnPropertyChanged(nameof(UfTotal));
            }
        }
    }

    public decimal MoUf
    {
        get => moUf;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref moUf, value))
            {
                Source.MoUf = value;
                OnPropertyChanged(nameof(UfUnit));
                OnPropertyChanged(nameof(UfTotal));
            }
        }
    }

    // ── Precio en CLP: fuente editable del recurso (columna MAT/MO de Detalle) ────────────
    // MatUf/MoUf quedan derivados de estos — se recalculan al instante con ValorUfClp, igual
    // que MaterialRow.MatClp/MoClp (mismo patrón de todo el modelo de costos).
    public decimal MatClp
    {
        get => Source.MatClp;
        set
        {
            if (!AllowEdit) return;
            if (Source.MatClp == value) return;
            Source.MatClp = value;
            Source.RecalcularUf(valorUfClp);
            matUf = Source.MatUf;
            OnPropertyChanged();
            NotifyPreciosDerivados();
            PropagatePriceToProjectCatalog();
        }
    }

    public decimal MoClp
    {
        get => Source.MoClp;
        set
        {
            if (!AllowEdit) return;
            if (Source.MoClp == value) return;
            Source.MoClp = value;
            Source.RecalcularUf(valorUfClp);
            moUf = Source.MoUf;
            OnPropertyChanged();
            NotifyPreciosDerivados();
            PropagatePriceToProjectCatalog();
        }
    }

    // $ UNIT = MAT + MO (CLP). SUBTOTAL = $ UNIT × cantidad.
    public decimal PrecioUnitClp => Source.PrecioUnitClp;
    public decimal SubtotalClp => Source.SubtotalClp;
    public decimal MoSubtotalClp => Source.MoSubtotalClp;

    // GG+UT: fijo en 1 por ahora (ver comentario en CubicacionRow.GgUtFactor) — no editable
    // todavía desde acá, pero queda expuesto para cuando se conecte a los % reales del proyecto.
    public decimal GgUtFactor => Source.GgUtFactor;

    // Valor de UF del proyecto (pestaña Moneda), propagado desde CubicacionViewModel — ver
    // Rows.CollectionChanged / RefreshValorUfEnFilasCargadas(). UF UNIT = REDONDEAR($ UNIT ×
    // GG+UT / valor UF ; 3).
    private decimal valorUfClp;
    public decimal ValorUfClp
    {
        get => valorUfClp;
        set
        {
            if (valorUfClp == value) return;
            valorUfClp = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UfUnitCalculado));
            OnPropertyChanged(nameof(UfUnitCalculadoDisplay));
            OnPropertyChanged(nameof(UfSubtotalCalculado));
            OnPropertyChanged(nameof(UfSubtotalCalculadoDisplay));
        }
    }

    public decimal UfUnitCalculado => Source.CalcularUfUnit(valorUfClp);
    public string UfUnitCalculadoDisplay => FormatUf(UfUnitCalculado);

    public decimal UfSubtotalCalculado => UfUnitCalculado * Cantidad;
    public string UfSubtotalCalculadoDisplay => FormatUf(UfSubtotalCalculado);

    public string MatClpDisplay => FormatClp(MatClp);
    public string MoClpDisplay => FormatClp(MoClp);
    public string PrecioUnitClpDisplay => FormatClp(PrecioUnitClp);
    public string SubtotalClpDisplay => FormatClp(SubtotalClp);
    public string MoSubtotalClpDisplay => FormatClp(MoSubtotalClp);

    private void NotifyPreciosDerivados()
    {
        OnPropertyChanged(nameof(MatUf));
        OnPropertyChanged(nameof(MoUf));
        OnPropertyChanged(nameof(UfUnit));
        OnPropertyChanged(nameof(UfTotal));
        OnPropertyChanged(nameof(PrecioUnitClp));
        OnPropertyChanged(nameof(SubtotalClp));
        OnPropertyChanged(nameof(MoSubtotalClp));
        OnPropertyChanged(nameof(UfUnitCalculado));
        OnPropertyChanged(nameof(MatClpDisplay));
        OnPropertyChanged(nameof(MoClpDisplay));
        OnPropertyChanged(nameof(PrecioUnitClpDisplay));
        OnPropertyChanged(nameof(SubtotalClpDisplay));
        OnPropertyChanged(nameof(MoSubtotalClpDisplay));
        OnPropertyChanged(nameof(UfUnitCalculadoDisplay));
        OnPropertyChanged(nameof(UfSubtotalCalculado));
        OnPropertyChanged(nameof(UfSubtotalCalculadoDisplay));
    }

    public string Area
    {
        get => area;
        set
        {
            if (!AllowEdit) return;
            string v = string.IsNullOrWhiteSpace(value) ? "General" : value.Trim();
            if (SetProperty(ref area, v))
                Source.Area = v;
        }
    }


    // Nota interna del recurso — solo para revisión del equipo, nunca se exporta a la oferta.
    public string NotaInterna
    {
        get => notaInterna;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref notaInterna, value ?? ""))
                Source.NotaInterna = value ?? "";
        }
    }

    // Delega en CubicacionRow (Domain): evita mantener la misma fórmula duplicada en dos capas.
    public decimal UfUnit => Source.UfUnit;
    public decimal UfTotal => Source.UfTotal;

    // Incluye la Operación (fórmula libre por fila, rol Operación de la plantilla): su
    // cantidad también está calculada, así que no debe alertar "Cantidad es cero" ni
    // propagarse como si fuera una fila normal.
    public bool HasFormula =>
        (Source.QtyExpressions != null && Source.QtyExpressions.Any(x => !string.IsNullOrWhiteSpace(x)))
        || !string.IsNullOrWhiteSpace(Source.TotalExpression)
        || !string.IsNullOrWhiteSpace(Source.Operacion);

    public bool HasContent => !string.IsNullOrWhiteSpace(codigo) || cantidad != 0m;

    public bool HasError
    {
        get => hasError;
        set
        {
            if (SetProperty(ref hasError, value))
                OnPropertyChanged(nameof(ShowPrecioVencidoBadge));
        }
    }

    public string ErrorMessage
    {
        get => errorMessage;
        set => SetProperty(ref errorMessage, value);
    }

    private bool precioVencido;

    public bool PrecioVencido
    {
        get => precioVencido;
        set
        {
            if (SetProperty(ref precioVencido, value))
                OnPropertyChanged(nameof(ShowPrecioVencidoBadge));
        }
    }

    public bool ShowPrecioVencidoBadge => precioVencido && !hasError;

    public string? CodigoCellError { get; private set; }
    public string? DescripcionCellError { get; private set; }
    public string? CantidadCellError { get; private set; }
    public string? UnidadCellError { get; private set; }
    public string? AreaCellError { get; private set; }

    public string? GetCellError(string columnKey) => columnKey switch
    {
        "CÓDIGO" or "Codigo" => CodigoCellError,
        "DESCRIPCIÓN" or "Descripcion" => DescripcionCellError,
        "CANTIDAD" or "Cantidad" => CantidadCellError,
        "UNIDAD" or "Unidad" => UnidadCellError,
        "ÁREA" or "Area" => AreaCellError,
        _ => null
    };

    public void ApplyCellValidation(string? codigoError, string? descripcionError, string? cantidadError, string? unidadError, string? areaError)
    {
        CodigoCellError = codigoError;
        DescripcionCellError = descripcionError;
        CantidadCellError = cantidadError;
        UnidadCellError = unidadError;
        AreaCellError = areaError;
        OnPropertyChanged(nameof(CodigoCellError));
        OnPropertyChanged(nameof(DescripcionCellError));
        OnPropertyChanged(nameof(CantidadCellError));
        OnPropertyChanged(nameof(UnidadCellError));
        OnPropertyChanged(nameof(AreaCellError));
    }

    public string CantidadDisplay => FormatDecimal(cantidad);

    public string UfUnitDisplay => FormatUf(UfUnit);
    public string UfTotalDisplay => FormatUf(UfTotal);
    public string MatUfDisplay => FormatUf(matUf);
    public string MoUfDisplay => FormatUf(moUf);

    public void RefreshDisplayProperties()
    {
        // Resincroniza el cache de MAT UF/MO UF por si Source fue modificado directamente
        // (p. ej. CubicacionService.ApplyMaterial), sin pasar por los setters de este VM.
        matUf = Source.MatUf;
        moUf = Source.MoUf;
        OnPropertyChanged(nameof(HasFormula));
        OnPropertyChanged(nameof(CantidadDisplay));
        OnPropertyChanged(nameof(MatClp));
        OnPropertyChanged(nameof(MoClp));
        OnPropertyChanged(nameof(MatUf));
        OnPropertyChanged(nameof(MoUf));
        OnPropertyChanged(nameof(UfUnit));
        OnPropertyChanged(nameof(UfTotal));
        OnPropertyChanged(nameof(UfUnitDisplay));
        OnPropertyChanged(nameof(UfTotalDisplay));
        OnPropertyChanged(nameof(PrecioUnitClp));
        OnPropertyChanged(nameof(SubtotalClp));
        OnPropertyChanged(nameof(MoSubtotalClp));
        OnPropertyChanged(nameof(MatClpDisplay));
        OnPropertyChanged(nameof(MoClpDisplay));
        OnPropertyChanged(nameof(PrecioUnitClpDisplay));
        OnPropertyChanged(nameof(SubtotalClpDisplay));
        OnPropertyChanged(nameof(MoSubtotalClpDisplay));
        OnPropertyChanged(nameof(UfUnitCalculado));
        OnPropertyChanged(nameof(UfUnitCalculadoDisplay));
        OnPropertyChanged(nameof(MatUfDisplay));
        OnPropertyChanged(nameof(MoUfDisplay));
        OnPropertyChanged(nameof(GgUtFactor));
    }

    internal void LoadSectorWindow(int start)
    {
        isLoadingSectorWindow = true;
        try
        {
            var expressions = Source.QtyExpressions;
            for (int slot = 0; slot < CubicacionViewModel.SectorWindowSize; slot++)
            {
                int offset = start + slot;
                string raw = expressions != null && offset < expressions.Length
                    ? expressions[offset] ?? string.Empty
                    : string.Empty;
                SectorValues[slot] = raw;
                // Se refresca con el valor calculado real justo después (el VM llama a
                // RefreshSectorDisplayValues); esto solo evita mostrar el resultado de la
                // ventana anterior por un instante mientras tanto.
                SectorDisplayValues[slot] = raw;
            }
        }
        finally
        {
            isLoadingSectorWindow = false;
        }
    }

    private void OnSectorValuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Replace || e.NewStartingIndex < 0)
            return;

        if (_isBatchUpdating)
        {
            _batchUpdatedSlots?.Add(e.NewStartingIndex);
            return;
        }

        if (isLoadingSectorWindow)
            return;

        if (!AllowEdit)
            return;

        SectorExpressionChanged?.Invoke(
            this,
            e.NewStartingIndex,
            e.NewItems?[0]?.ToString() ?? string.Empty);
    }

    // Números "cerrados": sin decimales en pantalla, redondeando al entero más cercano — a
    // pedido explícito, para que resultados de fórmulas (ej. divisiones) no muestren colas
    // como "6,6667" en la grilla de casillas ni en la columna CANT.
    internal static string FormatDecimal(decimal v)
        => Math.Round(v, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentCulture);

    private static string FormatUf(decimal v) => v.ToString("N3", CultureInfo.CurrentCulture);
    private static string FormatClp(decimal v) => v.ToString("N0", CultureInfo.CurrentCulture);

    private void PropagatePriceToProjectCatalog()
    {
        if (isSessionBacked && !string.IsNullOrWhiteSpace(Codigo))
        {
            var session = WpfAppHost.Current?.Session;
            if (session != null && !session.IsReadOnly)
            {
                var material = session.Materiales.FirstOrDefault(m => CodeHelper.Match(m.Codigo, Codigo));
                if (material != null)
                {
                    // En modo rendimiento o con fórmula porcentaje, el MO de la fila es
                    // EFECTIVO (derivado); no se propaga al catálogo para no pisar el M.O.
                    // directo del material.
                    bool moDirecto = !MaterialMoResolver.DebeUsarRendimiento(material, session.ProjectInfo)
                                  && !MaterialMoResolver.DebeUsarPorcentaje(material);
                    if (material.MatClp != MatClp || (moDirecto && material.MoClp != MoClp))
                    {
                        material.MatClp = MatClp;
                        if (moDirecto) material.MoClp = MoClp;
                        material.RecalcularUf(valorUfClp);
                        session.MarkDirty(ProjectDirtyScope.Materiales);
                    }
                }
                else
                {
                    material = new MaterialRow
                    {
                        Codigo = Codigo,
                        Descripcion = Descripcion,
                        Unidad = Unidad,
                        MatClp = MatClp,
                        MoClp = MoClp
                    };
                    material.RecalcularUf(valorUfClp);
                    session.Materiales.Add(material);
                    session.MarkDirty(ProjectDirtyScope.Materiales);
                }
            }
        }
    }

    public void SetPricesSilently(decimal newMatClp, decimal newMoClp)
    {
        bool oldBacked = isSessionBacked;
        isSessionBacked = false;
        try
        {
            MatClp = newMatClp;
            MoClp = newMoClp;
        }
        finally
        {
            isSessionBacked = oldBacked;
        }
    }
}
