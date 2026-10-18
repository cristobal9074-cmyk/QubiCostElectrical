using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed class ItemizadoRowVm : ViewModelBase, IGridCellValidationSource
{
    private static readonly Regex ChapterRegex = new(@"^\d+(\.0)?$", RegexOptions.Compiled);

    private ItemizadoRow model;
    private int rowNumber;
    private PartidaCompletionState completionState;
    private decimal completionPct;
    private decimal unitPriceUf;
    private decimal totalUf;
    private string statusLabel = "";
    private string statusKind = "neutral";
    private bool isSelected;
    private bool isTitleRow;
    private string? itemEditSnapshot;
    private readonly Func<bool>? canEdit;
    private bool isBatchUpdating;

    public void BeginBatchUpdate()
    {
        isBatchUpdating = true;
    }

    public void EndBatchUpdate()
    {
        isBatchUpdating = false;
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Unidad));
        OnPropertyChanged(nameof(Cantidad));
        OnPropertyChanged(nameof(CantidadText));
        OnPropertyChanged(nameof(WbsLevel));
        OnPropertyChanged(nameof(IndentMargin));
        OnPropertyChanged(nameof(IsChapterLevelItem));
        OnPropertyChanged(nameof(IsChapterStyle));
    }

    public ItemizadoRowVm(ItemizadoRow model, Func<bool>? canEdit = null)
    {
        this.model = model;
        this.canEdit = canEdit;
        this.isTitleRow = model?.EsTitulo ?? false;
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public void UpdateModel(ItemizadoRow newModel)
    {
        model = newModel ?? throw new ArgumentNullException(nameof(newModel));
        isTitleRow = model.EsTitulo;
        OnPropertyChanged(string.Empty);
    }

    public void ToggleTitleState()
    {
        bool newEsTitulo = !EsTitulo;
        if (!newEsTitulo)
        {
            if (Cantidad <= 0m)
                Cantidad = model.CantidadOriginal > 0m ? model.CantidadOriginal : 1m;
            if (string.IsNullOrWhiteSpace(Unidad))
                Unidad = Unidades.GL;
        }
        else
        {
            Cantidad = 0m;
        }
        EsTitulo = newEsTitulo;
    }

    public event Action<ItemizadoRowVm, string>? BeforeFieldChanging;
    public event Action<ItemizadoRowVm, string>? FieldCommitted;

    public Func<IReadOnlyList<ItemizadoRow>>? PeersProvider { get; set; }

    public ItemizadoRow Model => model;
    public string Id => model.Id ?? "";

    public int RowNumber
    {
        get => rowNumber;
        set => SetProperty(ref rowNumber, value);
    }

    public string Item
    {
        get => model.Item ?? "";
        set
        {
            if (!AllowEdit) return;
            string normalized = value ?? "";
            if (string.Equals(model.Item, normalized, StringComparison.Ordinal))
                return;
            model.Item = normalized;
            if (isBatchUpdating) return;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WbsLevel));
            OnPropertyChanged(nameof(IndentMargin));
            OnPropertyChanged(nameof(IsChapterLevelItem));
            OnPropertyChanged(nameof(IsChapterStyle));
        }
    }

    public int WbsLevel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Item))
                return isTitleRow ? 0 : 1;

            var trimmed = Item.Trim().TrimEnd('.', ',');
            var separators = 0;
            foreach (var ch in trimmed)
            {
                if (ch == '.' || ch == ',') separators++;
            }
            return Math.Min(separators, 4);
        }
    }

    public System.Windows.Thickness IndentMargin
    {
        get
        {
            int left = Math.Max(0, WbsLevel * 16);
            return new System.Windows.Thickness(left, 0, 0, 0);
        }
    }

    public string Descripcion
    {
        get => model.Descripcion ?? "";
        set
        {
            if (!AllowEdit) return;
            model.Descripcion = value;
            if (isBatchUpdating) return;
            OnPropertyChanged();
        }
    }

    public string Unidad
    {
        get => model.Unidad ?? "";
        set
        {
            if (!AllowEdit) return;
            model.Unidad = value;
            if (isBatchUpdating) return;
            OnPropertyChanged();
        }
    }

    public string Observaciones
    {
        get => model.Observaciones ?? "";
        set
        {
            if (!AllowEdit) return;
            string val = value ?? "";
            if (string.Equals(model.Observaciones, val, StringComparison.Ordinal))
                return;
            model.Observaciones = val;
            if (isBatchUpdating) return;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasObservaciones));
            OnPropertyChanged(nameof(ObservacionesToolTip));
        }
    }

    public bool HasObservaciones => !string.IsNullOrWhiteSpace(model.Observaciones);

    public string? ObservacionesToolTip => HasObservaciones ? model.Observaciones : null;

    public decimal Cantidad
    {
        get => model.Cantidad;
        set
        {
            if (!AllowEdit) return;
            model.Cantidad = value;
            if (isBatchUpdating) return;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CantidadText));
        }
    }

    public string CantidadText
    {
        get => model.Cantidad % 1 == 0 ? model.Cantidad.ToString("N0", CultureInfo.CurrentCulture) : model.Cantidad.ToString("#,##0.##", CultureInfo.CurrentCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Cantidad = 0m;
                OnPropertyChanged();
                return;
            }
            string s = value.Trim();
            bool hasComma = s.Contains(',');
            bool hasDot = s.Contains('.');

            decimal parsed;
            if (hasComma && hasDot)
            {
                int commaIdx = s.IndexOf(',');
                int dotIdx = s.IndexOf('.');
                if (dotIdx < commaIdx)
                    s = s.Replace(".", "").Replace(',', '.');
                else
                    s = s.Replace(",", "");

                if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                    Cantidad = parsed;
            }
            else if (hasComma)
            {
                if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                    Cantidad = parsed;
            }
            else
            {
                if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                    Cantidad = parsed;
            }
            OnPropertyChanged();
        }
    }

    public bool EsTitulo
    {
        get => model.EsTitulo;
        set
        {
            if (!AllowEdit) return;
            if (model.EsTitulo == value)
                return;
            BeforeFieldChanging?.Invoke(this, "EsTitulo");
            model.EsTitulo = value;
            IsTitleRow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPartidaEditable));
            ValidateEditableCells();
            FieldCommitted?.Invoke(this, "EsTitulo");
        }
    }

    private bool? isChapterLevelCustom;

    /// <summary>
    /// Indica si la fila tiene la apariencia visual de subtítulo/capítulo (fondo destacado, borde, badge WBS y texto en negrita),
    /// ya sea porque es un título agrupador o una partida valorizable de nivel principal (ej. "15.0", "16.0").
    /// </summary>
    public bool IsChapterStyle => isTitleRow || IsChapterLevelItem;

    public bool IsChapterLevelItem
    {
        get
        {
            if (isChapterLevelCustom.HasValue)
                return isChapterLevelCustom.Value;
            if (isTitleRow) return true;
            string raw = (Item ?? "").Trim().Replace(",", ".");
            return ChapterRegex.IsMatch(raw) || raw == "*";
        }
        set
        {
            if (isChapterLevelCustom == value) return;
            isChapterLevelCustom = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChapterStyle));
        }
    }

    public bool IsTitleRow
    {
        get => isTitleRow;
        set
        {
            if (SetProperty(ref isTitleRow, value))
            {
                OnPropertyChanged(nameof(IsChapterStyle));
                OnPropertyChanged(nameof(IsChapterLevelItem));
            }
        }
    }

    public bool IsPartidaEditable => !model.EsTitulo;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    private bool isSearchMatch;
    public bool IsSearchMatch
    {
        get => isSearchMatch;
        set => SetProperty(ref isSearchMatch, value);
    }

    public PartidaCompletionState CompletionState
    {
        get => completionState;
        set => SetProperty(ref completionState, value);
    }

    public decimal CompletionPct
    {
        get => completionPct;
        set => SetProperty(ref completionPct, value);
    }

    public decimal UnitPriceUf
    {
        get => unitPriceUf;
        set { SetProperty(ref unitPriceUf, value); OnPropertyChanged(nameof(UnitPriceText)); }
    }

    public decimal TotalUf
    {
        get => totalUf;
        set { SetProperty(ref totalUf, value); OnPropertyChanged(nameof(TotalText)); }
    }

    private CurrencyPresentationContext? currencyContext;

    public void ApplyCurrencyContext(CurrencyPresentationContext ctx)
    {
        currencyContext = ctx;
        OnPropertyChanged(nameof(UnitPriceText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(ChapterTotalDisplay));
    }

    public string UnitPriceText
    {
        get
        {
            if (UnitPriceUf <= 0m) return "—";
            if (currencyContext == null)
                return UnitPriceUf.ToString("N3", CultureInfo.CurrentCulture);
            return ItemizadoCurrencyCoordinator.FormatUnitPricePair(UnitPriceUf, currencyContext);
        }
    }

    public string TotalText
    {
        get
        {
            if (TotalUf <= 0m) return "—";
            if (currencyContext == null)
                return TotalUf.ToString("N3", CultureInfo.CurrentCulture);
            return ItemizadoCurrencyCoordinator.FormatTotalPair(TotalUf, currencyContext);
        }
    }

    private CommercialCostingModel commercialModel = CommercialCostingModel.ApuDirectCostWithCommercialFactors;
    private bool usesCommercialOverride;
    public CommercialCostingModel CommercialModel
    {
        get => commercialModel;
        set
        {
            if (SetProperty(ref commercialModel, value))
            {
                OnPropertyChanged(nameof(CommercialModelLabel));
                OnPropertyChanged(nameof(CommercialModelShortLabel));
                OnPropertyChanged(nameof(HasCustomCommercialModel));
            }
        }
    }

    public string CommercialModelLabel => isTitleRow ? "" : commercialModel switch
    {
        CommercialCostingModel.UnitPrice => "P.U. Fijo",
        CommercialCostingModel.LumpSum => "Suma Alzada",
        CommercialCostingModel.TimeAndMaterials => "T & M",
        _ => "APU"
    };

    public string CommercialModelShortLabel => isTitleRow ? "" : commercialModel switch
    {
        CommercialCostingModel.UnitPrice => "P.U.",
        CommercialCostingModel.LumpSum => "S.A.",
        CommercialCostingModel.TimeAndMaterials => "T&M",
        _ => "APU"
    };

    public bool HasCustomCommercialModel => !isTitleRow && commercialModel != CommercialCostingModel.ApuDirectCostWithCommercialFactors;
    public bool UsesCommercialOverride
    {
        get => usesCommercialOverride;
        set
        {
            if (SetProperty(ref usesCommercialOverride, value))
                OnPropertyChanged(nameof(CommercialOriginLabel));
        }
    }
    public string CommercialOriginLabel => isTitleRow ? "" : usesCommercialOverride ? "Excepción" : "Proyecto";

    private bool isCollapsed;
    public bool IsCollapsed
    {
        get => isCollapsed;
        set
        {
            if (SetProperty(ref isCollapsed, value))
            {
                OnPropertyChanged(nameof(CollapseIcon));
            }
        }
    }

    public string CollapseIcon => isCollapsed ? "▶" : "▼";

    private bool isHiddenByParentCollapse;
    public bool IsHiddenByParentCollapse
    {
        get => isHiddenByParentCollapse;
        set => SetProperty(ref isHiddenByParentCollapse, value);
    }

    public string ChapterTotalDisplay
    {
        get
        {
            if (TotalUf <= 0m) return "";
            if (currencyContext == null)
                return $"{TotalUf:N3} UF";
            return ItemizadoCurrencyCoordinator.FormatTotalPair(TotalUf, currencyContext);
        }
    }

    private int chapterPartidasCount;
    private int chapterCompletedCount;

    public void SetChapterStats(int totalPartidas, int completedPartidas)
    {
        chapterPartidasCount = totalPartidas;
        chapterCompletedCount = completedPartidas;
        OnPropertyChanged(nameof(ChapterPartidasBadgeText));
        OnPropertyChanged(nameof(ChapterTrazabilidadText));
        OnPropertyChanged(nameof(ChapterTrazabilidadKind));
        OnPropertyChanged(nameof(TrazabilidadText));
        OnPropertyChanged(nameof(TrazabilidadKind));
    }

    public string ChapterPartidasBadgeText => isTitleRow
        ? $"({chapterPartidasCount} {(chapterPartidasCount == 1 ? "partida" : "partidas")})"
        : "";

    public string ChapterTrazabilidadText
    {
        get
        {
            if (!isTitleRow) return "";
            if (chapterPartidasCount == 0) return "0 partidas";
            if (chapterCompletedCount >= chapterPartidasCount)
                return $"{chapterPartidasCount}/{chapterPartidasCount} Listas";
            int pending = chapterPartidasCount - chapterCompletedCount;
            return $"{pending} {(pending == 1 ? "Pendiente" : "Pendientes")}";
        }
    }

    public string ChapterTrazabilidadKind => (!isTitleRow || chapterPartidasCount == 0 || chapterCompletedCount >= chapterPartidasCount) ? "ok" : "warn";

    public string TrazabilidadText => isTitleRow ? ChapterTrazabilidadText : model.ExcluidoCubicacion ? "Excluida" : statusKind switch
    {
        "ok" => "Completa",
        "warn" => "Incompleta",
        "bad" => "Sin precio",
        _ => "Pendiente"
    };

    public string TrazabilidadKind => isTitleRow ? ChapterTrazabilidadKind : statusKind;

    public string StatusLabel
    {
        get => statusLabel;
        set
        {
            if (SetProperty(ref statusLabel, value))
            {
                OnPropertyChanged(nameof(StatusShortLabel));
            }
        }
    }

    public string StatusKind
    {
        get => statusKind;
        set
        {
            if (SetProperty(ref statusKind, value))
            {
                OnPropertyChanged(nameof(StatusShortLabel));
                OnPropertyChanged(nameof(StatusDotBrushKey));
            }
        }
    }

    public string StatusShortLabel => isTitleRow ? "" : model.ExcluidoCubicacion ? "Excluida" : statusKind switch
    {
        "ok" => "Lista",
        "warn" => "Incompleta",
        "bad" => "Sin precio",
        _ => "Pendiente"
    };

    public string StatusDotBrushKey => isTitleRow ? "Brush.Muted" : statusKind switch
    {
        "ok" => "Brush.Success",
        "warn" => "Brush.Amber",
        "bad" => "Brush.Red",
        _ => "Brush.Line2"
    };

    public bool ExcluidoCubicacion
    {
        get => model.ExcluidoCubicacion;
        set
        {
            if (!AllowEdit) return;
            if (model.ExcluidoCubicacion == value)
                return;
            BeforeFieldChanging?.Invoke(this, "Excl. cubic.");
            model.ExcluidoCubicacion = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MotivoNoCubicarHint));
            FieldCommitted?.Invoke(this, "Excl. cubic.");
        }
    }

    /// <summary>Motivo opcional de exclusión (vacío = sin observación).</summary>
    public string MotivoNoCubicar
    {
        get => model.MotivoNoCubicar ?? "";
        set
        {
            if (!AllowEdit) return;
            string clean = value ?? "";
            if (string.Equals(model.MotivoNoCubicar, clean, StringComparison.Ordinal))
                return;
            BeforeFieldChanging?.Invoke(this, "Motivo no cubicar");
            model.MotivoNoCubicar = clean;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MotivoNoCubicarHint));
            FieldCommitted?.Invoke(this, "Motivo no cubicar");
        }
    }

    public string MotivoNoCubicarHint
    {
        get
        {
            if (!model.ExcluidoCubicacion)
                return "Marque «No cubicar» (Ctrl+E) si la partida no se toma en el takeoff.";
            var motivo = (model.MotivoNoCubicar ?? "").Trim();
            return string.IsNullOrEmpty(motivo)
                ? "Marcada en Cubicación sin observación (escriba el motivo ahí)."
                : "Excluida de cubicación: " + motivo;
        }
    }

    public ItemizadoRowPresentation? FilterPresentation { get; private set; }

    public string? ItemCellError { get; private set; }
    public string? DescripcionCellError { get; private set; }
    public string? UnidadCellError { get; private set; }
    public string? CantidadCellError { get; private set; }

    public string? GetCellError(string columnKey) => columnKey switch
    {
        "Item" or "Ítem" => ItemCellError,
        "Descripcion" or "Descripción" => DescripcionCellError,
        "Unidad" or "Ud." => UnidadCellError,
        "Cantidad" => CantidadCellError,
        _ => null
    };

    public void BeginItemEdit() => itemEditSnapshot = model.Item ?? "";

    public bool CommitItemEdit(IReadOnlyList<ItemizadoRow> peers)
    {
        string proposed = model.Item ?? "";
        var result = ItemizadoGridValidation.TryApplyVisibleItemEdit(model, proposed, peers);
        if (!result.Success)
        {
            string previousItem = model.Item ?? "";
            model.Item = itemEditSnapshot ?? previousItem;
            model.WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(model.Item);
            ItemCellError = result.ErrorMessage;
            OnPropertyChanged(nameof(Item));
            NotifyCellErrors();
            return false;
        }

        ItemCellError = null;
        OnPropertyChanged(nameof(Item));
        NotifyCellErrors();
        return true;
    }

    public void ValidateEditableCells(IReadOnlyList<ItemizadoRow>? peers = null)
    {
        peers ??= PeersProvider?.Invoke() ?? Array.Empty<ItemizadoRow>();

        if (model.EsTitulo)
        {
            ItemCellError = ItemizadoGridValidation.ValidateItemCode(model, Item, peers);
            DescripcionCellError = null;
            UnidadCellError = null;
            CantidadCellError = null;
        }
        else
        {
            ItemCellError = ItemizadoGridValidation.ValidateItemCode(model, Item, peers);
            DescripcionCellError = string.IsNullOrWhiteSpace(Descripcion) ? "La descripción es obligatoria." : null;
            UnidadCellError = string.IsNullOrWhiteSpace(Unidad) ? "Indique la unidad." : null;
            CantidadCellError = Cantidad <= 0m ? "La cantidad debe ser mayor que cero." : null;
        }

        NotifyCellErrors();
    }

    private void NotifyCellErrors()
    {
        OnPropertyChanged(nameof(ItemCellError));
        OnPropertyChanged(nameof(DescripcionCellError));
        OnPropertyChanged(nameof(UnidadCellError));
        OnPropertyChanged(nameof(CantidadCellError));
    }

    public void ApplyPresentation(ItemizadoRowPresentation presentation)
    {
        RowNumber = presentation.SourceIndex + 1;
        CompletionState = presentation.CompletionState;
        CompletionPct = presentation.CompletionPct;
        UnitPriceUf = presentation.UnitPriceUf;
        TotalUf = presentation.TotalUf;
        StatusLabel = presentation.StatusLabel;
        StatusKind = presentation.StatusKind;
        IsTitleRow = presentation.EsTitulo;
        FilterPresentation = presentation;
        OnPropertyChanged(nameof(ExcluidoCubicacion));
        OnPropertyChanged(nameof(MotivoNoCubicar));
        OnPropertyChanged(nameof(MotivoNoCubicarHint));
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(WbsLevel));
        OnPropertyChanged(nameof(IndentMargin));
        OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Unidad));
        OnPropertyChanged(nameof(CantidadText));
        OnPropertyChanged(nameof(IsPartidaEditable));
        OnPropertyChanged(nameof(EsTitulo));
        OnPropertyChanged(nameof(CommercialModelLabel));
        OnPropertyChanged(nameof(CommercialModelShortLabel));
        OnPropertyChanged(nameof(HasCustomCommercialModel));
        OnPropertyChanged(nameof(ChapterTotalDisplay));
        OnPropertyChanged(nameof(StatusShortLabel));
        OnPropertyChanged(nameof(StatusDotBrushKey));
        OnPropertyChanged(nameof(CollapseIcon));
        OnPropertyChanged(nameof(TrazabilidadText));
        OnPropertyChanged(nameof(TrazabilidadKind));
        OnPropertyChanged(nameof(ChapterPartidasBadgeText));
        OnPropertyChanged(nameof(ChapterTrazabilidadText));
        OnPropertyChanged(nameof(Observaciones));
        OnPropertyChanged(nameof(HasObservaciones));
        OnPropertyChanged(nameof(ObservacionesToolTip));
    }
}
