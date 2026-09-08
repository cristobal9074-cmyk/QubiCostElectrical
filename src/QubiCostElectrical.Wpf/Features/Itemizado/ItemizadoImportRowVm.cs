using System;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoImportRowVm : ObservableObject
{
    private readonly ItemizadoImportPreviewRow? preview;
    private readonly ItemizadoRow? legacySource;

    public ItemizadoImportRowVm(ItemizadoRow source)
    {
        legacySource = source;
        Item = source.Item ?? "";
        Descripcion = source.Descripcion ?? "";
        Unidad = source.Unidad ?? "";
        Cantidad = source.Cantidad;
        EsTitulo = source.EsTitulo;
        Revalidate();
    }

    public ItemizadoImportRowVm(ItemizadoImportPreviewRow previewRow)
    {
        preview = previewRow ?? throw new ArgumentNullException(nameof(previewRow));
        legacySource = previewRow.Source;
        Item = previewRow.Item;
        Descripcion = previewRow.Descripcion;
        Unidad = previewRow.Unidad;
        Cantidad = previewRow.Cantidad;
        EsTitulo = previewRow.EsTitulo;
    }

    public ItemizadoRow Source => legacySource ?? preview!.Source;
    public bool EsTitulo { get; }

    [ObservableProperty]
    private string item = "";

    [ObservableProperty]
    private string descripcion = "";

    [ObservableProperty]
    private string unidad = "";

    [ObservableProperty]
    private decimal cantidad;

    private bool duplicateWbs;

    partial void OnItemChanged(string value)
    {
        if (preview != null)
        {
            preview.Item = value;
            SyncFromPreview();
        }
        else
            Revalidate();
    }

    partial void OnDescripcionChanged(string value)
    {
        if (preview != null)
        {
            preview.Descripcion = value;
            SyncFromPreview();
        }
        else
            Revalidate();
    }

    partial void OnUnidadChanged(string value)
    {
        if (preview != null)
        {
            preview.Unidad = value;
            SyncFromPreview();
        }
        else
            Revalidate();
    }

    partial void OnCantidadChanged(decimal value)
    {
        if (preview != null)
        {
            preview.Cantidad = value;
            SyncFromPreview();
        }
        else
            Revalidate();
    }

    public bool IsRejected => preview?.IsRejected ?? _legacyIsRejected;
    private bool _legacyIsRejected;
    public string RejectionReason => preview?.RejectionReason ?? _legacyRejectionReason;
    private string _legacyRejectionReason = "";
    public string StatusIcon => EsTitulo ? "" : IsRejected ? "✗" : RejectionReason != "" ? "⚠" : "✓";
    public string StatusColor => EsTitulo ? "Gray" : IsRejected ? "#C24141" : RejectionReason != "" ? "#B45309" : "#15803D";

    public void SetDuplicateWbs(bool value)
    {
        if (preview != null)
            preview.SetDuplicateWbs(value);
        else
        {
            duplicateWbs = value;
            Revalidate();
        }
        SyncFromPreview();
    }

    internal ItemizadoImportPreviewRow? Preview => preview;

    public void NotifyValidationChanged() => SyncFromPreview();

    private void SyncFromPreview()
    {
        if (preview == null)
            return;
        OnPropertyChanged(nameof(IsRejected));
        OnPropertyChanged(nameof(RejectionReason));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
    }

    public void Revalidate()
    {
        if (preview != null)
        {
            preview.Revalidate();
            SyncFromPreview();
            return;
        }

        if (EsTitulo)
        {
            _legacyIsRejected = false;
            _legacyRejectionReason = "";
        }
        else if (duplicateWbs && !string.IsNullOrWhiteSpace(Item))
        {
            _legacyIsRejected = true;
            _legacyRejectionReason = "Código WBS duplicado";
        }
        else if (string.IsNullOrWhiteSpace(Descripcion))
        {
            _legacyIsRejected = true;
            _legacyRejectionReason = "Descripción requerida";
        }
        else if (string.IsNullOrWhiteSpace(Item))
        {
            _legacyIsRejected = false;
            _legacyRejectionReason = "Sin código — se importará igual";
        }
        else if (Cantidad < 0)
        {
            _legacyIsRejected = true;
            _legacyRejectionReason = "Cantidad no puede ser < 0";
        }
        else
        {
            _legacyIsRejected = false;
            _legacyRejectionReason = "";
        }

        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(IsRejected));
        OnPropertyChanged(nameof(RejectionReason));
    }

    public void FixEmptyCode()
    {
        if (preview != null)
            preview.FixEmptyCode();
        else if (string.IsNullOrWhiteSpace(Item))
            Item = "(sin código)";
        Revalidate();
    }

    public void FixQuantity()
    {
        if (preview != null)
            preview.FixQuantity();
        else if (Cantidad < 0)
            Cantidad = 0;
        Revalidate();
    }

    public ItemizadoRow ToRow() => preview?.ToRow() ?? new()
    {
        PartidaId = Source.PartidaId == Guid.Empty ? Guid.NewGuid() : Source.PartidaId,
        Id = Source.Id,
        Item = Item,
        WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(Item),
        Descripcion = Descripcion,
        Unidad = Unidad,
        Cantidad = Cantidad,
        EsTitulo = EsTitulo,
        ParentId = Source.ParentId,
        NivelJerarquico = Source.NivelJerarquico,
        ExcluidoCubicacion = Source.ExcluidoCubicacion,
        MotivoNoCubicar = Source.MotivoNoCubicar,
        Observaciones = Source.Observaciones,
    };
}
