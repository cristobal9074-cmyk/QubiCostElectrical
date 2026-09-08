using System;
using System.Collections.Generic;
using System.Globalization;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed class ItemizadoRowVm : ViewModelBase, IGridCellValidationSource
{
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

    public ItemizadoRowVm(ItemizadoRow model, Func<bool>? canEdit = null)
    {
        this.model = model;
        this.canEdit = canEdit;
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public void UpdateModel(ItemizadoRow newModel)
    {
        model = newModel ?? throw new ArgumentNullException(nameof(newModel));
        OnPropertyChanged(string.Empty);
    }

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
            OnPropertyChanged();
        }
    }

    public string Descripcion
    {
        get => model.Descripcion ?? "";
        set
        {
            if (!AllowEdit) return;
            model.Descripcion = value;
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
            OnPropertyChanged();
        }
    }

    public decimal Cantidad
    {
        get => model.Cantidad;
        set
        {
            if (!AllowEdit) return;
            model.Cantidad = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CantidadText));
        }
    }

    public string CantidadText
    {
        get => model.Cantidad.ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
                Cantidad = parsed;
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
            model.EsTitulo = value;
            IsTitleRow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPartidaEditable));
            ValidateEditableCells();
            FieldCommitted?.Invoke(this, "EsTitulo");
        }
    }

    public bool IsTitleRow
    {
        get => isTitleRow;
        set => SetProperty(ref isTitleRow, value);
    }

    public bool IsPartidaEditable => !model.EsTitulo;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
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

    public string UnitPriceText => UnitPriceUf > 0m ? UnitPriceUf.ToString("N3", CultureInfo.CurrentCulture) : "—";
    public string TotalText => TotalUf > 0m ? TotalUf.ToString("N2", CultureInfo.CurrentCulture) : "—";

    public string StatusLabel
    {
        get => statusLabel;
        set => SetProperty(ref statusLabel, value);
    }

    public string StatusKind
    {
        get => statusKind;
        set => SetProperty(ref statusKind, value);
    }

    public bool ExcluidoCubicacion
    {
        get => model.ExcluidoCubicacion;
        set
        {
            if (!AllowEdit) return;
            if (model.ExcluidoCubicacion == value)
                return;
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
        OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Unidad));
        OnPropertyChanged(nameof(CantidadText));
        OnPropertyChanged(nameof(IsPartidaEditable));
        OnPropertyChanged(nameof(EsTitulo));
    }
}
